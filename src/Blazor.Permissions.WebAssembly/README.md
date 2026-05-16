# Blazorators: The Source Generated `permissions` JavaScript Interop library for Blazor WebAssembly

The [`Blazor.Permissions.WebAssembly`](https://www.nuget.org/packages/Blazor.Permissions.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source-generated `IPermissionsService` interface specific to Blazor WebAssembly and the [`permissions`](https://developer.mozilla.org/docs/Web/API/Permissions) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddPermissionsServices` method to register the `IPermissionsService` service type.

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

builder.Services.AddPermissionsServices();

await builder.Build().RunAsync();
```

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `IPermissionsService` type. The interface takes the following shape:

```csharp
#nullable enable
using System.Threading.Tasks;

namespace Microsoft.JSInterop;

/// <summary>
/// Source generated interface definition of the <c>Permissions</c> type.
/// </summary>
public interface IPermissionsService
{
    /// <summary>
    /// Source generated implementation of <c>window.navigator.permissions.query</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/Permissions/query"></a>
    /// </summary>
    ValueTask<PermissionStatus> QueryAsync(PermissionDescriptor permissionDesc);
}
```

### Add JavaScript dependency

In the _index.html_ file, add the following:

```html
<script src="_content/Blazor.Permissions.WebAssembly/blazorators.permissions.g.js"></script>
```
