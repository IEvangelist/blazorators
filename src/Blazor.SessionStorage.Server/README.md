# Blazorators: The Source Generated `sessionStorage` JavaScript Interop library for Blazor Server

The [`Blazor.SessionStorage.Server`](https://www.nuget.org/packages/Blazor.SessionStorage.Server) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `ISessionStorage` interface specific to Blazor WebAssembly and the [`sessionStorage`](https://developer.mozilla.org/docs/Web/API/Window/sessionStorage) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddSessionStorageServices` method to register the `IStorageService` service type.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSessionStorageServices();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
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
    ValueTask ClearAsync();

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.getItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/getItem"></a>
    /// </summary>
    ValueTask<string?> GetItemAsync(string key);

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.key</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/key"></a>
    /// </summary>
    ValueTask<string?> KeyAsync(double index);

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.removeItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/removeItem"></a>
    /// </summary>
    ValueTask RemoveItemAsync(string key);

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.setItem</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/setItem"></a>
    /// </summary>
    ValueTask SetItemAsync(string key, string value);

    /// <summary>
    /// Source generated implementation of <c>window.sessionStorage.length</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Storage/length"></a>
    /// </summary>
    ValueTask<double> Length { get; }
}
```
