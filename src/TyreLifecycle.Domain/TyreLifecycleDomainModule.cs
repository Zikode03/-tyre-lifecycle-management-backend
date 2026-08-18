using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace TyreLifecycle;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(TyreLifecycleDomainSharedModule)
)]
public class TyreLifecycleDomainModule : AbpModule
{
}
