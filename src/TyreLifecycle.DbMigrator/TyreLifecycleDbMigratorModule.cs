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
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        context.Services.ReplaceConfiguration(configuration);
    }
}
