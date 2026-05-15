#nullable enable
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

/// <summary>
/// Source-generated object representing an ideally immutable <c>GeolocationPosition</c> value.
/// </summary>
public class GeolocationPosition
{
    /// <summary>
    /// Source-generated property representing the <c>GeolocationPosition.coords</c> value.
    /// </summary>
    [JsonPropertyName("coords")]
    public GeolocationCoordinates Coords { get; set; } = default!;
    /// <summary>
    /// Source-generated property representing the <c>GeolocationPosition.timestamp</c> value.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    /// <summary>
    /// Source-generated property representing the <c>GeolocationPosition.timestamp</c> value, 
    /// converted as a <see cref="System.DateTime" /> in UTC.
    /// </summary>
    [JsonIgnore]
    public DateTime TimestampAsUtcDateTime => Timestamp.ToDateTimeFromUnix();
}
