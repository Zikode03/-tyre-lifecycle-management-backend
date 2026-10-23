using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.Workspace;
using Volo.Abp.Security.Claims;

namespace TyreLifecycle.HttpApi.Host.Workspace;

[ApiController,Route("api/v1/vehicle-access"),Authorize,IgnoreAntiforgeryToken]
public class VehicleAccessController(TyreLifecycleDbContext db):ControllerBase
{
    public record AssignmentInput(string? BranchId,string? FleetId);
    private async Task<StaffAccount?> Current() {
        if(User.FindFirstValue("kind")!="staff"||!Guid.TryParse(User.FindFirstValue(AbpClaimTypes.UserId),out var id))return null;
        return await db.StaffAccounts.FindAsync(id);
    }
    private async Task<JsonElement[]> Rows(string key) {
        var doc=await db.WorkspaceDocuments.FindAsync(key);
        return doc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(doc.Json)!;
    }
    private static string Text(JsonElement row,string key)=>row.ValueKind==JsonValueKind.Object&&row.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
    private static object Profile(StaffAccount user)=>new{user.Id,user.Name,user.Email,user.Role,user.BranchId,user.FleetId};
    [HttpGet]
    public async Task<IActionResult> Get() {
        if((await Current())?.Role!="system-admin")return Forbid();
        return Ok(new{staff=(await db.StaffAccounts.AsNoTracking().OrderBy(x=>x.Name).ToListAsync()).Select(Profile),branches=await Rows("tyretrack.demo.branches"),fleets=await Rows("tyretrack.demo.fleets")});
    }
    [HttpGet("technicians")]
    public async Task<IActionResult> Technicians() {
        var current=await Current();
        if(current is null||current.Role is not ("system-admin" or "branch-manager" or "service-advisor"))return Forbid();
        var users=await db.StaffAccounts.AsNoTracking().Where(x=>x.Role=="technician").ToListAsync();
        return Ok(users.Where(x=>current.Role=="system-admin"||!string.IsNullOrEmpty(current.BranchId)&&x.BranchId==current.BranchId).Select(x=>new{x.Id,x.Name,x.BranchId}));
    }
    [HttpPut("staff/{id:guid}")]
    public async Task<IActionResult> Assign(Guid id,AssignmentInput input) {
        var actor=await Current();if(actor?.Role!="system-admin")return Forbid();
        var user=await db.StaffAccounts.FindAsync(id);if(user is null)return NotFound();
        var branches=await Rows("tyretrack.demo.branches");var fleets=await Rows("tyretrack.demo.fleets");
        var branch=string.IsNullOrWhiteSpace(input.BranchId)?null:input.BranchId;
        var fleet=string.IsNullOrWhiteSpace(input.FleetId)?null:input.FleetId;
        if(user.Role=="fleet-manager") {
            if(fleet is not null) {
                var selected=fleets.FirstOrDefault(x=>Text(x,"id")==fleet);
                if(selected.ValueKind==JsonValueKind.Undefined)return BadRequest(new{message="Choose a registered fleet."});
                branch=Text(selected,"branchId");
            } else branch=null;
        } else fleet=null;
        if(branch is not null&&!branches.Any(x=>Text(x,"id")==branch&&Text(x,"status")!="Inactive"))return BadRequest(new{message="Choose an active registered branch."});
        // Clearing an assignment deliberately removes access; no broad fallback is granted.
        user.BranchId=branch;user.FleetId=fleet;
        if(branch is not null)user.Branch=Text(branches.First(x=>Text(x,"id")==branch),"name");
        if(fleet is not null)user.Branch=Text(fleets.First(x=>Text(x,"id")==fleet),"name");
        await db.SaveChangesAsync();return Ok(Profile(user));
    }
}
