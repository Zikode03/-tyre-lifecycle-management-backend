namespace TyreLifecycle;

public static class TyreLifecycleDomainErrorCodes
{
    public const string CustomerNumberAlreadyExists = "TyreLifecycle:Customer:0001";
    public const string VehicleRegistrationAlreadyExists = "TyreLifecycle:Vehicle:0001";
    public const string VehicleOdometerCannotMoveBackwards = "TyreLifecycle:Vehicle:0002";
    public const string TyreNumberAlreadyExists = "TyreLifecycle:Tyre:0001";
    public const string InvalidTreadDepth = "TyreLifecycle:Tyre:0002";
}
