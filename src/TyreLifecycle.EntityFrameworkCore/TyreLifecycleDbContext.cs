using Microsoft.EntityFrameworkCore;
using TyreLifecycle.Customers;
using TyreLifecycle.Tyres;
using TyreLifecycle.Vehicles;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace TyreLifecycle.EntityFrameworkCore;

[ConnectionStringName("Default")]
public class TyreLifecycleDbContext : AbpDbContext<TyreLifecycleDbContext>
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Tyre> Tyres => Set<Tyre>();

    public TyreLifecycleDbContext(DbContextOptions<TyreLifecycleDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Customer>(b =>
        {
            b.ToTable(TyreLifecycleConsts.DbTablePrefix + "Customers", TyreLifecycleConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.CustomerNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
            b.Property(x => x.MobileNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.Email).HasMaxLength(256);
            b.HasIndex(x => x.CustomerNumber).IsUnique();
        });

        builder.Entity<Vehicle>(b =>
        {
            b.ToTable(TyreLifecycleConsts.DbTablePrefix + "Vehicles", TyreLifecycleConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.RegistrationNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.Make).IsRequired().HasMaxLength(100);
            b.Property(x => x.Model).IsRequired().HasMaxLength(100);
            b.Property(x => x.Vin).HasMaxLength(64);
            b.HasIndex(x => x.RegistrationNumber).IsUnique();
            b.HasIndex(x => x.CustomerId);
        });

        builder.Entity<Tyre>(b =>
        {
            b.ToTable(TyreLifecycleConsts.DbTablePrefix + "Tyres", TyreLifecycleConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TyreNumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.Brand).IsRequired().HasMaxLength(100);
            b.Property(x => x.Model).IsRequired().HasMaxLength(100);
            b.Property(x => x.Size).IsRequired().HasMaxLength(32);
            b.Property(x => x.Position).IsRequired().HasMaxLength(32);
            b.Property(x => x.TreadDepthMm).HasPrecision(4, 1);
            b.HasIndex(x => x.TyreNumber).IsUnique();
            b.HasIndex(x => x.VehicleId);
        });
    }
}
