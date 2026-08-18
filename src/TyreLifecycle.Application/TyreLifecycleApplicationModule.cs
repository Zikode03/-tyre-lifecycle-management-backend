using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace TyreLifecycle;

[DependsOn(
    typeof(AbpDddApplicationModule),
    typeof(TyreLifecycleDomainModule),
    typeof(TyreLifecycleApplicationContractsModule)
)]
public class TyreLifecycleApplicationModule : AbpModule
{
}
