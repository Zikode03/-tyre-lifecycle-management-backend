using Microsoft.Extensions.DependencyInjection;
using TyreLifecycle.Customers;
using TyreLifecycle.EntityFrameworkCore.Customers;
using TyreLifecycle.EntityFrameworkCore.Tyres;
using TyreLifecycle.EntityFrameworkCore.Vehicles;
using TyreLifecycle.Tyres;
using TyreLifecycle.Vehicles;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Modularity;

namespace TyreLifecycle.EntityFrameworkCore;

[DependsOn(
    typeof(AbpEntityFrameworkCoreSqlServerModule),
    typeof(TyreLifecycleDomainModule)
)]
public class TyreLifecycleEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<TyreLifecycleDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: false);
            options.AddRepository<Customer, EfCoreCustomerRepository>();
            options.AddRepository<Vehicle, EfCoreVehicleRepository>();
            options.AddRepository<Tyre, EfCoreTyreRepository>();
        });

        Configure<AbpDbContextOptions>(options =>
        {
            options.UseSqlServer();
        });
    }
}
