using Blazor.ExampleConsumer.Services;

namespace Blazor.ExampleConsumer.Serialization;

[JsonSourceGenerationOptions(
    defaults: JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true,
    AllowTrailingCommas = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PropertyNameCaseInsensitive = false,
    IncludeFields = true,
    Converters =
    [
        typeof(JsonStringEnumConverter<DarkLightMode>)
    ]
)]
[JsonSerializable(typeof(Preferences))]
public sealed partial class AppJsonSerializerContext : JsonSerializerContext;