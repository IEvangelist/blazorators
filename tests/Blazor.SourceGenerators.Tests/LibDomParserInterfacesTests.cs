// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using TypeScript.TypeConverter.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests
{
    public class LibDomParserInterfacesTests
    {
        [Fact]
        public void CorrectlyConvertsTypeScriptInterfaceToCSharpRecord()
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
            var expected = @"namespace Microsoft.JSInterop;

public record MediaKeySystemConfiguration(
    MediaKeySystemMediaCapability[]? AudioCapabilities,
    MediaKeysRequirement? DistinctiveIdentifier,
    string[]? InitDataTypes,
    string? Label,
    MediaKeysRequirement? PersistentState,
    string[]? SessionTypes,
    MediaKeySystemMediaCapability[]? VideoCapabilities
);
";

            Assert.NotNull(actual);

            var actualRecordStr = actual.ToRecordString();
            Assert.Equal(expected.NormalizeNewlines(), actualRecordStr.NormalizeNewlines());
        }

        [Fact]
        public void CorrectlyConvertsTypeScriptInterfaceToCSharpAction()
        {
            //            var text = @"interface PositionCallback {
            //    (position: GeolocationPosition): void;
            //}";
            //            var sut = new InterfaceConverter();
            //            var actual = sut.ToCSharpSourceText(text);
            //            var expected = @"Action<GeolocationPosition> positionCallback";

            //Assert.Equal(expected, actual);
        }

        [Fact]
        public void CorrectlyConvertsTypeScriptInterfaceToCSharpStaticObject()
        {
            //            var text = @"interface Geolocation {
            //    clearWatch(watchId: number): void;
            //    getCurrentPosition(successCallback: PositionCallback, errorCallback ?: PositionErrorCallback | null, options ?: PositionOptions): void;
            //    watchPosition(successCallback: PositionCallback, errorCallback ?: PositionErrorCallback | null, options ?: PositionOptions): number;
            //}";
            //            var sut = new InterfaceConverter();
            //            var actual = sut.ToCSharpSourceText(text);
            //            var expected = @"Action<GeolocationPosition> positionCallback";

            //Assert.Equal(expected, actual);
        }
    }
}