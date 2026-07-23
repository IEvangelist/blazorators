using System.Text.Json;
using Blazor.DOM.AdvancedTypes;

namespace Blazor.DOM.AdvancedTypes.CompilationTests;

public static class AdvancedTypeUsage
{
    public static HeadersInit CreateHeaders()
    {
        HeadersInitItemsStringAndStringTuple pair = new()
        {
            Item1 = "content-type",
            Item2 = "application/json",
        };
        return HeadersInit.FromStringAndStringTupleArray([pair]);
    }

    public static string SerializeTuple()
    {
        HeadersInitItemsStringAndStringTuple pair = new()
        {
            Item1 = "accept",
            Item2 = "application/json",
        };
        return JsonSerializer.Serialize(pair);
    }

    public static HeadersInitItemsStringAndStringTuple? DeserializeTuple(string json)
        => JsonSerializer.Deserialize<HeadersInitItemsStringAndStringTuple>(json);
}
