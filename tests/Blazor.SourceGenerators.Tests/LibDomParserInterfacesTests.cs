// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class LibDomParserInterfacesTests
{
    [Fact]
    public void CorrectlyConvertsTypeScriptInterfaceToCSharpClass()
    {
        var sut = TypeDeclarationParser.Default;
        var actual = sut.ToObject("MediaKeySystemConfiguration");
        var expected = """
            #nullable enable
            using System.Text.Json.Serialization;

            namespace Microsoft.JSInterop;

            /// <summary>
            /// Source-generated object representing an ideally immutable <c>MediaKeySystemConfiguration</c> value.
            /// </summary>
            public class MediaKeySystemConfiguration
            {
                /// <summary>
                /// Source-generated property representing the <c>MediaKeySystemConfiguration.audioCapabilities</c> value.
                /// </summary>
                [JsonPropertyName("audioCapabilities")]
                public MediaKeySystemMediaCapability[]? AudioCapabilities { get; set; } = default!;
                /// <summary>
                /// Source-generated property representing the <c>MediaKeySystemConfiguration.distinctiveIdentifier</c> value.
                /// </summary>
                [JsonPropertyName("distinctiveIdentifier")]
                public MediaKeysRequirement? DistinctiveIdentifier { get; set; } = default!;
                /// <summary>
                /// Source-generated property representing the <c>MediaKeySystemConfiguration.initDataTypes</c> value.
                /// </summary>
                [JsonPropertyName("initDataTypes")]
                public string[]? InitDataTypes { get; set; } = default!;
                /// <summary>
                /// Source-generated property representing the <c>MediaKeySystemConfiguration.label</c> value.
                /// </summary>
                [JsonPropertyName("label")]
                public string? Label { get; set; } = default!;
                /// <summary>
                /// Source-generated property representing the <c>MediaKeySystemConfiguration.persistentState</c> value.
                /// </summary>
                [JsonPropertyName("persistentState")]
                public MediaKeysRequirement? PersistentState { get; set; } = default!;
                /// <summary>
                /// Source-generated property representing the <c>MediaKeySystemConfiguration.sessionTypes</c> value.
                /// </summary>
                [JsonPropertyName("sessionTypes")]
                public string[]? SessionTypes { get; set; } = default!;
                /// <summary>
                /// Source-generated property representing the <c>MediaKeySystemConfiguration.videoCapabilities</c> value.
                /// </summary>
                [JsonPropertyName("videoCapabilities")]
                public MediaKeySystemMediaCapability[]? VideoCapabilities { get; set; } = default!;
            }

            """;

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
        var sut = TypeDeclarationParser.Default;
        var actual = sut.ToTopLevelObject("Geolocation");

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
