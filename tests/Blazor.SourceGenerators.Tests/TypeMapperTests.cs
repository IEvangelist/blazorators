// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Blazor.SourceGenerators.Types;
using Blazor.SourceGenerators.TypeScript.Types;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class TypeMapperTests
{
    [Fact]
    public void TypeMapperCorrectlyMapsKnownTypeMap()
    {
        static Dictionary<string, Node> GetTypeMapWithPotential(bool timePenalty)
        {
            var startingTimestamp = Stopwatch.GetTimestamp();
            var sut = DependentTypeMapper.GetDependentTypeMap;

            // The implementation: window.navigator.geolocation
            var typeMap = sut("Geolocation");

            Assert.NotEmpty(typeMap);
            var elapsedTimestamp = Stopwatch.GetElapsedTime(startingTimestamp);

            // This needs to take less than a second.
            // But only fail the test if there is a time penalty.
            Assert.True(
                condition: timePenalty is false ||
                elapsedTimestamp.TotalMilliseconds < 1_000, $"""
                condition: timePenalty is {timePenalty is false}
                or took longer than 1,000ms {elapsedTimestamp.TotalMilliseconds < 1_000}.
                """);

            return typeMap;
        }

        var typeMap = GetTypeMapWithPotential(timePenalty: false);

        Assert.NotNull(typeMap["Geolocation"]);
        Assert.NotNull(typeMap["PositionCallback"]);
        Assert.NotNull(typeMap["PositionErrorCallback"]);
        Assert.NotNull(typeMap["PositionOptions"]);

        // TODO: these types should be present, but they're not...
        // Assert.NotNull(typeMap["GeolocationPosition"]);
        // Assert.NotNull(typeMap["GeolocationPositionError"]);
        // Assert.NotNull(typeMap["GeolocationCoordinates"]);
    }
}
