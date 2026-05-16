#nullable enable
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

/// <summary>
/// Source-generated object representing an ideally immutable <c>PermissionStatus</c> value.
/// </summary>
public class PermissionStatus
{
    /// <summary>
    /// Source-generated property representing the <c>PermissionStatus.name</c> value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
    /// <summary>
    /// Source-generated property representing the <c>PermissionStatus.state</c> value.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = default!;
}
