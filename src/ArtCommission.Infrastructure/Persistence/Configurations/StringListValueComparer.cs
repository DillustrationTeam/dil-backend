using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

internal sealed class StringListValueComparer : ValueComparer<List<string>>
{
    public static readonly StringListValueComparer Instance = new();

    private StringListValueComparer()
        : base(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value.ToList())
    {
    }
}
