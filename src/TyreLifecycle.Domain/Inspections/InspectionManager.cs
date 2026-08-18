using TyreLifecycle.Vehicles;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace TyreLifecycle.Inspections;

public class InspectionManager : DomainService
{
    private readonly IInspectionRepository _inspectionRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public InspectionManager(
        IInspectionRepository inspectionRepository,
        IVehicleRepository vehicleRepository)
    {
        _inspectionRepository = inspectionRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Inspection> CreateAsync(
        string inspectionNumber,
        Guid vehicleId,
        long? odometerKm = null)
    {
        Check.NotNullOrWhiteSpace(inspectionNumber, nameof(inspectionNumber));
        Check.NotDefaultOrNull(vehicleId, nameof(vehicleId));

        var normalizedNumber = inspectionNumber.Trim().ToUpperInvariant();
        if (await _inspectionRepository.FindByInspectionNumberAsync(normalizedNumber) is not null)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.InspectionNumberAlreadyExists)
                .WithData("InspectionNumber", normalizedNumber);
        }

        await _vehicleRepository.GetAsync(vehicleId);

        return new Inspection(GuidGenerator.Create(), normalizedNumber, vehicleId, odometerKm);
    }
}
