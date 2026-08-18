using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace TyreLifecycle;

[DependsOn(typeof(AbpDddDomainSharedModule))]
public class TyreLifecycleDomainSharedModule : AbpModule
{
}
