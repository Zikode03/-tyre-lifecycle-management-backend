using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace TyreLifecycle;

[DependsOn(
    typeof(AbpDddApplicationContractsModule),
    typeof(TyreLifecycleDomainSharedModule)
)]
public class TyreLifecycleApplicationContractsModule : AbpModule
{
}
