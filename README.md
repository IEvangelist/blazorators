<img src="https://raw.githubusercontent.com/IEvangelist/blazorators/main/logo.png" align="right"></img>
# Blazorators: Blazor C# Source Generators

> Thank you for perusing my Blazor C# Source Generator repository. I'd really appreciate a ⭐ if you find this interesting.

[![build](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml/badge.svg)](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml)
<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-11-orange.svg?style=flat-square)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->

<!--
## Stats

![Alt](https://repobeats.axiom.co/api/embed/6d5b98efd6598a4482f1fa6391fbe651a06f77d9.svg "Repobeats analytics image")
-->

A C# source generator that creates fully functioning Blazor JavaScript interop code, targeting either the `IJSInProcessRuntime` or `IJSRuntime` types. This library provides several NuGet packages:

**Core libraries**

| NuGet package | NuGet version | Description |
|--|--|--|
| [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) | [![NuGet](https://img.shields.io/nuget/v/Blazor.SourceGenerators.svg?style=flat)](https://www.nuget.org/packages/Blazor.SourceGenerators) | Core source generator library. |
| [`Blazor.Serialization`](https://www.nuget.org/packages/Blazor.Serialization) | [![NuGet](https://img.shields.io/nuget/v/Blazor.Serialization.svg?style=flat)](https://www.nuget.org/packages/Blazor.Serialization) | Common serialization library, required in some scenarios when using generics. |

**WebAssembly libraries**

| NuGet package | NuGet version | Description |
|--|--|--|
| [`Blazor.LocalStorage.WebAssembly`](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly) | [![NuGet](https://img.shields.io/nuget/v/Blazor.LocalStorage.WebAssembly.svg?style=flat)](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly) | Blazor WebAssembly class library exposing DI-ready `IStorageService` type for the `localStorage` implementation (relies on `IJSInProcessRuntime`). |
| [`Blazor.SessionStorage.WebAssembly`](https://www.nuget.org/packages/Blazor.SessionStorage.WebAssembly) | [![NuGet](https://img.shields.io/nuget/v/Blazor.SessionStorage.WebAssembly.svg?style=flat)](https://www.nuget.org/packages/Blazor.SessionStorage.WebAssembly) | Blazor WebAssembly class library exposing DI-ready `IStorageService` type for the `sessionStorage` implementation (relies on `IJSInProcessRuntime`). |
| [`Blazor.Geolocation.WebAssembly`](https://www.nuget.org/packages/Blazor.Geolocation.WebAssembly) | [![NuGet](https://img.shields.io/nuget/v/Blazor.Geolocation.WebAssembly.svg?style=flat)](https://www.nuget.org/packages/Blazor.Geolocation.WebAssembly) | Razor class library exposing DI-ready `IGeolocationService` type (and dependent callback types) for the `geolocation` implementation (relies on `IJSInProcessRuntime`). |
| [`Blazor.SpeechSynthesis.WebAssembly`](https://www.nuget.org/packages/Blazor.SpeechSynthesis.WebAssembly) | [![NuGet](https://img.shields.io/nuget/v/Blazor.SpeechSynthesis.WebAssembly.svg?style=flat)](https://www.nuget.org/packages/Blazor.SpeechSynthesis.WebAssembly) | Razor class library exposing DI-ready `ISpeechSynthesisService` type for the `speechSynthesis` implementation (relies on `IJSInProcessRuntime`). |

> Targets the `IJSInProcessRuntime` type.

**Server libraries**

| NuGet package | NuGet version | Description |
|--|--|--|
| [`Blazor.LocalStorage`](https://www.nuget.org/packages/Blazor.LocalStorage) | [![NuGet](https://img.shields.io/nuget/v/Blazor.LocalStorage.svg?style=flat)](https://www.nuget.org/packages/Blazor.LocalStorage) | Blazor Server class library exposing DI-ready `IStorageService` type for the `localStorage` implementation (relies on `IJSRuntime`) |
| [`Blazor.SessionStorage`](https://www.nuget.org/packages/Blazor.SessionStorage) | [![NuGet](https://img.shields.io/nuget/v/Blazor.SessionStorage.svg?style=flat)](https://www.nuget.org/packages/Blazor.SessionStorage) | Blazor Server class library exposing DI-ready `IStorageService` type for the `sessionStorage` implementation (relies on `IJSRuntime`) |
| [`Blazor.Geolocation`](https://www.nuget.org/packages/Blazor.Geolocation) | [![NuGet](https://img.shields.io/nuget/v/Blazor.Geolocation.svg?style=flat)](https://www.nuget.org/packages/Blazor.Geolocation) | Razor class library exposing DI-ready `IGeolocationService` type (and dependent callback types) for the `geolocation` implementation (relies on `IJSRuntime`). |
| [`Blazor.SpeechSynthesis`](https://www.nuget.org/packages/Blazor.SpeechSynthesis) | [![NuGet](https://img.shields.io/nuget/v/Blazor.SpeechSynthesis.svg?style=flat)](https://www.nuget.org/packages/Blazor.SpeechSynthesis) | Razor class library exposing DI-ready `ISpeechSynthesisService` type for the `speechSynthesis` implementation (relies on `IJSRuntime`). |

> Targets the `IJSRuntime` type.

> **Note**<br>
> The reason that I generate two separate packages, one with an async API and another with the synchronous version is due to the explicit usage of `IJSInProcessRuntime` when using Blazor WebAssembly. This decision allows the APIs to be separate, and easily consumable from their repsective consuming Blazor apps, either Blazor server or Blazor WebAssembly. I might change it later to make this a consumer configuration, in that each consuming library will have to explicitly define a preprocessor directive to specify `IS_WEB_ASSEMBLY` defined.

## Using the `Blazor.SourceGenerators` package 📦

As an example, the official [`Blazor.LocalStorage.WebAssembly`](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes extension methods specific to Blazor WebAssembly and the [`localStorage`](https://developer.mozilla.org/docs/Web/API/Window/localStorage) Web API.

Consider the _IStorageService.cs_ C# file:

```csharp
// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoGenericInterop(
    TypeName = "Storage",
    Implementation = "window.localStorage",
    Url = "https://developer.mozilla.org/docs/Web/API/Window/localStorage",
    GenericMethodDescriptors = new[]
    {
        "getItem",
        "setItem:value"
    })]
public partial interface IStorageService
{
}
```

This code designates itself into the `Microsoft.JSInterop` namespace, making the source generated implementation available to anyone consumer who uses types from this namespace. It uses the `JSAutoGenericInterop` to specify:

- `TypeName = "Storage"`: sets the type to [`Storage`](https://developer.mozilla.org/docs/Web/API/Storage).
- `Implementation = "window.localStorage"`: expresses how to locate the implementation of the specified type from the globally scoped `window` object, this is the [`localStorage`](https://developer.mozilla.org/docs/Web/API/Window/localStorage) implementation.
- `Url`: sets the URL for the implementation.
- `GenericMethodDescriptors`: Defines the methods that should support generics as part of their source-generation. The `localStorage.getItem` is specified to return a generic `TResult` type, and the `localStorage.setItem` has its parameter with a name of `value` specified as a generic `TArg` type.

> The generic method descriptors syntax is:
> `"methodName"` for generic return type and `"methodName:parameterName"` for generic parameter type.

The file needs to define an interface and it needs to be `partial`, for example; `public partial interface`. Decorating the class with the `JSAutoInterop` (or `JSAutoGenericInterop) attribute will source generate the following C# code, as shown in the source generated _IStorageServiceService.g.cs_:

```csharp
using Blazor.Serialization.Extensions;
using System.Text.Json;

#nullable enable
namespace Microsoft.JSInterop;

/// <summary>
/// Source generated interface definition of the <c>Storage</c> type.
/// </summary>
public partial interface IStorageServiceService
{
    /// <summary>
    /// Source generated implementation of <c>window.localStorage.length</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/length"></a>
    /// </summary>
    double Length
    {
        get;
    }

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.clear</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/clear"></a>
    /// </summary>
    void Clear();

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.getItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/getItem"></a>
    /// </summary>
    TValue? GetItem<TValue>(string key, JsonSerializerOptions? options = null);

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.key</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/key"></a>
    /// </summary>
    string? Key(double index);

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.removeItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/removeItem"></a>
    /// </summary>
    void RemoveItem(string key);

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.setItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/setItem"></a>
    /// </summary>
    void SetItem<TValue>(string key, TValue value, JsonSerializerOptions? options = null);
}
```

These internal extension methods rely on the `IJSInProcessRuntime` to perform JavaScript interop. From the given `TypeName` and corresponding `Implementation`, the following code is also generated:

- `IStorageService.g.cs`: The interface for the corresponding `Storage` Web API surface area.
- `LocalStorgeService.g.cs`: The `internal` implementation of the `IStorageService` interface.
- `LocalStorageServiceCollectionExtensions.g.cs`: Extension methods to add the `IStorageService` service to the dependency injection `IServiceCollection`.

Here is the source generated `LocalStorageService` implementation:

```csharp
// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License:
// https://github.com/IEvangelist/blazorators/blob/main/LICENSE
// Auto-generated by blazorators.

#nullable enable

using Blazor.Serialization.Extensions;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Microsoft.JSInterop;

/// <inheritdoc />
internal sealed class LocalStorageService : IStorageService
{
    private readonly IJSInProcessRuntime _javaScript = null;

    /// <inheritdoc cref="P:Microsoft.JSInterop.IStorageService.Length" />
    double IStorageService.Length => _javaScript.Invoke<double>("eval", new object[1]
    {
        "window.localStorage.length"
    });

    public LocalStorageService(IJSInProcessRuntime javaScript)
    {
        _javaScript = javaScript;
    }

    /// <inheritdoc cref="M:Microsoft.JSInterop.IStorageService.Clear" />
    void IStorageService.Clear()
    {
        _javaScript.InvokeVoid("window.localStorage.clear");
    }

    /// <inheritdoc cref="M:Microsoft.JSInterop.IStorageService.GetItem``1(System.String,System.Text.Json.JsonSerializerOptions)" />
    TValue? IStorageService.GetItem<TValue>(string key, JsonSerializerOptions? options)
    {
        return _javaScript.Invoke<string>("window.localStorage.getItem", new object[1]
        {
            key
        }).FromJson<TValue>(options);
    }

    /// <inheritdoc cref="M:Microsoft.JSInterop.IStorageService.Key(System.Double)" />
    string? IStorageService.Key(double index)
    {
        return _javaScript.Invoke<string>("window.localStorage.key", new object[1]
        {
            index
        });
    }

    /// <inheritdoc cref="M:Microsoft.JSInterop.IStorageService.RemoveItem(System.String)" />
    void IStorageService.RemoveItem(string key)
    {
        _javaScript.InvokeVoid("window.localStorage.removeItem", key);
    }

    /// <inheritdoc cref="M:Microsoft.JSInterop.IStorageService.SetItem``1(System.String,``0,System.Text.Json.JsonSerializerOptions)" />
    void IStorageService.SetItem<TValue>(string key, TValue value, JsonSerializerOptions? options)
    {
        _javaScript.InvokeVoid("window.localStorage.setItem", key, value.ToJson<TValue>(options));
    }
}
```

Finally, here is the source generated service collection extension methods:

```csharp
using Microsoft.JSInterop;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary></summary>
public static class LocalStorageServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="IStorageService" /> service to the service collection.
    /// </summary>
    public static IServiceCollection AddLocalStorageServices(
        this IServiceCollection services) =>
        services.AddSingleton<IJSInProcessRuntime>(serviceProvider =>
            (IJSInProcessRuntime)serviceProvider.GetRequiredService<IJSRuntime>())
            .AddSingleton<IStorageService, LocalStorageService>();
}
```

Putting this all together, the `Blazor.LocalStorage.WebAssembly` NuGet package is actually less than 15 lines of code, and it generates full DI-ready services with JavaScript interop.

The `Blazor.LocalStorage` package, generates extensions on the `IJSRuntime` type.

```csharp
// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Storage",
    Implementation = "window.localStorage",
    HostingModel = BlazorHostingModel.Server,
    OnlyGeneratePureJS = true,
    Url = "https://developer.mozilla.org/docs/Web/API/Window/localStorage")]
public partial interface IStorageServiceService
{
}
```

Generates the following:

```csharp
// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License:
// https://github.com/IEvangelist/blazorators/blob/main/LICENSE
// Auto-generated by blazorators.

using System.Threading.Tasks;

#nullable enable
namespace Microsoft.JSInterop;

public partial interface IStorageServiceService
{
    /// <summary>
    /// Source generated implementation of <c>window.localStorage.length</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/length"></a>
    /// </summary>
    ValueTask<double> Length
    {
        get;
    }

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.clear</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/clear"></a>
    /// </summary>
    ValueTask ClearAsync();

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.getItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/getItem"></a>
    /// </summary>
    ValueTask<string?> GetItemAsync(string key);

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.key</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/key"></a>
    /// </summary>
    ValueTask<string?> KeyAsync(double index);

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.removeItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/removeItem"></a>
    /// </summary>
    ValueTask RemoveItemAsync(string key);

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.setItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/setItem"></a>
    /// </summary>
    ValueTask SetItemAsync(string key, string value);
}
```

Notice, that since the generic method descriptors are not added generics are not supported. This is not yet implemented as I've been focusing on WebAssembly scenarios.

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
// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Geolocation",
    Implementation = "window.navigator.geolocation",
    Url = "https://developer.mozilla.org/docs/Web/API/Geolocation")]
public partial interface IGeolocationService
{
}
```

The source generator will expose the `JSAutoInteropAttribute`, and consuming libraries will decorate their classes with it. The generator code will see this class, and use the `TypeName` from the attribute to find the corresponding type to implement.
With the type name, the generator will generate the corresponding methods, and return types. The method implementations will be extensions of the `IJSRuntime`.

The following is an example resulting source generated `IGeolocationService` object:

```csharp
namespace Microsoft.JSInterop;

public partial interface IGeolocationService
{
    /// <summary>
    /// Source generated implementation of <c>window.navigator.geolocation.clearWatch</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Geolocation/clearWatch"></a>
    /// </summary>
    void ClearWatch(double watchId);

    /// <summary>
    /// Source generated implementation of <c>window.navigator.geolocation.getCurrentPosition</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Geolocation/getCurrentPosition"></a>
    /// </summary>
    /// <param name="component">The calling Razor (or Blazor) component.</param>
    /// <param name="onSuccessCallbackMethodName">Expects the name of a <c>"JSInvokableAttribute"</c> C# method with the following <c>System.Action{GeolocationPosition}"</c>.</param>
    /// <param name="onErrorCallbackMethodName">Expects the name of a <c>"JSInvokableAttribute"</c> C# method with the following <c>System.Action{GeolocationPositionError}"</c>.</param>
    /// <param name="options">The <c>PositionOptions</c> value.</param>
    void GetCurrentPosition<TComponent>(
        TComponent component,
        string onSuccessCallbackMethodName,
        string? onErrorCallbackMethodName = null,
        PositionOptions? options = null)
        where TComponent : class;

    /// <summary>
    /// Source generated implementation of <c>window.navigator.geolocation.watchPosition</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Geolocation/watchPosition"></a>
    /// </summary>
    /// <param name="component">The calling Razor (or Blazor) component.</param>
    /// <param name="onSuccessCallbackMethodName">Expects the name of a <c>"JSInvokableAttribute"</c> C# method with the following <c>System.Action{GeolocationPosition}"</c>.</param>
    /// <param name="onErrorCallbackMethodName">Expects the name of a <c>"JSInvokableAttribute"</c> C# method with the following <c>System.Action{GeolocationPositionError}"</c>.</param>
    /// <param name="options">The <c>PositionOptions</c> value.</param>
    double WatchPosition<TComponent>(
        TComponent component, 
        string onSuccessCallbackMethodName, 
        string? onErrorCallbackMethodName = null, 
        PositionOptions? options = null) 
        where TComponent : class;
}

```

The generator will also produce the corresponding APIs object types. For example, the Geolocation API defines the following:

- `GeolocationService`
- `PositionOptions`
- `GeolocationCoordinates`
- `GeolocationPosition`
- `GeolocationPositionError`

```csharp
namespace Microsoft.JSInterop;

/// <inheritdoc />
internal sealed class GeolocationService : IGeolocationService
{
    private readonly IJSInProcessRuntime _javaScript = null;

    public GeolocationService(IJSInProcessRuntime javaScript)
    {
        _javaScript = javaScript;
    }

    /// <inheritdoc cref="M:Microsoft.JSInterop.IGeolocationService.ClearWatch(System.Double)" />
    void IGeolocationService.ClearWatch(double watchId)
    {
        _javaScript.InvokeVoid("window.navigator.geolocation.clearWatch", watchId);
    }

    /// <inheritdoc cref="M:Microsoft.JSInterop.IGeolocationService.GetCurrentPosition``1(``0,System.String,System.String,Microsoft.JSInterop.PositionOptions)" />
    void IGeolocationService.GetCurrentPosition<TComponent>(
        TComponent component, 
        string onSuccessCallbackMethodName, 
        string? onErrorCallbackMethodName, 
        PositionOptions? options)
    {
        _javaScript.InvokeVoid("blazorators.getCurrentPosition", DotNetObjectReference.Create<TComponent>(component), onSuccessCallbackMethodName, onErrorCallbackMethodName, options);
    }

    /// <inheritdoc cref="M:Microsoft.JSInterop.IGeolocationService.WatchPosition``1(``0,System.String,System.String,Microsoft.JSInterop.PositionOptions)" />
    double IGeolocationService.WatchPosition<TComponent>(
        TComponent component, 
        string onSuccessCallbackMethodName, 
        string? onErrorCallbackMethodName, 
        PositionOptions? options)
    {
        return _javaScript.Invoke<double>("blazorators.watchPosition", new object[4]
        {
            DotNetObjectReference.Create<TComponent>(component),
            onSuccessCallbackMethodName,
            onErrorCallbackMethodName,
            options
        });
    }
}
```

```csharp
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

/// <summary>
/// Source-generated object representing an ideally immutable <c>GeolocationPosition</c> value.
/// </summary>
public class GeolocationPosition
{
    /// <summary>
    /// Source-generated property representing the <c>GeolocationPosition.coords</c> value.
    /// </summary>
    [JsonPropertyName("coords")]
    public GeolocationCoordinates Coords
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationPosition.timestamp</c> value.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationPosition.timestamp</c> value, 
    /// converted as a <see cref="T:System.DateTime" /> in UTC.
    /// </summary>
    [JsonIgnore]
    public DateTime TimestampAsUtcDateTime => Timestamp.ToDateTimeFromUnix();
}

/// <summary>
/// Source-generated object representing an ideally immutable <c>GeolocationCoordinates</c> value.
/// </summary>
public class GeolocationCoordinates
{
    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.accuracy</c> value.
    /// </summary>
    [JsonPropertyName("accuracy")]
    public double Accuracy
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.altitude</c> value.
    /// </summary>
    [JsonPropertyName("altitude")]
    public double? Altitude
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.altitudeAccuracy</c> value.
    /// </summary>
    [JsonPropertyName("altitudeAccuracy")]
    public double? AltitudeAccuracy
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.heading</c> value.
    /// </summary>
    [JsonPropertyName("heading")]
    public double? Heading
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.latitude</c> value.
    /// </summary>
    [JsonPropertyName("latitude")]
    public double Latitude
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.longitude</c> value.
    /// </summary>
    [JsonPropertyName("longitude")]
    public double Longitude
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationCoordinates.speed</c> value.
    /// </summary>
    [JsonPropertyName("speed")]
    public double? Speed
    {
        get;
        set;
    }
}

/// <summary>
/// Source-generated object representing an ideally immutable <c>GeolocationPositionError</c> value.
/// </summary>
public class GeolocationPositionError
{
    /// <summary>
    /// Source-generated property representing the <c>GeolocationPositionError.code</c> value.
    /// </summary>
    [JsonPropertyName("code")]
    public double Code
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationPositionError.message</c> value.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationPositionError.PERMISSION_DENIED</c> value.
    /// </summary>
    [JsonPropertyName("PERMISSION_DENIED")]
    public double PERMISSION_DENIED
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationPositionError.POSITION_UNAVAILABLE</c> value.
    /// </summary>
    [JsonPropertyName("POSITION_UNAVAILABLE")]
    public double POSITION_UNAVAILABLE
    {
        get;
        set;
    }

    /// <summary>
    /// Source-generated property representing the <c>GeolocationPositionError.TIMEOUT</c> value.
    /// </summary>
    [JsonPropertyName("TIMEOUT")]
    public double TIMEOUT
    {
        get;
        set;
    }
}

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

## Future work

- https://developer.mozilla.org/docs/Web/API/CredentialsContainer
- https://developer.mozilla.org/docs/Web/API/WakeLock
- https://developer.mozilla.org/docs/Web/API/Navigator/hid
- https://developer.mozilla.org/docs/Web/API/Web_Crypto_API

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
  <tbody>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://www.cnblogs.com/weihanli"><img src="https://avatars.githubusercontent.com/u/7604648?v=4?s=100" width="100px;" alt="Weihan Li"/><br /><sub><b>Weihan Li</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/commits?author=WeihanLi" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://www.microsoft.com"><img src="https://avatars.githubusercontent.com/u/7679720?v=4?s=100" width="100px;" alt="David Pine"/><br /><sub><b>David Pine</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/commits?author=IEvangelist" title="Code">💻</a> <a href="#design-IEvangelist" title="Design">🎨</a> <a href="https://github.com/IEvangelist/blazorators/pulls?q=is%3Apr+reviewed-by%3AIEvangelist" title="Reviewed Pull Requests">👀</a> <a href="#ideas-IEvangelist" title="Ideas, Planning, & Feedback">🤔</a> <a href="https://github.com/IEvangelist/blazorators/commits?author=IEvangelist" title="Tests">⚠️</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://nimbleapps.cloud"><img src="https://avatars.githubusercontent.com/u/1657085?v=4?s=100" width="100px;" alt="Robert McLaws"/><br /><sub><b>Robert McLaws</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/commits?author=robertmclaws" title="Code">💻</a> <a href="https://github.com/IEvangelist/blazorators/issues?q=author%3Arobertmclaws" title="Bug reports">🐛</a> <a href="#ideas-robertmclaws" title="Ideas, Planning, & Feedback">🤔</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://colinsalmcorner.com"><img src="https://avatars.githubusercontent.com/u/1932561?v=4?s=100" width="100px;" alt="Colin Dembovsky"/><br /><sub><b>Colin Dembovsky</b></sub></a><br /><a href="#infra-colindembovsky" title="Infrastructure (Hosting, Build-Tools, etc)">🚇</a> <a href="#platform-colindembovsky" title="Packaging/porting to new platform">📦</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://tanayparikh.com"><img src="https://avatars.githubusercontent.com/u/14852843?v=4?s=100" width="100px;" alt="Tanay Parikh"/><br /><sub><b>Tanay Parikh</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/commits?author=TanayParikh" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/taori"><img src="https://avatars.githubusercontent.com/u/5545184?v=4?s=100" width="100px;" alt="Andreas Müller"/><br /><sub><b>Andreas Müller</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/issues?q=author%3Ataori" title="Bug reports">🐛</a> <a href="https://github.com/IEvangelist/blazorators/commits?author=taori" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://www.mahmudx.com"><img src="https://avatars.githubusercontent.com/u/16564582?v=4?s=100" width="100px;" alt="Mahmudul Hasan"/><br /><sub><b>Mahmudul Hasan</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/commits?author=MahmudX" title="Code">💻</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/fabiansanchez18"><img src="https://avatars.githubusercontent.com/u/106093861?v=4?s=100" width="100px;" alt="fabiansanchez18"/><br /><sub><b>fabiansanchez18</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/issues?q=author%3Afabiansanchez18" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://weblogs.asp.net/sfeldman"><img src="https://avatars.githubusercontent.com/u/1309622?v=4?s=100" width="100px;" alt="Sean Feldman"/><br /><sub><b>Sean Feldman</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/issues?q=author%3ASeanFeldman" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/daver77"><img src="https://avatars.githubusercontent.com/u/2369739?v=4?s=100" width="100px;" alt="daver77"/><br /><sub><b>daver77</b></sub></a><br /><a href="#ideas-daver77" title="Ideas, Planning, & Feedback">🤔</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Denny09310"><img src="https://avatars.githubusercontent.com/u/50493437?v=4?s=100" width="100px;" alt="Denny09310"/><br /><sub><b>Denny09310</b></sub></a><br /><a href="https://github.com/IEvangelist/blazorators/commits?author=Denny09310" title="Code">💻</a> <a href="https://github.com/IEvangelist/blazorators/commits?author=Denny09310" title="Tests">⚠️</a> <a href="#ideas-Denny09310" title="Ideas, Planning, & Feedback">🤔</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind are welcome!
