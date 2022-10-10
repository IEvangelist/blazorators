// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using static Blazor.SourceGenerators.TypeScript.Parser.Scanner;

namespace Blazor.SourceGenerators.TypeScript.Parser;

internal sealed class JsDocParser
{
    internal Parser Parser { get; set; }
    internal JsDocParser(Parser parser) => Parser = parser;

    private Scanner Scanner => Parser.Scanner;
    private string? SourceText => Parser.SourceText;
    private SyntaxKind CurrentToken { get => Parser.CurrentToken; set => Parser.CurrentToken = value; }
    private bool ParseErrorBeforeNextFinishedNode { get => Parser.ParseErrorBeforeNextFinishedNode; set => Parser.ParseErrorBeforeNextFinishedNode = value; }
    private List<Diagnostic>? ParseDiagnostics { get => Parser.ParseDiagnostics; set => Parser.ParseDiagnostics = value; }

    private void ClearState() => Parser.ClearState();
    private void FixupParentReferences(INode rootNode) => Parser.FixupParentReferences(rootNode);
    private void ParseErrorAtCurrentToken(DiagnosticMessage? message, object? arg0 = null) => Parser.ParseErrorAtCurrentToken(message, arg0);
    private void ParseErrorAtPosition(int start, int length, DiagnosticMessage? message, object? arg0 = null) => Parser.ParseErrorAtPosition(start, length, message, arg0);
    private SyntaxKind Token => Parser.Token;
    private SyntaxKind NextToken => Parser.NextToken;
    private T TryParse<T>(Func<T> callback) => Parser.TryParse(callback);
    private bool ParseExpected(SyntaxKind kind, DiagnosticMessage? diagnosticMessage = null, bool shouldAdvance = true) => Parser.ParseExpected(kind, diagnosticMessage, shouldAdvance);
    private bool ParseOptional(SyntaxKind kind) => Parser.ParseOptional(kind);
    private INode? ParseOptionalToken<T>(SyntaxKind kind) where T : Node, new() => Parser.ParseOptionalToken<T>(kind);
    private T ParseTokenNode<T>() where T : Node, new() => Parser.ParseTokenNode<T>(Token);
    private NodeArray<T?> CreateList<T>(T[]? elements = null, int? pos = null) where T : Node, new() => Parser.CreateList<T>(elements, pos);
    private T FinishNode<T>(T node, int? end = null) where T : Node => Parser.FinishNode(node, end);
    private Identifier ParseIdentifierName() => Parser.ParseIdentifierName();
    private NodeArray<T> ParseDelimitedList<T>(ParsingContext kind, Func<T> parseElement, bool? considerSemicolonAsDelimiter = null) where T : INode => Parser.ParseDelimitedList(kind, parseElement, considerSemicolonAsDelimiter);
    private TypeLiteralNode ParseTypeLiteral() => Parser.ParseTypeLiteral();
    private IExpression ParseExpression() => Parser.ParseExpression();

    internal bool IsJsDocType() => Token switch
    {
        SyntaxKind.AsteriskToken or
        SyntaxKind.QuestionToken or
        SyntaxKind.OpenParenToken or
        SyntaxKind.OpenBracketToken or
        SyntaxKind.ExclamationToken or
        SyntaxKind.OpenBraceToken or
        SyntaxKind.FunctionKeyword or
        SyntaxKind.DotDotDotToken or
        SyntaxKind.NewKeyword or
        SyntaxKind.ThisKeyword => true,
        _ => TokenIsIdentifierOrKeyword(Token),
    };

    internal static (JsDocTypeExpression res, List<Diagnostic>? diagnostics) ParseJsDocTypeExpressionForTests(
        string content, int? start, int? length)
    {
        var dp = new JsDocParser(new Parser());
        dp.Parser.InitializeState(content, ScriptTarget.Latest, null, ScriptKind.Js);

        var sourceFile = dp.Parser.CreateSourceFile("file.js", ScriptTarget.Latest, ScriptKind.Js);

        dp.Parser.Scanner.SetText(content, start, length);

        var currentToken = dp.Parser.Scanner.Scan();
        var jsDocTypeExpression = dp.ParseJsDocTypeExpression();
        var diagnostics = dp.Parser.ParseDiagnostics;

        dp.Parser.ClearState();


        return (jsDocTypeExpression, diagnostics);
    }

