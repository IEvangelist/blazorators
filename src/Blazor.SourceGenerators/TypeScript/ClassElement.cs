// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ClassElement : Declaration, IClassElement
{
    object IClassElement.ClassElementBrand? { get; set; }
}