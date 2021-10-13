// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.JSInterop.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class JavaScriptInteropAttribute : Attribute
{
    public string? TypeName { get; set; }

    public string? Url { get; set; }
}
