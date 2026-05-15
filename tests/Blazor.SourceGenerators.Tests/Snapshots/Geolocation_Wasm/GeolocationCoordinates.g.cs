#nullable enable
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

/// <summary>
/// Source-generated object representing an ideally immutable <c>GeolocationCoordinates</c> value.
/// </summary>
public class GeolocationCoordinates
{
    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.accuracy</c> value.
    /// </summary>
    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }
    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.altitude</c> value.
    /// </summary>
    [JsonPropertyName("altitude")]
    public double? Altitude { get; set; } = default!;
    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.altitudeAccuracy</c> value.
    /// </summary>
    [JsonPropertyName("altitudeAccuracy")]
    public double? AltitudeAccuracy { get; set; } = default!;
    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.heading</c> value.
    /// </summary>
    [JsonPropertyName("heading")]
    public double? Heading { get; set; } = default!;
    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.latitude</c> value.
    /// </summary>
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }
    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.longitude</c> value.
    /// </summary>
    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.speed</c> value.
    /// </summary>
    [JsonPropertyName("speed")]
    public double? Speed { get; set; } = default!;
}
