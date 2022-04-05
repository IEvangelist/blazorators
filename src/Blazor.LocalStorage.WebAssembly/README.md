# Blazorators: The Source Generated `localStorage` JavaScript Interop library for Blazor WebAssembly

The [`Blazor.LocalStorage.WebAssembly`](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `IStorageService` interface specific to Blazor WebAssembly and the [`localStorage`](https://developer.mozilla.org/docs/Web/API/Window/localStorage) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddLocalStorageServices` method to register the `ILocalStorageService` service type.

```csharp
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

builder.Services.AddLocalStorageServices();

await builder.Build().RunAsync();
```

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `ILocalStorageService` type. The interface takes the following shape:

```csharp
using Blazor.Serialization.Extensions;
using System.Text.Json;

#nullable enable
namespace Microsoft.JSInterop;

/// <summary>
/// Source generated interface definition of the <c>Storage</c> type.
/// </summary>
public interface ILocalStorageService
{
    /// <summary>
    /// Source generated implementation of <c>window.localStorage.clear</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/clear"></a>
    /// </summary>
    void Clear();

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.getItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/getItem"></a>
    /// </summary>
    TResult? GetItem<TResult>(string key, JsonSerializerOptions? options = null);

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
    void SetItem<TArg>(string key, TArg value, JsonSerializerOptions? options = null);

    /// <summary>
    /// Source generated implementation of <c>window.localStorage.length</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/length"></a>
    /// </summary>
    double Length { get; }
}
```
