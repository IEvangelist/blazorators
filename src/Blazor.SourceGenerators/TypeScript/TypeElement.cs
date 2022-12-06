// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeElement : Declaration, ITypeElement
{
    public object? TypeElementBrand { get; set; }
    public QuestionToken? QuestionToken { get; set; }
}