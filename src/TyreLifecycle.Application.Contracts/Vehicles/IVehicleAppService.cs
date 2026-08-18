using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Vehicles;

public interface IVehicleAppService : IApplicationService
{
    Task<VehicleDto> GetAsync(Guid id);
    Task<PagedResultDto<VehicleDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<VehicleDto> CreateAsync(CreateVehicleDto input);
    Task<VehicleDto> UpdateAsync(Guid id, UpdateVehicleDto input);
    Task DeleteAsync(Guid id);
}
