using Microsoft.AspNetCore.Authorization;
using TyreLifecycle.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Tyres;

[Authorize(TyreLifecyclePermissions.Tyres.Default)]
public class TyreAppService : ApplicationService, ITyreAppService
{
    private readonly ITyreRepository _tyreRepository;
    private readonly TyreManager _tyreManager;

    public TyreAppService(ITyreRepository tyreRepository, TyreManager tyreManager)
    {
        _tyreRepository = tyreRepository;
        _tyreManager = tyreManager;
    }

    public async Task<TyreDto> GetAsync(Guid id)
        => ObjectMapper.Map<Tyre, TyreDto>(await _tyreRepository.GetAsync(id));

    public async Task<PagedResultDto<TyreDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _tyreRepository.GetCountAsync();
        var items = await _tyreRepository.GetPagedListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting ?? nameof(Tyre.CreationTime),
            includeDetails: true);

        return new PagedResultDto<TyreDto>(
            totalCount,
            ObjectMapper.Map<List<Tyre>, List<TyreDto>>(items));
    }

    [Authorize(TyreLifecyclePermissions.Tyres.Create)]
    public async Task<TyreDto> CreateAsync(CreateTyreDto input)
    {
        var tyre = await _tyreManager.CreateAsync(
            input.VehicleId,
            input.TyreNumber,
            input.Brand,
            input.Model,
            input.Size,
            input.Position,
            input.FitmentOdometerKm);

        await _tyreRepository.InsertAsync(tyre, autoSave: true);
        return ObjectMapper.Map<Tyre, TyreDto>(tyre);
    }

    [Authorize(TyreLifecyclePermissions.Tyres.Update)]
    public async Task<TyreDto> RecordTreadAsync(Guid id, RecordTreadDto input)
    {
        var tyre = await _tyreRepository.GetAsync(id);
        tyre.RecordTread(input.TreadDepthMm, input.HealthStatus);

        await _tyreRepository.UpdateAsync(tyre, autoSave: true);
        return ObjectMapper.Map<Tyre, TyreDto>(tyre);
    }

    [Authorize(TyreLifecyclePermissions.Tyres.Update)]
    public async Task<TyreDto> MoveAsync(Guid id, MoveTyreDto input)
    {
        var tyre = await _tyreRepository.GetAsync(id);
        tyre.MoveTo(input.Position);

        await _tyreRepository.UpdateAsync(tyre, autoSave: true);
        return ObjectMapper.Map<Tyre, TyreDto>(tyre);
    }

    [Authorize(TyreLifecyclePermissions.Tyres.Retire)]
    public async Task RetireAsync(Guid id)
    {
        var tyre = await _tyreRepository.GetAsync(id);
        tyre.Retire();
        await _tyreRepository.UpdateAsync(tyre, autoSave: true);
    }
}
