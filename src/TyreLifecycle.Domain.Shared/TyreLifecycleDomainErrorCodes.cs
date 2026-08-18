namespace TyreLifecycle;

public static class TyreLifecycleDomainErrorCodes
{
    public const string CustomerNumberAlreadyExists = "TyreLifecycle:Customer:0001";

    public const string VehicleRegistrationAlreadyExists = "TyreLifecycle:Vehicle:0001";
    public const string VehicleOdometerCannotMoveBackwards = "TyreLifecycle:Vehicle:0002";

    public const string TyreNumberAlreadyExists = "TyreLifecycle:Tyre:0001";
    public const string InvalidTreadDepth = "TyreLifecycle:Tyre:0002";

    public const string InspectionNumberAlreadyExists = "TyreLifecycle:Inspection:0001";
    public const string InspectionCannotStart = "TyreLifecycle:Inspection:0002";
    public const string InspectionIsClosed = "TyreLifecycle:Inspection:0003";
    public const string InspectionMustBeInProgress = "TyreLifecycle:Inspection:0004";
    public const string InspectionRequiresReading = "TyreLifecycle:Inspection:0005";
    public const string CompletedInspectionCannotBeCancelled = "TyreLifecycle:Inspection:0006";
    public const string InvalidInspectionMeasurement = "TyreLifecycle:Inspection:0007";
}
