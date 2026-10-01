using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Common;

/// <summary>Nhận diện các xung đột SQL Server có thể gửi lại an toàn.</summary>
public static class AuctionConcurrency
{
    public static bool IsExpectedConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return true;
        }

        for (var current = exception; current is not null; current = current.InnerException!)
        {
            var number = current.GetType().GetProperty("Number", BindingFlags.Public | BindingFlags.Instance)?.GetValue(current);
            if (number is int sqlNumber && sqlNumber is 1205 or 2601 or 2627 or 3960)
            {
                return true;
            }
        }

        return false;
    }
}
