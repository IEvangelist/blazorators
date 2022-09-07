// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;


internal class JsDocType : TypeNode, IJsDocType
{
    object IJsDocType.JsDocTypeBrand { get; set; } = default!;
}