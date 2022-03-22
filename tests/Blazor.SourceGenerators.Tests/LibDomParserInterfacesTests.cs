// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.Serialization.Extensions;
using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class LibDomParserInterfacesTests
{
    [Fact]
    public void CorrectlyConvertsTypeScriptInterfaceToCSharpClass()
    {
        var text = @"interface MediaKeySystemConfiguration {
    audioCapabilities?: MediaKeySystemMediaCapability[];
    distinctiveIdentifier?: MediaKeysRequirement;
    initDataTypes?: string[];
    label?: string;
    persistentState?: MediaKeysRequirement;
    sessionTypes?: string[];
    videoCapabilities?: MediaKeySystemMediaCapability[];
}";
        var sut = new LibDomParser();
        var actual = sut.ToObject(text);
        var expected = @"#nullable enable
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

public class MediaKeySystemConfiguration
{
    [JsonPropertyName(""audioCapabilities"")]
    public MediaKeySystemMediaCapability[]? AudioCapabilities { get; set; } = default!;
    [JsonPropertyName(""distinctiveIdentifier"")]
    public MediaKeysRequirement? DistinctiveIdentifier { get; set; } = default!;
    [JsonPropertyName(""initDataTypes"")]
    public string[]? InitDataTypes { get; set; } = default!;
    [JsonPropertyName(""label"")]
    public string? Label { get; set; } = default!;
    [JsonPropertyName(""persistentState"")]
    public MediaKeysRequirement? PersistentState { get; set; } = default!;
    [JsonPropertyName(""sessionTypes"")]
    public string[]? SessionTypes { get; set; } = default!;
    [JsonPropertyName(""videoCapabilities"")]
    public MediaKeySystemMediaCapability[]? VideoCapabilities { get; set; } = default!;
}
";

        Assert.NotNull(actual);

        var actualStr = actual.ToString();
        Assert.Equal(expected.NormalizeNewlines(), actualStr.NormalizeNewlines());

        // As of right now the MediaKeysRequirement is not parsed.
        // It's a type alias, not an interface.
        Assert.Single(actual.DependentTypes);
    }

    [Fact]
    public void CorrectlyConvertsTypeScriptInterfaceToCSharpExtensionObject()
    {
        var text = @"interface Geolocation {
    clearWatch(watchId: number): void;
    getCurrentPosition(successCallback: PositionCallback, errorCallback?: PositionErrorCallback | null, options?: PositionOptions): void;
    watchPosition(successCallback: PositionCallback, errorCallback?: PositionErrorCallback | null, options?: PositionOptions): number;
}";
        var sut = new LibDomParser();
        var actual = sut.ToTopLevelObject(text);

        Assert.NotNull(actual);

        // 1. "successCallback" -ignored
        //interface PositionCallback {
        //     (position: GeolocationPosition): void;
        //}
        // 2. "position"
        //interface GeolocationPosition {
        //    readonly coords: GeolocationCoordinates;
        //    readonly timestamp: DOMTimeStamp;
        //}
        // 3. "coords"
        //interface GeolocationCoordinates {
        //    readonly accuracy: number;
        //    readonly altitude: number | null;
        //    readonly altitudeAccuracy: number | null;
        //    readonly heading: number | null;
        //    readonly latitude: number;
        //    readonly longitude: number;
        //    readonly speed: number | null;
        //}
        // 4. "errorCallback" -ignored
        // interface PositionErrorCallback {
        //    (positionError: GeolocationPositionError): void;
        //}
        // 5. "positionError"
        // interface GeolocationPositionError {
        //    readonly code: number;
        //    readonly message: string;
        //    readonly PERMISSION_DENIED: number;
        //    readonly POSITION_UNAVAILABLE: number;
        //    readonly TIMEOUT: number;
        //}
        // 6. "options"
        //interface PositionOptions {
        //    enableHighAccuracy ?: boolean;
        //    maximumAge ?: number;
        //    timeout ?: number;
        //}

        Assert.Equal(4, actual.AllDependentTypes.Count);
    }
}
