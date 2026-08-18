using Microsoft.AspNetCore.Authorization;
using TyreLifecycle.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Inspections;

[Authorize(TyreLifecyclePermissions.Inspections.Default)]
public class InspectionAppService : ApplicationService, IInspectionAppService
{
    private readonly IInspectionRepository _inspectionRepository;
    private readonly InspectionManager _inspectionManager;

    public InspectionAppService(
        IInspectionRepository inspectionRepository,
        InspectionManager inspectionManager)
    {
        _inspectionRepository = inspectionRepository;
        _inspectionManager = inspectionManager;
    }

    public async Task<InspectionDto> GetAsync(Guid id)
        => ObjectMapper.Map<Inspection, InspectionDto>(
            await _inspectionRepository.GetAsync(id, includeDetails: true));

    public async Task<PagedResultDto<InspectionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _inspectionRepository.GetCountAsync();
        var items = await _inspectionRepository.GetPagedListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting ?? nameof(Inspection.CreationTime),
            includeDetails: true);

        return new PagedResultDto<InspectionDto>(
            totalCount,
            ObjectMapper.Map<List<Inspection>, List<InspectionDto>>(items));
    }

    [Authorize(TyreLifecyclePermissions.Inspections.Create)]
    public async Task<InspectionDto> CreateAsync(CreateInspectionDto input)
    {
        var inspection = await _inspectionManager.CreateAsync(
            input.InspectionNumber,
            input.VehicleId,
            input.OdometerKm);

        await _inspectionRepository.InsertAsync(inspection, autoSave: true);
        return ObjectMapper.Map<Inspection, InspectionDto>(inspection);
    }

    [Authorize(TyreLifecyclePermissions.Inspections.Update)]
    public async Task<InspectionDto> AssignTechnicianAsync(Guid id, AssignInspectionTechnicianDto input)
    {
        var inspection = await _inspectionRepository.GetAsync(id, includeDetails: true);
        inspection.AssignTechnician(input.TechnicianUserId);

        await _inspectionRepository.UpdateAsync(inspection, autoSave: true);
        return ObjectMapper.Map<Inspection, InspectionDto>(inspection);
    }

    [Authorize(TyreLifecyclePermissions.Inspections.Update)]
    public async Task<InspectionDto> StartAsync(Guid id)
    {
        var inspection = await _inspectionRepository.GetAsync(id, includeDetails: true);
        inspection.Start(Clock.Now);

        await _inspectionRepository.UpdateAsync(inspection, autoSave: true);
        return ObjectMapper.Map<Inspection, InspectionDto>(inspection);
    }

    [Authorize(TyreLifecyclePermissions.Inspections.Update)]
    public async Task<InspectionDto> AddOrUpdateReadingAsync(Guid id, AddOrUpdateInspectionReadingDto input)
    {
        var inspection = await _inspectionRepository.GetAsync(id, includeDetails: true);

        inspection.AddOrUpdateReading(
            GuidGenerator.Create(),
            input.Position,
            input.Source,
            input.InnerTreadMm,
            input.CentreTreadMm,
            input.OuterTreadMm,
            input.PressurePsi,
            input.TyreId,
            input.WearPattern,
            input.Defects,
            input.Recommendation);

        await _inspectionRepository.UpdateAsync(inspection, autoSave: true);
        return ObjectMapper.Map<Inspection, InspectionDto>(inspection);
    }

    [Authorize(TyreLifecyclePermissions.Inspections.Complete)]
    public async Task<InspectionDto> CompleteAsync(Guid id, CompleteInspectionDto input)
    {
        var inspection = await _inspectionRepository.GetAsync(id, includeDetails: true);
        inspection.Complete(Clock.Now, input.Notes);

        await _inspectionRepository.UpdateAsync(inspection, autoSave: true);
        return ObjectMapper.Map<Inspection, InspectionDto>(inspection);
    }

    [Authorize(TyreLifecyclePermissions.Inspections.Cancel)]
    public async Task<InspectionDto> CancelAsync(Guid id, CancelInspectionDto input)
    {
        var inspection = await _inspectionRepository.GetAsync(id, includeDetails: true);
        inspection.Cancel(input.Notes);

        await _inspectionRepository.UpdateAsync(inspection, autoSave: true);
        return ObjectMapper.Map<Inspection, InspectionDto>(inspection);
    }
}
