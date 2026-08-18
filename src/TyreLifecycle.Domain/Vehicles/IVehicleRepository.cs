using Volo.Abp.Domain.Repositories;

namespace TyreLifecycle.Vehicles;

public interface IVehicleRepository : IBasicRepository<Vehicle, Guid>
{
    Task<Vehicle?> FindByRegistrationAsync(string registrationNumber, CancellationToken cancellationToken = default);
}
