// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class TypeDeclarationParserMethodFilterTests
{
    [Theory]
    [InlineData("addEventListener(type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions): void;")]
    [InlineData("removeEventListener(type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions): void;")]
    public void IsMethod_FiltersExactEventListenerOverloads(string line)
    {
        Assert.False(TypeDeclarationParser.IsMethod(line, out _));
    }

    [Theory]
    [InlineData("addEventListener<K>(type: K, listener: any): void;")]
    [InlineData("removeEventListener<K>(type: K, listener: any): void;")]
    [InlineData("addEventListener<K,V>(type: K, listener: V): void;")]
    public void IsMethod_FiltersGenericEventListenerOverloads(string line)
    {
        // Regression: 'addEventListener<K>(...)' and 'removeEventListener<K>(...)'
        // (no spaces inside the type parameter list) match the method regex with a
        // 'MethodName' group equal to 'addEventListener<K>'. The original filter
        // compared only to the exact bare name, so the generic overload leaked
        // through and produced broken C# bindings.
        Assert.False(TypeDeclarationParser.IsMethod(line, out _));
    }

    [Fact]
    public void IsMethod_DoesNotFilterUnrelatedMethodWithSimilarPrefix()
    {
        // 'addEventListenerObject' is a hypothetical user-supplied method name that
        // happens to share a prefix with 'addEventListener'. The filter must not
        // swallow it.
        const string line = "addEventListenerObject(payload: string): void;";

        Assert.True(TypeDeclarationParser.IsMethod(line, out _));
    }
}