    internal JsDocTypeExpression ParseJsDocTypeExpression()
    {
        var result = new JsDocTypeExpression();


        ParseExpected(SyntaxKind.OpenBraceToken);

        result.Type = ParseJsDocTopLevelType();

        ParseExpected(SyntaxKind.CloseBraceToken);


        FixupParentReferences(result);

        return FinishNode(result);
    }


    internal IJsDocType ParseJsDocTopLevelType()
    {
        var type = ParseJsDocType();
        if (Token is SyntaxKind.BarToken)
        {
            var unionType = new JsDocUnionType { Types = ParseJsDocTypeList(type) };
            type = FinishNode(unionType);
        }

        if (Token is SyntaxKind.EqualsToken)
        {
            var optionalType = new JsDocOptionalType();

            _ = NextToken;
            optionalType.Type = type;
            type = FinishNode(optionalType);
        }

        return type;
    }

    internal IJsDocType ParseJsDocType()
    {
        var type = ParseBasicTypeExpression();
        while (true)
        {
            if (Token is SyntaxKind.OpenBracketToken)
            {
                var arrayType = new JsDocArrayType { ElementType = type };
                _ = NextToken;
                ParseExpected(SyntaxKind.CloseBracketToken);
                type = FinishNode(arrayType);
            }
            else if (Token is SyntaxKind.QuestionToken)
            {
                var nullableType = new JsDocNullableType { Type = type };
                _ = NextToken;
                type = FinishNode(nullableType);
            }
            else if (Token is SyntaxKind.ExclamationToken)
            {
                var nonNullableType = new JsDocNonNullableType { Type = type };
                _ = NextToken;
                type = FinishNode(nonNullableType);
            }
            else
            {
                break;
            }
        }

        return type;
    }


    internal IJsDocType ParseBasicTypeExpression() => Token switch
    {
        SyntaxKind.AsteriskToken => ParseJsDocAllType(),
        SyntaxKind.QuestionToken => ParseJsDocUnknownOrNullableType(),
        SyntaxKind.OpenParenToken => ParseJsDocUnionType(),
        SyntaxKind.OpenBracketToken => ParseJsDocTupleType(),
        SyntaxKind.ExclamationToken => ParseJsDocNonNullableType(),
        SyntaxKind.OpenBraceToken => ParseJsDocRecordType(),
        SyntaxKind.FunctionKeyword => ParseJsDocFunctionType(),
        SyntaxKind.DotDotDotToken => ParseJsDocVariadicType(),
        SyntaxKind.NewKeyword => ParseJsDocConstructorType(),
        SyntaxKind.ThisKeyword => ParseJsDocThisType(),
        SyntaxKind.AnyKeyword or
        SyntaxKind.StringKeyword or
        SyntaxKind.NumberKeyword or
        SyntaxKind.BooleanKeyword or
        SyntaxKind.SymbolKeyword or
        SyntaxKind.VoidKeyword or
        SyntaxKind.NullKeyword or
        SyntaxKind.UndefinedKeyword or
        SyntaxKind.NeverKeyword or
        SyntaxKind.ObjectKeyword => ParseTokenNode<JsDocType>(),
        SyntaxKind.StringLiteral or
        SyntaxKind.NumericLiteral or
        SyntaxKind.TrueKeyword or
        SyntaxKind.FalseKeyword => ParseJsDocLiteralType(),
        _ => ParseJsDocTypeReference(),
    };

    internal JsDocThisType ParseJsDocThisType()
    {
        var result = new JsDocThisType();
        _ = NextToken;
        ParseExpected(SyntaxKind.ColonToken);
        result.Type = ParseJsDocType();

        return FinishNode(result);
    }

    internal JsDocConstructorType ParseJsDocConstructorType()
    {
        var result = new JsDocConstructorType();
        _ = NextToken;
        ParseExpected(SyntaxKind.ColonToken);
        result.Type = ParseJsDocType();

        return FinishNode(result);
    }

    internal JsDocVariadicType ParseJsDocVariadicType()
    {
        var result = new JsDocVariadicType();
        _ = NextToken;
        result.Type = ParseJsDocType();

        return FinishNode(result);
    }

    internal JsDocFunctionType ParseJsDocFunctionType()
    {
        var result = new JsDocFunctionType();
        _ = NextToken;

        ParseExpected(SyntaxKind.OpenParenToken);

        var declaration = result as ISignatureDeclaration;
        declaration.Parameters = Parser.ParseDelimitedList(
            ParsingContext.JSDocFunctionParameters, ParseJsDocParameter);

        CheckForTrailingComma(declaration.Parameters);
        ParseExpected(SyntaxKind.CloseParenToken);

        if (Token is SyntaxKind.ColonToken)
        {
            _ = NextToken;
            declaration.Type = ParseJsDocType();
        }

        return FinishNode(result);
    }

