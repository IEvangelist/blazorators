// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class QuestionToken : Token
{
    public QuestionToken() => ((INode)this).Kind = TypeScriptSyntaxKind.QuestionToken;
}
