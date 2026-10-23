using System.Text.RegularExpressions;
namespace TyreLifecycle.Vehicles;
public static class VehicleRules
{
    public static string RegistrationKey(string value) => Regex.Replace(value.ToUpperInvariant(), @"[\s-]", "");
    public static string Registration(string value) {
        var display=Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();
        if(display.Length>32 || !Regex.IsMatch(display,@"^[A-Z0-9 -]+$") || !Regex.IsMatch(RegistrationKey(display),@"^[A-Z0-9]{2,16}$")) throw new ArgumentException("Enter a registration with 2–16 letters or digits; spaces and hyphens are accepted.");
        return display;
    }
    public static void Details(string make,string model,int year,long mileage) {
        if(string.IsNullOrWhiteSpace(make)||make.Trim().Length>100||string.IsNullOrWhiteSpace(model)||model.Trim().Length>100) throw new ArgumentException("Vehicle make and model are required (up to 100 characters each).");
        if(year<1900 || year>DateTime.UtcNow.Year+1) throw new ArgumentException("Enter a model year from 1900 to next year.");
        if(mileage<0 || mileage>int.MaxValue) throw new ArgumentException("Mileage must be whole kilometres from 0 to 2,147,483,647.");
    }
}
