using AutoMapper;
using TyreLifecycle.Customers;
using TyreLifecycle.Inspections;
using TyreLifecycle.Tyres;
using TyreLifecycle.Vehicles;

namespace TyreLifecycle;

public class TyreLifecycleApplicationAutoMapperProfile : Profile
{
    public TyreLifecycleApplicationAutoMapperProfile()
    {
        CreateMap<Customer, CustomerDto>();
        CreateMap<Vehicle, VehicleDto>();
        CreateMap<Tyre, TyreDto>();
        CreateMap<InspectionTyreReading, InspectionTyreReadingDto>();
        CreateMap<Inspection, InspectionDto>();
    }
}
