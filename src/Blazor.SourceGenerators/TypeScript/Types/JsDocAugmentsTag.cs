﻿// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocAugmentsTag : JsDocTag
{
    public JsDocAugmentsTag()
    {
        Kind = TypeScriptSyntaxKind.JsDocAugmentsTag;
    }

    public JsDocTypeExpression TypeExpression { get; set; }
}