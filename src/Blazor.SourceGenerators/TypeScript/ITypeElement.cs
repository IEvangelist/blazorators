// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public interface ITypeElement : IDeclaration
{
    internal object TypeElementBrand { get; set; }
    internal QuestionToken QuestionToken { get; set; }
}