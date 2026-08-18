using Volo.Abp.Authorization.Permissions;

namespace TyreLifecycle.Permissions;

public class TyreLifecyclePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(TyreLifecyclePermissions.GroupName, "Tyre Lifecycle");

        var customers = group.AddPermission(TyreLifecyclePermissions.Customers.Default, "Customers");
        customers.AddChild(TyreLifecyclePermissions.Customers.Create, "Create customers");
        customers.AddChild(TyreLifecyclePermissions.Customers.Update, "Update customers");
        customers.AddChild(TyreLifecyclePermissions.Customers.Delete, "Delete customers");

        var vehicles = group.AddPermission(TyreLifecyclePermissions.Vehicles.Default, "Vehicles");
        vehicles.AddChild(TyreLifecyclePermissions.Vehicles.Create, "Create vehicles");
        vehicles.AddChild(TyreLifecyclePermissions.Vehicles.Update, "Update vehicles");
        vehicles.AddChild(TyreLifecyclePermissions.Vehicles.Delete, "Delete vehicles");

        var tyres = group.AddPermission(TyreLifecyclePermissions.Tyres.Default, "Tyres");
        tyres.AddChild(TyreLifecyclePermissions.Tyres.Create, "Create tyres");
        tyres.AddChild(TyreLifecyclePermissions.Tyres.Update, "Update tyres");
        tyres.AddChild(TyreLifecyclePermissions.Tyres.Retire, "Retire tyres");
    }
}
