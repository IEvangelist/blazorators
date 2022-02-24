# Blazorators: Blazor C# Source Generators

![Blazorators Logo](https://raw.githubusercontent.com/IEvangelist/blazorators/main/logo.png)

> Thank you for perusing my Blazor C# Source Generators repository. I'd really appreciate a ⭐ if you find this interesting.

<!-- TODO: Create separate README.md files specific to the NuGet packages. -->

[![build](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml/badge.svg)](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml) [![pull request](https://github.com/IEvangelist/blazorators/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/IEvangelist/blazorators/actions/workflows/pr-validation.yml)
<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-2-orange.svg?style=flat-square)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->

A C# source generator that creates extensions methods on the Blazor WebAssembly JavaScript implementation of the `IJSInProcessRuntime` type. This library provides several NuGet packages:

| NuGet package | NuGet version |
|--|--|
| [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) | [![NuGet](https://img.shields.io/nuget/v/Blazor.SourceGenerators.svg?style=flat)](https://www.nuget.org/packages/Blazor.SourceGenerators) |
| [`Blazor.LocalStorage.WebAssembly`](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly) | [![NuGet](https://img.shields.io/nuget/v/Blazor.LocalStorage.WebAssembly.svg?style=flat)](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly) |
| [`Blazor.LocalStorage.Server`](https://www.nuget.org/packages/Blazor.LocalStorage.Server) | [![NuGet](https://img.shields.io/nuget/v/Blazor.LocalStorage.Server.svg?style=flat)](https://www.nuget.org/packages/Blazor.LocalStorage.Server) |

## Using the `Blazor.SourceGenerators` package 📦

As an example, the official [`Blazor.LocalStorage.WebAssembly`](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes extension methods specific to Blazor WebAssembly and the [`localStorage`](https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage) Web API.

Consider the _LocalStorageExtensions.cs_ C# file:

```csharp
namespace Microsoft.JSInterop;

/// <summary>
/// Source generated extension methods on the 
/// <see cref="IJSInProcessRuntime"/> implementation.
/// </summary>
[JSAutoInterop(
    TypeName = "Storage",
    PathFromWindow = "window.localStorage",
    HostingModel = BlazorHostingModel.WebAssembly,
    OnlyGeneratePureJS = true,
    Url = "https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage")]
public static partial class LocalStorageExtensions
{
}
```

This code designates itself into the `Microsoft.JSInterop` namespace, making all of the source generated extensions available to anyone consumer who uses types from this namespace. It uses the `JSAutoInterop` to specify:

- `TypeName = "Storage"`: sets the type to [`Storage`](https://developer.mozilla.org/docs/Web/API/Storage).
- `PathFromWindow = "window.localStorage"`: expresses how to locate the implementation of the specified type from the globally scoped `window` object, this is the [`localStorage`](https://developer.mozilla.org/docs/Web/API/Window/localStorage) implementation.
- `HostingModel = BlazorHostingModel.WebAssembly`: tells the generator to create synchronous extension methods on the `IJSInProcessRuntime` type (default), use `.Server` for `IJSRuntime` and Task-based asynchronous methods instead.
- `OnlyGeneratePureJS = true`: configures the source generator to emit only C#, when `false` will emit JavaScript.
- `Url`: sets the URL for the implementation.

The file needs to define an extension class and needs to be `partial`, for example; `public static partial class`. Decorating the class with the `JSAutoInterop` attribute will source generate the following C# code:

```csharp
#nullable enable
namespace Microsoft.JSInterop
{
    public static partial class LocalStorageExtensions
    {
        /// <summary>
        /// Source generated extension method implementation of <c>window.localStorage.clear</c>.
        /// <a href="https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage/clear"></a>
        /// </summary>
        public static void Clear(
            this IJSInProcessRuntime javaScript) =>
            javaScript.InvokeVoid("window.localStorage.clear");

        /// <summary>
        /// Source generated extension method implementation of <c>window.localStorage.getItem</c>.
        /// <a href="https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage/getItem"></a>
        /// </summary>
        public static string? GetItem(
            this IJSInProcessRuntime javaScript,
            string key) =>
            javaScript.Invoke<string?>(
                "window.localStorage.getItem",
                key);

        /// <summary>
        /// Source generated extension method implementation of <c>window.localStorage.key</c>.
        /// <a href="https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage/key"></a>
        /// </summary>
        public static string? Key(
            this IJSInProcessRuntime javaScript,
            double index) =>
            javaScript.Invoke<string?>(
                "window.localStorage.key",
                index);

        /// <summary>
        /// Source generated extension method implementation of <c>window.localStorage.removeItem</c>.
        /// <a href="https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage/removeItem"></a>
        /// </summary>
        public static void RemoveItem(
            this IJSInProcessRuntime javaScript,
            string key) =>
            javaScript.InvokeVoid(
                "window.localStorage.removeItem",
                key);

        /// <summary>
        /// Source generated extension method implementation of <c>window.localStorage.setItem</c>.
        /// <a href="https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage/setItem"></a>
        /// </summary>
        public static void SetItem(
            this IJSInProcessRuntime javaScript,
            string key,
            string value) =>
            javaScript.InvokeVoid(
                "window.localStorage.setItem",
                key,
                value);
    }
}
```

## Using the `Blazor.LocalStorage.WebAssembly` package 📦

The [`Blazor.LocalStorage.WebAssembly`](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly) package is a WebAssembly specific implementation of the `localStorage` Web API that has been source generated. The example above is the result of this published package.

This package exposes a convenience extension method on the `IServiceCollection` type, named `AddWebAssemblyLocalStorage`. Calling this will expose the `IJSInProcessRuntime` as a dependency injection service type as a scoped lifetime. Consider the following _Program.cs_ C# file for an example Blazor WebAssembly template project:

```csharp
using Blazor.ExampleConsumer;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(
    sp => new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    });

// Adds the IJSInProcessRuntime type to DI.
builder.Services.AddWebAssemblyLocalStorage();

await builder.Build().RunAsync();
```

Then, in your components you can consume this as you would any other service type. Consider the _Counter.razor_ file:

```razor
@page "/counter"
@inject IJSInProcessRuntime JavaScript

<PageTitle>Counter (@_currentCount)</PageTitle>

<h1>Counter</h1>

<p role="status">Current count: @_currentCount</p>

<button class="btn btn-primary" @onclick="IncrementCount">Increment</button>

@code {
    private int _currentCount = 0;

    private void IncrementCount() => JavaScript.SetItem("CounterValue", (++ _currentCount).ToString());

    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (JavaScript.GetItem("CounterValue") is { } count && int.TryParse(count, out var currentCount))
        {
            _currentCount = currentCount;
        }
    }
}
```

## Design goals 🎯

I was hoping to use the [TypeScript lib.dom.d.ts](https://github.com/microsoft/TypeScript/blob/315b807489b8ff3a892179488fb0c00398d9b2c3/lib/lib.dom.d.ts) bits as input. This input would be read, parsed, and cached within the generator. The generator code would be capable of generating extension methods on the `IJSRuntime`. Additionally, the generator will create object graphs from the well know web APIs.

Using the _lib.dom.d.ts_ file, we could hypothetically parse various TypeScript type definitions. These definitions could then be converted to C# counterparts. While I realize that not all TypeScript is mappable to C#, there is a bit of room for interpretation.

Consider the following type definition:

```typescript
/**
An object can programmatically obtain the position of the device.
It gives Web content access to the location of the device. This allows
a Web site or app to offer customized results based on the user's location.
*/
interface Geolocation {

    clearWatch(watchId: number): void;

    getCurrentPosition(
        successCallback: PositionCallback,
        errorCallback?: PositionErrorCallback | null,
        options?: PositionOptions): void;
    
    watchPosition(
        successCallback: PositionCallback,
        errorCallback?: PositionErrorCallback | null,
        options?: PositionOptions): number;
}
```

> This is from the TypeScript repo, [lib.dom.d.ts file lines 5,498-5,502](https://github.com/microsoft/TypeScript/blob/315b807489b8ff3a892179488fb0c00398d9b2c3/lib/lib.dom.d.ts#L5497-L5502).

### Example consumption of source generator ✔️

Ideally, I would like to be able to define a C# class such as this:

```csharp
[JSAutoInterop(
    TypeName = "Geolocation",
    PathFromWidow = "window.navigator.geolocation",
    Url = "https://developer.mozilla.org/en-US/docs/Web/API/Geolocation_API",
    OnlyGeneratePureJS = false)]
public static partial class GeolocationExtensions { }
```

The source generator will expose the `JSAutoInteropAttribute`, and consuming libraries will decorate their classes with it. The generator code will see this class, and use the `TypeName` from the attribute to find the corresponding type to implement.
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
    public static ValueTask GetCurrentPositionAsync<T>(
        this IJSRuntime jsRuntime,
        T dotnetObject,
        string successMethodName,
        string? errorMethodName = null,
        PositionOptions? options = null)
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
    public static ValueTask<double> WatchPositionAsync<T>(
        this IJSRuntime jsRuntime,
        T dotnetObject,
        string successMethodName,
        string? errorMethodName = null,
        PositionOptions? options = null)
        where T : class
    {
        return jsRuntime.InvokeAsync<double>(
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
    public ValueTask ClearWatchAsync(this IJSRuntime jsRuntime, double id)
    {
        return jsRuntime.InvokevoidAsync(
            "navigator.geolocation.clearWatch", id
        );
    }
}
```

The generator will also produce the corresponding APIs object types. For example, the Geolocation API defines the following:

- `PositionOptions`
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
                nameof(OnErrorRequestingCoordinates));
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
    public async Task OnErrorRequestingCoordinates(
        GeolocationPositionError error)
    {
        // TODO: consume/handle error.

        await InvokeAsync(StateHasChanged);
    }
}
```

## Pseudocode and logical flow ➡️

1. Consumer decorates a `static partial class` with the `JSAutoInteropAttribute`.
1. Source generator is called:
   - `JavaScriptInteropGenerator.Initialize`
   - `JavaScriptInteropGenerator.Execute`
1. The generator determines the `TypeName` from the attribute of the contextual class.
   1. The `TypeName` is used to look up the corresponding TypeScript type definition.
   1. If found, and a valid API - attempt source generation.

<!-- TODO: Add mermaid sequence diagram here -->

## Known limitations ⚠️

At the time of writing, only pure JavaScript interop is supported. It is a stretch goal to add the following (currently missing) features:

- Source generate corresponding (and supporting) JavaScript files.
  - We'd need to accept a desired output path from the consumer, `JavaScriptOutputPath`.
  - We would need to append all JavaScript into a single builder, and emit it collectively.
- Allow for declarative and custom type mappings, for example; suppose the consumer wants the API to use generics instead of `string`.
  - We'd need to expose a `TypeConverter` parameter and allow for consumers to implement their own.
  - We'd provide a default one for standard JSON serialization, `StringTypeConverter` (maybe make this the default).

## References and resources 📑

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
    <td align="center"><a href="https://www.microsoft.com"><img src="https://avatars.githubusercontent.com/u/7679720?v=4?s=100" width="100px;" alt=""/><br /><sub><b>David Pine</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/commits?author=IEvangelist" title="Code">💻</a> <a href="#design-IEvangelist" title="Design">🎨</a> <a href="https://github.com/IEvangelist/blazorators/pulls?q=is%3Apr+reviewed-by%3AIEvangelist" title="Reviewed Pull Requests">👀</a> <a href="#ideas-IEvangelist" title="Ideas, Planning, & Feedback">🤔</a> <a href="https://github.com/IEvangelist/blazorators/commits?author=IEvangelist" title="Tests">⚠️</a></td>
  </tr>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind are welcome!
