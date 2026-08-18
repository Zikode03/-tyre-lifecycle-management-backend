namespace TyreLifecycle.Inspections;

public enum InspectionStatus
{
    Booked = 0,
    InProgress = 1,
    AwaitingTechnician = 2,
    Completed = 3,
    Cancelled = 4
}

public enum MeasurementSource
{
    ManualGauge = 0,
    PhoneCamera = 1,
    Tpms = 2,
    ConnectedDevice = 3,
    DriveOverScanner = 4
}
