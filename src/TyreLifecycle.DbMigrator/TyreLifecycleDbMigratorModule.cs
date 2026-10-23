using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TyreLifecycle.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace TyreLifecycle.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(TyreLifecycleEntityFrameworkCoreModule)
)]
public class TyreLifecycleDbMigratorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        context.Services.ReplaceConfiguration(configuration);
        Configure<Volo.Abp.Data.AbpDbConnectionOptions>(options =>
            options.ConnectionStrings.Default = configuration.GetConnectionString("Default"));
    }
}
