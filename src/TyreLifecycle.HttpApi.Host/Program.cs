using TyreLifecycle.HttpApi.Host;
using Volo.Abp;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutofac();
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false).AddEnvironmentVariables();

await builder.AddApplicationAsync<TyreLifecycleHttpApiHostModule>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.LocalRedirect("/swagger")).AllowAnonymous();
}
await app.InitializeApplicationAsync();
await TyreLifecycle.HttpApi.Host.Workspace.WorkspaceBootstrap.InitializeAsync(app.Services, app.Environment);
await app.RunAsync();
