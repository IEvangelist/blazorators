# Blazorators: The Source Generated `clipboard` JavaScript Interop library for Blazor

The [`Blazor.Clipboard.WebAssembly`](https://www.nuget.org/packages/Blazor.Clipboard.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `IClipboardService` interface specific to Blazor WebAssembly and the [`clipboard`](https://developer.mozilla.org/docs/Web/API/Clipboard) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddClipboardServices` method to register the `IClipboardService` service type.

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

builder.Services.AddClipboardServices();

await builder.Build().RunAsync();
```

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `IClipboardService` type. The interface takes the following shape:

```csharp
#nullable enable
namespace Microsoft.JSInterop;

/// <summary>
/// Source generated interface definition of the <c>Clipboard</c> type.
/// </summary>
public partial interface IClipboardService
{
    /// <summary>
    /// Source generated implementation of <c>window.navigator.clipboard.read</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Clipboard/read"></a>
    /// </summary>
    ValueTask<ClipboardItems> ReadAsync();

    /// <summary>
    /// Source generated implementation of <c>window.navigator.clipboard.readText</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Clipboard/readText"></a>
    /// </summary>
    ValueTask<string> ReadTextAsync();

    /// <summary>
    /// Source generated implementation of <c>window.navigator.clipboard.write</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Clipboard/write"></a>
    /// </summary>
    ValueTask WriteAsync(
        ClipboardItems data);

    /// <summary>
    /// Source generated implementation of <c>window.navigator.clipboard.writeText</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Clipboard/writeText"></a>
    /// </summary>
    ValueTask WriteTextAsync(
        string data);
}
```
