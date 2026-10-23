using Microsoft.AspNetCore.Cors;
using Microsoft.OpenApi.Models;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.HttpApi;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Conventions;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;

namespace TyreLifecycle.HttpApi.Host;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule),
    typeof(TyreLifecycleApplicationModule),
    typeof(TyreLifecycleHttpApiModule),
    typeof(TyreLifecycleEntityFrameworkCoreModule)
)]
public class TyreLifecycleHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options => {
                options.Cookie.Name = "TyreTrack.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = context.Services.GetHostingEnvironment().IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
                options.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
            });
        Configure<Volo.Abp.Authorization.Permissions.AbpPermissionOptions>(options =>
            options.ValueProviders.Add<Workspace.StaffPermissionProvider>());
        context.Services.AddRateLimiter(options => {
            options.RejectionStatusCode = 429;
            options.AddPolicy("login", http => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions {
                    PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
        });

        PreConfigure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(
                typeof(TyreLifecycleApplicationModule).Assembly,
                controllerOptions => controllerOptions.RootPath = "v1"
            );
        });

        context.Services.AddAbpSwaggerGen(options =>
        {
            options.CustomSchemaIds(type => type.FullName!.Replace("+", "."));
            options.DocInclusionPredicate((documentName, description) => true);
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Tyre Lifecycle API",
                Version = "v1"
            });
        });

        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                var origins = (configuration["App:CorsOrigins"] ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                builder
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options => options.UseRequestInterceptor("(request) => { request.headers['X-TyreTrack-Request'] = '1'; return request; }"));
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors();
        app.UseRateLimiter();
        // JSON writes require a non-simple header plus an allowed browser origin (CSRF protection).
        app.Use(async (http, next) => {
            if (http.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(http.Request.Method) &&
                !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method)) {
                var origin = http.Request.Headers.Origin.ToString();
                var configuration = http.RequestServices.GetRequiredService<IConfiguration>();
                var allowed = (configuration["App:CorsOrigins"] ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var ownOrigin = $"{http.Request.Scheme}://{http.Request.Host}";
                if (http.Request.Headers["X-TyreTrack-Request"] != "1" ||
                    (!string.IsNullOrEmpty(origin) && origin != ownOrigin && !allowed.Contains(origin))) {
                    http.Response.StatusCode = 403; return;
                }
            }
            await next();
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseUnitOfWork();
        app.UseConfiguredEndpoints();
    }
}
