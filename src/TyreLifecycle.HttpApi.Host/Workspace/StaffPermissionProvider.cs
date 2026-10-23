using TyreLifecycle.EntityFrameworkCore;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;

namespace TyreLifecycle.HttpApi.Host.Workspace;

public class StaffPermissionProvider(IPermissionStore store,TyreLifecycleDbContext db):PermissionValueProvider(store)
{
    public override string Name=>"StaffRole";
    public override async Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context) {
        if(!context.Permission.Name.StartsWith("TyreLifecycle."))return PermissionGrantResult.Undefined;
        var principal=context.Principal;
        if(principal?.FindFirst("kind")?.Value!="staff"||!Guid.TryParse(principal.FindFirst(AbpClaimTypes.UserId)?.Value,out var id))return PermissionGrantResult.Prohibited;
        var account=await db.StaffAccounts.FindAsync(id);
        // Typed business endpoints have no row-level filter. Only a currently saved
        // administrator can use them; an old role cookie cannot restore that privilege.
        return account?.Role=="system-admin"&&principal.FindFirst(AbpClaimTypes.Role)?.Value==account.Role?PermissionGrantResult.Granted:PermissionGrantResult.Prohibited;
    }
    public override async Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context) {
        var result=new MultiplePermissionGrantResult();
        foreach(var permission in context.Permissions)result.Result[permission.Name]=await CheckAsync(new PermissionValueCheckContext(permission,context.Principal));
        return result;
    }
}
