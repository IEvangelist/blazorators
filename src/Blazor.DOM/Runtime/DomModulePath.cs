// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal static class DomModulePath
{
    public static string ForAssemblyContaining<TMarker>()
    {
        var assemblyName = typeof(TMarker).Assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            throw new InvalidOperationException(
                "The DOM runtime assembly must have a name for static web asset resolution.");
        }

        return $"./_content/{assemblyName}/blazorators.dom.js";
    }
}
