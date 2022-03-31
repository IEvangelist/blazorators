# Blazorators: The Source Generated `NetworkInformation` JavaScript Interop library for Blazor WebAssembly

The [`Blazor.NetworkInformation.WebAssembly`](https://www.nuget.org/packages/Blazor.NetworkInformation.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `INetworkInformation` interface specific to Blazor WebAssembly and the [`NetworkInformation`](https://developer.mozilla.org/docs/Web/API/Window/NetworkInformation) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddConnectionServices` method to register the `IStorageService` service type.

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

builder.Services.AddConnectionServices();

await builder.Build().RunAsync();
```

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `IStorageService` type. The interface takes the following shape:

```csharp
#nullable enable
namespace Microsoft.JSInterop;

/// <summary>
/// Source generated interface definition of the <c>NetworkInformation</c> type.
/// </summary>
public partial interface INetworkInformationService
{
    /// <summary>
    /// Source generated implementation of <c>window.navigator.connection.type</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/NetworkInformation/type"></a>
    /// </summary>
    string Type { get; }
}
```
