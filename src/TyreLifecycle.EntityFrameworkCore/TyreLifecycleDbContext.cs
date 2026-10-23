using Microsoft.EntityFrameworkCore;
using TyreLifecycle.Customers;
using TyreLifecycle.Inspections;
using TyreLifecycle.Tyres;
using TyreLifecycle.Vehicles;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace TyreLifecycle.EntityFrameworkCore;

[ConnectionStringName("Default")]
public class TyreLifecycleDbContext : AbpDbContext<TyreLifecycleDbContext>
{
    public DbSet<TyreLifecycle.Workspace.WorkspaceDocument> WorkspaceDocuments => Set<TyreLifecycle.Workspace.WorkspaceDocument>();
    public DbSet<TyreLifecycle.Workspace.StaffAccount> StaffAccounts => Set<TyreLifecycle.Workspace.StaffAccount>();
    public DbSet<TyreLifecycle.Workspace.PortalChallenge> PortalChallenges => Set<TyreLifecycle.Workspace.PortalChallenge>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<TyreRotation> TyreRotations => Set<TyreRotation>();
    public DbSet<TyreRemoval> TyreRemovals => Set<TyreRemoval>();
    public DbSet<TyreFitment> TyreFitments => Set<TyreFitment>();
    public DbSet<Tyre> Tyres => Set<Tyre>();
    public DbSet<TyreLifecycle.Workspace.JobWorkRecord> JobWorkRecords => Set<TyreLifecycle.Workspace.JobWorkRecord>();
    public DbSet<TyreLifecycle.Workspace.CompletedWorkshopJob> CompletedWorkshopJobs => Set<TyreLifecycle.Workspace.CompletedWorkshopJob>();
    public DbSet<WorkshopInspection> WorkshopInspections => Set<WorkshopInspection>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionTyreReading> InspectionTyreReadings => Set<InspectionTyreReading>();

