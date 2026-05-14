// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class TypeDeclarationParserCallbackTests
{
    [Theory]
    [InlineData(@"interface PositionCallback {
    (position: GeolocationPosition): void;
}", true, "named callback with single call signature")]
    [InlineData(@"interface EventListener {
    (evt: Event): void;
}", true, "non-`Callback`-suffixed but shape-identical")]
    [InlineData(@"interface VoidFunction {
    (): void;
}", true, "zero-argument callback")]
    [InlineData(@"interface IntersectionObserverCallback {
    (entries: IntersectionObserverEntry[], observer: IntersectionObserver): void;
}", true, "multi-argument callback")]
    [InlineData(@"interface Geolocation {
    clearWatch(watchId: number): void;
    getCurrentPosition(successCallback: PositionCallback): void;
}", false, "regular interface with named methods")]
    [InlineData(@"interface PositionOptions {
    enableHighAccuracy?: boolean;
    maximumAge?: number;
    timeout?: number;
}", false, "interface with only properties")]
    [InlineData("interface Empty { }", false, "empty interface (one-line)")]
    [InlineData(@"interface Empty {
}", false, "empty interface (no members)")]
    [InlineData(@"interface MultipleSignatures {
    (x: number): string;
    (x: string): number;
}", true, "multiple call-signature overloads")]
    [InlineData(@"interface Mixed {
    (x: number): void;
    extra: string;
}", false, "call signature mixed with property")]
    [InlineData("", false, "empty input")]
    [InlineData("    ", false, "whitespace-only input")]
    [InlineData("not an interface", false, "non-interface input")]
    [InlineData(@"interface NamedCommentExample {
    // anonymous call signature follows
    (item: Item): void;
}", true, "callback with trivia comments")]
    public void IsCallbackTypeDeclaration_ClassifiesShape(
        string typeScriptDeclaration,
        bool expected,
        string description)
    {
        Assert.True(
            TypeDeclarationParser.IsCallbackTypeDeclaration(typeScriptDeclaration) == expected,
            $"Expected {expected} for {description}");
    }
}
