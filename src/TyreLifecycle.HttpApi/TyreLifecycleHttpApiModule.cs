using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace TyreLifecycle.HttpApi;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(TyreLifecycleApplicationContractsModule)
)]
public class TyreLifecycleHttpApiModule : AbpModule
{
}
