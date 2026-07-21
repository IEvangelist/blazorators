using System.Text.Json;
using Blazor.DOM.AdvancedTypes;

namespace Blazor.DOM.AdvancedTypes.CompilationTests;

public static class AdvancedTypeUsage
{
    public static HeadersInit CreateHeaders()
    {
        HeadersInitTupleShape_11e4ef35dd pair = new()
        {
            Item1 = "content-type",
            Item2 = "application/json",
        };
        return HeadersInit.FromArray([pair]);
    }

    public static string SerializeTuple()
    {
        HeadersInitTupleShape_11e4ef35dd pair = new()
        {
            Item1 = "accept",
            Item2 = "application/json",
        };
        return JsonSerializer.Serialize(pair);
    }

    public static HeadersInitTupleShape_11e4ef35dd? DeserializeTuple(string json)
        => JsonSerializer.Deserialize<HeadersInitTupleShape_11e4ef35dd>(json);
}
