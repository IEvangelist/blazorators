// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Source-generator-driven interop surface for the
/// <a href="https://developer.mozilla.org/docs/Web/API/Permissions">Permissions API</a>.
/// </summary>
[JSAutoInterop(
    TypeName = "Permissions",
    Implementation = "window.navigator.permissions",
    Url = "https://developer.mozilla.org/docs/Web/API/Navigator/permissions")]
public partial interface IPermissionsService;