using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TyreLifecycle.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Autofac;

namespace TyreLifecycle.DbMigrator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            using var application = await AbpApplicationFactory.CreateAsync<TyreLifecycleDbMigratorModule>(options =>
            {
                options.UseAutofac();
            });

            await application.InitializeAsync();

            using var scope = application.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TyreLifecycleDbContext>();

            await dbContext.Database.MigrateAsync();

            await application.ShutdownAsync();
            Console.WriteLine("TyreLifecycle database migration completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
