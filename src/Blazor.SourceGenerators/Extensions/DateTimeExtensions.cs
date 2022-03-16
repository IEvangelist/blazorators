// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class DateTimeExtensions
{
    const long UnixEpochTicks =
        /* Days to 1970 */ 719_162 * /* Ticks per day */ 864_000_000_000L;

    internal static DateTime UnixEpoch(this object? obj) =>
        new(UnixEpochTicks, DateTimeKind.Utc);

    internal static DateTime ToLocal(this DateTime dateTime)
    {
        var unixEpoch = dateTime.UnixEpoch();
        var difference = dateTime - unixEpoch;

        return unixEpoch.AddMilliseconds(difference.TotalMilliseconds);
    }
}
