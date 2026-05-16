#nullable enable
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

/// <summary>
/// Source-generated object representing an ideally immutable <c>PermissionDescriptor</c> value.
/// </summary>
public class PermissionDescriptor
{
    /// <summary>
    /// Source-generated property representing the <c>PermissionDescriptor.name</c> value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
}
