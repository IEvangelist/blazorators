// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Extension methods for converting the JavaScript <c>DOMTimeStamp</c>
/// value into a C# localized <see cref="DateTime"/>.
/// </summary>
public static class LongExtensions
{
    /// <summary>
    /// Converts the <paramref name="millisecondsFromEpoch"/> to a <see cref="DateTime"/> instance.
    /// </summary>
    /// <param name="millisecondsFromEpoch">The number of milliseconds past <see cref="DateTime.UnixEpoch"/>.</param>
    /// <returns>A new <see cref="DateTime"/> instance converted to a local date and time.</returns>
    public static DateTime ToDateTimeFromUnix(this long millisecondsFromEpoch) =>
        DateTime.UnixEpoch.AddMilliseconds(millisecondsFromEpoch);

    /// <summary>
    /// Converts the <paramref name="millisecondsFromEpoch"/> to a <see cref="DateTime"/> instance.
    /// When <paramref name="millisecondsFromEpoch"/> is <c>null</c>, <c>null</c> is returned.
    /// </summary>
    /// <param name="millisecondsFromEpoch">The number of milliseconds past <see cref="DateTime.UnixEpoch"/>.</param>
    /// <returns>A new <see cref="DateTime"/> instance converted to a local date and time.</returns>
    public static DateTime? ToDateTimeFromUnix(this long? millisecondsFromEpoch) =>
        millisecondsFromEpoch.HasValue
            ? millisecondsFromEpoch.Value.ToDateTimeFromUnix()
            : null;
}
