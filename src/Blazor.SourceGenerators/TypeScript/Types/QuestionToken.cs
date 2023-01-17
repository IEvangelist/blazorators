// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class QuestionToken : Token
{
    public QuestionToken()
    {
        Kind = TypeScriptSyntaxKind.QuestionToken;
    }
}