using Volo.Abp.Localization;
using Volo.Abp.Authorization.Permissions;

namespace TyreLifecycle.Permissions;

public class TyreLifecyclePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(TyreLifecyclePermissions.GroupName, new FixedLocalizableString("Tyre Lifecycle"));

        var customers = group.AddPermission(TyreLifecyclePermissions.Customers.Default, new FixedLocalizableString("Customers"));
        customers.AddChild(TyreLifecyclePermissions.Customers.Create, new FixedLocalizableString("Create customers"));
        customers.AddChild(TyreLifecyclePermissions.Customers.Update, new FixedLocalizableString("Update customers"));
        customers.AddChild(TyreLifecyclePermissions.Customers.Delete, new FixedLocalizableString("Delete customers"));

        var vehicles = group.AddPermission(TyreLifecyclePermissions.Vehicles.Default, new FixedLocalizableString("Vehicles"));
        vehicles.AddChild(TyreLifecyclePermissions.Vehicles.Create, new FixedLocalizableString("Create vehicles"));
        vehicles.AddChild(TyreLifecyclePermissions.Vehicles.Update, new FixedLocalizableString("Update vehicles"));
        vehicles.AddChild(TyreLifecyclePermissions.Vehicles.Delete, new FixedLocalizableString("Delete vehicles"));

        var tyres = group.AddPermission(TyreLifecyclePermissions.Tyres.Default, new FixedLocalizableString("Tyres"));
        tyres.AddChild(TyreLifecyclePermissions.Tyres.Create, new FixedLocalizableString("Create tyres"));
        tyres.AddChild(TyreLifecyclePermissions.Tyres.Update, new FixedLocalizableString("Update tyres"));
        tyres.AddChild(TyreLifecyclePermissions.Tyres.Retire, new FixedLocalizableString("Retire tyres"));

        var inspections = group.AddPermission(TyreLifecyclePermissions.Inspections.Default, new FixedLocalizableString("Inspections"));
        inspections.AddChild(TyreLifecyclePermissions.Inspections.Create, new FixedLocalizableString("Create inspections"));
        inspections.AddChild(TyreLifecyclePermissions.Inspections.Update, new FixedLocalizableString("Update inspections"));
        inspections.AddChild(TyreLifecyclePermissions.Inspections.Complete, new FixedLocalizableString("Complete inspections"));
        inspections.AddChild(TyreLifecyclePermissions.Inspections.Cancel, new FixedLocalizableString("Cancel inspections"));
    }
}
