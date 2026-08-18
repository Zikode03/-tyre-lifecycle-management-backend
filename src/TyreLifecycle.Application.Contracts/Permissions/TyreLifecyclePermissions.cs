namespace TyreLifecycle.Permissions;

public static class TyreLifecyclePermissions
{
    public const string GroupName = "TyreLifecycle";

    public static class Customers
    {
        public const string Default = GroupName + ".Customers";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class Vehicles
    {
        public const string Default = GroupName + ".Vehicles";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class Tyres
    {
        public const string Default = GroupName + ".Tyres";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Retire = Default + ".Retire";
    }
}
