using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Inspections;

public interface IInspectionAppService : IApplicationService
{
    Task<InspectionDto> GetAsync(Guid id);
    Task<PagedResultDto<InspectionDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<InspectionDto> CreateAsync(CreateInspectionDto input);
    Task<InspectionDto> AssignTechnicianAsync(Guid id, AssignInspectionTechnicianDto input);
    Task<InspectionDto> StartAsync(Guid id);
    Task<InspectionDto> AddOrUpdateReadingAsync(Guid id, AddOrUpdateInspectionReadingDto input);
    Task<InspectionDto> CompleteAsync(Guid id, CompleteInspectionDto input);
    Task<InspectionDto> CancelAsync(Guid id, CancelInspectionDto input);
}
