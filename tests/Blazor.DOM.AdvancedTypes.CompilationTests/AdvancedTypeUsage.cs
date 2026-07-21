using System.Text.Json;
using Blazor.DOM.AdvancedTypes;

namespace Blazor.DOM.AdvancedTypes.CompilationTests;

public static class AdvancedTypeUsage
{
    public static HeadersInit CreateHeaders()
    {
        HeadersInitTupleShape_d077850b0e pair = new()
        {
            Item1 = "content-type",
            Item2 = "application/json",
        };
        HeadersInit value = new[] { pair };
        return value;
    }

    public static string SerializeTuple()
    {
        HeadersInitTupleShape_d077850b0e pair = new()
        {
            Item1 = "accept",
            Item2 = "application/json",
        };
        return JsonSerializer.Serialize(pair);
    }

    public static HeadersInitTupleShape_d077850b0e? DeserializeTuple(string json)
        => JsonSerializer.Deserialize<HeadersInitTupleShape_d077850b0e>(json);
}
