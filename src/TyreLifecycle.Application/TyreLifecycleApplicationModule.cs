using Volo.Abp.Application;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace TyreLifecycle;

[DependsOn(
    typeof(AbpDddApplicationModule),
    typeof(AbpAutoMapperModule),
    typeof(TyreLifecycleDomainModule),
    typeof(TyreLifecycleApplicationContractsModule)
)]
public class TyreLifecycleApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<TyreLifecycleApplicationModule>(validate: true);
        });
    }
}