    public TyreLifecycleDbContext(DbContextOptions<TyreLifecycleDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<TyreLifecycle.Workspace.WorkspaceDocument>(b => {
            b.ToTable("TlWorkspaceDocuments"); b.HasKey(x => x.Key);
            b.Property(x => x.Key).HasMaxLength(160);
            b.Property(x => x.Json).IsRequired();
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        builder.Entity<TyreLifecycle.Workspace.StaffAccount>(b => {
            b.ToTable("TlStaffAccounts"); b.HasKey(x => x.Id);
            b.Property(x => x.Email).HasMaxLength(256); b.HasIndex(x => x.Email).IsUnique();
            b.Property(x => x.PasswordHash).IsRequired();
            b.Property(x => x.Role).HasMaxLength(64);
            b.Property(x => x.BranchId).HasMaxLength(64);
            b.Property(x => x.FleetId).HasMaxLength(64);
        });
        builder.Entity<TyreLifecycle.Workspace.PortalChallenge>(b => {
            b.ToTable("TlPortalChallenges"); b.HasKey(x => x.Id);
            b.Property(x => x.CustomerId).HasMaxLength(64);
            b.Property(x => x.CodeHash).HasMaxLength(128);
        });

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
            b.HasIndex(x => x.MobileNumber).IsUnique().HasFilter("[IsDeleted] = 0");
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
            b.Property<string>("RegistrationKey").HasMaxLength(32).HasComputedColumnSql("UPPER(REPLACE(REPLACE([RegistrationNumber], ' ', ''), '-', ''))", stored:true);
            b.HasIndex("RegistrationKey").IsUnique().HasFilter(null);
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
            b.Property<string>("ReferenceKey").HasMaxLength(64).HasComputedColumnSql("UPPER(REPLACE([TyreNumber], ' ', ''))",stored:true);
            b.HasIndex("ReferenceKey").IsUnique().HasFilter(null);
            b.Property<string>("PositionKey").HasMaxLength(32).HasComputedColumnSql("UPPER(REPLACE(REPLACE([Position], ' ', ''), '-', ''))",stored:true);
            b.HasIndex(nameof(Tyre.VehicleId),"PositionKey").IsUnique().HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
            b.HasIndex(x => x.VehicleId);
        });

        builder.Entity<TyreRotation>(b=> {
            b.ToTable("TlTyreRotations");b.HasKey(x=>x.Id);
            b.Property(x=>x.ActorName).IsRequired().HasMaxLength(256);
            b.Property(x=>x.RequestHash).IsRequired().HasMaxLength(64);
            b.Property(x=>x.MovesJson).IsRequired().HasMaxLength(4000);
            b.Property(x=>x.Notes).HasMaxLength(500);b.HasIndex(x=>x.VehicleId);
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<TyreRemoval>(b=> {
            b.ToTable("TlTyreRemovals");b.HasKey(x=>x.Id);
            b.Property(x=>x.ActorName).IsRequired().HasMaxLength(256);
            b.Property(x=>x.RequestHash).IsRequired().HasMaxLength(64);
            b.Property(x=>x.Position).IsRequired().HasMaxLength(32);
            b.Property(x=>x.Reason).IsRequired().HasMaxLength(200);
            b.Property(x=>x.Destination).IsRequired().HasMaxLength(32);
            b.Property(x=>x.Notes).HasMaxLength(500);
            b.HasIndex(x=>x.TyreId).IsUnique();b.HasIndex(x=>x.VehicleId);
            b.HasOne<Tyre>().WithMany().HasForeignKey(x=>x.TyreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Tyre>().WithMany().HasForeignKey(x=>x.ReplacementTyreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<TyreFitment>(b=> {
            b.ToTable("TlTyreFitments");b.HasKey(x=>x.Id);
            b.Property(x=>x.ActorName).IsRequired().HasMaxLength(256);
            b.Property(x=>x.RequestHash).IsRequired().HasMaxLength(64);
            b.Property(x=>x.BranchId).IsRequired().HasMaxLength(64);
            b.Property(x=>x.Position).IsRequired().HasMaxLength(32);
            b.Property(x=>x.Notes).HasMaxLength(500);
            b.Property(x=>x.StockSku).HasMaxLength(64);
            b.Property(x=>x.InitialTreadMm).HasPrecision(4,1);
            b.HasIndex(x=>x.TyreId);b.HasIndex(x=>x.VehicleId);
            b.HasOne<Tyre>().WithMany().HasForeignKey(x=>x.TyreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TyreLifecycle.Workspace.JobWorkRecord>(b=> {
            b.ToTable("TlJobWorkRecords");b.HasKey(x=>x.Id);
            b.Property(x=>x.JobCardId).HasMaxLength(64);b.Property(x=>x.ItemId).HasMaxLength(64);
            b.Property(x=>x.Kind).HasMaxLength(32);b.Property(x=>x.ActorName).HasMaxLength(256);b.Property(x=>x.RequestHash).HasMaxLength(64);b.Property(x=>x.Notes).HasMaxLength(1000);
            b.HasIndex(x=>new{x.JobCardId,x.ItemId}).IsUnique();
            b.HasIndex(x=>x.FitmentId).IsUnique().HasFilter("[FitmentId] IS NOT NULL");b.HasIndex(x=>x.RotationId).IsUnique().HasFilter("[RotationId] IS NOT NULL");
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TyreFitment>().WithMany().HasForeignKey(x=>x.FitmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TyreRotation>().WithMany().HasForeignKey(x=>x.RotationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<TyreLifecycle.Workspace.CompletedWorkshopJob>(b=> {
            b.ToTable("TlCompletedWorkshopJobs");b.HasKey(x=>x.Id);b.Property(x=>x.JobCardId).HasMaxLength(64);b.HasIndex(x=>x.JobCardId).IsUnique();
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<WorkshopInspection>(b=> {
            b.ToTable("TlWorkshopInspections");b.HasKey(x=>x.Id);
            b.Property(x=>x.RequestHash).IsRequired().HasMaxLength(64);
            b.Property(x=>x.InspectionIdsJson).IsRequired().HasMaxLength(512);
            b.Property(x=>x.JobCardId).HasMaxLength(64);b.HasIndex(x=>x.VehicleId);
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Inspection>(b =>
        {
            b.ToTable(TyreLifecycleConsts.DbTablePrefix + "Inspections", TyreLifecycleConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.InspectionNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasIndex(x => x.InspectionNumber).IsUnique();
            b.HasIndex(x => x.VehicleId);
            b.HasIndex(x => x.Status);

            b.HasMany(x => x.Readings)
                .WithOne()
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation(x => x.Readings)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<InspectionTyreReading>(b =>
        {
            b.ToTable(TyreLifecycleConsts.DbTablePrefix + "InspectionTyreReadings", TyreLifecycleConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Position).IsRequired().HasMaxLength(32);
            b.Property(x => x.InnerTreadMm).HasPrecision(4, 1);
            b.Property(x => x.CentreTreadMm).HasPrecision(4, 1);
            b.Property(x => x.OuterTreadMm).HasPrecision(4, 1);
            b.Property(x => x.PressurePsi).HasPrecision(5, 1);
            b.Property(x => x.WearPattern).HasMaxLength(128);
            b.Property(x => x.Defects).HasMaxLength(1000);
            b.Property(x => x.Recommendation).HasMaxLength(1000);
            b.HasIndex(x => x.InspectionId);
            b.HasIndex(x => x.TyreId);
            b.HasIndex(x => new { x.InspectionId, x.Position }).IsUnique();
        });
    }
}
