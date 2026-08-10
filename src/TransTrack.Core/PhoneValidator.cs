using System.Text.RegularExpressions;

namespace TransTrack.Core;

/// <summary>
/// Every phone number in this app is optional — a trip still books without
/// one — but a number that is entered has to actually be one. Indian mobile
/// numbers: an optional +91 or leading 0, then ten digits starting 6-9.
/// </summary>
public static partial class PhoneValidator
{
    public static bool IsValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true;
        return Pattern().IsMatch(phone.Trim());
    }

    [GeneratedRegex(@"^(\+91[\-\s]?|0)?[6-9]\d{9}$")]
    private static partial Regex Pattern();
}
