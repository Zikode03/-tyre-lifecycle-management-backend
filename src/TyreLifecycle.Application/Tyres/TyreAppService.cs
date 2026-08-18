using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Tyres;

public class TyreAppService : ApplicationService, ITyreAppService
{
    private readonly ITyreRepository _tyreRepository;
    private readonly TyreManager _tyreManager;

    public TyreAppService(ITyreRepository tyreRepository, TyreManager tyreManager)
    {
        _tyreRepository = tyreRepository;
        _tyreManager = tyreManager;
    }

    public async Task<TyreDto> GetAsync(Guid id) => Map(await _tyreRepository.GetAsync(id));

    public async Task<PagedResultDto<TyreDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _tyreRepository.GetCountAsync();
        var items = await _tyreRepository.GetPagedListAsync(input.SkipCount, input.MaxResultCount, input.Sorting ?? nameof(Tyre.CreationTime), true);
        return new PagedResultDto<TyreDto>(totalCount, items.Select(Map).ToList());
    }

    public async Task<TyreDto> CreateAsync(CreateTyreDto input)
    {
        var tyre = await _tyreManager.CreateAsync(input.VehicleId, input.TyreNumber, input.Brand, input.Model, input.Size, input.Position, input.FitmentOdometerKm);
        await _tyreRepository.InsertAsync(tyre, autoSave: true);
        return Map(tyre);
    }

    public async Task<TyreDto> RecordTreadAsync(Guid id, RecordTreadDto input)
    {
        var tyre = await _tyreRepository.GetAsync(id);
        tyre.RecordTread(input.TreadDepthMm, input.HealthStatus);
        await _tyreRepository.UpdateAsync(tyre, autoSave: true);
        return Map(tyre);
    }

    public async Task<TyreDto> MoveAsync(Guid id, MoveTyreDto input)
    {
        var tyre = await _tyreRepository.GetAsync(id);
        tyre.MoveTo(input.Position);
        await _tyreRepository.UpdateAsync(tyre, autoSave: true);
        return Map(tyre);
    }

    public async Task RetireAsync(Guid id)
    {
        var tyre = await _tyreRepository.GetAsync(id);
        tyre.Retire();
        await _tyreRepository.UpdateAsync(tyre, autoSave: true);
    }

    private static TyreDto Map(Tyre tyre) => new()
    {
        Id = tyre.Id,
        VehicleId = tyre.VehicleId,
        TyreNumber = tyre.TyreNumber,
        Brand = tyre.Brand,
        Model = tyre.Model,
        Size = tyre.Size,
        Position = tyre.Position,
        TreadDepthMm = tyre.TreadDepthMm,
        HealthStatus = tyre.HealthStatus,
        FitmentOdometerKm = tyre.FitmentOdometerKm,
        IsActive = tyre.IsActive,
        CreationTime = tyre.CreationTime,
        CreatorId = tyre.CreatorId,
        LastModificationTime = tyre.LastModificationTime,
        LastModifierId = tyre.LastModifierId
    };
}
