using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace TyreLifecycle;

[DependsOn(typeof(AbpDddApplicationContractsModule))]
public class TyreLifecycleApplicationContractsModule : AbpModule
{
}