    internal ParameterDeclaration ParseJsDocParameter()
    {
        var parameter = new ParameterDeclaration();
        var declaration = parameter as IVariableLikeDeclaration;
        declaration.Type = ParseJsDocType();

        if (ParseOptional(SyntaxKind.EqualsToken))
        {
            declaration.QuestionToken = new QuestionToken();
        }

        return FinishNode(parameter);
    }

    internal JsDocTypeReference ParseJsDocTypeReference()
    {
        var result = new JsDocTypeReference
        {
            Name = Parser.ParseSimplePropertyName() as Identifier
        };

        if (Token is SyntaxKind.LessThanToken)
        {
            result.TypeArguments = ParseTypeArguments();
        }
        else
        {
            while (ParseOptional(SyntaxKind.DotToken))
            {
                if (Token is SyntaxKind.LessThanToken)
                {
                    result.TypeArguments = ParseTypeArguments();
                    break;
                }
                else if (result.Name is not null)
                {
                    result.Name = ParseQualifiedName(result.Name);
                }
            }
        }

        return FinishNode(result);
    }

    internal NodeArray<IJsDocType> ParseTypeArguments()
    {
        _ = NextToken;
        var typeArguments = ParseDelimitedList(
            ParsingContext.JSDocTypeArguments, ParseJsDocType);

        CheckForTrailingComma(typeArguments);
        CheckForEmptyTypeArgumentList(typeArguments);
        ParseExpected(SyntaxKind.GreaterThanToken);

        return typeArguments;
    }

    internal void CheckForEmptyTypeArgumentList<T>(NodeArray<T> typeArguments)
    {
        if (!ParseDiagnostics.Any() && typeArguments != null && !typeArguments.Any() &&
            typeArguments is ITextRange textRange && !string.IsNullOrWhiteSpace(SourceText))
        {
            var start = (textRange.Pos ?? 0) - "<".Length;
            var end = SkipTriviaM(SourceText!, textRange.End ?? 0) + ">".Length;

            ParseErrorAtPosition(start, end - start, Diagnostics.Type_argument_list_cannot_be_empty);
        }
    }

    internal QualifiedName ParseQualifiedName(IEntityName left) =>
        FinishNode(new QualifiedName
        {
            Left = left,
            Right = ParseIdentifierName()
        });

    internal JsDocRecordType ParseJsDocRecordType() =>
        FinishNode(new JsDocRecordType { Literal = ParseTypeLiteral() });

    internal JsDocNonNullableType ParseJsDocNonNullableType()
    {
        var result = new JsDocNonNullableType();
        _ = NextToken;
        result.Type = ParseJsDocType();

        return FinishNode(result);
    }

    internal JsDocTupleType ParseJsDocTupleType()
    {
        var result = new JsDocTupleType();
        _ = NextToken;
        result.Types = ParseDelimitedList(
            ParsingContext.JSDocTupleTypes, ParseJsDocType);

        CheckForTrailingComma(result.Types);
        ParseExpected(SyntaxKind.CloseBracketToken);

        return FinishNode(result);
    }

    internal void CheckForTrailingComma<T>(NodeArray<T> list)
    {
        if (Parser.ParseDiagnostics?.Count == 0 && list.HasTrailingComma && list is ITextRange range)
        {
            var start = range.End.GetValueOrDefault() - ",".Length;
            Parser.ParseErrorAtPosition(
                start, ",".Length, Diagnostics.Trailing_comma_not_allowed);
        }
    }

    internal JsDocUnionType ParseJsDocUnionType()
    {
        var result = new JsDocUnionType();
        _ = NextToken;
        result.Types = ParseJsDocTypeList(ParseJsDocType());
        ParseExpected(SyntaxKind.CloseParenToken);

        return FinishNode(result);
    }

    internal NodeArray<IJsDocType> ParseJsDocTypeList(IJsDocType firstType)
    {
        var types = new NodeArray<IJsDocType>
        {
            firstType
        };
        var range = types as ITextRange;
        range.Pos = firstType.Pos;
        while (ParseOptional(SyntaxKind.BarToken))
        {
            types.Add(ParseJsDocType());
        }
        range.End = Scanner.StartPos;

        return types;
    }

