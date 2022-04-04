# Blazorators: The Source Generated `sessionStorage` JavaScript Interop library for Blazor WebAssembly

The [`Blazor.SessionStorage.WebAssembly`](https://www.nuget.org/packages/Blazor.SessionStorage.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `IStorageService` interface specific to Blazor WebAssembly and the [`sessionStorage`](https://developer.mozilla.org/docs/Web/API/Window/sessionStorage) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddSessionStorageServices` method to register the `IStorageService` service type.

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

builder.Services.AddSessionStorageServices();

await builder.Build().RunAsync();
```

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `IStorageService` type. The interface takes the following shape:

```csharp
using Blazor.Serialization.Extensions;
using System.Text.Json;

#nullable enable
namespace Microsoft.JSInterop;

/// <summary>
/// Source generated interface definition of the <c>Storage</c> type.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.clear</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/clear"></a>
    /// </summary>
    void Clear();

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.getItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/getItem"></a>
    /// </summary>
    TResult? GetItem<TResult>(string key, JsonSerializerOptions? options = null);

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.key</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/key"></a>
    /// </summary>
    string? Key(double index);

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.removeItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/removeItem"></a>
    /// </summary>
    void RemoveItem(string key);

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.setItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/setItem"></a>
    /// </summary>
    void SetItem<TArg>(string key, TArg value, JsonSerializerOptions? options = null);

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.length</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/length"></a>
    /// </summary>
    double Length { get; }
}
```
