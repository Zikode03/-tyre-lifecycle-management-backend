using System.Text.RegularExpressions;

namespace TyreLifecycle.Customers;

public static class CustomerContact
{
    public static string NormalizeMobile(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || !Regex.IsMatch(input.Trim(), @"^[+0-9\s()-]+$"))
            throw new ArgumentException("Enter a South African mobile number, for example 082 123 4567 or +27 82 123 4567.");
        var digits = Regex.Replace(input, @"[^0-9]", "");
        if (digits.StartsWith("0027")) digits = "0" + digits[4..];
        else if (digits.StartsWith("27")) digits = "0" + digits[2..];
        if (!Regex.IsMatch(digits, @"^0[6-8][0-9]{8}$"))
            throw new ArgumentException("Enter a valid South African mobile number starting with 06, 07 or 08.");
        return digits;
    }

    public static string? NormalizeEmail(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var email = input.Trim();
        if (email.Length > 256 || !Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            throw new ArgumentException("Enter a valid email address, or leave it empty.");
        return email;
    }

    public static string NormalizeName(string input)
    {
        var name = Regex.Replace(input.Trim(), @"\s+", " ");
        var parts = name.Split(' ', 2);
        if (name.Length == 0 || parts[0].Length > 100 || parts.Length > 1 && parts[1].Length > 100)
            throw new ArgumentException("Enter a customer name with a first name and surname of no more than 100 characters each.");
        return name;
    }
}
