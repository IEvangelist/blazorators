// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace TypeScript.TypeConverter.Tests
{
    public class InterfaceConverterTests
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
            var sut = new InterfaceConverter();
            var actual = sut.ToCSharpSourceText(text);
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

            Assert.Equal(expected, actual);
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
    }
}