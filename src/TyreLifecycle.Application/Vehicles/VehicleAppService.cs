using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Vehicles;

public class VehicleAppService : ApplicationService, IVehicleAppService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly VehicleManager _vehicleManager;

    public VehicleAppService(IVehicleRepository vehicleRepository, VehicleManager vehicleManager)
    {
        _vehicleRepository = vehicleRepository;
        _vehicleManager = vehicleManager;
    }

    public async Task<VehicleDto> GetAsync(Guid id) => Map(await _vehicleRepository.GetAsync(id));

    public async Task<PagedResultDto<VehicleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _vehicleRepository.GetCountAsync();
        var items = await _vehicleRepository.GetPagedListAsync(input.SkipCount, input.MaxResultCount, input.Sorting ?? nameof(Vehicle.CreationTime), true);
        return new PagedResultDto<VehicleDto>(totalCount, items.Select(Map).ToList());
    }

    public async Task<VehicleDto> CreateAsync(CreateVehicleDto input)
    {
        var vehicle = await _vehicleManager.CreateAsync(input.CustomerId, input.RegistrationNumber, input.Make, input.Model, input.Year, input.OdometerKm, input.Vin);
        await _vehicleRepository.InsertAsync(vehicle, autoSave: true);
        return Map(vehicle);
    }

    public async Task<VehicleDto> UpdateAsync(Guid id, UpdateVehicleDto input)
    {
        var vehicle = await _vehicleRepository.GetAsync(id);
        vehicle.UpdateOdometer(input.OdometerKm);
        vehicle.SetActive(input.IsActive);
        await _vehicleRepository.UpdateAsync(vehicle, autoSave: true);
        return Map(vehicle);
    }

    public Task DeleteAsync(Guid id) => _vehicleRepository.DeleteAsync(id, autoSave: true);

    private static VehicleDto Map(Vehicle vehicle) => new()
    {
        Id = vehicle.Id,
        CustomerId = vehicle.CustomerId,
        RegistrationNumber = vehicle.RegistrationNumber,
        Make = vehicle.Make,
        Model = vehicle.Model,
        Year = vehicle.Year,
        OdometerKm = vehicle.OdometerKm,
        Vin = vehicle.Vin,
        IsActive = vehicle.IsActive,
        CreationTime = vehicle.CreationTime,
        CreatorId = vehicle.CreatorId,
        LastModificationTime = vehicle.LastModificationTime,
        LastModifierId = vehicle.LastModifierId
    };
}
