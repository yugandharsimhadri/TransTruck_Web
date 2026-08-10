namespace TransTrack.Core;

/// <summary>Converts a rupee amount to the words printed on an LR — Indian
/// numbering (lakh/crore), the convention the sample stationery uses.</summary>
public static class NumberToWords
{
    private static readonly string[] Ones =
    [
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    ];

    private static readonly string[] Tens =
        ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    public static string ToRupees(decimal amount)
    {
        var rupees = (long)Math.Floor(Math.Abs(amount));
        var paise = (int)Math.Round((Math.Abs(amount) - rupees) * 100, MidpointRounding.AwayFromZero);

        var words = rupees == 0 ? "Zero" : Convert(rupees);
        var result = $"Rupees {words} Only";

        return paise > 0 ? $"Rupees {words} and {Convert(paise)} Paise Only" : result;
    }

    private static string Convert(long number)
    {
        if (number == 0) return "";

        var parts = new List<string>();

        var crore = number / 1_00_00_000;
        number %= 1_00_00_000;
        var lakh = number / 1_00_000;
        number %= 1_00_000;
        var thousand = number / 1_000;
        number %= 1_000;
        var hundred = number / 100;
        var rest = number % 100;

        if (crore > 0) parts.Add($"{Convert(crore)} Crore");
        if (lakh > 0) parts.Add($"{Convert(lakh)} Lakh");
        if (thousand > 0) parts.Add($"{Convert(thousand)} Thousand");
        if (hundred > 0) parts.Add($"{Ones[hundred]} Hundred");
        if (rest > 0) parts.Add(TwoDigits(rest));

        return string.Join(" ", parts);
    }

    private static string TwoDigits(long n) =>
        n < 20 ? Ones[n] : $"{Tens[n / 10]}{(n % 10 > 0 ? " " + Ones[n % 10] : "")}";
}
