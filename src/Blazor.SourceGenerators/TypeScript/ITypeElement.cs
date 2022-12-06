// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public interface ITypeElement : IDeclaration
{
    public object TypeElementBrand { get; set; }
    public QuestionToken QuestionToken { get; set; }
}