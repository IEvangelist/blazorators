# Blazorators: Blazor C# Source Generators
<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-1-orange.svg?style=flat-square)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->

[![build](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml/badge.svg)](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml) [![pull request](https://github.com/IEvangelist/blazorators/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/IEvangelist/blazorators/actions/workflows/pr-validation.yml)

## Design goals

I was hoping to use the [TypeScript DOM lib generator](https://github.com/microsoft/TypeScript-DOM-lib-generator/tree/main/inputfiles) bits as input. This input would be read, parsed, and cached within the generator. The generator code would be capable of generating extension methods on the `IJSRuntime`. Additionally, the generator will create object graphs from the well know web APIs.

### Example consumption of source generator

Ideally, I would like to be able to define a C# class such as this:

```csharp
[JavaScriptInterop]
public static partial class GeolocationExtensions { }
```

The source generator will expose the `JavaScriptInteropAttribute`, and consuming libraries will decorate their classes with it. The generator code will see this class, and remove the "Extensions" from the class name to find corresponding type to implement.

Alternatively, the consumer can override the `TypeName` and provide the `Url` as well.

```csharp
[JavaScriptInterop(
    TypeName = "Geolocation",
    Url = "https://developer.mozilla.org/en-US/docs/Web/API/Geolocation_API")]
public static partial class GeolocationExtensions { }
```

With the type name, the generator will generate the corresponding methods, and return types. The method implementations will be extensions of the `IJSRuntime`.

The following is an example resulting source generated `GeolocationExtensions` object:

```csharp
using Microsoft.JSInterop;

namespace Microsoft.JSInterop.Extensions;

public static partial class GeolocationExtensions
{
    /// <summary>
    /// See <a href="https://developer.mozilla.org/en-US/docs/Web/API/Geolocation/getCurrentPosition"></a>.
    /// </summary>
    public ValueTask GetCurrentPositionAsync<T>(
        this IJSRuntime jsRuntime,
        T dotnetObject,
        string successMethodName,
        string? errorMethodName = null,
        GeolocationOptions? options = null)
        where T : class
    {
        return jsRuntime.InvokeVoidAsync(
            "blazorator.getCurrentLocation",
            DotNetObjectReference.Create(dotnetObject),
            successMethodName,
            errorMethodName,
            options
        );
    }

    /// <summary>
    /// See <a href="https://developer.mozilla.org/en-US/docs/Web/API/Geolocation/watchPosition"></a>
    /// </summary>
    public ValueTask<long> WatchPositionAsync<T>(
        this IJSRuntime jsRuntime,
        T dotnetObject,
        string successMethodName,
        string? errorMethodName = null,
        GeolocationOptions? options = null)
        where T : class
    {
        return jsRuntime.InvokeAsync<long>(
            "blazorator.watchPosition",
            DotNetObjectReference.Create(dotnetObject),
            successMethodName,
            errorMethodName,
            options
        );
    }

    /// <summary>
    /// See <a href="https://developer.mozilla.org/en-US/docs/Web/API/Geolocation/clearWatch"></a>
    /// </summary>
    public ValueTask ClearWatchAsync(this IJSRuntime jsRuntime, long id)
    {
        return jsRuntime.InvokevoidAsync(
            "navigator.geolocation.clearWatch", id
        );
    }
}
```

The generator will also produce the corresponding APIs object types. For example, the Geolocation API defines the following:

- `GeolocationCoordinates`
- `GeolocationPosition`
- `GeolocationPositionError`

```csharp
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop.Extensions;

/// <summary>
/// See <a href="https://developer.mozilla.org/en-US/docs/Web/API/GeolocationPosition"></a>
/// </summary>
public record GeolocationPosition(
    [property: JsonPropertyName("coords")] GeolocationCoordinates Coordinates,
    [property: JsonPropertyName("timestamp")] DOMTimeStamp TimeStamp
);

/// <summary>
/// See <a href="https://developer.mozilla.org/en-US/docs/Web/API/GeolocationCoordinates"></a>
/// </summary>
public record GeolocationCoordinates(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("altitude")] double Altitude,
    [property: JsonPropertyName("altitudeAccuracy")] double? AltitudeAccuracy,
    [property: JsonPropertyName("heading")] double? Heading,
    [property: JsonPropertyName("speed")] double Speed
);

/// <summary>
/// See <a href="https://developer.mozilla.org/en-US/docs/Web/API/GeolocationPositionError"></a>
/// </summary>
public record GeolocationPositionError(
    [property: JsonPropertyName("code")] short Code,
    [property: JsonPropertyName("message")] string Message
);

// Additional models omitted for brevity...
```

In addition to this `GeolocationExtensions` class being generated, the generator will also generate a bit of JavaScript. Some methods cannot be directly invoked as they define callbacks. The approach the generator takes is to delegate callback methods on a given `T` instance, with the `JSInvokable` attribute. Our generator should also warn when the corresponding `T` instance doesn't define a matching method name that is also `JSInvokable`.

```javascript
const getCurrentLocation =
    (dotnetObj, successMethodName, errorMethodName, options) =>
    {
        if (navigator && navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(
                (position) => {
                    dotnetObj.invokeMethodAsync(
                        successMethodName, position);
                },
                (error) => {
                    dotnetObj.invokeMethodAsync(
                        errorMethodName, error);
                },
                options);
        }
    };

// Other implementations omitted for brevity...
// But we'd also define a "watchPosition" wrapper.
// The "clearWatch" is a straight pass-thru, no wrapper needed.

window.blazorator = {
    getCurrentLocation,
    watchPosition
};
```
The resulting JavaScript will have to be exposed to consuming projects. Additionally, consuming projects will need to adhere to extension method consumption semantics. When calling generated extension methods that require .NET object references of type `T`, the callback names should be marked with `JSInvokable` and the `nameof` operator should be used to ensure names are accurate. Consider the following example consuming Blazor component:

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Extensions;

namespace Example.Components;

// This is the other half of ConsumingComponent.razor
public sealed partial class ConsumingComponent
{
    [Inject]
    public IJSRuntime JavaScript { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JavaScript.GetCurrentPositionAsync(
                this,
                nameof(OnCoordinatesPermitted),
                nameof(OnErrorRequestingCooridnates));
        }
    }

    [JSInvokable]
    public async Task OnCoordinatesPermitted(
        GeolocationPosition position)
    {
        // TODO: consume/handle position.

        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnErrorRequestingCooridnates(
        GeolocationPositionError error)
    {
        // TODO: consume/handle error.

        await InvokeAsync(StateHasChanged);
    }
}
```

## NuGet packages

This repository will expose two NuGet packages:

1. The source-generated `IJSRuntime` extension methods for a select few well defined APIs.
1. The source generator itself, as a consumable analyzer package.

## References and resources

- [MDN Web Docs: Web APIs](https://developer.mozilla.org/docs/Web/API)
- [TypeScript DOM lib generator](https://github.com/microsoft/TypeScript-DOM-lib-generator)
- [ASP.NET Core Docs: Blazor JavaScript interop](https://docs.microsoft.com/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet)
- [Jared Parsons - GitHub Channel 9 Source Generators](https://github.com/jaredpar/channel9-source-generators)
- [.NET Docs: C# Source Generators](https://docs.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [Source Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
- [Source Generators: Design Document](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md)
## Contributors ✨

Thanks goes to these wonderful people ([emoji key](https://allcontributors.org/docs/en/emoji-key)):

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tr>
    <td align="center"><a href="https://www.cnblogs.com/weihanli"><img src="https://avatars.githubusercontent.com/u/7604648?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Weihan Li</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/commits?author=WeihanLi" title="Code">💻</a></td>
  </tr>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind welcome!