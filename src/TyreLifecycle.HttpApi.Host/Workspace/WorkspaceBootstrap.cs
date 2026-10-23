using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.Workspace;

namespace TyreLifecycle.HttpApi.Host.Workspace;

public static class WorkspaceBootstrap
{
    public static async Task InitializeAsync(IServiceProvider services, IWebHostEnvironment env)
    {
        // The sample accounts and sample business data are local-development fixtures only.
        if (!env.IsDevelopment()) return;
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TyreLifecycleDbContext>();
        var hasher = new PasswordHasher<StaffAccount>();
        var accounts = new[] {
            ("admin", "System Administrator", "system-admin", "System Administrator", "Platform Administration"),
            ("manager", "N. Mthembu", "branch-manager", "Branch Manager", "Durban Central"),
            ("advisor", "L. Dlamini", "service-advisor", "Service Advisor", "Durban Central"),
            ("technician", "S. Khumalo", "technician", "Technician / Tyre Fitter", "Durban Central"),
            ("fleet", "P. Maseko", "fleet-manager", "Fleet Manager", "KZN Logistics Fleet")
        };
        foreach (var (email, name, role, label, branch) in accounts) {
            var address = email + "@tyretrack.co.za";
            if (await db.StaffAccounts.AnyAsync(x => x.Email == address)) continue;
            var user = new StaffAccount { Id = Guid.NewGuid(), Email = address, Name = name,
                Role = role, RoleLabel = label, Branch = branch };
            user.PasswordHash = hasher.HashPassword(user, "TyreTrack@2026");
            db.StaffAccounts.Add(user);
        }
        var seedFile = Path.Combine(env.ContentRootPath, "Workspace", "development-seed.json");
        var seeds = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await File.ReadAllTextAsync(seedFile))!;
        foreach (var (key, data) in seeds) {
            if (!await db.WorkspaceDocuments.AnyAsync(x => x.Key == key))
                db.WorkspaceDocuments.Add(new WorkspaceDocument { Key = key, Json = data.GetRawText(), Version = 1, UpdatedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        const string scopeMarker="tyretrack.system.staff-scope-v1";
        if(!await db.WorkspaceDocuments.AnyAsync(x=>x.Key==scopeMarker)) {
            foreach(var user in await db.StaffAccounts.ToListAsync()) {
                if(new[]{"manager@tyretrack.co.za","advisor@tyretrack.co.za","technician@tyretrack.co.za"}.Contains(user.Email)) user.BranchId="BR-DBN";
                if(user.Email=="fleet@tyretrack.co.za") user.FleetId="FLT-LOG";
            }
            db.WorkspaceDocuments.Add(new WorkspaceDocument{Key=scopeMarker,Json="true",Version=1,UpdatedAt=DateTime.UtcNow});
            await db.SaveChangesAsync();
        }
        await CoreWorkspace.InitializeAsync(db);
        await CoreWorkspace.RefreshAsync(db);
    }
}