    internal JsDocAllType ParseJsDocAllType()
    {
        var result = new JsDocAllType();
        _ = NextToken;

        return FinishNode(result);
    }

    internal JsDocLiteralType ParseJsDocLiteralType() =>
        FinishNode(new JsDocLiteralType { Literal = Parser.ParseLiteralTypeNode() });

    internal JsDocType ParseJsDocUnknownOrNullableType()
    {
        var pos = Scanner.StartPos;

        _ = NextToken;

        return Token is SyntaxKind.CommaToken or
            SyntaxKind.CloseBraceToken or
            SyntaxKind.CloseParenToken or
            SyntaxKind.GreaterThanToken or
            SyntaxKind.EqualsToken or
            SyntaxKind.BarToken
                ? FinishNode(new JsDocUnknownType())
                : FinishNode(new JsDocNullableType { Type = ParseJsDocType() });
    }


    internal Tuple<JsDoc, List<Diagnostic>?>? ParseIsolatedJsDocComment(string content, int start, int length)
    {
        Parser ??= new Parser();
        Parser.InitializeState(content, ScriptTarget.Latest, null, ScriptKind.Js);

        Parser.SourceFile = new SourceFile { LanguageVariant = LanguageVariant.Standard, Text = content };
        var jsDoc = ParseJsDocCommentWorker(start, length);
        var diagnostics = ParseDiagnostics;

        ClearState();

        return jsDoc != null ? Tuple.Create(jsDoc, diagnostics) : null;
    }


    internal JsDoc? ParseJsDocComment(INode parent, int? start, int? length)
    {
        var saveToken = CurrentToken;
        var saveParseDiagnosticsLength = Parser.ParseDiagnostics?.Count ?? 0;
        var saveParseErrorBeforeNextFinishedNode = ParseErrorBeforeNextFinishedNode;
        var comment = ParseJsDocCommentWorker(start, length);
        if (comment is INode node)
        {
            node.Parent = parent;
        }

        CurrentToken = saveToken;
        ParseDiagnostics = ParseDiagnostics.Take(saveParseDiagnosticsLength).ToList();
        ParseErrorBeforeNextFinishedNode = saveParseErrorBeforeNextFinishedNode;

        return comment;
    }

