using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace TyreLifecycle.Tyres;

public class TyreManager : DomainService
{
    private readonly ITyreRepository _tyreRepository;

    public TyreManager(ITyreRepository tyreRepository)
    {
        _tyreRepository = tyreRepository;
    }

    public async Task<Tyre> CreateAsync(Guid vehicleId, string tyreNumber, string brand, string model, string size, string position, long fitmentOdometerKm)
    {
        Check.NotDefaultOrNull<Guid>(vehicleId, nameof(vehicleId));
        Check.NotNullOrWhiteSpace(tyreNumber, nameof(tyreNumber));
        Check.NotNullOrWhiteSpace(brand, nameof(brand));
        Check.NotNullOrWhiteSpace(model, nameof(model));
        Check.NotNullOrWhiteSpace(size, nameof(size));
        Check.NotNullOrWhiteSpace(position, nameof(position));

        var normalizedTyreNumber = tyreNumber.Trim().ToUpperInvariant();
        if (await _tyreRepository.FindByTyreNumberAsync(normalizedTyreNumber) is not null)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.TyreNumberAlreadyExists)
                .WithData("TyreNumber", normalizedTyreNumber);
        }

        return new Tyre(GuidGenerator.Create(), vehicleId, normalizedTyreNumber, brand.Trim(), model.Trim(), size.Trim(), position.Trim(), fitmentOdometerKm);
    }
}
