using System.Text.Json.Nodes;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace TyreLifecycle.Vehicles;

public class Vehicle : FullAuditedAggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }
    public string RegistrationNumber { get; private set; } = string.Empty;
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public long OdometerKm { get; private set; }
    public string? Vin { get; private set; }
    public bool IsActive { get; private set; }

    protected Vehicle() { }

    public Vehicle(Guid id, Guid customerId, string registrationNumber, string make, string model, int year, long odometerKm, string? vin = null)
        : base(id)
    {
        VehicleRules.Details(make,model,year,odometerKm);
        if(customerId==Guid.Empty) throw new ArgumentException("Choose a registered customer.");
        CustomerId = customerId;
        RegistrationNumber = VehicleRules.Registration(registrationNumber);
        Make = Check.NotNullOrWhiteSpace(make, nameof(make));
        Model = Check.NotNullOrWhiteSpace(model, nameof(model));
        Year = year;
        OdometerKm = odometerKm;
        Vin = vin;
        IsActive = true;
        RecordMileage(odometerKm);
    }

    public void UpdateOdometer(long odometerKm)
    {
        if(odometerKm<0 || odometerKm>int.MaxValue) throw new ArgumentException("Enter whole kilometres from 0 to 2,147,483,647.");
        if (odometerKm < OdometerKm)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.VehicleOdometerCannotMoveBackwards)
                .WithData("CurrentOdometerKm", OdometerKm)
                .WithData("RequestedOdometerKm", odometerKm);
        }

        if(odometerKm>OdometerKm) RecordMileage(odometerKm);
        OdometerKm = odometerKm;
    }

    private void RecordMileage(long km) {
        var history=ExtraProperties.TryGetValue("MileageHistory",out var saved) && saved is not null ? JsonNode.Parse(saved.ToString()!) as JsonArray??new JsonArray() : new JsonArray();
        history.Add(new JsonObject{["at"]=DateTime.UtcNow.ToString("O"),["km"]=km});
        ExtraProperties["MileageHistory"]=history.ToJsonString();
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