    internal JsDoc? ParseJsDocCommentWorker(int? start = null, int? length = null)
    {
        var content = Parser.SourceText ?? "";

        start ??= 0;
        var end = length is null ? content.Length : start + length;
        length = end - start;

        NodeArray<IJsDocTag> tags = new();
        List<string> comments = new();
        JsDoc? result = null;
        if (!IsJsDocStart(content, (int)start))
        {
            return result;
        }

        Scanner.ScanRange(start + 3, (length ?? 0) - 5, () =>
        {
            var advanceToken = true;
            var state = JSDocState.SawAsterisk;
            int? margin = null;
            var indent = start - Math.Max(content.LastIndexOf('\n', (int)start), 0) + 4;

            NextJsDocToken();
            while (Token is SyntaxKind.WhitespaceTrivia)
            {
                NextJsDocToken();
            }

            if (Token is SyntaxKind.NewLineTrivia)
            {
                state = JSDocState.BeginningOfLine;
                indent = 0;

                NextJsDocToken();
            }

            while (Token is not SyntaxKind.EndOfFileToken)
            {
                switch (Token)
                {
                    case SyntaxKind.AtToken:
                        if (state is JSDocState.BeginningOfLine || state is JSDocState.SawAsterisk)
                        {
                            RemoveTrailingNewlines(comments);
                            ParseTag(indent.GetValueOrDefault());

                            state = JSDocState.BeginningOfLine;
                            advanceToken = false;
                            margin = null;
                            indent++;
                        }
                        else
                        {
                            PushComment(Scanner.TokenText);
                        }
                        break;
                    case SyntaxKind.NewLineTrivia:
                        comments.Add(Scanner.TokenText);
                        state = JSDocState.BeginningOfLine;
                        indent = 0;
                        break;
                    case SyntaxKind.AsteriskToken:
                        var asterisk = Scanner.TokenText;
                        if (state is JSDocState.SawAsterisk || state is JSDocState.SavingComments)
                        {
                            state = JSDocState.SavingComments;
                            PushComment(asterisk);
                        }
                        else
                        {
                            state = JSDocState.SawAsterisk;
                            indent += asterisk.Length;
                        }
                        break;
                    case SyntaxKind.Identifier:
                        PushComment(Scanner.TokenText);
                        state = JSDocState.SavingComments;
                        break;
                    case SyntaxKind.WhitespaceTrivia:
                        var whitespace = Scanner.TokenText;
                        if (state is JSDocState.SavingComments)
                        {
                            comments.Add(whitespace);
                        }
                        else if (margin != null && (indent ?? 0) + whitespace.Length > margin)
                        {
                            comments.Add(whitespace.Slice((int)margin - (indent ?? 0) - 1));
                        }
                        indent += whitespace.Length;
                        break;
                    case SyntaxKind.EndOfFileToken:
                        break;
                    default:
                        state = JSDocState.SavingComments;
                        PushComment(Scanner.TokenText);
                        break;
                }

                if (advanceToken)
                {
                    NextJsDocToken();
                }
                else
                {
                    advanceToken = true;
                }
            }

            RemoveLeadingNewlines(comments);
            RemoveTrailingNewlines(comments);

            result = CreateJsDocComment();
            return result;

            void PushComment(string text)
            {
                margin ??= indent;
                comments.Add(text);
                indent += text.Length;
            }
        });

        return result;

        static void RemoveLeadingNewlines(List<string> comments)
        {
            while (comments.Any() && (comments[0] == "\n" || comments[0] == "\r"))
            {
                comments = comments.Skip(1).ToList();
            }
        }

        static void RemoveTrailingNewlines(List<string> comments)
        {
            while (comments.Any() && (comments[comments.Count - 1] == "\n" || comments[comments.Count - 1] == "\r"))
            {
                comments.Pop();
            }
        }

        static bool IsJsDocStart(string content2, int start2) =>
            content2.CharCodeAt(start2) == CharacterCode.Slash &&
            content2.CharCodeAt(start2 + 1) == CharacterCode.Asterisk &&
            content2.CharCodeAt(start2 + 2) == CharacterCode.Asterisk &&
            content2.CharCodeAt(start2 + 3) != CharacterCode.Asterisk;


        JsDoc CreateJsDocComment()
        {
            var result2 = new JsDoc
            {
                Tags = tags,
                Comment = comments.Any() ? string.Join("", comments) : null
            };

            return FinishNode(result2, end);
        }


        void SkipWhitespace()
        {
            while (Token is SyntaxKind.WhitespaceTrivia or SyntaxKind.NewLineTrivia)
            {
                NextJsDocToken();
            }
        }

        void ParseTag(int indent)
        {
            var atToken = new AtToken();
            var textRange = atToken as ITextRange;
            textRange.End = Scanner.TextPos;

            NextJsDocToken();
            var tagName = ParseJsDocIdentifierName();

            SkipWhitespace();
            if (tagName is null)
            {
                return;
            }

            var tag = tagName != null
                ? tagName.Text switch
                {
                    "augments" => ParseAugmentsTag(atToken, tagName),
                    "param" => ParseParamTag(atToken, tagName),
                    "return" or "returns" => ParseReturnTag(atToken, tagName),
                    "template" => ParseTemplateTag(atToken, tagName),
                    "type" => ParseTypeTag(atToken, tagName),
                    "typedef" => ParseTypedefTag(atToken, tagName),
                    _ => ParseUnknownTag(atToken, tagName),
                }
                : ParseUnknownTag(atToken, null);

            if (tag is null)
            {
                return;
            }

            AddTag(tag, ParseTagComments(indent + (tag.End ?? 0) - (tag.Pos ?? 0)));
        }


        List<string> ParseTagComments(int indent)
        {
            List<string> comments2 = new();
            var state = JSDocState.SawAsterisk;
            int? margin = null;
            while (Token is not SyntaxKind.AtToken && Token is not SyntaxKind.EndOfFileToken)
            {
                switch (Token)
                {
                    case SyntaxKind.NewLineTrivia:
                        if (state >= JSDocState.SawAsterisk)
                        {
                            state = JSDocState.BeginningOfLine;
                            comments2.Add(Scanner.TokenText);
                        }
                        indent = 0;
                        break;
                    case SyntaxKind.AtToken:
                        break;
                    case SyntaxKind.WhitespaceTrivia:
                        if (state is JSDocState.SavingComments)
                        {
                            PushComment(Scanner.TokenText);
                        }
                        else
                        {
                            var whitespace = Scanner.TokenText;
                            if (margin != null && indent + whitespace.Length > margin)
                            {
                                comments2.Add(whitespace.Slice((int)margin - indent - 1));
                            }
                            indent += whitespace.Length;
                        }
                        break;
                    case SyntaxKind.AsteriskToken:
                        if (state is JSDocState.BeginningOfLine)
                        {
                            state = JSDocState.SawAsterisk;
                            indent += Scanner.TokenText.Length;
                            break;
                        }
                        goto caseLabel5;
                    default:

caseLabel5: state = JSDocState.SavingComments;
                        PushComment(Scanner.TokenText);
                        break;
                }

                if (Token is SyntaxKind.AtToken)
                {
                    break;
                }

                NextJsDocToken();
            }

            RemoveLeadingNewlines(comments2);
            RemoveTrailingNewlines(comments2);

            return comments2;

            void PushComment(string text)
            {
                margin ??= indent;
                comments2.Add(text);
                indent += text.Length;
            }
        }

        JsDocTag ParseUnknownTag(AtToken atToken, Identifier? tagName)
        {
            var result2 = new JsDocTag();
            var jsDocTag = result2 as IJsDocTag;
            jsDocTag.AtToken = atToken;
            if (tagName is not null)
            {
                jsDocTag.TagName = tagName;
            }

            return FinishNode(result2);
        }

        void AddTag(IJsDocTag tag, List<string> comments2)
        {
            tag.Comment = string.Join("", comments2);
            if (tags is null)
            {
                tags = new NodeArray<IJsDocTag>();
                var textRange = tags as ITextRange;
                textRange.Pos = tag.Pos;
            }
            else
            {
                tags.Add(tag);
            }

            var range = tags as ITextRange;
            range.End = tag.End;
        }

        JsDocTypeExpression? TryParseTypeExpression() =>
            TryParse(() =>
            {
                SkipWhitespace();
                return Token is not SyntaxKind.OpenBraceToken ? null : ParseJsDocTypeExpression();
            });

        JsDocParameterTag? ParseParamTag(AtToken atToken, Identifier tagName)
        {
            var typeExpression = TryParseTypeExpression();

            SkipWhitespace();
            Identifier? name = null;
            var isBracketed = false;

            if (ParseOptionalToken<OpenBracketToken>(SyntaxKind.OpenBracketToken) is not null)
            {
                name = ParseJsDocIdentifierName();
                SkipWhitespace();
                isBracketed = true;
                if (ParseOptionalToken<EqualsToken>(SyntaxKind.EqualsToken) is not null)
                {
                    ParseExpression();
                }

                ParseExpected(SyntaxKind.CloseBracketToken);
            }
            else if (TokenIsIdentifierOrKeyword(Token))
            {
                name = ParseJsDocIdentifierName();
            }

            if (name is null)
            {
                ParseErrorAtPosition(Scanner.StartPos, 0, Diagnostics.Identifier_expected);
                return null;
            }
            Identifier? preName = null;
            Identifier? postName = null;
            if (typeExpression != null)
            {
                postName = name;
            }
            else
            {
                preName = name;
            }
            typeExpression ??= TryParseTypeExpression();
            var docParamTag = new JsDocParameterTag
            {
                PreParameterName = preName,
                TypeExpression = typeExpression,
                PostParameterName = postName,
                ParameterName = postName ?? preName,
                IsBracketed = isBracketed
            };

            var tag = docParamTag as IJsDocTag;
            tag.AtToken = atToken;
            tag.TagName = tagName;

            return FinishNode(docParamTag);
        }

        JsDocReturnTag ParseReturnTag(AtToken atToken, Identifier tagName)
        {
            if (tags.Any(t => t.Kind is SyntaxKind.JsDocReturnTag) && tagName is ITextRange range)
            {
                ParseErrorAtPosition(
                    range.Pos ?? 0,
                    Scanner.TokenPos - (range.Pos ?? 0),
                    Diagnostics._0_tag_already_specified, tagName.Text);
            }

            var result = new JsDocReturnTag();
            var returnTag = result as IJsDocTag;
            returnTag.AtToken = atToken;
            returnTag.TagName = tagName;
            result.TypeExpression = TryParseTypeExpression();

            return FinishNode(result);
        }

        JsDocTypeTag ParseTypeTag(AtToken atToken, Identifier tagName)
        {
            if (tags.Any(t => t.Kind is SyntaxKind.JsDocTypeTag) && tagName is ITextRange range)
            {
                ParseErrorAtPosition(
                    range.Pos ?? 0,
                    Scanner.TokenPos - (range.Pos ?? 0),
                    Diagnostics._0_tag_already_specified, tagName.Text);
            }

            var result = new JsDocTypeTag();
            var returnTag = result as IJsDocTag;
            returnTag.AtToken = atToken;
            returnTag.TagName = tagName;
            result.TypeExpression = TryParseTypeExpression();

            return FinishNode(result);
        }


        JsDocPropertyTag? ParsePropertyTag(AtToken atToken, Identifier tagName)
        {
            var typeExpression = TryParseTypeExpression();
            SkipWhitespace();
            var name = ParseJsDocIdentifierName();
            SkipWhitespace();
            if (name is null)
            {
                ParseErrorAtPosition(Scanner.StartPos, 0, Diagnostics.Identifier_expected);
                return null;
            }

            var result = new JsDocPropertyTag();
            var returnTag = result as IJsDocTag;
            returnTag.AtToken = atToken;
            returnTag.TagName = tagName;
            var declaration = result as IDeclaration;
            declaration.Name = name;
            result.TypeExpression = typeExpression;

            return FinishNode(result);
        }

        JsDocAugmentsTag ParseAugmentsTag(AtToken atToken, Identifier tagName)
        {
            var typeExpression = TryParseTypeExpression();
            var result = new JsDocAugmentsTag();
            var returnTag = result as IJsDocTag;
            returnTag.AtToken = atToken;
            returnTag.TagName = tagName;
            result.TypeExpression = typeExpression;

            return FinishNode(result);
        }


        IJsDocTag ParseTypedefTag(AtToken atToken, Identifier tagName)
        {
            var typeExpression = TryParseTypeExpression();

            SkipWhitespace();
            var typedefTag = new JsDocTypedefTag
            {
                FullName = ParseJsDocTypeNameWithNamespace(0)
            };
            var tag = typedefTag as IJsDocTag;
            tag.AtToken = atToken;
            tag.TagName = tagName;
            var declaration = typedefTag as IDeclaration;

            if (typedefTag.FullName is not null)
            {
                var rightNode = typedefTag.FullName as INode;
                while (rightNode is not null)
                {
                    if (rightNode.Kind is SyntaxKind.Identifier || (rightNode as JsDocNamespaceDeclaration)?.Body is null)
                    {
                        declaration.Name = rightNode.Kind is SyntaxKind.Identifier
                            ? rightNode
                            : (rightNode as IDeclaration)?.Name;

                        break;
                    }

                    rightNode = (rightNode as JsDocNamespaceDeclaration)?.Body;
                }
            }

            typedefTag.TypeExpression = typeExpression;

            SkipWhitespace();
            if (typeExpression is not null)
            {
                if (typeExpression.Type.Kind is SyntaxKind.JsDocTypeReference)
                {
                    var jsDocTypeReference = (JsDocTypeReference)typeExpression.Type;
                    if (jsDocTypeReference.Name?.Kind is SyntaxKind.Identifier)
                    {
                        var name = jsDocTypeReference.Name as Identifier;
                        if (name?.Text == "Object")
                        {
                            typedefTag.JsDocTypeLiteral = ScanChildTags();
                        }
                    }
                }
                typedefTag.JsDocTypeLiteral ??= (JsDocTypeLiteral)typeExpression.Type;
            }
            else
            {
                typedefTag.JsDocTypeLiteral = ScanChildTags();
            }

            return FinishNode(typedefTag);
        }


        JsDocTypeLiteral ScanChildTags()
        {
            var jsDocTypeLiteral = new JsDocTypeLiteral();
            var resumePos = Scanner.StartPos;
            var canParseTag = true;
            var seenAsterisk = false;
            var parentTagTerminated = false;
            while (Token is not SyntaxKind.EndOfFileToken && !parentTagTerminated)
            {
                NextJsDocToken();
                switch (Token)
                {
                    case SyntaxKind.AtToken:
                        if (canParseTag)
                        {
                            parentTagTerminated = !TryParseChildTag(jsDocTypeLiteral);
                            if (!parentTagTerminated)
                            {
                                resumePos = Scanner.StartPos;
                            }
                        }
                        seenAsterisk = false;
                        break;
                    case SyntaxKind.NewLineTrivia:
                        resumePos = Scanner.StartPos - 1;
                        canParseTag = true;
                        seenAsterisk = false;
                        break;
                    case SyntaxKind.AsteriskToken:
                        if (seenAsterisk)
                        {
                            canParseTag = false;
                        }
                        seenAsterisk = true;
                        break;
                    case SyntaxKind.Identifier:
                        canParseTag = false;
                        break;
                    case SyntaxKind.EndOfFileToken:
                        break;
                }
            }

            Scanner.SetTextPos(resumePos);
            return FinishNode(jsDocTypeLiteral);
        }

        INode? ParseJsDocTypeNameWithNamespace(NodeFlags flags)
        {
            var pos = Scanner.TokenPos;
            var typeNameOrNamespaceName = ParseJsDocIdentifierName();
            if (typeNameOrNamespaceName != null && ParseOptional(SyntaxKind.DotToken))
            {
                var jsDocNamespaceNode = new JsDocNamespaceDeclaration();
                var declaration = jsDocNamespaceNode as IDeclaration;

                declaration.Flags |= flags;
                declaration.Name = typeNameOrNamespaceName;
                jsDocNamespaceNode.Body = ParseJsDocTypeNameWithNamespace(NodeFlags.NestedNamespace);

                return jsDocNamespaceNode;
            }

            if (typeNameOrNamespaceName != null && (flags & NodeFlags.NestedNamespace) != 0)
            {
                typeNameOrNamespaceName.IsInJsDocNamespace = true;
            }

            return typeNameOrNamespaceName;
        }


        bool TryParseChildTag(JsDocTypeLiteral parentTag)
        {
            var atToken = new AtToken();
            var range = atToken as ITextRange;
            range.End = Scanner.TextPos;

            NextJsDocToken();
            var tagName = ParseJsDocIdentifierName();

            SkipWhitespace();
            if (tagName is null)
            {
                return false;
            }

            switch (tagName.Text)
            {
                case "type":
                    if (parentTag.JsDocTypeTag is not null)
                    {
                        return false;
                    }
                    parentTag.JsDocTypeTag = ParseTypeTag(atToken, tagName);
                    return true;

                case "prop":
                case "property":
                    var propertyTag = ParsePropertyTag(atToken, tagName);
                    if (propertyTag is not null)
                    {
                        parentTag.JsDocPropertyTags ??= new NodeArray<JsDocPropertyTag>();
                        parentTag.JsDocPropertyTags.Add(propertyTag);
                        return true;
                    }
                    return false;
            }

            return false;
        }

        JsDocTemplateTag? ParseTemplateTag(AtToken atToken, Identifier tagName)
        {
            if (tags.Any(t => t.Kind is SyntaxKind.JsDocTemplateTag) &&
                tagName is ITextRange range)
            {
                ParseErrorAtPosition(
                    range.Pos ?? 0,
                    Scanner.TokenPos - (range.Pos ?? 0),
                    Diagnostics._0_tag_already_specified, tagName.Text);
            }
            var typeParameters = CreateList<TypeParameterDeclaration>();
            while (true)
            {
                var name = ParseJsDocIdentifierName();
                SkipWhitespace();
                if (name is null)
                {
                    ParseErrorAtPosition(
                        Scanner.StartPos, 0, Diagnostics.Identifier_expected);
                    return null;
                }
                var typeParameter = new TypeParameterDeclaration();
                var declaration = typeParameter as IDeclaration;
                declaration.Name = name;

                FinishNode(typeParameter);

                typeParameters.Add(typeParameter);
                if (Token is SyntaxKind.CommaToken)
                {
                    NextJsDocToken();
                    SkipWhitespace();
                }
                else
                {
                    break;
                }
            }
            var result = new JsDocTemplateTag
            {
                TypeParameters = typeParameters
            };
            var tag = result as IJsDocTag;
            tag.AtToken = atToken;
            tag.TagName = tagName;

            FinishNode(result);

            var tpRange = typeParameters as ITextRange;
            tpRange.End = tag.End;

            return result;
        }

        SyntaxKind NextJsDocToken()
        {
            CurrentToken = Scanner.ScanJsDocToken();
            return CurrentToken;
        }

        Identifier? ParseJsDocIdentifierName() =>
            CreateJsDocIdentifier(TokenIsIdentifierOrKeyword(Token));

        Identifier? CreateJsDocIdentifier(bool isIdentifier)
        {
            if (!isIdentifier)
            {
                ParseErrorAtCurrentToken(Diagnostics.Identifier_expected);
                return null;
            }
            var pos = Scanner.TokenPos;
            var end2 = Scanner.TextPos;
            var result = new Identifier { Text = content!.SubString(pos, end2) };

            FinishNode(result, end2);
            NextJsDocToken();

            return result;
        }
    }
}
