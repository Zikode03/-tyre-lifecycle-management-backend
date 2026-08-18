using Microsoft.AspNetCore.Authorization;
using TyreLifecycle.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Vehicles;

[Authorize(TyreLifecyclePermissions.Vehicles.Default)]
public class VehicleAppService : ApplicationService, IVehicleAppService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly VehicleManager _vehicleManager;

    public VehicleAppService(IVehicleRepository vehicleRepository, VehicleManager vehicleManager)
    {
        _vehicleRepository = vehicleRepository;
        _vehicleManager = vehicleManager;
    }

    public async Task<VehicleDto> GetAsync(Guid id)
        => ObjectMapper.Map<Vehicle, VehicleDto>(await _vehicleRepository.GetAsync(id));

    public async Task<PagedResultDto<VehicleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _vehicleRepository.GetCountAsync();
        var items = await _vehicleRepository.GetPagedListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting ?? nameof(Vehicle.CreationTime),
            includeDetails: true);

        return new PagedResultDto<VehicleDto>(
            totalCount,
            ObjectMapper.Map<List<Vehicle>, List<VehicleDto>>(items));
    }

    [Authorize(TyreLifecyclePermissions.Vehicles.Create)]
    public async Task<VehicleDto> CreateAsync(CreateVehicleDto input)
    {
        var vehicle = await _vehicleManager.CreateAsync(
            input.CustomerId,
            input.RegistrationNumber,
            input.Make,
            input.Model,
            input.Year,
            input.OdometerKm,
            input.Vin);

        await _vehicleRepository.InsertAsync(vehicle, autoSave: true);
        return ObjectMapper.Map<Vehicle, VehicleDto>(vehicle);
    }

    [Authorize(TyreLifecyclePermissions.Vehicles.Update)]
    public async Task<VehicleDto> UpdateAsync(Guid id, UpdateVehicleDto input)
    {
        var vehicle = await _vehicleRepository.GetAsync(id);
        vehicle.UpdateOdometer(input.OdometerKm);
        vehicle.SetActive(input.IsActive);

        await _vehicleRepository.UpdateAsync(vehicle, autoSave: true);
        return ObjectMapper.Map<Vehicle, VehicleDto>(vehicle);
    }

    [Authorize(TyreLifecyclePermissions.Vehicles.Delete)]
    public Task DeleteAsync(Guid id)
        => _vehicleRepository.DeleteAsync(id, autoSave: true);
}
