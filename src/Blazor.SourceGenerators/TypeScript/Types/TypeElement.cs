// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class TypeElement : Declaration, ITypeElement
{
    public object TypeElementBrand { get; set; }
    public QuestionToken QuestionToken { get; set; }
}