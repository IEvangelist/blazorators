// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Types;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class TypeMapTests
{
    [Theory]
    // Originally-supported primitives — guard regression of the existing set.
    [InlineData("number", "double")]
    [InlineData("string", "string")]
    [InlineData("boolean", "bool")]
    [InlineData("Date", "DateTime")]
    [InlineData("DOMTimeStamp", "long")]
    [InlineData("EpochTimeStamp", "long")]
    [InlineData("number | null", "double?")]
    [InlineData("string | null", "string?")]
    [InlineData("boolean | null", "bool?")]
    // Added primitives — see T5.2.
    [InlineData("void", "void")]
    [InlineData("any", "object")]
    [InlineData("any | null", "object?")]
    [InlineData("unknown", "object")]
    [InlineData("unknown | null", "object?")]
    [InlineData("object", "object")]
    [InlineData("object | null", "object?")]
    [InlineData("bigint", "long")]
    [InlineData("bigint | null", "long?")]
    // Typed-array views map to native .NET arrays.
    [InlineData("ArrayBuffer", "byte[]")]
    [InlineData("ArrayBuffer | null", "byte[]?")]
    [InlineData("Uint8Array", "byte[]")]
    [InlineData("Uint8ClampedArray", "byte[]")]
    [InlineData("Uint16Array", "ushort[]")]
    [InlineData("Uint32Array", "uint[]")]
    [InlineData("Int8Array", "sbyte[]")]
    [InlineData("Int16Array", "short[]")]
    [InlineData("Int32Array", "int[]")]
    [InlineData("BigInt64Array", "long[]")]
    [InlineData("BigUint64Array", "ulong[]")]
    [InlineData("Float32Array", "float[]")]
    [InlineData("Float64Array", "double[]")]
    public void Indexer_MapsTypeScriptTokenToCSharpType(
        string typeScriptType,
        string expectedCSharpType)
    {
        Assert.Equal(expectedCSharpType, TypeMap.PrimitiveTypes[typeScriptType]);
    }

    [Theory]
    [InlineData("number")]
    [InlineData("boolean")]
    [InlineData("void")]
    [InlineData("any")]
    [InlineData("unknown")]
    [InlineData("object")]
    [InlineData("bigint")]
    [InlineData("ArrayBuffer")]
    [InlineData("Uint8Array")]
    [InlineData("Float32Array")]
    public void IsPrimitiveType_RecognizesAddedTypeScriptTokens(string typeScriptType)
    {
        Assert.True(TypeMap.PrimitiveTypes.IsPrimitiveType(typeScriptType));
    }

    [Theory]
    [InlineData("double")]
    [InlineData("string")]
    [InlineData("byte[]")]
    [InlineData("object")]
    [InlineData("long")]
    [InlineData("float[]")]
    public void IsPrimitiveType_RecognizesAlreadyMappedCSharpTypes(string csharpType)
    {
        // The IsPrimitiveType check looks at both keys and values, so a type
        // that's already been mapped through once must still report true on
        // the second pass (the parser relies on this when walking through
        // already-translated parameter types).
        Assert.True(TypeMap.PrimitiveTypes.IsPrimitiveType(csharpType));
    }

    [Fact]
    public void IsPrimitiveType_DoesNotMatchUnknownTokens()
    {
        Assert.False(TypeMap.PrimitiveTypes.IsPrimitiveType("Geolocation"));
        Assert.False(TypeMap.PrimitiveTypes.IsPrimitiveType("EventTarget"));
    }
}
