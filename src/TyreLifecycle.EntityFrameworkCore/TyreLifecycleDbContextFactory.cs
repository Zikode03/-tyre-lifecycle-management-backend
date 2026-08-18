using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TyreLifecycle.EntityFrameworkCore;

public class TyreLifecycleDbContextFactory : IDesignTimeDbContextFactory<TyreLifecycleDbContext>
{
    public TyreLifecycleDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "TyreLifecycle.HttpApi.Host");
        if (!Directory.Exists(basePath))
        {
            basePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "TyreLifecycle.HttpApi.Host");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<TyreLifecycleDbContext>();
        optionsBuilder.UseSqlServer(configuration.GetConnectionString("Default"));

        return new TyreLifecycleDbContext(optionsBuilder.Options);
    }
}
