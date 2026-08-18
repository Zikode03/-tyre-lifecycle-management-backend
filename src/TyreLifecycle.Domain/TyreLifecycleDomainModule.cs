using Volo.Abp.Ddd.Domain;
using Volo.Abp.Modularity;

namespace TyreLifecycle;

[DependsOn(typeof(AbpDddDomainModule))]
public class TyreLifecycleDomainModule : AbpModule
{
}
