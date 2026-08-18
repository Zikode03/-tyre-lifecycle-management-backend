using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Tyres;

public interface ITyreAppService : IApplicationService
{
    Task<TyreDto> GetAsync(Guid id);
    Task<PagedResultDto<TyreDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<TyreDto> CreateAsync(CreateTyreDto input);
    Task<TyreDto> RecordTreadAsync(Guid id, RecordTreadDto input);
    Task<TyreDto> MoveAsync(Guid id, MoveTyreDto input);
    Task RetireAsync(Guid id);
}
