namespace TransTrack.Core;

/// <summary>
/// The invoice arithmetic, in one place.
///
/// A trip's money is computed on the full <see cref="Trip"/> entity, on the
/// lighter row the trips list projects, and on the report rows a bill is built
/// from. Those cannot share a base class — one is an entity, the others are
/// records — so they share this instead. A second copy of the rounding rule is
/// how a bill and a list end up disagreeing by a rupee.
/// </summary>
public static class TripMath
{
    /// <summary>Tax on a pre-tax total, rounded to whole rupees. Null or
    /// non-positive rate means the party is not charged GST at all — not that
    /// it is charged at zero, though the two happen to agree here.</summary>
    public static decimal Gst(decimal totalBeforeTax, decimal? gstPercentage) =>
        gstPercentage is > 0
            ? Math.Round(totalBeforeTax * gstPercentage.Value / 100m, 0, MidpointRounding.AwayFromZero)
            : 0m;
}
