// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using static Blazor.SourceGenerators.TypeScript.Parser.Core;
using static Blazor.SourceGenerators.TypeScript.Parser.Scanner;
using static Blazor.SourceGenerators.TypeScript.Parser.Ts;
using static Blazor.SourceGenerators.TypeScript.Parser.Utilities;

namespace Blazor.SourceGenerators.TypeScript.Parser;

public sealed class Parser
{
    public Scanner Scanner = new(ScriptTarget.Latest, true, LanguageVariant.Standard, null, null);
    public NodeFlags DisallowInAndDecoratorContext = NodeFlags.DisallowInContext | NodeFlags.DecoratorContext;

    public NodeFlags ContextFlags;
    public bool ParseErrorBeforeNextFinishedNode = false;
    public SourceFile? SourceFile = default!;
    public List<TypeScriptDiagnostic>? ParseDiagnostics = default!;
    public object? SyntaxCursor = default!;

    public TypeScriptSyntaxKind CurrentToken;
    public string? SourceText = default!;
    public int NodeCount;
    public List<string>? Identifiers = default!;
    public int IdentifierCount;

    public int ParsingContext;
    public JsDocParser JsDocParser;
    public Parser() => JsDocParser = new JsDocParser(this);

    public async Task<SourceFile> ParseSourceFileAsync(
        string fileName,
        string sourceText,
        ScriptTarget languageVersion,
        object? syntaxCursor,
        bool setParentNodes,
        ScriptKind scriptKind)
    {
        scriptKind = EnsureScriptKind(fileName, scriptKind);
        InitializeState(sourceText, languageVersion, syntaxCursor, scriptKind);
        var result = await ParseSourceFileWorkerAsync(
            fileName, languageVersion, setParentNodes, scriptKind);

        ClearState();

        return result;
    }

    public IEntityName? ParseIsolatedEntityName(
        string content,
        ScriptTarget languageVersion)
    {
        InitializeState(content, languageVersion, syntaxCursor: null, ScriptKind.Js);

        // Prime the scanner.
        _ = NextToken;
        var entityName = ParseEntityName(allowReservedWords: true);
        var isInvalid = Token is TypeScriptSyntaxKind.EndOfFileToken && !ParseDiagnostics.Any();

        ClearState();

        return isInvalid ? entityName : null;
    }

    public LanguageVariant GetLanguageVariant(ScriptKind scriptKind) =>
        // .tsx and .jsx files are treated as jsx language variant.
        scriptKind == ScriptKind.Tsx ||
        scriptKind == ScriptKind.Jsx ||
        scriptKind == ScriptKind.Js
            ? LanguageVariant.Jsx
            : LanguageVariant.Standard;


    public void InitializeState(
        string sourceText,
        ScriptTarget languageVersion,
        object? syntaxCursor,
        ScriptKind scriptKind)
    {
        SourceText = sourceText;
        SyntaxCursor = syntaxCursor;
        ParseDiagnostics = new List<TypeScriptDiagnostic>();
        ParsingContext = 0;
        Identifiers = new List<string>();
        IdentifierCount = 0;
        NodeCount = 0;
        ContextFlags = scriptKind == ScriptKind.Js || scriptKind == ScriptKind.Jsx
            ? NodeFlags.JavaScriptFile
            : NodeFlags.None;
        ParseErrorBeforeNextFinishedNode = false;

        // Initialize and prime the scanner before parsing the source elements.
        Scanner.SetText(SourceText);
        Scanner.OnError += ScanError;
        Scanner.SetScriptTarget(languageVersion);
        Scanner.SetLanguageVariant(GetLanguageVariant(scriptKind));
    }

    public void ClearState()
    {
        // Clear out the text the scanner is pointing at, so it doesn't keep anything alive unnecessarily.
        Scanner.SetText("");
        Scanner.SetOnError(null);

        // Clear any data.  We don't want to accidentally hold onto it for too long.
        ParseDiagnostics = null;
        SourceFile = null;
        Identifiers = null;
        SyntaxCursor = null;
        SourceText = null;
    }


    public Task<SourceFile> ParseSourceFileWorkerAsync(
        string fileName,
        ScriptTarget languageVersion,
        bool setParentNodes,
        ScriptKind scriptKind)
    {
        SourceFile = CreateSourceFile(fileName, languageVersion, scriptKind);
        var node = SourceFile as INode;
        node.Flags = ContextFlags;

        // Prime the scanner.
        _ = NextToken;

        SourceFile.Statements = ParseList2(TypeScript.ParsingContext.SourceElements, ParseStatement);
        SourceFile.EndOfFileToken = ParseTokenNode<EndOfFileToken>(Token);

        SetExternalModuleIndicator(SourceFile);

        SourceFile.NodeCount = NodeCount;
        SourceFile.IdentifierCount = IdentifierCount;
        SourceFile.Identifiers = Identifiers ?? new();
        SourceFile.ParseDiagnostics = ParseDiagnostics ?? new();
        if (setParentNodes)
        {
            FixupParentReferences(SourceFile);
        }

        return Task.FromResult(SourceFile);
    }

    public T AddJsDocComment<T>(T node) where T : INode
    {
        var comments = GetJsDocCommentRanges(node, SourceFile.Text);
        if (comments.Any())
        {
            foreach (var comment in comments)
            {
                var jsDoc = JsDocParser.ParseJsDocComment(node, comment.Pos, comment.End - comment.Pos);
                if (jsDoc is null)
                {
                    continue;
                }
                node.JsDoc ??= new List<JsDoc>();
                node.JsDoc.Add(jsDoc);
            }
        }

        return node;
    }

    public void FixupParentReferences(INode rootNode)
    {
        var parent = rootNode;

        ForEachChild(rootNode, visitNode);

        return;

        INode? visitNode(INode n)
        {
            if (n.Parent != parent)
            {
                n.Parent = parent;
                var saveParent = parent;

                parent = n;

                ForEachChild(n, visitNode);
                if (n.JsDoc != null)
                {
                    foreach (var jsDoc in n.JsDoc)
                    {
                        jsDoc.Parent = n;

                        parent = jsDoc;

                        ForEachChild(jsDoc, visitNode);
                    }
                }

                parent = saveParent;
            }
            return null;
        }
    }

    public SourceFile CreateSourceFile(
        string fileName,
        ScriptTarget languageVersion,
        ScriptKind scriptKind)
    {
        var normalizedPath = NormalizePath(fileName);
        var sourceFile = new SourceFile
        {
            Pos = 0,
            End = SourceText.Length,
            Text = SourceText,
            BindDiagnostics = new List<TypeScriptDiagnostic>(),
            LanguageVersion = languageVersion,
            FileName = normalizedPath,
            LanguageVariant = GetLanguageVariant(scriptKind),
            IsDeclarationFile = FileExtensionIs(normalizedPath, ".d.ts"),
            ScriptKind = scriptKind
        };

        NodeCount++;

        return sourceFile;
    }

    public void SetContextFlag(bool val, NodeFlags flag)
    {
        if (val)
        {
            ContextFlags |= flag;
        }
        else
        {
            ContextFlags &= ~flag;
        }
    }


    public void SetDisallowInContext(bool val) =>
        SetContextFlag(val, NodeFlags.DisallowInContext);


    public void SetYieldContext(bool val) =>
        SetContextFlag(val, NodeFlags.YieldContext);


    public void SetDecoratorContext(bool val) =>
        SetContextFlag(val, NodeFlags.DecoratorContext);


    public void SetAwaitContext(bool val) =>
        SetContextFlag(val, NodeFlags.AwaitContext);


    public T DoOutsideOfContext<T>(NodeFlags context, Func<T> func)
    {
        var contextFlagsToClear = context & ContextFlags;
        if (contextFlagsToClear != 0)
        {
            // clear the requested context flags
            SetContextFlag(/*val*/ false, contextFlagsToClear);
            var result = func();

            // restore the context flags we just cleared
            SetContextFlag(/*val*/ true, contextFlagsToClear);

            return result;
        }


        // no need to do anything special as we are not in any of the requested contexts
        return func();
    }


    public T DoInsideOfContext<T>(NodeFlags context, Func<T> func)
    {
        var contextFlagsToSet = context & ~ContextFlags;
        if (contextFlagsToSet != 0)
        {

            // set the requested context flags
            SetContextFlag(/*val*/ true, contextFlagsToSet);
            var result = func();

            // reset the context flags we just set
            SetContextFlag(/*val*/ false, contextFlagsToSet);

            return result;
        }


        // no need to do anything special as we are already in all of the requested contexts
        return func();
    }

    public T AllowInAnd<T>(Func<T> func) =>
        DoOutsideOfContext(NodeFlags.DisallowInContext, func);


    public T DisallowInAnd<T>(Func<T> func) =>
        DoInsideOfContext(NodeFlags.DisallowInContext, func);


    public T DoInYieldContext<T>(Func<T> func) =>
        DoInsideOfContext(NodeFlags.YieldContext, func);


    public T DoInDecoratorContext<T>(Func<T> func) =>
        DoInsideOfContext(NodeFlags.DecoratorContext, func);


    public T DoInAwaitContext<T>(Func<T> func) =>
        DoInsideOfContext(NodeFlags.AwaitContext, func);


    public T DoOutsideOfAwaitContext<T>(Func<T> func) =>
        DoOutsideOfContext(NodeFlags.AwaitContext, func);


    public T DoInYieldAndAwaitContext<T>(Func<T> func) =>
        DoInsideOfContext(NodeFlags.YieldContext | NodeFlags.AwaitContext, func);


    public bool InContext(NodeFlags flags) => (ContextFlags & flags) != 0;


    public bool InYieldContext() => InContext(NodeFlags.YieldContext);


    public bool InDisallowInContext() => InContext(NodeFlags.DisallowInContext);


    public bool InDecoratorContext() => InContext(NodeFlags.DecoratorContext);


    public bool InAwaitContext() => InContext(NodeFlags.AwaitContext);


    public void ParseErrorAtCurrentToken(DiagnosticMessage? message, object? arg0 = null)
    {
        var start = Scanner.TokenPos;
        var length = Scanner.TextPos - start;

        ParseErrorAtPosition(start, length, message, arg0);
    }


    public void ParseErrorAtPosition(int start, int length, DiagnosticMessage? message = null, object? arg0 = null)
    {
        var lastError = LastOrUndefined(ParseDiagnostics);
        if (lastError is null || start != lastError.Start)
        {

            ParseDiagnostics.Add(CreateFileDiagnostic(SourceFile, start, length, message)); //, arg0));
        }

        // Mark that we've encountered an error.  We'll set an appropriate bit on the next
        // node we finish so that it can't be reused incrementally.
        ParseErrorBeforeNextFinishedNode = true;
    }


    public void ScanError(DiagnosticMessage message, int? length = null)
    {
        var pos = Scanner.TextPos;
        ParseErrorAtPosition(pos, length ?? 0, message);
    }

    public int NodePos => Scanner.StartPos;

    public int NodeEnd => Scanner.StartPos;

    public TypeScriptSyntaxKind Token => CurrentToken;

    public TypeScriptSyntaxKind NextToken => CurrentToken = Scanner.Scan();

    public TypeScriptSyntaxKind ReScanGreaterToken => CurrentToken = Scanner.ReScanGreaterToken();

    public TypeScriptSyntaxKind ReScanSlashToken => CurrentToken = Scanner.ReScanSlashToken();

    public TypeScriptSyntaxKind ReScanTemplateToken => CurrentToken = Scanner.ReScanTemplateToken();

    public TypeScriptSyntaxKind ScanJsxIdentifier => CurrentToken = Scanner.ScanJsxIdentifier();

    public TypeScriptSyntaxKind ScanJsxText => CurrentToken = Scanner.ScanJsxToken();

    public TypeScriptSyntaxKind ScanJsxAttributeValue => CurrentToken = Scanner.ScanJsxAttributeValue();

    public T SpeculationHelper<T>(Func<T> callback, bool isLookAhead)
    {
        var saveToken = CurrentToken;
        var saveParseDiagnosticsLength = ParseDiagnostics.Count;
        var saveParseErrorBeforeNextFinishedNode = ParseErrorBeforeNextFinishedNode;
        var saveContextFlags = ContextFlags;
        var result = isLookAhead
            ? Scanner.LookAhead(callback)
            : Scanner.TryScan(callback);

        if (result is null || (result is bool && Convert.ToBoolean(result) == false) || isLookAhead)
        {
            CurrentToken = saveToken;
            ParseDiagnostics = ParseDiagnostics.Take(saveParseDiagnosticsLength).ToList();
            ParseErrorBeforeNextFinishedNode = saveParseErrorBeforeNextFinishedNode;
        }

        return result;
    }


    public T LookAhead<T>(Func<T> callback) => SpeculationHelper(callback, isLookAhead: true);

    public T TryParse<T>(Func<T> callback) => SpeculationHelper(callback, isLookAhead: false);

    public bool IsIdentifier()
    {
        if (Token is TypeScriptSyntaxKind.Identifier)
        {
            return true;
        }

        if (Token is TypeScriptSyntaxKind.YieldKeyword && InYieldContext())
        {
            return false;
        }

        return (Token is not TypeScriptSyntaxKind.AwaitKeyword || !InAwaitContext())
            && Token > TypeScriptSyntaxKind.LastReservedWord;
    }


    public bool ParseExpected(TypeScriptSyntaxKind kind, DiagnosticMessage? diagnosticMessage = null, bool shouldAdvance = true)
    {
        if (Token is kind)
        {
            if (shouldAdvance)
            {
                _ = NextToken;
            }

            return true;
        }
        if (diagnosticMessage != null)
        {

            ParseErrorAtCurrentToken(diagnosticMessage);
        }
        else
        {

            ParseErrorAtCurrentToken(Diagnostics._0_expected, TokenToString(kind));
        }

        return false;
    }


    public bool ParseOptional(TypeScriptSyntaxKind syntaxKind)
    {
        if (Token is syntaxKind)
        {
            _ = NextToken;

            return true;
        }

        return false;
    }


    public T? ParseOptionalToken<T>(TypeScriptSyntaxKind syntaxKind) where T : Node, new() =>
        Token is syntaxKind ? ParseTokenNode<T>(Token) : null;

    public Node ParseExpectedToken<T>(
        TypeScriptSyntaxKind t,
        bool reportAtCurrentPosition,
        DiagnosticMessage diagnosticMessage,
        object? arg0 = null) where T : Node, new() =>
        ParseOptionalToken<T>(t) ??
        CreateMissingNode<T>(t, reportAtCurrentPosition, diagnosticMessage, arg0);

    public T ParseTokenNode<T>(TypeScriptSyntaxKind syntaxKind) where T : Node, new()
    {
        var node = new T
        {
            Pos = Scanner.StartPos,
            Kind = syntaxKind
        };

        _ = NextToken;

        return FinishNode(node);
    }

    public bool CanParseSemicolon()
    {
        if (Token is TypeScriptSyntaxKind.SemicolonToken)
        {
            return true;
        }

        // We can parse out an optional semicolon in ASI cases in the following cases.
        return Token is TypeScriptSyntaxKind.CloseBraceToken
            || Token is TypeScriptSyntaxKind.EndOfFileToken
            || Scanner.HasPrecedingLineBreak;
    }

    public bool ParseSemicolon()
    {
        if (CanParseSemicolon())
        {
            if (Token is TypeScriptSyntaxKind.SemicolonToken)
            {
                // Consume the semicolon if it was explicitly provided.
                _ = NextToken;
            }

            return true;
        }
        else return ParseExpected(TypeScriptSyntaxKind.SemicolonToken);
    }


    public NodeArray<T?> CreateList<T>(T[]? elements = null, int? pos = null) where T : Node, new()
    {
        var array = elements is null ? new NodeArray<T?>() : new NodeArray<T?>(elements);
        if (!(pos >= 0))
        {
            pos = NodePos;
        }

        var textRange = array as ITextRange;

        textRange.Pos = pos;
        textRange.End = pos;

        return array;
    }


    public T FinishNode<T>(T node, int? end = null) where T : INode
    {
        node.End = end is null ? Scanner.StartPos : end.Value;
        if (ContextFlags is not NodeFlags.None)
        {
            node.Flags |= ContextFlags;
        }

        if (ParseErrorBeforeNextFinishedNode)
        {
            ParseErrorBeforeNextFinishedNode = false;
            node.Flags |= NodeFlags.ThisNodeHasError;
        }

        return node;
    }

    public Node CreateMissingNode<T>(
        TypeScriptSyntaxKind kind,
        bool reportAtCurrentPosition,
        DiagnosticMessage? diagnosticMessage = null,
        object? arg0 = null) where T : INode, new()
    {
        if (reportAtCurrentPosition)
        {
            ParseErrorAtPosition(Scanner.StartPos, 0, diagnosticMessage, arg0);
        }
        else
        {
            ParseErrorAtCurrentToken(diagnosticMessage, arg0);
        }

        var result = new T
        {
            Kind = TypeScriptSyntaxKind.MissingDeclaration,
            Pos = Scanner.StartPos
        };

        var node = result as Node;
        return FinishNode(node!);
    }

    public string InternIdentifier(string text)
    {

        text = EscapeIdentifier(text);
        //var identifier = identifiers.get(text);
        if (!Identifiers.Contains(text))// identifier is null)
        {

            Identifiers.Add(text); //.set(text, identifier = text);
        }

        return text; // identifier;
    }


    public Identifier CreateIdentifier(bool isIdentifier, DiagnosticMessage? diagnosticMessage = null)
    {

        IdentifierCount++;
        if (isIdentifier)
        {
            var node = new Identifier { Pos = Scanner.StartPos };
            if (Token is not TypeScriptSyntaxKind.Identifier)
            {

                node.OriginalKeywordKind = Token;
            }

            node.Text = InternIdentifier(Scanner.TokenValue);

            _ = NextToken;

            return FinishNode(node);
        }


        return (Identifier)CreateMissingNode<Identifier>(TypeScriptSyntaxKind.Identifier, /*reportAtCurrentPosition*/ false, diagnosticMessage ?? Diagnostics.Identifier_expected);
    }


    public Identifier ParseIdentifier(DiagnosticMessage? diagnosticMessage = null) => CreateIdentifier(IsIdentifier(), diagnosticMessage);


    public Identifier ParseIdentifierName() => CreateIdentifier(TokenIsIdentifierOrKeyword(Token));


    public bool IsLiteralPropertyName() => TokenIsIdentifierOrKeyword(Token) ||
            Token is TypeScriptSyntaxKind.StringLiteral ||
            Token is TypeScriptSyntaxKind.NumericLiteral;


    public IPropertyName? ParsePropertyNameWorker(bool allowComputedPropertyNames)
    {
        if (Token is TypeScriptSyntaxKind.StringLiteral || Token is TypeScriptSyntaxKind.NumericLiteral)
        {

            var le = ParseLiteralNode(/*internName*/ true);
            if (le is StringLiteral literal) return literal;
            else if (le is NumericLiteral numberLiteral) return numberLiteral;
            return null; // /*(StringLiteral | NumericLiteral)*/le;
        }
        return allowComputedPropertyNames && Token is TypeScriptSyntaxKind.OpenBracketToken ? ParseComputedPropertyName() : ParseIdentifierName();
    }


    public IPropertyName ParsePropertyName() => ParsePropertyNameWorker(/*allowComputedPropertyNames*/ true);


    public /*Identifier | LiteralExpression*/IPropertyName ParseSimplePropertyName() => ParsePropertyNameWorker(/*allowComputedPropertyNames*/ false);


    public bool IsSimplePropertyName() => Token is TypeScriptSyntaxKind.StringLiteral || Token is TypeScriptSyntaxKind.NumericLiteral || TokenIsIdentifierOrKeyword(Token);


    public ComputedPropertyName ParseComputedPropertyName()
    {
        var node = new ComputedPropertyName() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenBracketToken);


        // We parse any expression (including a comma expression). But the grammar
        // says that only an assignment expression is allowed, so the grammar checker
        // will error if it sees a comma expression.
        node.Expression = AllowInAnd(ParseExpression);


        ParseExpected(TypeScriptSyntaxKind.CloseBracketToken);

        return FinishNode(node);
    }


    public bool ParseContextualModifier(TypeScriptSyntaxKind t) => Token is t && TryParse(NextTokenCanFollowModifier);


    public bool NextTokenIsOnSameLineAndCanFollowModifier()
    {

        _ = NextToken;
        return Scanner.HasPrecedingLineBreak ? false : CanFollowModifier();
    }


    public bool NextTokenCanFollowModifier()
    {
        if (Token is TypeScriptSyntaxKind.ConstKeyword)
        {

            // 'const' is only a modifier if followed by 'enum'.
            return NextToken is TypeScriptSyntaxKind.EnumKeyword;
        }
        if (Token is TypeScriptSyntaxKind.ExportKeyword)
        {

            _ = NextToken;
            return Token is TypeScriptSyntaxKind.DefaultKeyword
                ? LookAhead(NextTokenIsClassOrFunctionOrAsync)
                : Token is not TypeScriptSyntaxKind.AsteriskToken && Token is not TypeScriptSyntaxKind.AsKeyword && Token is not TypeScriptSyntaxKind.OpenBraceToken && CanFollowModifier();
        }
        if (Token is TypeScriptSyntaxKind.DefaultKeyword)
        {

            return NextTokenIsClassOrFunctionOrAsync();
        }
        if (Token is TypeScriptSyntaxKind.StaticKeyword)
        {

            _ = NextToken;

            return CanFollowModifier();
        }


        return NextTokenIsOnSameLineAndCanFollowModifier();
    }


    public bool ParseAnyContextualModifier() => IsModifierKind(Token) && TryParse(NextTokenCanFollowModifier);


    public bool CanFollowModifier() => Token is TypeScriptSyntaxKind.OpenBracketToken
            || Token is TypeScriptSyntaxKind.OpenBraceToken
            || Token is TypeScriptSyntaxKind.AsteriskToken
            || Token is TypeScriptSyntaxKind.DotDotDotToken
            || IsLiteralPropertyName();


    public bool NextTokenIsClassOrFunctionOrAsync()
    {

        _ = NextToken;

        return Token is TypeScriptSyntaxKind.ClassKeyword || Token is TypeScriptSyntaxKind.FunctionKeyword ||
            (Token is TypeScriptSyntaxKind.AsyncKeyword && LookAhead(NextTokenIsFunctionKeywordOnSameLine));
    }


    public bool IsListElement(ParsingContext parsingContext, bool inErrorRecovery)
    {
        var node = CurrentNode(parsingContext);
        if (node != null)
        {

            return true;
        }
        switch (parsingContext)
        {
            case ParsingContext.SourceElements:
            case ParsingContext.BlockStatements:
            case ParsingContext.SwitchClauseStatements:

                // If we're in error recovery, then we don't want to treat ';' as an empty statement.
                // The problem is that ';' can show up in far too many contexts, and if we see one
                // and assume it's a statement, then we may bail out inappropriately from whatever
                // we're parsing.  For example, if we have a semicolon in the middle of a class, then
                // we really don't want to assume the class is over and we're on a statement in the
                // outer module.  We just want to consume and move on.
                return !(Token is TypeScriptSyntaxKind.SemicolonToken && inErrorRecovery) && IsStartOfStatement();
            case ParsingContext.SwitchClauses:

                return Token is TypeScriptSyntaxKind.CaseKeyword || Token is TypeScriptSyntaxKind.DefaultKeyword;
            case ParsingContext.TypeMembers:

                return LookAhead(IsTypeMemberStart);
            case ParsingContext.ClassMembers:

                // We allow semicolons as class elements (as specified by ES6) as long as we're
                // not in error recovery.  If we're in error recovery, we don't want an errant
                // semicolon to be treated as a class member (since they're almost always used
                // for statements.
                return LookAhead(IsClassMemberStart) || (Token is TypeScriptSyntaxKind.SemicolonToken && !inErrorRecovery);
            case ParsingContext.EnumMembers:

                // Include open bracket computed properties. This technically also lets in indexers,
                // which would be a candidate for improved error reporting.
                return Token is TypeScriptSyntaxKind.OpenBracketToken || IsLiteralPropertyName();
            case ParsingContext.ObjectLiteralMembers:

                return Token is TypeScriptSyntaxKind.OpenBracketToken || Token is TypeScriptSyntaxKind.AsteriskToken || Token is TypeScriptSyntaxKind.DotDotDotToken || IsLiteralPropertyName();
            case ParsingContext.RestProperties:

                return IsLiteralPropertyName();
            case ParsingContext.ObjectBindingElements:

                return Token is TypeScriptSyntaxKind.OpenBracketToken || Token is TypeScriptSyntaxKind.DotDotDotToken || IsLiteralPropertyName();
            case ParsingContext.HeritageClauseElement:
                if (Token is TypeScriptSyntaxKind.OpenBraceToken)
                {

                    return LookAhead(IsValidHeritageClauseObjectLiteral);
                }
                if (!inErrorRecovery)
                {

                    return IsStartOfLeftHandSideExpression() && !IsHeritageClauseExtendsOrImplementsKeyword();
                }
                else
                {

                    // If we're in error recovery we tighten up what we're willing to match.
                    // That way we don't treat something like "this" as a valid heritage clause
                    // element during recovery.
                    return IsIdentifier() && !IsHeritageClauseExtendsOrImplementsKeyword();
                }
            //goto caseLabel12;
            case ParsingContext.VariableDeclarations:
                //caseLabel12:
                return IsIdentifierOrPattern();
            case ParsingContext.ArrayBindingElements:

                return Token is TypeScriptSyntaxKind.CommaToken || Token is TypeScriptSyntaxKind.DotDotDotToken || IsIdentifierOrPattern();
            case ParsingContext.TypeParameters:

                return IsIdentifier();
            case ParsingContext.ArgumentExpressions:
            case ParsingContext.ArrayLiteralMembers:

                return Token is TypeScriptSyntaxKind.CommaToken || Token is TypeScriptSyntaxKind.DotDotDotToken || IsStartOfExpression();
            case ParsingContext.Parameters:

                return IsStartOfParameter();
            case ParsingContext.TypeArguments:
            case ParsingContext.TupleElementTypes:

                return Token is TypeScriptSyntaxKind.CommaToken || IsStartOfType();
            case ParsingContext.HeritageClauses:

                return IsHeritageClause();
            case ParsingContext.ImportOrExportSpecifiers:

                return TokenIsIdentifierOrKeyword(Token);
            case ParsingContext.JsxAttributes:

                return TokenIsIdentifierOrKeyword(Token) || Token is TypeScriptSyntaxKind.OpenBraceToken;
            case ParsingContext.JsxChildren:

                return true;
            case ParsingContext.JSDocFunctionParameters:
            case ParsingContext.JSDocTypeArguments:
            case ParsingContext.JSDocTupleTypes:

                return JsDocParser.IsJsDocType();
            case ParsingContext.JSDocRecordMembers:

                return IsSimplePropertyName();
        }

        return false;
    }


    public bool IsValidHeritageClauseObjectLiteral()
    {
        if (NextToken is TypeScriptSyntaxKind.CloseBraceToken)
        {
            var next = NextToken;

            return next == TypeScriptSyntaxKind.CommaToken || next == TypeScriptSyntaxKind.OpenBraceToken || next == TypeScriptSyntaxKind.ExtendsKeyword || next == TypeScriptSyntaxKind.ImplementsKeyword;
        }


        return true;
    }


    public bool NextTokenIsIdentifier()
    {

        _ = NextToken;

        return IsIdentifier();
    }


    public bool NextTokenIsIdentifierOrKeyword()
    {

        _ = NextToken;

        return TokenIsIdentifierOrKeyword(Token);
    }


    public bool IsHeritageClauseExtendsOrImplementsKeyword() => Token is TypeScriptSyntaxKind.ImplementsKeyword ||
                        Token is TypeScriptSyntaxKind.ExtendsKeyword
            ? LookAhead(NextTokenIsStartOfExpression)
            : false;


    public bool NextTokenIsStartOfExpression()
    {

        _ = NextToken;

        return IsStartOfExpression();
    }


    public bool IsListTerminator(ParsingContext kind)
    {
        if (Token is TypeScriptSyntaxKind.EndOfFileToken)
        {

            // Being at the end of the file ends all lists.
            return true;
        }
        return kind switch
        {
            ParsingContext.BlockStatements or ParsingContext.SwitchClauses or ParsingContext.TypeMembers or ParsingContext.ClassMembers or ParsingContext.EnumMembers or ParsingContext.ObjectLiteralMembers or ParsingContext.ObjectBindingElements or ParsingContext.ImportOrExportSpecifiers => Token is TypeScriptSyntaxKind.CloseBraceToken,
            ParsingContext.SwitchClauseStatements => Token is TypeScriptSyntaxKind.CloseBraceToken || Token is TypeScriptSyntaxKind.CaseKeyword || Token is TypeScriptSyntaxKind.DefaultKeyword,
            ParsingContext.HeritageClauseElement => Token is TypeScriptSyntaxKind.OpenBraceToken || Token is TypeScriptSyntaxKind.ExtendsKeyword || Token is TypeScriptSyntaxKind.ImplementsKeyword,
            ParsingContext.VariableDeclarations => IsVariableDeclaratorListTerminator(),
            ParsingContext.TypeParameters => Token is TypeScriptSyntaxKind.GreaterThanToken || Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.OpenBraceToken || Token is TypeScriptSyntaxKind.ExtendsKeyword || Token is TypeScriptSyntaxKind.ImplementsKeyword,// Tokens other than '>' are here for better error recovery
            ParsingContext.ArgumentExpressions => Token is TypeScriptSyntaxKind.CloseParenToken || Token is TypeScriptSyntaxKind.SemicolonToken,// Tokens other than ')' are here for better error recovery
            ParsingContext.ArrayLiteralMembers or ParsingContext.TupleElementTypes or ParsingContext.ArrayBindingElements => Token is TypeScriptSyntaxKind.CloseBracketToken,
            ParsingContext.Parameters or ParsingContext.RestProperties => Token is TypeScriptSyntaxKind.CloseParenToken || Token is TypeScriptSyntaxKind.CloseBracketToken /*|| Token is TypeScriptSyntaxKind.OpenBraceToken*/,// Tokens other than ')' and ']' (the latter for index signatures) are here for better error recovery
            ParsingContext.TypeArguments => Token is not TypeScriptSyntaxKind.CommaToken,// All other tokens should cause the type-argument to terminate except comma token
            ParsingContext.HeritageClauses => Token is TypeScriptSyntaxKind.OpenBraceToken || Token is TypeScriptSyntaxKind.CloseBraceToken,
            ParsingContext.JsxAttributes => Token is TypeScriptSyntaxKind.GreaterThanToken || Token is TypeScriptSyntaxKind.SlashToken,
            ParsingContext.JsxChildren => Token is TypeScriptSyntaxKind.LessThanToken && LookAhead(NextTokenIsSlash),
            ParsingContext.JSDocFunctionParameters => Token is TypeScriptSyntaxKind.CloseParenToken || Token is TypeScriptSyntaxKind.ColonToken || Token is TypeScriptSyntaxKind.CloseBraceToken,
            ParsingContext.JSDocTypeArguments => Token is TypeScriptSyntaxKind.GreaterThanToken || Token is TypeScriptSyntaxKind.CloseBraceToken,
            ParsingContext.JSDocTupleTypes => Token is TypeScriptSyntaxKind.CloseBracketToken || Token is TypeScriptSyntaxKind.CloseBraceToken,
            ParsingContext.JSDocRecordMembers => Token is TypeScriptSyntaxKind.CloseBraceToken,
            _ => false,// ?
        };
    }


    public bool IsVariableDeclaratorListTerminator()
    {
        if (CanParseSemicolon())
        {

            return true;
        }
        if (IsInOrOfKeyword(Token))
        {

            return true;
        }
        if (Token is TypeScriptSyntaxKind.EqualsGreaterThanToken)
        {

            return true;
        }


        // Keep trying to parse out variable declarators.
        return false;
    }


    public bool IsInSomeParsingContext()
    {
        //throw new NotImplementedException();
        //for (var kind = 0; kind < Enum.GetNames(typeof(ParsingContext)).Count(); kind++)
        foreach (ParsingContext kind in Enum.GetValues(typeof(ParsingContext)))
        {
            if ((ParsingContext & (1 << (int)kind)) != 0)
            {
                if (IsListElement(kind, /*inErrorRecovery*/ true) || IsListTerminator(kind))
                {
                    return true;
                }
            }
        }


        return false;
    }


    public NodeArray<T?> ParseList<T>(ParsingContext kind, Func<T> parseElement) where T : INode
    {
        var saveParsingContext = ParsingContext;

        ParsingContext |= 1 << (int)kind;
        var result = CreateList<T>();
        while (!IsListTerminator(kind))
        {
            if (IsListElement(kind, inErrorRecovery: false))
            {
                var element = ParseListElement(kind, parseElement);
                result.Add(element);

                continue;
            }

            if (AbortParsingListOrMoveToNextToken(kind))
            {
                break;
            }
        }


        result.End = NodeEnd;

        ParsingContext = saveParsingContext;

        return result;
    }

    public NodeArray<T> ParseList2<T>(ParsingContext kind, Func<T> parseElement) where T : INode
    {
        var saveParsingContext = ParsingContext;

        ParsingContext |= 1 << (int)kind;
        var result = CreateList<T>();
        while (!IsListTerminator(kind))
        {
            if (IsListElement(kind, /*inErrorRecovery*/ false))
            {
                var element = ParseListElement2(kind, parseElement);

                result.Add(element);


                continue;
            }
            if (AbortParsingListOrMoveToNextToken(kind))
            {

                break;
            }
        }


        result.End = NodeEnd;

        ParsingContext = saveParsingContext;

        return result;
    }
    public T ParseListElement<T>(ParsingContext parsingContext, Func<T> parseElement) where T : INode
    {
        var node = CurrentNode(parsingContext);
        return node != null ? (T)ConsumeNode(node) : parseElement();
    }
    public T ParseListElement2<T>(ParsingContext parsingContext, Func<T> parseElement) where T : INode
    {
        var node = CurrentNode2(parsingContext);
        return node != null ? (T)ConsumeNode(node) : parseElement();
    }


    public Node? CurrentNode(ParsingContext parsingContext) => ParseErrorBeforeNextFinishedNode ? null : null;//if (syntaxCursor is null)//{//    // if we don't have a cursor, we could never return a node from the old tree.//    return null;//}//var node = syntaxCursor.currentNode(scanner.getStartPos());//if (nodeIsMissing(node))//{//    return null;//}//if (node.intersectsChange)//{//    return null;//}//if (containsParseError(node) != null)//{//    return null;//}//var nodeContextFlags = node.flags & NodeFlags.ContextFlags;//if (nodeContextFlags != contextFlags)//{//    return null;//}//if (!canReuseNode(node, parsingContext))//{//    return null;//}//return node;

    public INode? CurrentNode2(ParsingContext parsingContext) => ParseErrorBeforeNextFinishedNode ? null : null;
    public INode ConsumeNode(INode node)
    {

        // Move the scanner so it is after the node we just consumed.
        Scanner.SetTextPos(node.End ?? 0);

        _ = NextToken;

        return node;
    }
    //public INode consumeNode(INode node)
    //{

    //    // Move the scanner so it is after the node we just consumed.
    //    scanner.setTextPos(node.end);

    //    nextToken();

    //    return node;
    //}


    public bool CanReuseNode(Node node, ParsingContext parsingContext)
    {
        switch (parsingContext)
        {
            case ParsingContext.ClassMembers:

                return IsReusableClassMember(node);
            case ParsingContext.SwitchClauses:

                return IsReusableSwitchClause(node);
            case ParsingContext.SourceElements:
            case ParsingContext.BlockStatements:
            case ParsingContext.SwitchClauseStatements:

                return IsReusableStatement(node);
            case ParsingContext.EnumMembers:

                return IsReusableEnumMember(node);
            case ParsingContext.TypeMembers:

                return IsReusableTypeMember(node);
            case ParsingContext.VariableDeclarations:

                return IsReusableVariableDeclaration(node);
            case ParsingContext.Parameters:

                return IsReusableParameter(node);
            case ParsingContext.RestProperties:

                return false;
            case ParsingContext.HeritageClauses:
            case ParsingContext.TypeParameters:
            case ParsingContext.TupleElementTypes:
            case ParsingContext.TypeArguments:
            case ParsingContext.ArgumentExpressions:
            case ParsingContext.ObjectLiteralMembers:
            case ParsingContext.HeritageClauseElement:
            case ParsingContext.JsxAttributes:
            case ParsingContext.JsxChildren:
                break;
        }


        return false;
    }


    public bool IsReusableClassMember(Node node)
    {
        if (node != null)
        {
            switch (node.Kind)
            {
                case TypeScriptSyntaxKind.Constructor:
                case TypeScriptSyntaxKind.IndexSignature:
                case TypeScriptSyntaxKind.GetAccessor:
                case TypeScriptSyntaxKind.SetAccessor:
                case TypeScriptSyntaxKind.PropertyDeclaration:
                case TypeScriptSyntaxKind.SemicolonClassElement:

                    return true;
                case TypeScriptSyntaxKind.MethodDeclaration:
                    var methodDeclaration = (MethodDeclaration)node;
                    var nameIsConstructor = methodDeclaration.Name.Kind is TypeScriptSyntaxKind.Identifier &&
                                                ((Identifier)methodDeclaration.Name).OriginalKeywordKind is TypeScriptSyntaxKind.ConstructorKeyword;


                    return !nameIsConstructor;
            }
        }


        return false;
    }


    public bool IsReusableSwitchClause(Node node)
    {
        if (node != null)
        {
            switch (node.Kind)
            {
                case TypeScriptSyntaxKind.CaseClause:
                case TypeScriptSyntaxKind.DefaultClause:

                    return true;
            }
        }


        return false;
    }


    public bool IsReusableStatement(Node node)
    {
        if (node != null)
        {
            switch (node.Kind)
            {
                case TypeScriptSyntaxKind.FunctionDeclaration:
                case TypeScriptSyntaxKind.VariableStatement:
                case TypeScriptSyntaxKind.Block:
                case TypeScriptSyntaxKind.IfStatement:
                case TypeScriptSyntaxKind.ExpressionStatement:
                case TypeScriptSyntaxKind.ThrowStatement:
                case TypeScriptSyntaxKind.ReturnStatement:
                case TypeScriptSyntaxKind.SwitchStatement:
                case TypeScriptSyntaxKind.BreakStatement:
                case TypeScriptSyntaxKind.ContinueStatement:
                case TypeScriptSyntaxKind.ForInStatement:
                case TypeScriptSyntaxKind.ForOfStatement:
                case TypeScriptSyntaxKind.ForStatement:
                case TypeScriptSyntaxKind.WhileStatement:
                case TypeScriptSyntaxKind.WithStatement:
                case TypeScriptSyntaxKind.EmptyStatement:
                case TypeScriptSyntaxKind.TryStatement:
                case TypeScriptSyntaxKind.LabeledStatement:
                case TypeScriptSyntaxKind.DoStatement:
                case TypeScriptSyntaxKind.DebuggerStatement:
                case TypeScriptSyntaxKind.ImportDeclaration:
                case TypeScriptSyntaxKind.ImportEqualsDeclaration:
                case TypeScriptSyntaxKind.ExportDeclaration:
                case TypeScriptSyntaxKind.ExportAssignment:
                case TypeScriptSyntaxKind.ModuleDeclaration:
                case TypeScriptSyntaxKind.ClassDeclaration:
                case TypeScriptSyntaxKind.InterfaceDeclaration:
                case TypeScriptSyntaxKind.EnumDeclaration:
                case TypeScriptSyntaxKind.TypeAliasDeclaration:

                    return true;
            }
        }


        return false;
    }


    public bool IsReusableEnumMember(Node node) => node.Kind is TypeScriptSyntaxKind.EnumMember;


    public bool IsReusableTypeMember(Node node)
    {
        if (node != null)
        {
            switch (node.Kind)
            {
                case TypeScriptSyntaxKind.ConstructSignature:
                case TypeScriptSyntaxKind.MethodSignature:
                case TypeScriptSyntaxKind.IndexSignature:
                case TypeScriptSyntaxKind.PropertySignature:
                case TypeScriptSyntaxKind.CallSignature:

                    return true;
            }
        }


        return false;
    }


    public bool IsReusableVariableDeclaration(Node node)
    {
        if (node.Kind != TypeScriptSyntaxKind.VariableDeclaration)
        {

            return false;
        }
        var variableDeclarator = (VariableDeclaration)node;

        return variableDeclarator.Initializer is null;
    }


    public bool IsReusableParameter(Node node)
    {
        if (node.Kind != TypeScriptSyntaxKind.Parameter)
        {

            return false;
        }
        var parameter = (ParameterDeclaration)node;

        return parameter.Initializer is null;
    }


    public bool AbortParsingListOrMoveToNextToken(ParsingContext kind)
    {

        ParseErrorAtCurrentToken(ParsingContextErrors(kind));
        if (IsInSomeParsingContext())
        {

            return true;
        }


        _ = NextToken;

        return false;
    }


    public DiagnosticMessage? ParsingContextErrors(ParsingContext context) => context switch
    {
        ParsingContext.SourceElements => Diagnostics.Declaration_or_statement_expected,
        ParsingContext.BlockStatements => Diagnostics.Declaration_or_statement_expected,
        ParsingContext.SwitchClauses => Diagnostics.case_or_default_expected,
        ParsingContext.SwitchClauseStatements => Diagnostics.Statement_expected,
        ParsingContext.RestProperties or ParsingContext.TypeMembers => Diagnostics.Property_or_signature_expected,
        ParsingContext.ClassMembers => Diagnostics.Unexpected_token_A_constructor_method_accessor_or_property_was_expected,
        ParsingContext.EnumMembers => Diagnostics.Enum_member_expected,
        ParsingContext.HeritageClauseElement => Diagnostics.Expression_expected,
        ParsingContext.VariableDeclarations => Diagnostics.Variable_declaration_expected,
        ParsingContext.ObjectBindingElements => Diagnostics.Property_destructuring_pattern_expected,
        ParsingContext.ArrayBindingElements => Diagnostics.Array_element_destructuring_pattern_expected,
        ParsingContext.ArgumentExpressions => Diagnostics.Argument_expression_expected,
        ParsingContext.ObjectLiteralMembers => Diagnostics.Property_assignment_expected,
        ParsingContext.ArrayLiteralMembers => Diagnostics.Expression_or_comma_expected,
        ParsingContext.Parameters => Diagnostics.Parameter_declaration_expected,
        ParsingContext.TypeParameters => Diagnostics.Type_parameter_declaration_expected,
        ParsingContext.TypeArguments => Diagnostics.Type_argument_expected,
        ParsingContext.TupleElementTypes => Diagnostics.Type_expected,
        ParsingContext.HeritageClauses => Diagnostics.Unexpected_token_expected,
        ParsingContext.ImportOrExportSpecifiers => Diagnostics.Identifier_expected,
        ParsingContext.JsxAttributes => Diagnostics.Identifier_expected,
        ParsingContext.JsxChildren => Diagnostics.Identifier_expected,
        ParsingContext.JSDocFunctionParameters => Diagnostics.Parameter_declaration_expected,
        ParsingContext.JSDocTypeArguments => Diagnostics.Type_argument_expected,
        ParsingContext.JSDocTupleTypes => Diagnostics.Type_expected,
        ParsingContext.JSDocRecordMembers => Diagnostics.Property_assignment_expected,
        _ => null,
    };


    public NodeArray<T> ParseDelimitedList<T>(ParsingContext kind, Func<T> parseElement, bool? considerSemicolonAsDelimiter = null) where T : INode
    {
        var saveParsingContext = ParsingContext;

        ParsingContext |= 1 << (int)kind;
        var result = CreateList<T>();
        var commaStart = -1;
        while (true)
        {
            if (IsListElement(kind, /*inErrorRecovery*/ false))
            {

                result.Add(ParseListElement(kind, parseElement));

                commaStart = Scanner.TokenPos;
                if (ParseOptional(TypeScriptSyntaxKind.CommaToken))
                {

                    continue;
                }


                commaStart = -1;
                if (IsListTerminator(kind))
                {

                    break;
                }


                // We didn't get a comma, and the list wasn't terminated, explicitly parse
                // out a comma so we give a good error message.
                ParseExpected(TypeScriptSyntaxKind.CommaToken);
                if (considerSemicolonAsDelimiter == true && Token is TypeScriptSyntaxKind.SemicolonToken && !Scanner.HasPrecedingLineBreak)
                {

                    _ = NextToken;
                }

                continue;
            }
            if (IsListTerminator(kind))
            {

                break;
            }
            if (AbortParsingListOrMoveToNextToken(kind))
            {

                break;
            }
        }
        if (commaStart >= 0)
        {

            // Always preserve a trailing comma by marking it on the NodeArray
            result.HasTrailingComma = true;
        }


        result.End = NodeEnd;

        ParsingContext = saveParsingContext;

        return result;
    }


    public NodeArray<T> CreateMissingList<T>() where T : INode => CreateList<T>();




    public NodeArray<T> ParseBracketedList<T>(ParsingContext kind, Func<T> parseElement, TypeScriptSyntaxKind open, TypeScriptSyntaxKind close) where T : INode
    {
        if (ParseExpected(open))
        {
            var result = ParseDelimitedList(kind, parseElement);

            ParseExpected(close);

            return result;
        }


        return CreateMissingList<T>();
    }


    public IEntityName ParseEntityName(bool allowReservedWords, DiagnosticMessage? diagnosticMessage = null)
    {
        IEntityName entity = ParseIdentifier(diagnosticMessage);
        while (ParseOptional(TypeScriptSyntaxKind.DotToken))
        {
            QualifiedName node = new()
            {
                Pos = entity.Pos,             //(QualifiedName)createNode(TypeScriptSyntaxKind.QualifiedName, entity.pos);
                                              // !!!
                Left = entity,

                Right = ParseRightSideOfDot(allowReservedWords)
            };

            entity = FinishNode(node);
        }

        return entity;
    }


    public Identifier ParseRightSideOfDot(bool allowIdentifierNames)
    {
        if (Scanner.HasPrecedingLineBreak && TokenIsIdentifierOrKeyword(Token))
        {
            var matchesPattern = LookAhead(NextTokenIsIdentifierOrKeywordOnSameLine);
            if (matchesPattern)
            {

                // Report that we need an identifier.  However, report it right after the dot,
                // and not on the next token.  This is because the next token might actually
                // be an identifier and the error would be quite confusing.
                return (Identifier)CreateMissingNode<Identifier>(TypeScriptSyntaxKind.Identifier, /*reportAtCurrentPosition*/ true, Diagnostics.Identifier_expected);
            }
        }


        return allowIdentifierNames ? ParseIdentifierName() : ParseIdentifier();
    }


    public TemplateExpression ParseTemplateExpression()
    {
        var template = new TemplateExpression
        {
            Pos = Scanner.StartPos,
            Head = ParseTemplateHead()
        };

        var templateSpans = CreateList<TemplateSpan>();


        do
        {
            templateSpans.Add(ParseTemplateSpan());
        }
        while (LastOrUndefined(templateSpans).Literal.Kind is TypeScriptSyntaxKind.TemplateMiddle);


        templateSpans.End = NodeEnd;

        template.TemplateSpans = templateSpans;


        return FinishNode(template);
    }


    public TemplateSpan ParseTemplateSpan()
    {
        var span = new TemplateSpan
        {
            Pos = Scanner.StartPos,
            Expression = AllowInAnd(ParseExpression)
        };
        //var literal = TemplateMiddle | TemplateTail;
        if (Token is TypeScriptSyntaxKind.CloseBraceToken)
        {

            ReScanTemplateToken;

            span.Literal = ParseTemplateMiddleOrTemplateTail();
        }
        else
        {

            span.Literal = (TemplateTail)ParseExpectedToken<TemplateTail>(TypeScriptSyntaxKind.TemplateTail, /*reportAtCurrentPosition*/ false, Diagnostics._0_expected, TokenToString(TypeScriptSyntaxKind.CloseBraceToken));
        }


        //span.literal = literal;

        return FinishNode(span);
    }


    public ILiteralExpression ParseLiteralNode(bool? internName = null)
    {
        var t = Token;
        if (t == TypeScriptSyntaxKind.StringLiteral) return (ILiteralExpression)ParseLiteralLikeNode(new StringLiteral(), internName == true);
        else if (t == TypeScriptSyntaxKind.RegularExpressionLiteral) return (ILiteralExpression)ParseLiteralLikeNode(new RegularExpressionLiteral(), internName == true);
        else if (t == TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral) return (ILiteralExpression)ParseLiteralLikeNode(new NoSubstitutionTemplateLiteral(), internName == true);
        else return t == TypeScriptSyntaxKind.NumericLiteral
            ? (ILiteralExpression)ParseLiteralLikeNode(new NumericLiteral(), internName == true)
            : throw new NotSupportedException("parseLiteralNode");
        //return parseLiteralLikeNode(token(), internName == true);
    }


    public TemplateHead ParseTemplateHead()
    {
        var t = Token;
        var fragment = new TemplateHead();
        ParseLiteralLikeNode(fragment, /*internName*/ false);

        return fragment;
    }


    public /*TemplateMiddle | TemplateTail*/ILiteralLikeNode ParseTemplateMiddleOrTemplateTail()
    {
        var t = Token;
        ILiteralLikeNode fragment = null;
        if (t == TypeScriptSyntaxKind.TemplateMiddle)
        {
            fragment = ParseLiteralLikeNode(new TemplateMiddle(), /*internName*/ false);
        }
        else if (t == TypeScriptSyntaxKind.TemplateTail)
        {
            fragment = ParseLiteralLikeNode(new TemplateTail(), /*internName*/ false);
        }
        //var fragment = parseLiteralLikeNode(token(), /*internName*/ false);

        return /*(TemplateMiddle | TemplateTail)*/fragment;
    }


    public ILiteralLikeNode ParseLiteralLikeNode(/*TypeScriptSyntaxKind kind*/ILiteralLikeNode node, bool internName)
    {
        node.Pos = Scanner.StartPos;
        //var node = new LiteralLikeNode { pos = scanner.getStartPos() }; // LiteralExpression();
        var text = Scanner.TokenValue;

        node.Text = internName ? InternIdentifier(text) : text;
        if (Scanner.HasExtendedUnicodeEscape)
        {

            node.HasExtendedUnicodeEscape = true;
        }
        if (Scanner.IsUnterminated)
        {

            node.IsUnterminated = true;
        }
        var tokenPos = Scanner.TokenPos;

        _ = NextToken;

        FinishNode(node);
        if (node.Kind is TypeScriptSyntaxKind.NumericLiteral
                        && SourceText.CharCodeAt(tokenPos) == CharacterCode._0
                        && IsOctalDigit(SourceText.CharCodeAt(tokenPos + 1)))
        {


            node.IsOctalLiteral = true;
        }


        return node;
    }


    public TypeReferenceNode ParseTypeReference()
    {
        var typeName = ParseEntityName(/*allowReservedWords*/ false, Diagnostics.Type_expected);
        var node = new TypeReferenceNode
        {
            Pos = typeName.Pos,
            TypeName = typeName
        };
        if (!Scanner.HasPrecedingLineBreak && Token is TypeScriptSyntaxKind.LessThanToken)
        {

            node.TypeArguments = ParseBracketedList(ParsingContext.TypeArguments, ParseType, TypeScriptSyntaxKind.LessThanToken, TypeScriptSyntaxKind.GreaterThanToken);
        }

        return FinishNode(node);
    }


    public TypePredicateNode ParseThisTypePredicate(ThisTypeNode lhs)
    {

        _ = NextToken;
        var node = new TypePredicateNode
        {
            Pos = lhs.Pos,
            ParameterName = lhs,

            Type = ParseType()
        };

        return FinishNode(node);
    }


    public ThisTypeNode ParseThisTypeNode()
    {
        var node = new ThisTypeNode { Pos = Scanner.StartPos };

        _ = NextToken;

        return FinishNode(node);
    }


    public TypeQueryNode ParseTypeQuery()
    {
        var node = new TypeQueryNode() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.TypeOfKeyword);

        node.ExprName = ParseEntityName(/*allowReservedWords*/ true);

        return FinishNode(node);
    }


    public TypeParameterDeclaration ParseTypeParameter()
    {
        var node = new TypeParameterDeclaration
        {
            Pos = Scanner.StartPos,
            Name = ParseIdentifier()
        };
        if (ParseOptional(TypeScriptSyntaxKind.ExtendsKeyword))
        {
            if (IsStartOfType() || !IsStartOfExpression())
            {

                node.Constraint = ParseType();
            }
            else
            {

                // It was not a type, and it looked like an expression.  Parse out an expression
                // here so we recover well.  Note: it is important that we call parseUnaryExpression
                // and not parseExpression here.  If the user has:
                //
                //      <T extends "">
                //
                // We do *not* want to consume the  >  as we're consuming the expression for "".
                node.Expression = (Expression)ParseUnaryExpressionOrHigher();
            }
        }
        if (ParseOptional(TypeScriptSyntaxKind.EqualsToken))
        {

            node.Default = ParseType();
        }


        return FinishNode(node);
    }


    public NodeArray<TypeParameterDeclaration>? ParseTypeParameters() => Token is TypeScriptSyntaxKind.LessThanToken
            ? ParseBracketedList(ParsingContext.TypeParameters, ParseTypeParameter, TypeScriptSyntaxKind.LessThanToken, TypeScriptSyntaxKind.GreaterThanToken)
            : (NodeArray<TypeParameterDeclaration>?)null;


    public ITypeNode? ParseParameterType() => ParseOptional(TypeScriptSyntaxKind.ColonToken) ? ParseType() : null;


    public bool IsStartOfParameter() => Token is TypeScriptSyntaxKind.DotDotDotToken || IsIdentifierOrPattern() || IsModifierKind(Token) || Token is TypeScriptSyntaxKind.AtToken || Token is TypeScriptSyntaxKind.ThisKeyword;


    public ParameterDeclaration ParseParameter()
    {
        var node = new ParameterDeclaration() { Pos = Scanner.StartPos };
        if (Token is TypeScriptSyntaxKind.ThisKeyword)
        {

            node.Name = CreateIdentifier(/*isIdentifier*/true, null);

            node.Type = ParseParameterType();

            return FinishNode(node);
        }


        node.Decorators = ParseDecorators();

        node.Modifiers = ParseModifiers();

        node.DotDotDotToken = ParseOptionalToken<DotDotDotToken>(TypeScriptSyntaxKind.DotDotDotToken);


        // FormalParameter [Yield,Await]:
        //      BindingElement[?Yield,?Await]
        node.Name = ParseIdentifierOrPattern();
        if (GetFullWidth(node.Name) == 0 && !HasModifiers(node) && IsModifierKind(Token))
        {

            // in cases like
            // 'use strict'
            // function foo(static)
            // isParameter('static') == true, because of isModifier('static')
            // however 'static' is not a legal identifier in a strict mode.
            // so result of this function will be ParameterDeclaration (flags = 0, name = missing, type = null, initializer = null)
            // and current token will not change => parsing of the enclosing parameter list will last till the end of time (or OOM)
            // to avoid this we'll advance cursor to the next token.
            _ = NextToken;
        }


        node.QuestionToken = ParseOptionalToken<QuestionToken>(TypeScriptSyntaxKind.QuestionToken);

        node.Type = ParseParameterType();

        node.Initializer = ParseBindingElementInitializer(/*inParameter*/ true);


        // Do not check for initializers in an ambient context for parameters. This is not
        // a grammar error because the grammar allows arbitrary call signatures in
        // an ambient context.
        // It is actually not necessary for this to be an error at all. The reason is that
        // function/constructor implementations are syntactically disallowed in ambient
        // contexts. In addition, parameter initializers are semantically disallowed in
        // overload signatures. So parameter initializers are transitively disallowed in
        // ambient contexts.

        return AddJsDocComment(FinishNode(node));
    }


    public IExpression ParseBindingElementInitializer(bool inParameter) => inParameter ? ParseParameterInitializer() : ParseNonParameterInitializer();


    public IExpression ParseParameterInitializer() => ParseInitializer(/*inParameter*/ true);


    public void FillSignature(TypeScriptSyntaxKind returnToken, bool
                yieldContext, bool
                awaitContext, bool
                requireCompleteParameterList, ISignatureDeclaration
                signature)
    {
        var returnTokenRequired = returnToken is TypeScriptSyntaxKind.EqualsGreaterThanToken;

        signature.TypeParameters = ParseTypeParameters();

        signature.Parameters = ParseParameterList(yieldContext, awaitContext, requireCompleteParameterList);
        if (returnTokenRequired)
        {

            ParseExpected(returnToken);

            signature.Type = ParseTypeOrTypePredicate();
        }
        else
        if (ParseOptional(returnToken))
        {

            signature.Type = ParseTypeOrTypePredicate();
        }
    }
    public void FillSignatureEqualsGreaterThanToken(TypeScriptSyntaxKind returnToken, bool
                yieldContext, bool
                awaitContext, bool
                requireCompleteParameterList, SignatureDeclaration
                signature)
    {
        var returnTokenRequired = returnToken is TypeScriptSyntaxKind.EqualsGreaterThanToken;

        signature.TypeParameters = ParseTypeParameters();

        signature.Parameters = ParseParameterList(yieldContext, awaitContext, requireCompleteParameterList);
        if (returnTokenRequired)
        {

            ParseExpected(returnToken);

            signature.Type = ParseTypeOrTypePredicate();
        }
        else
        if (ParseOptional(returnToken))
        {

            signature.Type = ParseTypeOrTypePredicate();
        }
    }
    public void FillSignatureColonToken(TypeScriptSyntaxKind
                returnToken, bool
                yieldContext, bool
                awaitContext, bool
                requireCompleteParameterList, SignatureDeclaration
                signature)
    {
        var returnTokenRequired = returnToken is TypeScriptSyntaxKind.EqualsGreaterThanToken;

        signature.TypeParameters = ParseTypeParameters();

        signature.Parameters = ParseParameterList(yieldContext, awaitContext, requireCompleteParameterList);
        if (returnTokenRequired)
        {

            ParseExpected(returnToken);

            signature.Type = ParseTypeOrTypePredicate();
        }
        else
        if (ParseOptional(returnToken))
        {

            signature.Type = ParseTypeOrTypePredicate();
        }
    }

    public NodeArray<ParameterDeclaration>? ParseParameterList(bool yieldContext, bool awaitContext, bool requireCompleteParameterList)
    {
        if (ParseExpected(TypeScriptSyntaxKind.OpenParenToken))
        {
            var savedYieldContext = InYieldContext();
            var savedAwaitContext = InAwaitContext();


            SetYieldContext(yieldContext);

            SetAwaitContext(awaitContext);
            var result = ParseDelimitedList(ParsingContext.Parameters, ParseParameter);


            SetYieldContext(savedYieldContext);

            SetAwaitContext(savedAwaitContext);
            if (!ParseExpected(TypeScriptSyntaxKind.CloseParenToken) && requireCompleteParameterList)
            {

                // Caller insisted that we had to end with a )   We didn't.  So just return
                // null here.
                return null;
            }


            return result;
        }


        // We didn't even have an open paren.  If the caller requires a complete parameter list,
        // we definitely can't provide that.  However, if they're ok with an incomplete one,
        // then just return an empty set of parameters.
        return requireCompleteParameterList ? null : CreateMissingList<ParameterDeclaration>();
    }


    public void ParseTypeMemberSemicolon()
    {
        if (ParseOptional(TypeScriptSyntaxKind.CommaToken))
        {

            return;
        }


        // Didn't have a comma.  We must have a (possible ASI) semicolon.
        ParseSemicolon();
    }


    public /*CallSignatureDeclaration | ConstructSignatureDeclaration*/ITypeElement ParseSignatureMember(TypeScriptSyntaxKind kind)
    {
        //var node = new CallSignatureDeclaration | ConstructSignatureDeclaration();
        if (Kind is TypeScriptSyntaxKind.ConstructSignature)
        {

            var node = new ConstructSignatureDeclaration { Pos = Scanner.StartPos };
            ParseExpected(TypeScriptSyntaxKind.NewKeyword);
            FillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ false, /*awaitContext*/ false, /*requireCompleteParameterList*/ false, node);

            ParseTypeMemberSemicolon();

            return AddJsDocComment(FinishNode(node));
        }
        else
        {
            var node = new CallSignatureDeclaration { Pos = Scanner.StartPos };
            FillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ false, /*awaitContext*/ false, /*requireCompleteParameterList*/ false, node);

            ParseTypeMemberSemicolon();

            return AddJsDocComment(FinishNode(node));
        }

        //fillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ false, /*awaitContext*/ false, /*requireCompleteParameterList*/ false, node);

        //parseTypeMemberSemicolon();

        //return addJSDocComment(finishNode(node));
    }


    public bool IsIndexSignature() => Token is not TypeScriptSyntaxKind.OpenBracketToken ? false : LookAhead(IsUnambiguouslyIndexSignature);


    public bool IsUnambiguouslyIndexSignature()
    {

        // The only allowed sequence is:
        //
        //   [id:
        //
        // However, for error recovery, we also check the following cases:
        //
        //   [...
        //   [id,
        //   [id?,
        //   [id?:
        //   [id?]
        //   [public id
        //   [private id
        //   [protected id
        //   []
        //
        _ = NextToken;
        if (Token is TypeScriptSyntaxKind.DotDotDotToken || Token is TypeScriptSyntaxKind.CloseBracketToken)
        {

            return true;
        }
        if (IsModifierKind(Token))
        {

            _ = NextToken;
            if (IsIdentifier())
            {

                return true;
            }
        }
        else
        if (!IsIdentifier())
        {

            return false;
        }
        else
        {

            // Skip the identifier
            _ = NextToken;
        }
        if (Token is TypeScriptSyntaxKind.ColonToken || Token is TypeScriptSyntaxKind.CommaToken)
        {

            return true;
        }
        if (Token is not TypeScriptSyntaxKind.QuestionToken)
        {

            return false;
        }


        // If any of the following tokens are after the question mark, it cannot
        // be a conditional expression, so treat it as an indexer.
        _ = NextToken;

        return Token is TypeScriptSyntaxKind.ColonToken || Token is TypeScriptSyntaxKind.CommaToken || Token is TypeScriptSyntaxKind.CloseBracketToken;
    }


    public IndexSignatureDeclaration ParseIndexSignatureDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new IndexSignatureDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,

            Modifiers = modifiers,

            Parameters = ParseBracketedList(ParsingContext.Parameters, ParseParameter, TypeScriptSyntaxKind.OpenBracketToken, TypeScriptSyntaxKind.CloseBracketToken),

            Type = ParseTypeAnnotation()
        };

        ParseTypeMemberSemicolon();

        return FinishNode(node);
    }


    public /*PropertySignature | MethodSignature*/ITypeElement ParsePropertyOrMethodSignature(int fullStart, NodeArray<Modifier> modifiers)
    {
        var name = ParsePropertyName();
        var questionToken = ParseOptionalToken<QuestionToken>(TypeScriptSyntaxKind.QuestionToken);
        if (Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.LessThanToken)
        {
            var method = new MethodSignature
            {
                Pos = fullStart,
                Modifiers = modifiers,

                Name = name,

                QuestionToken = questionToken
            };


            // Method signatures don't exist in expression contexts.  So they have neither
            // [Yield] nor [Await]
            FillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ false, /*awaitContext*/ false, /*requireCompleteParameterList*/ false, method);

            ParseTypeMemberSemicolon();

            return AddJsDocComment(FinishNode(method));
        }
        else
        {
            var property = new PropertySignature
            {
                Pos = fullStart,
                Modifiers = modifiers,

                Name = name,

                QuestionToken = questionToken,

                Type = ParseTypeAnnotation()
            };
            if (Token is TypeScriptSyntaxKind.EqualsToken)
            {

                // Although type literal properties cannot not have initializers, we attempt
                // to parse an initializer so we can report in the checker that an interface
                // property or type literal property cannot have an initializer.
                property.Initializer = ParseNonParameterInitializer();
            }


            ParseTypeMemberSemicolon();

            return AddJsDocComment(FinishNode(property));
        }
    }


    public bool IsTypeMemberStart()
    {
        if (Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.LessThanToken)
        {

            return true;
        }
        var idToken = false;
        while (IsModifierKind(Token))
        {

            idToken = true;

            _ = NextToken;
        }
        if (Token is TypeScriptSyntaxKind.OpenBracketToken)
        {

            return true;
        }
        if (IsLiteralPropertyName())
        {

            idToken = true;

            _ = NextToken;
        }
        return idToken
            ? Token is TypeScriptSyntaxKind.OpenParenToken ||
                Token is TypeScriptSyntaxKind.LessThanToken ||
                Token is TypeScriptSyntaxKind.QuestionToken ||
                Token is TypeScriptSyntaxKind.ColonToken ||
                Token is TypeScriptSyntaxKind.CommaToken ||
                CanParseSemicolon()
            : false;
    }


    public ITypeElement ParseTypeMember()
    {
        if (Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.LessThanToken)
        {

            return ParseSignatureMember(TypeScriptSyntaxKind.CallSignature);
        }
        if (Token is TypeScriptSyntaxKind.NewKeyword && LookAhead(IsStartOfConstructSignature))
        {

            return ParseSignatureMember(TypeScriptSyntaxKind.ConstructSignature);
        }
        var fullStart = NodePos;
        var modifiers = ParseModifiers();
        return IsIndexSignature()
            ? ParseIndexSignatureDeclaration(fullStart, /*decorators*/ null, modifiers)
            : ParsePropertyOrMethodSignature(fullStart, modifiers);
    }


    public bool IsStartOfConstructSignature()
    {

        _ = NextToken;

        return Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.LessThanToken;
    }


    public TypeLiteralNode ParseTypeLiteral()
    {
        var node = new TypeLiteralNode
        {
            Pos = Scanner.StartPos,
            Members = ParseObjectTypeMembers()
        };

        return FinishNode(node);
    }


    public NodeArray<ITypeElement> ParseObjectTypeMembers()
    {
        NodeArray<ITypeElement> members = null;
        if (ParseExpected(TypeScriptSyntaxKind.OpenBraceToken))
        {

            members = ParseList(ParsingContext.TypeMembers, ParseTypeMember);

            ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);
        }
        else
        {

            members = CreateMissingList<ITypeElement>();
        }


        return members;
    }


    public bool IsStartOfMappedType()
    {

        _ = NextToken;
        if (Token is TypeScriptSyntaxKind.ReadonlyKeyword)
        {

            _ = NextToken;
        }

        return Token is TypeScriptSyntaxKind.OpenBracketToken && NextTokenIsIdentifier() && NextToken is TypeScriptSyntaxKind.InKeyword;
    }


    public TypeParameterDeclaration ParseMappedTypeParameter()
    {
        var node = new TypeParameterDeclaration
        {
            Pos = Scanner.StartPos,
            Name = ParseIdentifier()
        };

        ParseExpected(TypeScriptSyntaxKind.InKeyword);

        node.Constraint = ParseType();

        return FinishNode(node);
    }


    public MappedTypeNode ParseMappedType()
    {
        var node = new MappedTypeNode() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenBraceToken);

        node.ReadonlyToken = ParseOptionalToken<ReadonlyToken>(TypeScriptSyntaxKind.ReadonlyKeyword);

        ParseExpected(TypeScriptSyntaxKind.OpenBracketToken);

        node.TypeParameter = ParseMappedTypeParameter();

        ParseExpected(TypeScriptSyntaxKind.CloseBracketToken);

        node.QuestionToken = ParseOptionalToken<QuestionToken>(TypeScriptSyntaxKind.QuestionToken);

        node.Type = ParseTypeAnnotation();

        ParseSemicolon();

        ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);

        return FinishNode(node);
    }


    public TupleTypeNode ParseTupleType()
    {
        var node = new TupleTypeNode
        {
            Pos = Scanner.StartPos,
            ElementTypes = ParseBracketedList(ParsingContext.TupleElementTypes, ParseType, TypeScriptSyntaxKind.OpenBracketToken, TypeScriptSyntaxKind.CloseBracketToken)
        };

        return FinishNode(node);
    }


    public ParenthesizedTypeNode ParseParenthesizedType()
    {
        var node = new ParenthesizedTypeNode() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);

        node.Type = ParseType();

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

        return FinishNode(node);
    }


    public IFunctionOrConstructorTypeNode ParseFunctionOrConstructorType(TypeScriptSyntaxKind kind)
    {

        var node = Kind is TypeScriptSyntaxKind.FunctionType ?
            (IFunctionOrConstructorTypeNode)new FunctionTypeNode { Kind = TypeScriptSyntaxKind.FunctionType } :
            Kind is TypeScriptSyntaxKind.ConstructorType ?
            new ConstructorTypeNode { Kind = TypeScriptSyntaxKind.ConstructorType } :
            throw new NotSupportedException("parseFunctionOrConstructorType");
        node.Pos = Scanner.StartPos;
        //new FunctionOrConstructorTypeNode { kind = kind, pos = scanner.getStartPos() };
        if (Kind is TypeScriptSyntaxKind.ConstructorType)
        {

            ParseExpected(TypeScriptSyntaxKind.NewKeyword);
        }

        FillSignature(TypeScriptSyntaxKind.EqualsGreaterThanToken, /*yieldContext*/ false, /*awaitContext*/ false, /*requireCompleteParameterList*/ false, node);

        return FinishNode(node);
    }


    public TypeNode? ParseKeywordAndNoDot()
    {
        var node = ParseTokenNode<TypeNode>(Token);

        return Token is TypeScriptSyntaxKind.DotToken ? null : node;
    }


    public LiteralTypeNode ParseLiteralTypeNode()
    {
        var node = new LiteralTypeNode
        {
            Pos = Scanner.StartPos,
            Literal = ParseSimpleUnaryExpression()
        };

        FinishNode(node);

        return node;
    }


    public bool NextTokenIsNumericLiteral() => NextToken is TypeScriptSyntaxKind.NumericLiteral;


    public ITypeNode ParseNonArrayType()
    {
        switch (Token)
        {
            case TypeScriptSyntaxKind.AnyKeyword:
            case TypeScriptSyntaxKind.StringKeyword:
            case TypeScriptSyntaxKind.NumberKeyword:
            case TypeScriptSyntaxKind.BooleanKeyword:
            case TypeScriptSyntaxKind.SymbolKeyword:
            case TypeScriptSyntaxKind.UndefinedKeyword:
            case TypeScriptSyntaxKind.NeverKeyword:
            case TypeScriptSyntaxKind.ObjectKeyword:
                var node = TryParse(ParseKeywordAndNoDot);

                return node ?? ParseTypeReference();
            case TypeScriptSyntaxKind.StringLiteral:
            case TypeScriptSyntaxKind.NumericLiteral:
            case TypeScriptSyntaxKind.TrueKeyword:
            case TypeScriptSyntaxKind.FalseKeyword:

                return ParseLiteralTypeNode();
            case TypeScriptSyntaxKind.MinusToken:

                return LookAhead(NextTokenIsNumericLiteral) ? ParseLiteralTypeNode() : ParseTypeReference();
            case TypeScriptSyntaxKind.VoidKeyword:
            case TypeScriptSyntaxKind.NullKeyword:

                return ParseTokenNode<TypeNode>(Token);
            case TypeScriptSyntaxKind.ThisKeyword:
                {
                    var thisKeyword = ParseThisTypeNode();
                    return Token is TypeScriptSyntaxKind.IsKeyword && !Scanner.HasPrecedingLineBreak ? ParseThisTypePredicate(thisKeyword) : thisKeyword;
                }
            //goto caseLabel17;
            case TypeScriptSyntaxKind.TypeOfKeyword:
                //caseLabel17:
                return ParseTypeQuery();
            case TypeScriptSyntaxKind.OpenBraceToken:

                return LookAhead(IsStartOfMappedType) ? ParseMappedType() : ParseTypeLiteral();
            case TypeScriptSyntaxKind.OpenBracketToken:

                return ParseTupleType();
            case TypeScriptSyntaxKind.OpenParenToken:

                return ParseParenthesizedType();
            default:

                return ParseTypeReference();
        }
    }


    public bool IsStartOfType() => Token switch
    {
        TypeScriptSyntaxKind.AnyKeyword or TypeScriptSyntaxKind.StringKeyword or TypeScriptSyntaxKind.NumberKeyword or TypeScriptSyntaxKind.BooleanKeyword or TypeScriptSyntaxKind.SymbolKeyword or TypeScriptSyntaxKind.VoidKeyword or TypeScriptSyntaxKind.UndefinedKeyword or TypeScriptSyntaxKind.NullKeyword or TypeScriptSyntaxKind.ThisKeyword or TypeScriptSyntaxKind.TypeOfKeyword or TypeScriptSyntaxKind.NeverKeyword or TypeScriptSyntaxKind.OpenBraceToken or TypeScriptSyntaxKind.OpenBracketToken or TypeScriptSyntaxKind.LessThanToken or TypeScriptSyntaxKind.BarToken or TypeScriptSyntaxKind.AmpersandToken or TypeScriptSyntaxKind.NewKeyword or TypeScriptSyntaxKind.StringLiteral or TypeScriptSyntaxKind.NumericLiteral or TypeScriptSyntaxKind.TrueKeyword or TypeScriptSyntaxKind.FalseKeyword or TypeScriptSyntaxKind.ObjectKeyword => true,
        TypeScriptSyntaxKind.MinusToken => LookAhead(NextTokenIsNumericLiteral),
        TypeScriptSyntaxKind.OpenParenToken => LookAhead(IsStartOfParenthesizedOrFunctionType),// Only consider '(' the start of a type if followed by ')', '...', an identifier, a modifier,
                                                                                     // or something that starts a type. We don't want to consider things like '(1)' a type.
        _ => IsIdentifier(),
    };


    public bool IsStartOfParenthesizedOrFunctionType()
    {

        _ = NextToken;

        return Token is TypeScriptSyntaxKind.CloseParenToken || IsStartOfParameter() || IsStartOfType();
    }


    public ITypeNode ParseArrayTypeOrHigher()
    {
        var type = ParseNonArrayType();
        while (!Scanner.HasPrecedingLineBreak && ParseOptional(TypeScriptSyntaxKind.OpenBracketToken))
        {
            if (IsStartOfType())
            {
                var node = new IndexedAccessTypeNode
                {
                    Pos = type.Pos,
                    ObjectType = type,

                    IndexType = ParseType()
                };

                ParseExpected(TypeScriptSyntaxKind.CloseBracketToken);

                type = FinishNode(node);
            }
            else
            {
                var node = new ArrayTypeNode
                {
                    Pos = type.Pos,
                    ElementType = type
                };

                ParseExpected(TypeScriptSyntaxKind.CloseBracketToken);

                type = FinishNode(node);
            }
        }

        return type;
    }


    public /*MappedTypeNode*/TypeOperatorNode ParseTypeOperator(TypeScriptSyntaxKind/*.KeyOfKeyword*/ @operator)
    {
        var node = new TypeOperatorNode() { Pos = Scanner.StartPos };

        ParseExpected(@operator);

        node.Operator = @operator;

        node.Type = ParseTypeOperatorOrHigher();

        return FinishNode(node);
    }


    public ITypeNode ParseTypeOperatorOrHigher() => Token switch
    {
        TypeScriptSyntaxKind.KeyOfKeyword => ParseTypeOperator(TypeScriptSyntaxKind.KeyOfKeyword),
        _ => ParseArrayTypeOrHigher(),
    };


    public ITypeNode ParseUnionOrIntersectionType(TypeScriptSyntaxKind/*.UnionType | TypeScriptSyntaxKind.IntersectionType*/ kind, Func<ITypeNode> parseConstituentType, TypeScriptSyntaxKind/*.BarToken | TypeScriptSyntaxKind.AmpersandToken*/ @operator)
    {

        ParseOptional(@operator);
        var type = parseConstituentType();
        if (Token is @operator)
        {
            var types = CreateList<ITypeNode>(); //[type], type.pos);
            types.Pos = type.Pos;
            types.Add(type);


            while (ParseOptional(@operator))
            {

                types.Add(parseConstituentType());
            }

            types.End = NodeEnd;
            var node = Kind is TypeScriptSyntaxKind.UnionType ?
                (IUnionOrIntersectionTypeNode)new UnionTypeNode { Kind = kind, Pos = type.Pos } :
                Kind is TypeScriptSyntaxKind.IntersectionType ? new IntersectionTypeNode { Kind = kind, Pos = type.Pos }
                : throw new NotSupportedException("parseUnionOrIntersectionType");

            node.Types = types;

            type = FinishNode(node);
        }

        return type;
    }


    public ITypeNode ParseIntersectionTypeOrHigher() => ParseUnionOrIntersectionType(TypeScriptSyntaxKind.IntersectionType, ParseTypeOperatorOrHigher, TypeScriptSyntaxKind.AmpersandToken);


    public ITypeNode ParseUnionTypeOrHigher() => ParseUnionOrIntersectionType(TypeScriptSyntaxKind.UnionType, ParseIntersectionTypeOrHigher, TypeScriptSyntaxKind.BarToken);


    public bool IsStartOfFunctionType() => Token is TypeScriptSyntaxKind.LessThanToken
            ? true
            : Token is TypeScriptSyntaxKind.OpenParenToken && LookAhead(IsUnambiguouslyStartOfFunctionType);


    public bool SkipParameterStart()
    {
        if (IsModifierKind(Token))
        {

            // Skip modifiers
            ParseModifiers();
        }
        if (IsIdentifier() || Token is TypeScriptSyntaxKind.ThisKeyword)
        {

            _ = NextToken;

            return true;
        }
        if (Token is TypeScriptSyntaxKind.OpenBracketToken || Token is TypeScriptSyntaxKind.OpenBraceToken)
        {
            var previousErrorCount = ParseDiagnostics.Count;

            ParseIdentifierOrPattern();

            return previousErrorCount == ParseDiagnostics.Count;
        }

        return false;
    }


    public bool IsUnambiguouslyStartOfFunctionType()
    {

        _ = NextToken;
        if (Token is TypeScriptSyntaxKind.CloseParenToken || Token is TypeScriptSyntaxKind.DotDotDotToken)
        {

            // ( )
            // ( ...
            return true;
        }
        if (SkipParameterStart())
        {
            if (Token is TypeScriptSyntaxKind.ColonToken || Token is TypeScriptSyntaxKind.CommaToken ||
                                Token is TypeScriptSyntaxKind.QuestionToken || Token is TypeScriptSyntaxKind.EqualsToken)
            {

                // ( xxx :
                // ( xxx ,
                // ( xxx ?
                // ( xxx =
                return true;
            }
            if (Token is TypeScriptSyntaxKind.CloseParenToken)
            {

                _ = NextToken;
                if (Token is TypeScriptSyntaxKind.EqualsGreaterThanToken)
                {

                    // ( xxx ) =>
                    return true;
                }
            }
        }

        return false;
    }


    public ITypeNode ParseTypeOrTypePredicate()
    {
        var typePredicateVariable = IsIdentifier() ? TryParse(ParseTypePredicatePrefix) : null;
        var type = ParseType();
        if (typePredicateVariable != null)
        {
            var node = new TypePredicateNode
            {
                Pos = typePredicateVariable.Pos,
                ParameterName = typePredicateVariable,

                Type = type
            };

            return FinishNode(node);
        }
        else
        {

            return type;
        }
    }


    public Identifier? ParseTypePredicatePrefix()
    {
        var id = ParseIdentifier();
        if (Token is TypeScriptSyntaxKind.IsKeyword && !Scanner.HasPrecedingLineBreak)
        {

            _ = NextToken;

            return id;
        }
        return null;
    }


    public ITypeNode ParseType() =>

        // The rules about 'yield' only apply to actual code/expression contexts.  They don't
        // apply to 'type' contexts.  So we disable these parameters here before moving on.
        DoOutsideOfContext(NodeFlags.TypeExcludesFlags, ParseTypeWorker);


    public ITypeNode ParseTypeWorker()
    {
        if (IsStartOfFunctionType())
        {

            return ParseFunctionOrConstructorType(TypeScriptSyntaxKind.FunctionType);
        }
        return Token is TypeScriptSyntaxKind.NewKeyword ? ParseFunctionOrConstructorType(TypeScriptSyntaxKind.ConstructorType) : ParseUnionTypeOrHigher();
    }


    public ITypeNode? ParseTypeAnnotation() =>
        ParseOptional(TypeScriptSyntaxKind.ColonToken) ? ParseType() : null;

    public bool IsStartOfLeftHandSideExpression() => Token switch
    {
        TypeScriptSyntaxKind.ThisKeyword or
        TypeScriptSyntaxKind.SuperKeyword or
        TypeScriptSyntaxKind.NullKeyword or
        TypeScriptSyntaxKind.TrueKeyword or
        TypeScriptSyntaxKind.FalseKeyword or
        TypeScriptSyntaxKind.NumericLiteral or
        TypeScriptSyntaxKind.StringLiteral or
        TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral or
        TypeScriptSyntaxKind.TemplateHead or
        TypeScriptSyntaxKind.OpenParenToken or
        TypeScriptSyntaxKind.OpenBracketToken or
        TypeScriptSyntaxKind.OpenBraceToken or
        TypeScriptSyntaxKind.FunctionKeyword or
        TypeScriptSyntaxKind.ClassKeyword or
        TypeScriptSyntaxKind.NewKeyword or
        TypeScriptSyntaxKind.SlashToken or
        TypeScriptSyntaxKind.SlashEqualsToken or
        TypeScriptSyntaxKind.Identifier => true,

        _ => IsIdentifier(),
    };


    public bool IsStartOfExpression()
    {
        if (IsStartOfLeftHandSideExpression())
        {
            return true;
        }

        switch (Token)
        {
            case TypeScriptSyntaxKind.PlusToken:
            case TypeScriptSyntaxKind.MinusToken:
            case TypeScriptSyntaxKind.TildeToken:
            case TypeScriptSyntaxKind.ExclamationToken:
            case TypeScriptSyntaxKind.DeleteKeyword:
            case TypeScriptSyntaxKind.TypeOfKeyword:
            case TypeScriptSyntaxKind.VoidKeyword:
            case TypeScriptSyntaxKind.PlusPlusToken:
            case TypeScriptSyntaxKind.MinusMinusToken:
            case TypeScriptSyntaxKind.LessThanToken:
            case TypeScriptSyntaxKind.AwaitKeyword:
            case TypeScriptSyntaxKind.YieldKeyword:
                // Yield/await always starts an expression.  Either it is an identifier (in which case
                // it is definitely an expression).  Or it's a keyword (either because we're in
                // a generator or async function, or in strict mode (or both)) and it started a yield or await expression.
                return true;
            default:
                // Error tolerance.  If we see the start of some binary operator, we consider
                // that the start of an expression.  That way we'll parse out a missing identifier,
                // give a good message about an identifier being missing, and then consume the
                // rest of the binary expression.
                if (IsBinaryOperator())
                {
                    return true;
                }

                return IsIdentifier();
        }
    }

    public bool IsStartOfExpressionStatement() =>
        // As per the grammar, none of '{' or 'function' or 'class' can start an expression statement.
        Token is not TypeScriptSyntaxKind.OpenBraceToken &&
        Token is not TypeScriptSyntaxKind.FunctionKeyword &&
        Token is not TypeScriptSyntaxKind.ClassKeyword &&
        Token is not TypeScriptSyntaxKind.AtToken &&
        IsStartOfExpression();


    public IExpression ParseExpression()
    {
        var saveDecoratorContext = InDecoratorContext();
        if (saveDecoratorContext)
        {
            SetDecoratorContext(val: false);
        }

        var expr = ParseAssignmentExpressionOrHigher();
        Token? operatorToken;
        while ((operatorToken = ParseOptionalToken<Token>(TypeScriptSyntaxKind.CommaToken)) != null)
        {
            expr = MakeBinaryExpression(expr, operatorToken, ParseAssignmentExpressionOrHigher());
        }

        if (saveDecoratorContext)
        {
            SetDecoratorContext(val: true);
        }

        return expr;
    }


    public IExpression? ParseInitializer(bool inParameter)
    {
        if (Token is not TypeScriptSyntaxKind.EqualsToken)
        {
            if (Scanner.HasPrecedingLineBreak ||
                (inParameter && Token is TypeScriptSyntaxKind.OpenBraceToken) || !IsStartOfExpression())
            {
                // preceding line break, open brace in a parameter (likely a function body) or current token is not an expression -
                // do not try to parse initializer
                return null;
            }
        }

        ParseExpected(TypeScriptSyntaxKind.EqualsToken);

        return ParseAssignmentExpressionOrHigher();
    }


    public IExpression ParseAssignmentExpressionOrHigher()
    {
        if (IsYieldExpression())
        {
            return ParseYieldExpression();
        }

        var arrowExpression =
            TryParseParenthesizedArrowFunctionExpression() ??
            TryParseAsyncSimpleArrowFunctionExpression();

        if (arrowExpression is not null)
        {
            return arrowExpression;
        }

        var expr = ParseBinaryExpressionOrHigher(precedence: 0);
        if (expr.Kind is TypeScriptSyntaxKind.Identifier && Token is TypeScriptSyntaxKind.EqualsGreaterThanToken)
        {
            return ParseSimpleArrowFunctionExpression((Identifier)expr);
        }

        if (IsLeftHandSideExpression(expr) && IsAssignmentOperator(ReScanGreaterToken))
        {
            return MakeBinaryExpression(
                expr, ParseTokenNode<Token>(Token), ParseAssignmentExpressionOrHigher());
        }

        // It wasn't an assignment or a lambda.  This is a conditional expression:
        return ParseConditionalExpressionRest(expr);
    }


    public bool IsYieldExpression()
    {
        if (Token is TypeScriptSyntaxKind.YieldKeyword)
        {
            if (InYieldContext())
            {
                return true;
            }

            // We're in a context where 'yield expr' is not allowed.  However, if we can
            // definitely tell that the user was trying to parse a 'yield expr' and not
            // just a normal expr that start with a 'yield' identifier, then parse out
            // a 'yield expr'.  We can then report an error later that they are only
            // allowed in generator expressions.
            //
            // for example, if we see 'yield(foo)', then we'll have to treat that as an
            // invocation expression of something called 'yield'.  However, if we have
            // 'yield foo' then that is not legal as a normal expression, so we can
            // definitely recognize this as a yield expression.
            //
            // for now we just check if the next token is an identifier.  More heuristics
            // can be added here later as necessary.  We just need to make sure that we
            // don't accidentally consume something legal.
            return LookAhead(NextTokenIsIdentifierOrKeywordOrNumberOnSameLine);
        }

        return false;
    }


    public bool NextTokenIsIdentifierOnSameLine()
    {
        _ = NextToken;

        return !Scanner.HasPrecedingLineBreak && IsIdentifier();
    }


    public YieldExpression ParseYieldExpression()
    {
        var node = new YieldExpression() { Pos = Scanner.StartPos };

        _ = NextToken;
        if (!Scanner.HasPrecedingLineBreak &&
            (Token is TypeScriptSyntaxKind.AsteriskToken || IsStartOfExpression()))
        {
            node.AsteriskToken = ParseOptionalToken<AsteriskToken>(TypeScriptSyntaxKind.AsteriskToken);
            node.Expression = ParseAssignmentExpressionOrHigher();

            return FinishNode(node);
        }
        else
        {

            // if the next token is not on the same line as yield.  or we don't have an '*' or
            // the start of an expression, then this is just a simple "yield" expression.
            return FinishNode(node);
        }
    }


    public ArrowFunction ParseSimpleArrowFunctionExpression(Identifier identifier, NodeArray<Modifier>? asyncModifier = null)
    {


        ArrowFunction node = null;
        if (asyncModifier != null)
        {

            node = new ArrowFunction
            {
                Pos = (int)asyncModifier.Pos,
                Modifiers = asyncModifier
            }; // (ArrowFunction)createNode(TypeScriptSyntaxKind.ArrowFunction, asyncModifier.pos);
        }
        else
        {

            node = new ArrowFunction { Pos = identifier.Pos }; // (ArrowFunction)createNode(TypeScriptSyntaxKind.ArrowFunction, identifier.pos);
        }
        var parameter = new ParameterDeclaration
        {
            Pos = identifier.Pos,
            Name = identifier
        };

        FinishNode(parameter);


        node.Parameters = CreateList<ParameterDeclaration>(); // ([parameter], parameter.pos);
        node.Parameters.Pos = parameter.Pos;
        node.Parameters.Add(parameter);


        node.Parameters.End = parameter.End;


        node.EqualsGreaterThanToken = (EqualsGreaterThanToken)ParseExpectedToken<EqualsGreaterThanToken>(TypeScriptSyntaxKind.EqualsGreaterThanToken, /*reportAtCurrentPosition*/ false, Diagnostics._0_expected, "=>");

        node.Body = ParseArrowFunctionExpressionBody(/*isAsync*/ /*!!*/asyncModifier?.Any() == true);


        return AddJsDocComment(FinishNode(node));
    }


    public ArrowFunction? TryParseParenthesizedArrowFunctionExpression()
    {
        var triState = IsParenthesizedArrowFunctionExpression();
        if (triState == Tristate.False)
        {

            // It's definitely not a parenthesized arrow function expression.
            return null;
        }
        var arrowFunction = triState == Tristate.True
                        ? ParseParenthesizedArrowFunctionExpressionHead(/*allowAmbiguity*/ true)
                        : TryParse(ParsePossibleParenthesizedArrowFunctionExpressionHead);
        if (arrowFunction is null)
        {

            // Didn't appear to actually be a parenthesized arrow function.  Just bail out.
            return null;
        }
        var isAsync = /*!!*/(GetModifierFlags(arrowFunction) & ModifierFlags.Async) != 0;
        var lastToken = Token;

        arrowFunction.EqualsGreaterThanToken = (EqualsGreaterThanToken)ParseExpectedToken<EqualsGreaterThanToken>(TypeScriptSyntaxKind.EqualsGreaterThanToken, /*reportAtCurrentPosition*/false, Diagnostics._0_expected, "=>");

        arrowFunction.Body = lastToken is TypeScriptSyntaxKind.EqualsGreaterThanToken || lastToken is TypeScriptSyntaxKind.OpenBraceToken
            ? ParseArrowFunctionExpressionBody(isAsync)
            : ParseIdentifier();


        return AddJsDocComment(FinishNode(arrowFunction));
    }


    public Tristate IsParenthesizedArrowFunctionExpression()
    {
        if (Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.LessThanToken || Token is TypeScriptSyntaxKind.AsyncKeyword)
        {

            return LookAhead(IsParenthesizedArrowFunctionExpressionWorker);
        }
        if (Token is TypeScriptSyntaxKind.EqualsGreaterThanToken)
        {

            // ERROR RECOVERY TWEAK:
            // If we see a standalone => try to parse it as an arrow function expression as that's
            // likely what the user intended to write.
            return Tristate.True;
        }

        // Definitely not a parenthesized arrow function.
        return Tristate.False;
    }


    public Tristate IsParenthesizedArrowFunctionExpressionWorker()
    {
        if (Token is TypeScriptSyntaxKind.AsyncKeyword)
        {

            _ = NextToken;
            if (Scanner.HasPrecedingLineBreak)
            {

                return Tristate.False;
            }
            if (Token is not TypeScriptSyntaxKind.OpenParenToken && Token is not TypeScriptSyntaxKind.LessThanToken)
            {

                return Tristate.False;
            }
        }
        var first = Token;
        var second = NextToken;
        if (first == TypeScriptSyntaxKind.OpenParenToken)
        {
            if (second == TypeScriptSyntaxKind.CloseParenToken)
            {
                var third = NextToken;
                return third switch
                {
                    TypeScriptSyntaxKind.EqualsGreaterThanToken or TypeScriptSyntaxKind.ColonToken or TypeScriptSyntaxKind.OpenBraceToken => Tristate.True,
                    _ => Tristate.False,
                };
            }
            if (second == TypeScriptSyntaxKind.OpenBracketToken || second == TypeScriptSyntaxKind.OpenBraceToken)
            {

                return Tristate.Unknown;
            }
            if (second == TypeScriptSyntaxKind.DotDotDotToken)
            {

                return Tristate.True;
            }
            if (!IsIdentifier())
            {

                return Tristate.False;
            }
            if (NextToken is TypeScriptSyntaxKind.ColonToken)
            {

                return Tristate.True;
            }


            // This *could* be a parenthesized arrow function.
            // Return Unknown to let the caller know.
            return Tristate.Unknown;
        }
        else
        {

            if (!IsIdentifier())
            {

                return Tristate.False;
            }
            if (SourceFile.LanguageVariant == LanguageVariant.Jsx)
            {
                var isArrowFunctionInJsx = LookAhead(() =>
                {
                    var third = NextToken;
                    if (third == TypeScriptSyntaxKind.ExtendsKeyword)
                    {
                        var fourth = NextToken;
                        return fourth switch
                        {
                            TypeScriptSyntaxKind.EqualsToken or TypeScriptSyntaxKind.GreaterThanToken => false,
                            _ => true,
                        };
                    }
                    else if (third == TypeScriptSyntaxKind.CommaToken)
                    {
                        return true;
                    }
                    return false;
                });
                return isArrowFunctionInJsx ? Tristate.True : Tristate.False;
            }


            // This *could* be a parenthesized arrow function.
            return Tristate.Unknown;
        }
    }


    public ArrowFunction ParsePossibleParenthesizedArrowFunctionExpressionHead() => ParseParenthesizedArrowFunctionExpressionHead(/*allowAmbiguity*/ false);


    public ArrowFunction? TryParseAsyncSimpleArrowFunctionExpression()
    {
        if (Token is TypeScriptSyntaxKind.AsyncKeyword)
        {
            var isUnParenthesizedAsyncArrowFunction = LookAhead(IsUnParenthesizedAsyncArrowFunctionWorker);
            if (isUnParenthesizedAsyncArrowFunction == Tristate.True)
            {
                var asyncModifier = ParseModifiersForArrowFunction();
                var expr = ParseBinaryExpressionOrHigher(/*precedence*/ 0);

                return ParseSimpleArrowFunctionExpression((Identifier)expr, asyncModifier);
            }
        }

        return null;
    }


    public Tristate IsUnParenthesizedAsyncArrowFunctionWorker()
    {
        if (Token is TypeScriptSyntaxKind.AsyncKeyword)
        {

            _ = NextToken;
            if (Scanner.HasPrecedingLineBreak || Token is TypeScriptSyntaxKind.EqualsGreaterThanToken)
            {

                return Tristate.False;
            }
            var expr = ParseBinaryExpressionOrHigher(/*precedence*/ 0);
            if (!Scanner.HasPrecedingLineBreak && expr.Kind is TypeScriptSyntaxKind.Identifier && Token is TypeScriptSyntaxKind.EqualsGreaterThanToken)
            {

                return Tristate.True;
            }
        }


        return Tristate.False;
    }


    public ArrowFunction? ParseParenthesizedArrowFunctionExpressionHead(bool allowAmbiguity)
    {
        var node = new ArrowFunction
        {
            Pos = Scanner.StartPos,
            Modifiers = ParseModifiersForArrowFunction()
        };
        var isAsync = /*!!*/(GetModifierFlags(node) & ModifierFlags.Async) != 0;


        // Arrow functions are never generators.
        //
        // If we're speculatively parsing a signature for a parenthesized arrow function, then
        // we have to have a complete parameter list.  Otherwise we might see something like
        // a => (b => c)
        // And think that "(b =>" was actually a parenthesized arrow function with a missing
        // close paren.
        FillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ false, /*awaitContext*/ isAsync, /*requireCompleteParameterList*/ !allowAmbiguity, node);
        if (node.Parameters is null)
        {

            return null;
        }
        if (!allowAmbiguity && Token is not TypeScriptSyntaxKind.EqualsGreaterThanToken && Token is not TypeScriptSyntaxKind.OpenBraceToken)
        {

            // Returning null here will cause our caller to rewind to where we started from.
            return null;
        }


        return node;
    }


    public /*Block | Expression*/IBlockOrExpression ParseArrowFunctionExpressionBody(bool isAsync)
    {
        if (Token is TypeScriptSyntaxKind.OpenBraceToken)
        {

            return ParseFunctionBlock(/*allowYield*/ false, /*allowAwait*/ isAsync, /*ignoreMissingOpenBrace*/ false);
        }
        if (Token is not TypeScriptSyntaxKind.SemicolonToken &&
                        Token is not TypeScriptSyntaxKind.FunctionKeyword &&
                        Token is not TypeScriptSyntaxKind.ClassKeyword &&
                        IsStartOfStatement() &&
                        !IsStartOfExpressionStatement())
        {

            // Check if we got a plain statement (i.e. no expression-statements, no function/class expressions/declarations)
            //
            // Here we try to recover from a potential error situation in the case where the
            // user meant to supply a block. For example, if the user wrote:
            //
            //  a =>
            //      let v = 0;
            //  }
            //
            // they may be missing an open brace.  Check to see if that's the case so we can
            // try to recover better.  If we don't do this, then the next close curly we see may end
            // up preemptively closing the containing construct.
            //
            // Note: even when 'ignoreMissingOpenBrace' is passed as true, parseBody will still error.
            return ParseFunctionBlock(/*allowYield*/ false, /*allowAwait*/ isAsync, /*ignoreMissingOpenBrace*/ true);
        }


        return isAsync
            ? DoInAwaitContext(ParseAssignmentExpressionOrHigher)
            : DoOutsideOfAwaitContext(ParseAssignmentExpressionOrHigher);
    }


    public IExpression ParseConditionalExpressionRest(IExpression leftOperand)
    {
        var questionToken = ParseOptionalToken<QuestionToken>(TypeScriptSyntaxKind.QuestionToken);
        if (questionToken is null)
        {

            return leftOperand;
        }
        var node = new ConditionalExpression
        {
            Pos = leftOperand.Pos,
            Condition = leftOperand,

            QuestionToken = questionToken,

            WhenTrue = DoOutsideOfContext(DisallowInAndDecoratorContext, ParseAssignmentExpressionOrHigher),

            ColonToken = (ColonToken)ParseExpectedToken<ColonToken>(TypeScriptSyntaxKind.ColonToken, /*reportAtCurrentPosition*/ false,
            Diagnostics._0_expected, TokenToString(TypeScriptSyntaxKind.ColonToken)),

            WhenFalse = ParseAssignmentExpressionOrHigher()
        };

        return FinishNode(node);
    }


    public IExpression ParseBinaryExpressionOrHigher(int precedence)
    {
        var leftOperand = ParseUnaryExpressionOrHigher();

        return leftOperand is null
            ? throw new NullReferenceException()
            : ParseBinaryExpressionRest(precedence, leftOperand);
    }


    public bool IsInOrOfKeyword(TypeScriptSyntaxKind t) => t == TypeScriptSyntaxKind.InKeyword || t == TypeScriptSyntaxKind.OfKeyword;


    public IExpression ParseBinaryExpressionRest(int precedence, IExpression leftOperand)
    {
        while (true)
        {

            // We either have a binary operator here, or we're finished.  We call
            // reScanGreaterToken so that we merge token sequences like > and = into >=

            ReScanGreaterToken;
            var newPrecedence = GetBinaryOperatorPrecedence();
            var consumeCurrentOperator = Token is TypeScriptSyntaxKind.AsteriskAsteriskToken ?
                                newPrecedence >= precedence :
                                newPrecedence > precedence;
            if (!consumeCurrentOperator)
            {

                break;
            }
            if (Token is TypeScriptSyntaxKind.InKeyword && InDisallowInContext())
            {

                break;
            }
            if (Token is TypeScriptSyntaxKind.AsKeyword)
            {
                if (Scanner.HasPrecedingLineBreak)
                {

                    break;
                }
                else
                {

                    _ = NextToken;

                    leftOperand = MakeAsExpression(leftOperand, ParseType());
                }
            }
            else
            {

                leftOperand = MakeBinaryExpression(leftOperand, ParseTokenNode</*BinaryOperator*/Token>(Token), ParseBinaryExpressionOrHigher(newPrecedence));
            }
        }


        return leftOperand;
    }


    public bool IsBinaryOperator() => InDisallowInContext() && Token is TypeScriptSyntaxKind.InKeyword ? false : GetBinaryOperatorPrecedence() > 0;


    public int GetBinaryOperatorPrecedence() => Token switch
    {
        TypeScriptSyntaxKind.BarBarToken => 1,
        TypeScriptSyntaxKind.AmpersandAmpersandToken => 2,
        TypeScriptSyntaxKind.BarToken => 3,
        TypeScriptSyntaxKind.CaretToken => 4,
        TypeScriptSyntaxKind.AmpersandToken => 5,
        TypeScriptSyntaxKind.EqualsEqualsToken or TypeScriptSyntaxKind.ExclamationEqualsToken or TypeScriptSyntaxKind.EqualsEqualsEqualsToken or TypeScriptSyntaxKind.ExclamationEqualsEqualsToken => 6,
        TypeScriptSyntaxKind.LessThanToken or TypeScriptSyntaxKind.GreaterThanToken or TypeScriptSyntaxKind.LessThanEqualsToken or TypeScriptSyntaxKind.GreaterThanEqualsToken or TypeScriptSyntaxKind.InstanceOfKeyword or TypeScriptSyntaxKind.InKeyword or TypeScriptSyntaxKind.AsKeyword => 7,
        TypeScriptSyntaxKind.LessThanLessThanToken or TypeScriptSyntaxKind.GreaterThanGreaterThanToken or TypeScriptSyntaxKind.GreaterThanGreaterThanGreaterThanToken => 8,
        TypeScriptSyntaxKind.PlusToken or TypeScriptSyntaxKind.MinusToken => 9,
        TypeScriptSyntaxKind.AsteriskToken or TypeScriptSyntaxKind.SlashToken or TypeScriptSyntaxKind.PercentToken => 10,
        TypeScriptSyntaxKind.AsteriskAsteriskToken => 11,
        // -1 is lower than all other precedences.  Returning it will cause binary expression
        // parsing to stop.
        _ => -1,
    };


    public BinaryExpression MakeBinaryExpression(IExpression left, /*BinaryOperator*/Token operatorToken, IExpression right)
    {
        var node = new BinaryExpression
        {
            Pos = left.Pos,
            Left = left,

            OperatorToken = operatorToken,

            Right = right
        };

        return FinishNode(node);
    }


    public AsExpression MakeAsExpression(IExpression left, ITypeNode right)
    {
        var node = new AsExpression
        {
            Pos = left.Pos,
            Expression = left,

            Type = right
        };

        return FinishNode(node);
    }


    public PrefixUnaryExpression ParsePrefixUnaryExpression()
    {
        var node = new PrefixUnaryExpression
        {
            Pos = Scanner.StartPos,
            Operator = /*(PrefixUnaryOperator)*/Token
        };

        _ = NextToken;

        node.Operand = ParseSimpleUnaryExpression();


        return FinishNode(node);
    }


    public DeleteExpression ParseDeleteExpression()
    {
        var node = new DeleteExpression() { Pos = Scanner.StartPos };

        _ = NextToken;

        node.Expression = ParseSimpleUnaryExpression(); // as UnaryExpression;

        return FinishNode(node);
    }


    public TypeOfExpression ParseTypeOfExpression()
    {
        var node = new TypeOfExpression() { Pos = Scanner.StartPos };

        _ = NextToken;

        node.Expression = ParseSimpleUnaryExpression(); //  as UnaryExpression;

        return FinishNode(node);
    }


    public VoidExpression ParseVoidExpression()
    {
        var node = new VoidExpression() { Pos = Scanner.StartPos };

        _ = NextToken;

        node.Expression = ParseSimpleUnaryExpression(); //  as UnaryExpression;

        return FinishNode(node);
    }


    public bool IsAwaitExpression()
    {
        if (Token is TypeScriptSyntaxKind.AwaitKeyword)
        {
            if (InAwaitContext())
            {

                return true;
            }


            // here we are using similar heuristics as 'isYieldExpression'
            return LookAhead(NextTokenIsIdentifierOnSameLine);
        }


        return false;
    }


    public AwaitExpression ParseAwaitExpression()
    {
        var node = new AwaitExpression() { Pos = Scanner.StartPos };

        _ = NextToken;

        node.Expression = ParseSimpleUnaryExpression(); // as UnaryExpression;

        return FinishNode(node);
    }

    //UnaryExpression | BinaryExpression
    public IExpression ParseUnaryExpressionOrHigher()
    {
        if (IsUpdateExpression())
        {
            var incrementExpression = ParseIncrementExpression();

            return Token is TypeScriptSyntaxKind.AsteriskAsteriskToken ?
                ParseBinaryExpressionRest(GetBinaryOperatorPrecedence(), incrementExpression) :
                incrementExpression;
        }
        var unaryOperator = Token;
        var simpleUnaryExpression = ParseSimpleUnaryExpression();
        if (Token is TypeScriptSyntaxKind.AsteriskAsteriskToken)
        {
            var start = SkipTriviaM(SourceText, simpleUnaryExpression.Pos ?? 0);
            if (simpleUnaryExpression.Kind is TypeScriptSyntaxKind.TypeAssertionExpression)
            {

                ParseErrorAtPosition(start, (simpleUnaryExpression.End ?? 0) - start, Diagnostics.A_type_assertion_expression_is_not_allowed_in_the_left_hand_side_of_an_exponentiation_expression_Consider_enclosing_the_expression_in_parentheses);
            }
            else
            {

                ParseErrorAtPosition(start, (simpleUnaryExpression.End ?? 0) - start, Diagnostics.An_unary_expression_with_the_0_operator_is_not_allowed_in_the_left_hand_side_of_an_exponentiation_expression_Consider_enclosing_the_expression_in_parentheses, TokenToString(unaryOperator));
            }
        }

        return simpleUnaryExpression;
    }


    public /*Unary*/IExpression? ParseSimpleUnaryExpression()
    {
        switch (Token)
        {
            case TypeScriptSyntaxKind.PlusToken:
            case TypeScriptSyntaxKind.MinusToken:
            case TypeScriptSyntaxKind.TildeToken:
            case TypeScriptSyntaxKind.ExclamationToken:

                return ParsePrefixUnaryExpression();
            case TypeScriptSyntaxKind.DeleteKeyword:

                return ParseDeleteExpression();
            case TypeScriptSyntaxKind.TypeOfKeyword:

                return ParseTypeOfExpression();
            case TypeScriptSyntaxKind.VoidKeyword:

                return ParseVoidExpression();
            case TypeScriptSyntaxKind.LessThanToken:

                // This is modified UnaryExpression grammar in TypeScript
                //  UnaryExpression (modified):
                //      < type > UnaryExpression
                return ParseTypeAssertion();
            case TypeScriptSyntaxKind.AwaitKeyword:
                if (IsAwaitExpression())
                {

                    return ParseAwaitExpression();
                }
                break;
            default:

                return ParseIncrementExpression();
        }
        return null;
    }


    public bool IsUpdateExpression()
    {
        switch (Token)
        {
            case TypeScriptSyntaxKind.PlusToken:
            case TypeScriptSyntaxKind.MinusToken:
            case TypeScriptSyntaxKind.TildeToken:
            case TypeScriptSyntaxKind.ExclamationToken:
            case TypeScriptSyntaxKind.DeleteKeyword:
            case TypeScriptSyntaxKind.TypeOfKeyword:
            case TypeScriptSyntaxKind.VoidKeyword:
            case TypeScriptSyntaxKind.AwaitKeyword:

                return false;
            case TypeScriptSyntaxKind.LessThanToken:
                if (SourceFile.LanguageVariant != LanguageVariant.Jsx)
                {

                    return false;
                }
                break;
            default:

                return true;
        }
        return true;
    }


    public /*Increment*/IExpression ParseIncrementExpression()
    {
        if (Token is TypeScriptSyntaxKind.PlusPlusToken || Token is TypeScriptSyntaxKind.MinusMinusToken)
        {
            var node = new PrefixUnaryExpression
            {
                Pos = Scanner.StartPos,
                Operator = /*(PrefixUnaryOperator)*/Token
            };

            _ = NextToken;

            node.Operand = ParseLeftHandSideExpressionOrHigher();

            return FinishNode(node);
        }
        else
        if (SourceFile.LanguageVariant == LanguageVariant.Jsx && Token is TypeScriptSyntaxKind.LessThanToken && LookAhead(NextTokenIsIdentifierOrKeyword))
        {

            // JSXElement is part of primaryExpression
            return ParseJsxElementOrSelfClosingElement(/*inExpressionContext*/ true);
        }
        var expression = ParseLeftHandSideExpressionOrHigher();


        //Debug.assert(isLeftHandSideExpression(expression));
        if ((Token is TypeScriptSyntaxKind.PlusPlusToken || Token is TypeScriptSyntaxKind.MinusMinusToken) && !Scanner.HasPrecedingLineBreak)
        {
            var node = new PostfixUnaryExpression
            {
                Pos = expression.Pos,
                Operand = expression,

                Operator = /*(PostfixUnaryOperator)*/Token
            };

            _ = NextToken;

            return FinishNode(node);
        }


        return expression;
    }


    public /*LeftHandSideExpression*/IExpression ParseLeftHandSideExpressionOrHigher()
    {
        var expression = Token is TypeScriptSyntaxKind.SuperKeyword
                        ? ParseSuperExpression()
                        : ParseMemberExpressionOrHigher();


        // Now, we *may* be complete.  However, we might have consumed the start of a
        // CallExpression.  As such, we need to consume the rest of it here to be complete.
        return ParseCallExpressionRest(expression);
    }


    public IMemberExpression ParseMemberExpressionOrHigher()
    {
        var expression = ParsePrimaryExpression();

        return ParseMemberExpressionRest(expression);
    }


    public IMemberExpression ParseSuperExpression()
    {
        var expression = ParseTokenNode<PrimaryExpression>(Token);
        if (Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.DotToken || Token is TypeScriptSyntaxKind.OpenBracketToken)
        {

            return expression;
        }
        var node = new PropertyAccessExpression
        {
            Pos = expression.Pos,
            Expression = expression
        };

        ParseExpectedToken<DotToken>(TypeScriptSyntaxKind.DotToken, /*reportAtCurrentPosition*/ false, Diagnostics.super_must_be_followed_by_an_argument_list_or_member_access);

        node.Name = ParseRightSideOfDot(/*allowIdentifierNames*/ true);

        return FinishNode(node);
    }


    public bool TagNamesAreEquivalent(IJsxTagNameExpression lhs, IJsxTagNameExpression rhs)
    {
        if (lhs.Kind != rhs.Kind)
        {

            return false;
        }
        if (lhs.Kind is TypeScriptSyntaxKind.Identifier)
        {

            return (lhs as Identifier).Text == (rhs as Identifier).Text;
        }
        if (lhs.Kind is TypeScriptSyntaxKind.ThisKeyword)
        {

            return true;
        }


        // If we are at this statement then we must have PropertyAccessExpression and because tag name in Jsx element can only
        // take forms of JsxTagNameExpression which includes an identifier, "this" expression, or another propertyAccessExpression
        // it is safe to case the expression property as such. See parseJsxElementName for how we parse tag name in Jsx element
        return true;
        //todo
        //((PropertyAccessExpression)lhs).name.text == ((PropertyAccessExpression)rhs).name.text &&
        //tagNamesAreEquivalent(((PropertyAccessExpression)lhs).expression as JsxTagNameExpression, ((PropertyAccessExpression)rhs).expression as JsxTagNameExpression);
    }


    public /*JsxElement | JsxSelfClosingElement*/PrimaryExpression ParseJsxElementOrSelfClosingElement(bool inExpressionContext)
    {
        var opening = ParseJsxOpeningOrSelfClosingElement(inExpressionContext);
        //var result = JsxElement | JsxSelfClosingElement;
        if (opening.Kind is TypeScriptSyntaxKind.JsxOpeningElement)
        {
            var node = new JsxElement
            {
                Pos = opening.Pos,
                OpeningElement = opening
            };

            var tn = (opening as JsxOpeningElement)?.TagName;
            tn ??= (opening as JsxSelfClosingElement)?.TagName;

            node.JsxChildren = ParseJsxChildren(tn); // IJsxTagNameExpression);

            node.ClosingElement = ParseJsxClosingElement(inExpressionContext);
            // todo check     node.closingElement.tagName as JsxTagNameExpression
            if (!TagNamesAreEquivalent(tn, node.ClosingElement.TagName))
            {

                ParseErrorAtPosition(node.ClosingElement.Pos ?? 0, (node.ClosingElement.End ?? 0) - (node.ClosingElement.Pos ?? 0), Diagnostics.Expected_corresponding_JSX_closing_tag_for_0, GetTextOfNodeFromSourceText(SourceText, tn));
            }


            var result = FinishNode(node);

            if (inExpressionContext && Token is TypeScriptSyntaxKind.LessThanToken)
            {
                var invalidElement = TryParse(() => ParseJsxElementOrSelfClosingElement(/*inExpressionContext*/true));
                if (invalidElement != null)
                {

                    ParseErrorAtCurrentToken(Diagnostics.JSX_expressions_must_have_one_parent_element);
                    var badNode = new BinaryExpression
                    {
                        Pos = result.Pos,
                        End = invalidElement.End,

                        Left = result,

                        Right = invalidElement,

                        OperatorToken = (/*BinaryOperator*/Token)CreateMissingNode<Token>(TypeScriptSyntaxKind.CommaToken, /*reportAtCurrentPosition*/ false, /*diagnosticMessage*/ null)
                    };

                    badNode.OperatorToken.Pos = badNode.OperatorToken.End = badNode.Right.Pos;

                    return (JsxElement)(Node)badNode;
                }
            }



            return result;
        }
        else
        {

            // Nothing else to do for self-closing elements
            var result = (JsxSelfClosingElement)opening;
            if (inExpressionContext && Token is TypeScriptSyntaxKind.LessThanToken)
            {
                var invalidElement = TryParse(() => ParseJsxElementOrSelfClosingElement(/*inExpressionContext*/true));
                if (invalidElement != null)
                {

                    ParseErrorAtCurrentToken(Diagnostics.JSX_expressions_must_have_one_parent_element);
                    var badNode = new BinaryExpression
                    {
                        Pos = result.Pos,
                        End = invalidElement.End,

                        Left = result,

                        Right = invalidElement,

                        OperatorToken = (/*BinaryOperator*/Token)CreateMissingNode<Token>(TypeScriptSyntaxKind.CommaToken, /*reportAtCurrentPosition*/ false, /*diagnosticMessage*/ null)
                    };

                    badNode.OperatorToken.Pos = badNode.OperatorToken.End = badNode.Right.Pos;

                    return (JsxElement)(Node)badNode;
                }
            }

            return result;
        }

    }


    public JsxText ParseJsxText()
    {
        var node = new JsxText() { Pos = Scanner.StartPos };

        CurrentToken = Scanner.ScanJsxToken();

        return FinishNode(node);
    }


    public /*JsxChild*/Node? ParseJsxChild() => Token switch
    {
        TypeScriptSyntaxKind.JsxText => ParseJsxText(),
        TypeScriptSyntaxKind.OpenBraceToken => ParseJsxExpression(/*inExpressionContext*/ false),
        TypeScriptSyntaxKind.LessThanToken => ParseJsxElementOrSelfClosingElement(/*inExpressionContext*/ false),
        _ => null,
    };


    public NodeArray<IJsxChild> ParseJsxChildren(/*LeftHandSide*/IExpression openingTagName)
    {
        var result = CreateList<IJsxChild>(); //List<IJsxChild>(); // 
        var saveParsingContext = ParsingContext;

        ParsingContext |= 1 << (int)ParsingContext.JsxChildren;
        while (true)
        {

            CurrentToken = Scanner.ReScanJsxToken();
            if (Token is TypeScriptSyntaxKind.LessThanSlashToken)
            {

                // Closing tag
                break;
            }
            else
            if (Token is TypeScriptSyntaxKind.EndOfFileToken)
            {

                // If we hit EOF, issue the error at the tag that lacks the closing element
                // rather than at the end of the file (which is useless)
                ParseErrorAtPosition(openingTagName.Pos ?? 0, (openingTagName.End ?? 0) - (openingTagName.Pos ?? 0), Diagnostics.JSX_element_0_has_no_corresponding_closing_tag, GetTextOfNodeFromSourceText(SourceText, openingTagName));

                break;
            }
            else
            if (Token is TypeScriptSyntaxKind.ConflictMarkerTrivia)
            {

                break;
            }

            result.Add(ParseJsxChild() as IJsxChild);
        }


        result.End = Scanner.TokenPos;


        ParsingContext = saveParsingContext;


        return result;
    }


    public JsxAttributes ParseJsxAttributes()
    {
        var jsxAttributes = new JsxAttributes
        {
            Pos = Scanner.StartPos,
            Properties = ParseList(ParsingContext.JsxAttributes, ParseJsxAttribute)
        };

        return FinishNode(jsxAttributes);
    }

    //JsxOpeningElement | JsxSelfClosingElement
    public Expression ParseJsxOpeningOrSelfClosingElement(bool inExpressionContext)
    {
        var fullStart = Scanner.StartPos;


        ParseExpected(TypeScriptSyntaxKind.LessThanToken);
        var tagName = ParseJsxElementName();
        var attributes = ParseJsxAttributes();
        //JsxOpeningLikeElement node = null;
        if (Token is TypeScriptSyntaxKind.GreaterThanToken)
        {

            // Closing tag, so scan the immediately-following text with the JSX scanning instead
            // of regular scanning to avoid treating illegal characters (e.g. '#') as immediate
            // scanning errors
            var node = new JsxOpeningElement
            {
                Pos = fullStart,
                TagName = tagName,

                Attributes = attributes
            }; //(JsxOpeningElement)createNode(TypeScriptSyntaxKind.JsxOpeningElement, fullStart);

            ScanJsxText;
            return FinishNode(node);
        }
        else
        {

            ParseExpected(TypeScriptSyntaxKind.SlashToken);
            if (inExpressionContext)
            {

                ParseExpected(TypeScriptSyntaxKind.GreaterThanToken);
            }
            else
            {

                ParseExpected(TypeScriptSyntaxKind.GreaterThanToken, /*diagnostic*/ null, /*shouldAdvance*/ false);

                ScanJsxText;
            }

            var node = new JsxSelfClosingElement
            {
                Pos = fullStart,
                TagName = tagName,

                Attributes = attributes
            }; //(JsxSelfClosingElement)createNode(TypeScriptSyntaxKind.JsxSelfClosingElement, fullStart);
            return FinishNode(node);
        }


        //node.tagName = tagName;

        //node.attributes = attributes;


        //return finishNode(node);
    }


    public IJsxTagNameExpression ParseJsxElementName()
    {

        ScanJsxIdentifier;
        IJsxTagNameExpression expression = Token is TypeScriptSyntaxKind.ThisKeyword ?
                        ParseTokenNode<PrimaryExpression>(Token) : ParseIdentifierName();
        if (Token is TypeScriptSyntaxKind.ThisKeyword)
        {
            IJsxTagNameExpression expression2 = ParseTokenNode<PrimaryExpression>(Token);
            while (ParseOptional(TypeScriptSyntaxKind.DotToken))
            {
                PropertyAccessExpression propertyAccess = new()
                {
                    Pos = expression2.Pos,
                    Expression = expression2,

                    Name = ParseRightSideOfDot(/*allowIdentifierNames*/ true)
                }; //(PropertyAccessExpression)createNode(TypeScriptSyntaxKind.PropertyAccessExpression, expression.pos);

                expression2 = FinishNode(propertyAccess);
            }

            return expression2;
        }
        else
        {
            IJsxTagNameExpression expression2 = ParseIdentifierName();
            while (ParseOptional(TypeScriptSyntaxKind.DotToken))
            {
                PropertyAccessExpression propertyAccess = new()
                {
                    Pos = expression2.Pos,
                    Expression = expression2,

                    Name = ParseRightSideOfDot(/*allowIdentifierNames*/ true)
                }; //(PropertyAccessExpression)createNode(TypeScriptSyntaxKind.PropertyAccessExpression, expression.pos);

                expression2 = FinishNode(propertyAccess);
            }

            return expression2;
        }
    }


    public JsxExpression ParseJsxExpression(bool inExpressionContext)
    {
        var node = new JsxExpression() { Pos = Scanner.StartPos };


        ParseExpected(TypeScriptSyntaxKind.OpenBraceToken);
        if (Token is not TypeScriptSyntaxKind.CloseBraceToken)
        {

            node.DotDotDotToken = ParseOptionalToken<DotDotDotToken>(TypeScriptSyntaxKind.DotDotDotToken);

            node.Expression = ParseAssignmentExpressionOrHigher();
        }
        if (inExpressionContext)
        {

            ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);
        }
        else
        {

            ParseExpected(TypeScriptSyntaxKind.CloseBraceToken, /*message*/ null, /*shouldAdvance*/ false);

            ScanJsxText;
        }


        return FinishNode(node);
    }

    //JsxAttribute | JsxSpreadAttribute
    public ObjectLiteralElement ParseJsxAttribute()
    {
        if (Token is TypeScriptSyntaxKind.OpenBraceToken)
        {

            return ParseJsxSpreadAttribute();
        }


        ScanJsxIdentifier;
        var node = new JsxAttribute
        {
            Pos = Scanner.StartPos,
            Name = ParseIdentifierName()
        };
        if (Token is TypeScriptSyntaxKind.EqualsToken)
        {
            node.Initializer = ScanJsxAttributeValue switch
            {
                TypeScriptSyntaxKind.StringLiteral => (StringLiteral)ParseLiteralNode(),
                _ => ParseJsxExpression(/*inExpressionContext*/ true),
            };
        }

        return FinishNode(node);
    }


    public JsxSpreadAttribute ParseJsxSpreadAttribute()
    {
        var node = new JsxSpreadAttribute() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenBraceToken);

        ParseExpected(TypeScriptSyntaxKind.DotDotDotToken);

        node.Expression = ParseExpression();

        ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);

        return FinishNode(node);
    }


    public JsxClosingElement ParseJsxClosingElement(bool inExpressionContext)
    {
        var node = new JsxClosingElement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.LessThanSlashToken);

        node.TagName = ParseJsxElementName();
        if (inExpressionContext)
        {

            ParseExpected(TypeScriptSyntaxKind.GreaterThanToken);
        }
        else
        {

            ParseExpected(TypeScriptSyntaxKind.GreaterThanToken, /*diagnostic*/ null, /*shouldAdvance*/ false);

            ScanJsxText;
        }

        return FinishNode(node);
    }


    public TypeAssertion ParseTypeAssertion()
    {
        var node = new TypeAssertion() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.LessThanToken);

        node.Type = ParseType();

        ParseExpected(TypeScriptSyntaxKind.GreaterThanToken);

        node.Expression = ParseSimpleUnaryExpression(); // as UnaryExpression;

        return FinishNode(node);
    }


    public IMemberExpression ParseMemberExpressionRest(/*LeftHandSideExpression*/IMemberExpression expression)
    {
        while (true)
        {
            var dotToken = ParseOptionalToken<DotToken>(TypeScriptSyntaxKind.DotToken);
            if (dotToken is not null)
            {
                var propertyAccess = new PropertyAccessExpression
                {
                    Pos = expression.Pos,
                    Expression = expression,

                    Name = ParseRightSideOfDot(/*allowIdentifierNames*/ true)
                };

                expression = FinishNode(propertyAccess);

                continue;
            }
            if (Token is TypeScriptSyntaxKind.ExclamationToken && !Scanner.HasPrecedingLineBreak)
            {

                _ = NextToken;
                var nonNullExpression = new NonNullExpression
                {
                    Pos = expression.Pos,
                    Expression = expression
                };

                expression = FinishNode(nonNullExpression);

                continue;
            }
            if (!InDecoratorContext() && ParseOptional(TypeScriptSyntaxKind.OpenBracketToken))
            {
                var indexedAccess = new ElementAccessExpression
                {
                    Pos = expression.Pos,
                    Expression = expression
                };
                if (Token is not TypeScriptSyntaxKind.CloseBracketToken)
                {

                    indexedAccess.ArgumentExpression = AllowInAnd(ParseExpression);
                    if (indexedAccess.ArgumentExpression.Kind is TypeScriptSyntaxKind.StringLiteral ||
                        indexedAccess.ArgumentExpression.Kind is TypeScriptSyntaxKind.NumericLiteral)
                    {
                        var literal = (LiteralExpression)indexedAccess.ArgumentExpression;//(LiteralExpression)

                        literal.Text = InternIdentifier(literal.Text);
                    }
                }


                ParseExpected(TypeScriptSyntaxKind.CloseBracketToken);

                expression = FinishNode(indexedAccess);

                continue;
            }
            if (Token is TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral || Token is TypeScriptSyntaxKind.TemplateHead)
            {
                var tagExpression = new TaggedTemplateExpression
                {
                    Pos = expression.Pos,
                    Tag = expression,

                    Template = Token is TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral
                    ? (Node)/*(NoSubstitutionTemplateLiteral)*/ParseLiteralNode()
                    : ParseTemplateExpression()
                };

                expression = FinishNode(tagExpression);

                continue;
            }


            return expression;
        }
    }


    public /*LeftHandSideExpression*/IMemberExpression ParseCallExpressionRest(/*LeftHandSideExpression*/IMemberExpression expression)
    {
        while (true)
        {

            expression = ParseMemberExpressionRest(expression);
            if (Token is TypeScriptSyntaxKind.LessThanToken)
            {
                var typeArguments = TryParse(ParseTypeArgumentsInExpression);
                if (typeArguments is null)
                {

                    return expression;
                }
                var callExpr = new CallExpression
                {
                    Pos = expression.Pos,
                    Expression = expression,

                    TypeArguments = typeArguments,

                    Arguments = ParseArgumentList()
                };

                expression = FinishNode(callExpr);

                continue;
            }
            else
            if (Token is TypeScriptSyntaxKind.OpenParenToken)
            {
                var callExpr = new CallExpression
                {
                    Pos = expression.Pos,
                    Expression = expression,

                    Arguments = ParseArgumentList()
                };

                expression = FinishNode(callExpr);

                continue;
            }


            return expression;
        }
    }


    public NodeArray<IExpression> ParseArgumentList()
    {

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);
        var result = ParseDelimitedList(ParsingContext.ArgumentExpressions, ParseArgumentExpression);

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

        return result;
    }


    public NodeArray<ITypeNode>? ParseTypeArgumentsInExpression()
    {
        if (!ParseOptional(TypeScriptSyntaxKind.LessThanToken))
        {

            return null;
        }
        var typeArguments = ParseDelimitedList(ParsingContext.TypeArguments, ParseType);
        if (!ParseExpected(TypeScriptSyntaxKind.GreaterThanToken))
        {

            // If it doesn't have the closing >  then it's definitely not an type argument list.
            return null;
        }


        // If we have a '<', then only parse this as a argument list if the type arguments
        // are complete and we have an open paren.  if we don't, rewind and return nothing.
        return typeArguments != null && CanFollowTypeArgumentsInExpression()
            ? typeArguments
            : null;
    }


    public bool CanFollowTypeArgumentsInExpression() => Token switch
    {
        TypeScriptSyntaxKind.OpenParenToken or TypeScriptSyntaxKind.DotToken or TypeScriptSyntaxKind.CloseParenToken or TypeScriptSyntaxKind.CloseBracketToken or TypeScriptSyntaxKind.ColonToken or TypeScriptSyntaxKind.SemicolonToken or TypeScriptSyntaxKind.QuestionToken or TypeScriptSyntaxKind.EqualsEqualsToken or TypeScriptSyntaxKind.EqualsEqualsEqualsToken or TypeScriptSyntaxKind.ExclamationEqualsToken or TypeScriptSyntaxKind.ExclamationEqualsEqualsToken or TypeScriptSyntaxKind.AmpersandAmpersandToken or TypeScriptSyntaxKind.BarBarToken or TypeScriptSyntaxKind.CaretToken or TypeScriptSyntaxKind.AmpersandToken or TypeScriptSyntaxKind.BarToken or TypeScriptSyntaxKind.CloseBraceToken or TypeScriptSyntaxKind.EndOfFileToken => true,// foo<x>
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               // these cases can't legally follow a type arg list.  However, they're not legal
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               // expressions either.  The user is probably in the middle of a generic type. So
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               // treat it as such.
        _ => false,// Anything else treat as an expression.
    };


    public IPrimaryExpression ParsePrimaryExpression()
    {
        switch (Token)
        {
            case TypeScriptSyntaxKind.NumericLiteral:
            case TypeScriptSyntaxKind.StringLiteral:
            case TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral:

                return ParseLiteralNode();
            case TypeScriptSyntaxKind.ThisKeyword:
            case TypeScriptSyntaxKind.SuperKeyword:
            case TypeScriptSyntaxKind.NullKeyword:
            case TypeScriptSyntaxKind.TrueKeyword:
            case TypeScriptSyntaxKind.FalseKeyword:

                return ParseTokenNode<PrimaryExpression>(Token);
            case TypeScriptSyntaxKind.OpenParenToken:

                return ParseParenthesizedExpression();
            case TypeScriptSyntaxKind.OpenBracketToken:

                return ParseArrayLiteralExpression();
            case TypeScriptSyntaxKind.OpenBraceToken:

                return ParseObjectLiteralExpression();
            case TypeScriptSyntaxKind.AsyncKeyword:
                if (!LookAhead(NextTokenIsFunctionKeywordOnSameLine))
                {

                    break;
                }


                return ParseFunctionExpression();
            case TypeScriptSyntaxKind.ClassKeyword:

                return ParseClassExpression();
            case TypeScriptSyntaxKind.FunctionKeyword:

                return ParseFunctionExpression();
            case TypeScriptSyntaxKind.NewKeyword:

                return ParseNewExpression();
            case TypeScriptSyntaxKind.SlashToken:
            case TypeScriptSyntaxKind.SlashEqualsToken:
                if (ReScanSlashToken is TypeScriptSyntaxKind.RegularExpressionLiteral)
                {

                    return ParseLiteralNode();
                }

                break;
            case TypeScriptSyntaxKind.TemplateHead:

                return ParseTemplateExpression();
        }


        return ParseIdentifier(Diagnostics.Expression_expected);
    }


    public ParenthesizedExpression ParseParenthesizedExpression()
    {
        var node = new ParenthesizedExpression() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);

        node.Expression = AllowInAnd(ParseExpression);

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

        return FinishNode(node);
    }


    public Expression ParseSpreadElement()
    {
        var node = new SpreadElement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.DotDotDotToken);

        node.Expression = ParseAssignmentExpressionOrHigher();

        return FinishNode(node);
    }


    public IExpression ParseArgumentOrArrayLiteralElement() => Token is TypeScriptSyntaxKind.DotDotDotToken ? ParseSpreadElement() :
            Token is TypeScriptSyntaxKind.CommaToken ? new OmittedExpression() { Pos = Scanner.StartPos } /*createNode(TypeScriptSyntaxKind.OmittedExpression)*/ :
                ParseAssignmentExpressionOrHigher();


    public IExpression ParseArgumentExpression() => DoOutsideOfContext(DisallowInAndDecoratorContext, ParseArgumentOrArrayLiteralElement);


    public ArrayLiteralExpression ParseArrayLiteralExpression()
    {
        var node = new ArrayLiteralExpression() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenBracketToken);
        if (Scanner.HasPrecedingLineBreak)
        {

            node.MultiLine = true;
        }

        node.Elements = ParseDelimitedList(ParsingContext.ArrayLiteralMembers, ParseArgumentOrArrayLiteralElement);

        ParseExpected(TypeScriptSyntaxKind.CloseBracketToken);

        return FinishNode(node);
    }


    public IAccessorDeclaration? TryParseAccessorDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        if (ParseContextualModifier(TypeScriptSyntaxKind.GetKeyword))
        {

            return ParseAccessorDeclaration(TypeScriptSyntaxKind.GetAccessor, fullStart, decorators, modifiers);
        }
        else
        if (ParseContextualModifier(TypeScriptSyntaxKind.SetKeyword))
        {

            return ParseAccessorDeclaration(TypeScriptSyntaxKind.SetAccessor, fullStart, decorators, modifiers);
        }


        return null;
    }


    public IObjectLiteralElementLike ParseObjectLiteralElement()
    {
        var fullStart = Scanner.StartPos;
        var dotDotDotToken = ParseOptionalToken<DotDotDotToken>(TypeScriptSyntaxKind.DotDotDotToken);
        if (dotDotDotToken is not null)
        {
            var spreadElement = new SpreadAssignment
            {
                Pos = fullStart,
                Expression = ParseAssignmentExpressionOrHigher()
            };

            return AddJsDocComment(FinishNode(spreadElement));
        }
        var decorators = ParseDecorators();
        var modifiers = ParseModifiers();
        var accessor = TryParseAccessorDeclaration(fullStart, decorators, modifiers);
        if (accessor != null)
        {

            return accessor;
        }
        var asteriskToken = ParseOptionalToken<AsteriskToken>(TypeScriptSyntaxKind.AsteriskToken);
        var tokenIsIdentifier = IsIdentifier();
        var propertyName = ParsePropertyName(); // parseIdentifierName(); // 
        var questionToken = ParseOptionalToken<QuestionToken>(TypeScriptSyntaxKind.QuestionToken);
        if (asteriskToken is not null || Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.LessThanToken)
        {

            return ParseMethodDeclaration(fullStart, decorators, modifiers, asteriskToken, propertyName, questionToken);
        }
        var isShorthandPropertyAssignment =
                        tokenIsIdentifier && (Token is TypeScriptSyntaxKind.CommaToken || Token is TypeScriptSyntaxKind.CloseBraceToken || Token is TypeScriptSyntaxKind.EqualsToken);
        if (isShorthandPropertyAssignment)
        {
            var shorthandDeclaration = new ShorthandPropertyAssignment
            {
                Pos = fullStart,
                Name = (Identifier)propertyName,

                QuestionToken = questionToken
            };
            var equalsToken = ParseOptionalToken<EqualsToken>(TypeScriptSyntaxKind.EqualsToken);
            if (equalsToken is not null)
            {

                shorthandDeclaration.EqualsToken = equalsToken;

                shorthandDeclaration.ObjectAssignmentInitializer = AllowInAnd(ParseAssignmentExpressionOrHigher);
            }

            return AddJsDocComment(FinishNode(shorthandDeclaration));
        }
        else
        {
            var propertyAssignment = new PropertyAssignment
            {
                Pos = fullStart,
                Modifiers = modifiers,

                Name = propertyName,

                QuestionToken = questionToken
            };

            ParseExpected(TypeScriptSyntaxKind.ColonToken);

            propertyAssignment.Initializer = AllowInAnd(ParseAssignmentExpressionOrHigher);

            return AddJsDocComment(FinishNode(propertyAssignment));
        }
    }


    public ObjectLiteralExpression ParseObjectLiteralExpression()
    {
        var node = new ObjectLiteralExpression() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenBraceToken);
        if (Scanner.HasPrecedingLineBreak)
        {

            node.MultiLine = true;
        }


        node.Properties = ParseDelimitedList(ParsingContext.ObjectLiteralMembers, ParseObjectLiteralElement, /*considerSemicolonAsDelimiter*/ true);

        ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);

        return FinishNode(node);
    }


    public FunctionExpression ParseFunctionExpression()
    {
        var saveDecoratorContext = InDecoratorContext();
        if (saveDecoratorContext)
        {

            SetDecoratorContext(/*val*/ false);
        }
        var node = new FunctionExpression
        {
            Pos = Scanner.StartPos,
            Modifiers = ParseModifiers()
        };

        ParseExpected(TypeScriptSyntaxKind.FunctionKeyword);

        node.AsteriskToken = ParseOptionalToken<AsteriskToken>(TypeScriptSyntaxKind.AsteriskToken);
        var isGenerator = /*!!*/node.AsteriskToken is not null;
        var isAsync = /*!!*/(GetModifierFlags(node) & ModifierFlags.Async) != 0;

        node.Name =
            isGenerator && isAsync ? DoInYieldAndAwaitContext(ParseOptionalIdentifier) :
                isGenerator ? DoInYieldContext(ParseOptionalIdentifier) :
                    isAsync ? DoInAwaitContext(ParseOptionalIdentifier) :
                        ParseOptionalIdentifier();


        FillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ isGenerator, /*awaitContext*/ isAsync, /*requireCompleteParameterList*/ false, node);

        node.Body = ParseFunctionBlock(/*allowYield*/ isGenerator, /*allowAwait*/ isAsync, /*ignoreMissingOpenBrace*/ false);
        if (saveDecoratorContext)
        {

            SetDecoratorContext(/*val*/ true);
        }


        return AddJsDocComment(FinishNode(node));
    }


    public Identifier? ParseOptionalIdentifier() => IsIdentifier() ? ParseIdentifier() : null;


    public /*NewExpression | MetaProperty*/IPrimaryExpression ParseNewExpression()
    {
        var fullStart = Scanner.StartPos;

        ParseExpected(TypeScriptSyntaxKind.NewKeyword);
        if (ParseOptional(TypeScriptSyntaxKind.DotToken))
        {
            var node = new MetaProperty
            {
                Pos = fullStart,
                KeywordToken = TypeScriptSyntaxKind.NewKeyword,

                Name = ParseIdentifierName()
            };

            return FinishNode(node);
        }
        else
        {
            var node = new NewExpression
            {
                Pos = fullStart,
                Expression = ParseMemberExpressionOrHigher(),

                TypeArguments = TryParse(ParseTypeArgumentsInExpression)
            };
            if (node.TypeArguments != null || Token is TypeScriptSyntaxKind.OpenParenToken)
            {

                node.Arguments = ParseArgumentList();
            }

            return FinishNode(node);
        }
    }


    public Block ParseBlock(bool ignoreMissingOpenBrace, DiagnosticMessage? diagnosticMessage = null)
    {
        var node = new Block() { Pos = Scanner.StartPos };
        if (ParseExpected(TypeScriptSyntaxKind.OpenBraceToken, diagnosticMessage) || ignoreMissingOpenBrace)
        {
            if (Scanner.HasPrecedingLineBreak)
            {

                node.MultiLine = true;
            }


            node.Statements = ParseList2(ParsingContext.BlockStatements, ParseStatement);

            ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);
        }
        else
        {

            node.Statements = new NodeArray<IStatement>(); //.Cast<Node>().ToList(); createMissingList
        }

        return FinishNode(node);
    }


    public Block ParseFunctionBlock(bool allowYield, bool allowAwait, bool ignoreMissingOpenBrace, DiagnosticMessage? diagnosticMessage = null)
    {
        var savedYieldContext = InYieldContext();

        SetYieldContext(allowYield);
        var savedAwaitContext = InAwaitContext();

        SetAwaitContext(allowAwait);
        var saveDecoratorContext = InDecoratorContext();
        if (saveDecoratorContext)
        {

            SetDecoratorContext(/*val*/ false);
        }
        var block = ParseBlock(ignoreMissingOpenBrace, diagnosticMessage);
        if (saveDecoratorContext)
        {

            SetDecoratorContext(/*val*/ true);
        }


        SetYieldContext(savedYieldContext);

        SetAwaitContext(savedAwaitContext);


        return block;
    }


    public EmptyStatement ParseEmptyStatement()
    {
        var node = new EmptyStatement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.SemicolonToken);

        return FinishNode(node);
    }


    public IfStatement ParseIfStatement()
    {
        var node = new IfStatement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.IfKeyword);

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);

        node.Expression = AllowInAnd(ParseExpression);

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

        node.ThenStatement = ParseStatement();

        node.ElseStatement = ParseOptional(TypeScriptSyntaxKind.ElseKeyword) ? ParseStatement() : null;

        return FinishNode(node);
    }


    public DoStatement ParseDoStatement()
    {
        var node = new DoStatement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.DoKeyword);

        node.Statement = ParseStatement();

        ParseExpected(TypeScriptSyntaxKind.WhileKeyword);

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);

        node.Expression = AllowInAnd(ParseExpression);

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);


        // From: https://mail.mozilla.org/pipermail/es-discuss/2011-August/016188.html
        // 157 min --- All allen at wirfs-brock.com CONF --- "do{;}while(false)false" prohibited in
        // spec but allowed in consensus reality. Approved -- this is the de-facto standard whereby
        //  do;while(0)x will have a semicolon inserted before x.
        ParseOptional(TypeScriptSyntaxKind.SemicolonToken);

        return FinishNode(node);
    }


    public WhileStatement ParseWhileStatement()
    {
        var node = new WhileStatement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.WhileKeyword);

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);

        node.Expression = AllowInAnd(ParseExpression);

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

        node.Statement = ParseStatement();

        return FinishNode(node);
    }


    public Statement ParseForOrForInOrForOfStatement()
    {
        var pos = NodePos;

        ParseExpected(TypeScriptSyntaxKind.ForKeyword);
        var awaitToken = ParseOptionalToken<AwaitKeywordToken>(TypeScriptSyntaxKind.AwaitKeyword);

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);
        IVariableDeclarationListOrExpression initializer = null;
        //Node initializer = null;
        if (Token is not TypeScriptSyntaxKind.SemicolonToken)
        {
            if (Token is TypeScriptSyntaxKind.VarKeyword || Token is TypeScriptSyntaxKind.LetKeyword || Token is TypeScriptSyntaxKind.ConstKeyword)
            {

                initializer = ParseVariableDeclarationList(/*inForStatementInitializer*/ true);
            }
            else
            {

                initializer = DisallowInAnd(ParseExpression);
            }
        }
        IterationStatement forOrForInOrForOfStatement = null;
        if (awaitToken is not null ? ParseExpected(TypeScriptSyntaxKind.OfKeyword) : ParseOptional(TypeScriptSyntaxKind.OfKeyword))
        {
            var forOfStatement = new ForOfStatement
            {
                Pos = pos,
                AwaitModifier = awaitToken,

                Initializer = initializer,

                Expression = AllowInAnd(ParseAssignmentExpressionOrHigher)
            };

            ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

            forOrForInOrForOfStatement = forOfStatement;
        }
        else
        if (ParseOptional(TypeScriptSyntaxKind.InKeyword))
        {
            var forInStatement = new ForInStatement
            {
                Pos = pos,
                Initializer = initializer,

                Expression = AllowInAnd(ParseExpression)
            };

            ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

            forOrForInOrForOfStatement = forInStatement;
        }
        else
        {
            var forStatement = new ForStatement
            {
                Pos = pos,
                Initializer = initializer
            };

            ParseExpected(TypeScriptSyntaxKind.SemicolonToken);
            if (Token is not TypeScriptSyntaxKind.SemicolonToken && Token is not TypeScriptSyntaxKind.CloseParenToken)
            {

                forStatement.Condition = AllowInAnd(ParseExpression);
            }

            ParseExpected(TypeScriptSyntaxKind.SemicolonToken);
            if (Token is not TypeScriptSyntaxKind.CloseParenToken)
            {

                forStatement.Incrementor = AllowInAnd(ParseExpression);
            }

            ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

            forOrForInOrForOfStatement = forStatement;
        }


        forOrForInOrForOfStatement.Statement = ParseStatement();


        return FinishNode(forOrForInOrForOfStatement);
    }


    public IBreakOrContinueStatement ParseBreakOrContinueStatement(TypeScriptSyntaxKind kind)
    {
        var node = Kind is TypeScriptSyntaxKind.ContinueStatement ? (IBreakOrContinueStatement)new ContinueStatement { Pos = Scanner.StartPos } : Kind is TypeScriptSyntaxKind.BreakStatement ? new BreakStatement { Pos = Scanner.StartPos } : throw new NotSupportedException("parseBreakOrContinueStatement");


        ParseExpected(Kind is TypeScriptSyntaxKind.BreakStatement ? TypeScriptSyntaxKind.BreakKeyword : TypeScriptSyntaxKind.ContinueKeyword);
        if (!CanParseSemicolon())
        {

            node.Label = ParseIdentifier();
        }


        ParseSemicolon();

        return FinishNode(node);
    }


    public ReturnStatement ParseReturnStatement()
    {
        var node = new ReturnStatement() { Pos = Scanner.StartPos };


        ParseExpected(TypeScriptSyntaxKind.ReturnKeyword);
        if (!CanParseSemicolon())
        {

            node.Expression = AllowInAnd(ParseExpression);
        }


        ParseSemicolon();

        return FinishNode(node);
    }


    public WithStatement ParseWithStatement()
    {
        var node = new WithStatement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.WithKeyword);

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);

        node.Expression = AllowInAnd(ParseExpression);

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

        node.Statement = ParseStatement();

        return FinishNode(node);
    }


    public CaseClause ParseCaseClause()
    {
        var node = new CaseClause() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.CaseKeyword);

        node.Expression = AllowInAnd(ParseExpression);

        ParseExpected(TypeScriptSyntaxKind.ColonToken);

        node.Statements = ParseList2(ParsingContext.SwitchClauseStatements, ParseStatement);

        return FinishNode(node);
    }


    public DefaultClause ParseDefaultClause()
    {
        var node = new DefaultClause() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.DefaultKeyword);

        ParseExpected(TypeScriptSyntaxKind.ColonToken);

        node.Statements = ParseList2(ParsingContext.SwitchClauseStatements, ParseStatement);

        return FinishNode(node);
    }


    public ICaseOrDefaultClause ParseCaseOrDefaultClause() => Token is TypeScriptSyntaxKind.CaseKeyword ? ParseCaseClause() : ParseDefaultClause();


    public SwitchStatement ParseSwitchStatement()
    {
        var node = new SwitchStatement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.SwitchKeyword);

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);

        node.Expression = AllowInAnd(ParseExpression);

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);
        var caseBlock = new CaseBlock() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenBraceToken);

        caseBlock.Clauses = ParseList(ParsingContext.SwitchClauses, ParseCaseOrDefaultClause);

        ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);

        node.CaseBlock = FinishNode(caseBlock);

        return FinishNode(node);
    }


    public ThrowStatement ParseThrowStatement()
    {
        var node = new ThrowStatement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.ThrowKeyword);

        node.Expression = Scanner.HasPrecedingLineBreak ? null : AllowInAnd(ParseExpression);

        ParseSemicolon();

        return FinishNode(node);
    }


    public TryStatement ParseTryStatement()
    {
        var node = new TryStatement() { Pos = Scanner.StartPos };


        ParseExpected(TypeScriptSyntaxKind.TryKeyword);

        node.TryBlock = ParseBlock(/*ignoreMissingOpenBrace*/ false);

        node.CatchClause = Token is TypeScriptSyntaxKind.CatchKeyword ? ParseCatchClause() : null;
        if (node.CatchClause is null || Token is TypeScriptSyntaxKind.FinallyKeyword)
        {

            ParseExpected(TypeScriptSyntaxKind.FinallyKeyword);

            node.FinallyBlock = ParseBlock(/*ignoreMissingOpenBrace*/ false);
        }


        return FinishNode(node);
    }


    public CatchClause ParseCatchClause()
    {
        var result = new CatchClause() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.CatchKeyword);
        if (ParseExpected(TypeScriptSyntaxKind.OpenParenToken))
        {

            result.VariableDeclaration = ParseVariableDeclaration();
        }


        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

        result.Block = ParseBlock(/*ignoreMissingOpenBrace*/ false);

        return FinishNode(result);
    }


    public DebuggerStatement ParseDebuggerStatement()
    {
        var node = new DebuggerStatement() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.DebuggerKeyword);

        ParseSemicolon();

        return FinishNode(node);
    }


    public /*ExpressionStatement | LabeledStatement*/Statement ParseExpressionOrLabeledStatement()
    {
        var fullStart = Scanner.StartPos;
        var expression = AllowInAnd(ParseExpression);
        if (expression.Kind is TypeScriptSyntaxKind.Identifier && ParseOptional(TypeScriptSyntaxKind.ColonToken))
        {
            var labeledStatement = new LabeledStatement
            {
                Pos = fullStart,
                Label = (Identifier)expression,

                Statement = ParseStatement()
            };

            return AddJsDocComment(FinishNode(labeledStatement));
        }
        else
        {
            var expressionStatement = new ExpressionStatement
            {
                Pos = fullStart,
                Expression = expression
            };

            ParseSemicolon();

            return AddJsDocComment(FinishNode(expressionStatement));
        }
    }


    public bool NextTokenIsIdentifierOrKeywordOnSameLine()
    {

        _ = NextToken;

        return TokenIsIdentifierOrKeyword(Token) && !Scanner.HasPrecedingLineBreak;
    }


    public bool NextTokenIsFunctionKeywordOnSameLine()
    {

        _ = NextToken;

        return Token is TypeScriptSyntaxKind.FunctionKeyword && !Scanner.HasPrecedingLineBreak;
    }


    public bool NextTokenIsIdentifierOrKeywordOrNumberOnSameLine()
    {

        _ = NextToken;

        return (TokenIsIdentifierOrKeyword(Token) || Token is TypeScriptSyntaxKind.NumericLiteral) && !Scanner.HasPrecedingLineBreak;
    }


    public bool IsDeclaration()
    {
        while (true)
        {
            switch (Token)
            {
                case TypeScriptSyntaxKind.VarKeyword:
                case TypeScriptSyntaxKind.LetKeyword:
                case TypeScriptSyntaxKind.ConstKeyword:
                case TypeScriptSyntaxKind.FunctionKeyword:
                case TypeScriptSyntaxKind.ClassKeyword:
                case TypeScriptSyntaxKind.EnumKeyword:

                    return true;
                case TypeScriptSyntaxKind.InterfaceKeyword:
                case TypeScriptSyntaxKind.TypeKeyword:

                    return NextTokenIsIdentifierOnSameLine();
                case TypeScriptSyntaxKind.ModuleKeyword:
                case TypeScriptSyntaxKind.NamespaceKeyword:

                    return NextTokenIsIdentifierOrStringLiteralOnSameLine();
                case TypeScriptSyntaxKind.AbstractKeyword:
                case TypeScriptSyntaxKind.AsyncKeyword:
                case TypeScriptSyntaxKind.DeclareKeyword:
                case TypeScriptSyntaxKind.PrivateKeyword:
                case TypeScriptSyntaxKind.ProtectedKeyword:
                case TypeScriptSyntaxKind.PublicKeyword:
                case TypeScriptSyntaxKind.ReadonlyKeyword:

                    _ = NextToken;
                    if (Scanner.HasPrecedingLineBreak)
                    {

                        return false;
                    }

                    continue;
                case TypeScriptSyntaxKind.GlobalKeyword:

                    _ = NextToken;

                    return Token is TypeScriptSyntaxKind.OpenBraceToken || Token is TypeScriptSyntaxKind.Identifier || Token is TypeScriptSyntaxKind.ExportKeyword;
                case TypeScriptSyntaxKind.ImportKeyword:

                    _ = NextToken;

                    return Token is TypeScriptSyntaxKind.StringLiteral || Token is TypeScriptSyntaxKind.AsteriskToken ||
                        Token is TypeScriptSyntaxKind.OpenBraceToken || TokenIsIdentifierOrKeyword(Token);
                case TypeScriptSyntaxKind.ExportKeyword:

                    _ = NextToken;
                    if (Token is TypeScriptSyntaxKind.EqualsToken || Token is TypeScriptSyntaxKind.AsteriskToken ||
                                                Token is TypeScriptSyntaxKind.OpenBraceToken || Token is TypeScriptSyntaxKind.DefaultKeyword ||
                                                Token is TypeScriptSyntaxKind.AsKeyword)
                    {

                        return true;
                    }

                    continue;
                case TypeScriptSyntaxKind.StaticKeyword:

                    _ = NextToken;

                    continue;
                default:

                    return false;
            }
        }
    }


    public bool IsStartOfDeclaration() => LookAhead(IsDeclaration);


    public bool IsStartOfStatement() => Token switch
    {
        TypeScriptSyntaxKind.AtToken or TypeScriptSyntaxKind.SemicolonToken or TypeScriptSyntaxKind.OpenBraceToken or TypeScriptSyntaxKind.VarKeyword or TypeScriptSyntaxKind.LetKeyword or TypeScriptSyntaxKind.FunctionKeyword or TypeScriptSyntaxKind.ClassKeyword or TypeScriptSyntaxKind.EnumKeyword or TypeScriptSyntaxKind.IfKeyword or TypeScriptSyntaxKind.DoKeyword or TypeScriptSyntaxKind.WhileKeyword or TypeScriptSyntaxKind.ForKeyword or TypeScriptSyntaxKind.ContinueKeyword or TypeScriptSyntaxKind.BreakKeyword or TypeScriptSyntaxKind.ReturnKeyword or TypeScriptSyntaxKind.WithKeyword or TypeScriptSyntaxKind.SwitchKeyword or TypeScriptSyntaxKind.ThrowKeyword or TypeScriptSyntaxKind.TryKeyword or TypeScriptSyntaxKind.DebuggerKeyword or TypeScriptSyntaxKind.CatchKeyword or TypeScriptSyntaxKind.FinallyKeyword => true,
        TypeScriptSyntaxKind.ConstKeyword or TypeScriptSyntaxKind.ExportKeyword or TypeScriptSyntaxKind.ImportKeyword => IsStartOfDeclaration(),
        TypeScriptSyntaxKind.AsyncKeyword or TypeScriptSyntaxKind.DeclareKeyword or TypeScriptSyntaxKind.InterfaceKeyword or TypeScriptSyntaxKind.ModuleKeyword or TypeScriptSyntaxKind.NamespaceKeyword or TypeScriptSyntaxKind.TypeKeyword or TypeScriptSyntaxKind.GlobalKeyword => true,// When these don't start a declaration, they're an identifier in an expression statement
        TypeScriptSyntaxKind.PublicKeyword or TypeScriptSyntaxKind.PrivateKeyword or TypeScriptSyntaxKind.ProtectedKeyword or TypeScriptSyntaxKind.StaticKeyword or TypeScriptSyntaxKind.ReadonlyKeyword => IsStartOfDeclaration() || !LookAhead(NextTokenIsIdentifierOrKeywordOnSameLine),// When these don't start a declaration, they may be the start of a class member if an identifier
                                                                                                                                                                                                                                         // immediately follows. Otherwise they're an identifier in an expression statement.
        _ => IsStartOfExpression(),
    };


    public bool NextTokenIsIdentifierOrStartOfDestructuring()
    {

        _ = NextToken;

        return IsIdentifier() || Token is TypeScriptSyntaxKind.OpenBraceToken || Token is TypeScriptSyntaxKind.OpenBracketToken;
    }


    public bool IsLetDeclaration() =>

        // In ES6 'let' always starts a lexical declaration if followed by an identifier or {
        // or [.
        LookAhead(NextTokenIsIdentifierOrStartOfDestructuring);


    public IStatement ParseStatement()
    {
        switch (Token)
        {
            case TypeScriptSyntaxKind.SemicolonToken:

                return ParseEmptyStatement();
            case TypeScriptSyntaxKind.OpenBraceToken:

                return ParseBlock(/*ignoreMissingOpenBrace*/ false);
            case TypeScriptSyntaxKind.VarKeyword:

                return ParseVariableStatement(Scanner.StartPos, /*decorators*/ null, /*modifiers*/ null);
            case TypeScriptSyntaxKind.LetKeyword:
                if (IsLetDeclaration())
                {

                    return ParseVariableStatement(Scanner.StartPos, /*decorators*/ null, /*modifiers*/ null);
                }

                break;
            case TypeScriptSyntaxKind.FunctionKeyword:

                return ParseFunctionDeclaration(Scanner.StartPos, /*decorators*/ null, /*modifiers*/ null);
            case TypeScriptSyntaxKind.ClassKeyword:

                return ParseClassDeclaration(Scanner.StartPos, /*decorators*/ null, /*modifiers*/ null);
            case TypeScriptSyntaxKind.IfKeyword:

                return ParseIfStatement();
            case TypeScriptSyntaxKind.DoKeyword:

                return ParseDoStatement();
            case TypeScriptSyntaxKind.WhileKeyword:

                return ParseWhileStatement();
            case TypeScriptSyntaxKind.ForKeyword:

                return ParseForOrForInOrForOfStatement();
            case TypeScriptSyntaxKind.ContinueKeyword:

                return ParseBreakOrContinueStatement(TypeScriptSyntaxKind.ContinueStatement);
            case TypeScriptSyntaxKind.BreakKeyword:

                return ParseBreakOrContinueStatement(TypeScriptSyntaxKind.BreakStatement);
            case TypeScriptSyntaxKind.ReturnKeyword:

                return ParseReturnStatement();
            case TypeScriptSyntaxKind.WithKeyword:

                return ParseWithStatement();
            case TypeScriptSyntaxKind.SwitchKeyword:

                return ParseSwitchStatement();
            case TypeScriptSyntaxKind.ThrowKeyword:

                return ParseThrowStatement();
            case TypeScriptSyntaxKind.TryKeyword:
            case TypeScriptSyntaxKind.CatchKeyword:
            case TypeScriptSyntaxKind.FinallyKeyword:

                return ParseTryStatement();
            case TypeScriptSyntaxKind.DebuggerKeyword:

                return ParseDebuggerStatement();
            case TypeScriptSyntaxKind.AtToken:

                return ParseDeclaration();
            case TypeScriptSyntaxKind.AsyncKeyword:
            case TypeScriptSyntaxKind.InterfaceKeyword:
            case TypeScriptSyntaxKind.TypeKeyword:
            case TypeScriptSyntaxKind.ModuleKeyword:
            case TypeScriptSyntaxKind.NamespaceKeyword:
            case TypeScriptSyntaxKind.DeclareKeyword:
            case TypeScriptSyntaxKind.ConstKeyword:
            case TypeScriptSyntaxKind.EnumKeyword:
            case TypeScriptSyntaxKind.ExportKeyword:
            case TypeScriptSyntaxKind.ImportKeyword:
            case TypeScriptSyntaxKind.PrivateKeyword:
            case TypeScriptSyntaxKind.ProtectedKeyword:
            case TypeScriptSyntaxKind.PublicKeyword:
            case TypeScriptSyntaxKind.AbstractKeyword:
            case TypeScriptSyntaxKind.StaticKeyword:
            case TypeScriptSyntaxKind.ReadonlyKeyword:
            case TypeScriptSyntaxKind.GlobalKeyword:
                if (IsStartOfDeclaration())
                {

                    return ParseDeclaration();
                }

                break;
        }

        return ParseExpressionOrLabeledStatement();
    }


    public IStatement? ParseDeclaration()
    {
        var fullStart = NodePos;
        var decorators = ParseDecorators();
        var modifiers = ParseModifiers();
        switch (Token)
        {
            case TypeScriptSyntaxKind.VarKeyword:
            case TypeScriptSyntaxKind.LetKeyword:
            case TypeScriptSyntaxKind.ConstKeyword:

                return ParseVariableStatement(fullStart, decorators, modifiers);
            case TypeScriptSyntaxKind.FunctionKeyword:

                return ParseFunctionDeclaration(fullStart, decorators, modifiers);
            case TypeScriptSyntaxKind.ClassKeyword:

                return ParseClassDeclaration(fullStart, decorators, modifiers);
            case TypeScriptSyntaxKind.InterfaceKeyword:

                return ParseInterfaceDeclaration(fullStart, decorators, modifiers);
            case TypeScriptSyntaxKind.TypeKeyword:

                return ParseTypeAliasDeclaration(fullStart, decorators, modifiers);
            case TypeScriptSyntaxKind.EnumKeyword:

                return ParseEnumDeclaration(fullStart, decorators, modifiers);
            case TypeScriptSyntaxKind.GlobalKeyword:
            case TypeScriptSyntaxKind.ModuleKeyword:
            case TypeScriptSyntaxKind.NamespaceKeyword:

                return ParseModuleDeclaration(fullStart, decorators, modifiers);
            case TypeScriptSyntaxKind.ImportKeyword:

                return ParseImportDeclarationOrImportEqualsDeclaration(fullStart, decorators, modifiers);
            case TypeScriptSyntaxKind.ExportKeyword:

                _ = NextToken;
                return Token switch
                {
                    TypeScriptSyntaxKind.DefaultKeyword or TypeScriptSyntaxKind.EqualsToken => ParseExportAssignment(fullStart, decorators, modifiers),
                    TypeScriptSyntaxKind.AsKeyword => ParseNamespaceExportDeclaration(fullStart, decorators, modifiers),
                    _ => ParseExportDeclaration(fullStart, decorators, modifiers),
                };
                break;
            default:

                if (decorators?.Any() == true || modifiers?.Any() == true)
                {
                    // We reached this point because we encountered decorators and/or modifiers and assumed a declaration
                    // would follow. For recovery and error reporting purposes, return an incomplete declaration.
                    var node = (Statement)CreateMissingNode<Statement>(TypeScriptSyntaxKind.MissingDeclaration, /*reportAtCurrentPosition*/ true, Diagnostics.Declaration_expected);
                    node.Pos = fullStart;
                    node.Decorators = decorators;
                    node.Modifiers = modifiers;
                    return FinishNode(node);
                }
                break;
        }
        return null;
    }


    public bool NextTokenIsIdentifierOrStringLiteralOnSameLine()
    {
        _ = NextToken;

        return !Scanner.HasPrecedingLineBreak &&
            (IsIdentifier() || Token is TypeScriptSyntaxKind.StringLiteral);
    }


    public Block? ParseFunctionBlockOrSemicolon(
        bool isGenerator,
        bool isAsync,
        DiagnosticMessage? diagnosticMessage = null)
    {
        if (Token is not TypeScriptSyntaxKind.OpenBraceToken && CanParseSemicolon())
        {
            ParseSemicolon();
            return null;
        }

        return ParseFunctionBlock(
            isGenerator,
            isAsync,
            false,
            diagnosticMessage);
    }


    public IArrayBindingElement ParseArrayBindingElement()
    {
        if (Token is TypeScriptSyntaxKind.CommaToken)
        {
            return new OmittedExpression { Pos = Scanner.StartPos };
        }

        var node = new BindingElement
        {
            Pos = Scanner.StartPos,
            DotDotDotToken = ParseOptionalToken<DotDotDotToken>(TypeScriptSyntaxKind.DotDotDotToken),
            Name = ParseIdentifierOrPattern(),
            Initializer = ParseBindingElementInitializer(false)
        };

        return FinishNode(node);
    }


    public IArrayBindingElement ParseObjectBindingElement()
    {
        var node = new BindingElement
        {
            Pos = Scanner.StartPos,
            DotDotDotToken = ParseOptionalToken<DotDotDotToken>(TypeScriptSyntaxKind.DotDotDotToken)
        };
        var tokenIsIdentifier = IsIdentifier();
        var propertyName = ParsePropertyName();
        if (tokenIsIdentifier && Token is not TypeScriptSyntaxKind.ColonToken)
        {

            node.Name = (Identifier)propertyName;
        }
        else
        {

            ParseExpected(TypeScriptSyntaxKind.ColonToken);

            node.PropertyName = propertyName;

            node.Name = ParseIdentifierOrPattern();
        }

        node.Initializer = ParseBindingElementInitializer(/*inParameter*/ false);

        return FinishNode(node);
    }


    public ObjectBindingPattern ParseObjectBindingPattern()
    {
        var node = new ObjectBindingPattern() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenBraceToken);

        node.Elements = ParseDelimitedList(ParsingContext.ObjectBindingElements, ParseObjectBindingElement);

        ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);

        return FinishNode(node);
    }


    public ArrayBindingPattern ParseArrayBindingPattern()
    {
        var node = new ArrayBindingPattern() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.OpenBracketToken);

        node.Elements = ParseDelimitedList(ParsingContext.ArrayBindingElements, ParseArrayBindingElement);

        ParseExpected(TypeScriptSyntaxKind.CloseBracketToken);

        return FinishNode(node);
    }


    public bool IsIdentifierOrPattern() => Token is TypeScriptSyntaxKind.OpenBraceToken || Token is TypeScriptSyntaxKind.OpenBracketToken || IsIdentifier();


    public /*Identifier | BindingPattern*/Node ParseIdentifierOrPattern()
    {
        if (Token is TypeScriptSyntaxKind.OpenBracketToken)
        {

            return ParseArrayBindingPattern();
        }
        return Token is TypeScriptSyntaxKind.OpenBraceToken ? ParseObjectBindingPattern() : ParseIdentifier();
    }


    public VariableDeclaration ParseVariableDeclaration()
    {
        var node = new VariableDeclaration
        {
            Pos = Scanner.StartPos,
            Name = ParseIdentifierOrPattern(),

            Type = ParseTypeAnnotation()
        };
        if (!IsInOrOfKeyword(Token))
        {

            node.Initializer = ParseInitializer(/*inParameter*/ false);
        }

        return FinishNode(node);
    }


    public IVariableDeclarationList ParseVariableDeclarationList(bool inForStatementInitializer)
    {
        var node = new VariableDeclarationList() { Pos = Scanner.StartPos };
        switch (Token)
        {
            case TypeScriptSyntaxKind.VarKeyword:

                break;
            case TypeScriptSyntaxKind.LetKeyword:

                node.Flags |= NodeFlags.Let;

                break;
            case TypeScriptSyntaxKind.ConstKeyword:

                node.Flags |= NodeFlags.Const;

                break;
            default:

                Debug.Fail("Oops...");
                break;
        }


        _ = NextToken;
        if (Token is TypeScriptSyntaxKind.OfKeyword && LookAhead(CanFollowContextualOfKeyword))
        {

            node.Declarations = CreateMissingList<VariableDeclaration>();
        }
        else
        {
            var savedDisallowIn = InDisallowInContext();

            SetDisallowInContext(inForStatementInitializer);


            node.Declarations = ParseDelimitedList(ParsingContext.VariableDeclarations, ParseVariableDeclaration);


            SetDisallowInContext(savedDisallowIn);
        }


        return FinishNode(node);
    }


    public bool CanFollowContextualOfKeyword() => NextTokenIsIdentifier() && NextToken is TypeScriptSyntaxKind.CloseParenToken;


    public VariableStatement ParseVariableStatement(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new VariableStatement
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers,
            DeclarationList = ParseVariableDeclarationList(/*inForStatementInitializer*/ false)
        };

        ParseSemicolon();

        return AddJsDocComment(FinishNode(node));
    }


    public FunctionDeclaration ParseFunctionDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new FunctionDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };

        ParseExpected(TypeScriptSyntaxKind.FunctionKeyword);

        node.AsteriskToken = ParseOptionalToken<AsteriskToken>(TypeScriptSyntaxKind.AsteriskToken);

        node.Name = HasModifier(node, ModifierFlags.Default) ? ParseOptionalIdentifier() : ParseIdentifier();
        var isGenerator = /*!!*/node.AsteriskToken is not null;
        var isAsync = HasModifier(node, ModifierFlags.Async);

        FillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ isGenerator, /*awaitContext*/ isAsync, /*requireCompleteParameterList*/ false, node);

        node.Body = ParseFunctionBlockOrSemicolon(isGenerator, isAsync, Diagnostics.or_expected);

        return AddJsDocComment(FinishNode(node));
    }


    public ConstructorDeclaration ParseConstructorDeclaration(int pos, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new ConstructorDeclaration
        {
            Pos = pos,
            Decorators = decorators,
            Modifiers = modifiers
        };

        ParseExpected(TypeScriptSyntaxKind.ConstructorKeyword);

        FillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ false, /*awaitContext*/ false, /*requireCompleteParameterList*/ false, node);

        node.Body = ParseFunctionBlockOrSemicolon(/*isGenerator*/ false, /*isAsync*/ false, Diagnostics.or_expected);

        return AddJsDocComment(FinishNode(node));
    }


    public MethodDeclaration ParseMethodDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers, AsteriskToken asteriskToken, IPropertyName name, QuestionToken questionToken, DiagnosticMessage? diagnosticMessage = null)
    {
        var method = new MethodDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers,
            AsteriskToken = asteriskToken,
            Name = name,
            QuestionToken = questionToken
        };
        var isGenerator = asteriskToken is not null;
        var isAsync = HasModifier(method, ModifierFlags.Async);

        FillSignature(TypeScriptSyntaxKind.ColonToken, isGenerator, isAsync, false, method);

        method.Body = ParseFunctionBlockOrSemicolon(isGenerator, isAsync, diagnosticMessage);

        return AddJsDocComment(FinishNode(method));
    }


    public ClassElement ParsePropertyDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers, IPropertyName name, QuestionToken questionToken)
    {
        var property = new PropertyDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers,
            Name = name,
            QuestionToken = questionToken,
            Type = ParseTypeAnnotation()
        };

        // For instance properties specifically, since they are evaluated inside the constructor,
        // we do *not * want to parse yield expressions, so we specifically turn the yield context
        // off. The grammar would look something like this:
        //
        //    MemberVariableDeclaration[Yield]:
        //        AccessibilityModifier_opt   PropertyName   TypeAnnotation_opt   Initializer_opt[In];
        //        AccessibilityModifier_opt  static_opt  PropertyName   TypeAnnotation_opt   Initializer_opt[In, ?Yield];
        //
        // The checker may still error in the static case to explicitly disallow the yield expression.
        property.Initializer = HasModifier(property, ModifierFlags.Static)
            ? AllowInAnd(ParseNonParameterInitializer)
            : DoOutsideOfContext(NodeFlags.YieldContext | NodeFlags.DisallowInContext, ParseNonParameterInitializer);


        ParseSemicolon();

        return AddJsDocComment(FinishNode(property));
    }


    public IClassElement ParsePropertyOrMethodDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var asteriskToken = ParseOptionalToken<AsteriskToken>(TypeScriptSyntaxKind.AsteriskToken);
        var name = ParsePropertyName();
        var questionToken = ParseOptionalToken<QuestionToken>(TypeScriptSyntaxKind.QuestionToken);
        return asteriskToken is not null || Token is TypeScriptSyntaxKind.OpenParenToken || Token is TypeScriptSyntaxKind.LessThanToken
            ? ParseMethodDeclaration(fullStart, decorators, modifiers, asteriskToken, name, questionToken, Diagnostics.or_expected)
            : ParsePropertyDeclaration(fullStart, decorators, modifiers, name, questionToken);
    }


    public IExpression ParseNonParameterInitializer() => ParseInitializer(/*inParameter*/ false);


    public IAccessorDeclaration ParseAccessorDeclaration(TypeScriptSyntaxKind kind, int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = Kind is TypeScriptSyntaxKind.GetAccessor ? (IAccessorDeclaration)new GetAccessorDeclaration() { Kind = kind, Pos = fullStart } : Kind is TypeScriptSyntaxKind.SetAccessor ? new SetAccessorDeclaration() { Kind = kind, Pos = fullStart } : throw new NotSupportedException("parseAccessorDeclaration");

        node.Decorators = decorators;

        node.Modifiers = modifiers;

        node.Name = ParsePropertyName();

        FillSignature(TypeScriptSyntaxKind.ColonToken, /*yieldContext*/ false, /*awaitContext*/ false, /*requireCompleteParameterList*/ false, node);

        node.Body = ParseFunctionBlockOrSemicolon(/*isGenerator*/ false, /*isAsync*/ false);

        return AddJsDocComment(FinishNode(node));
    }


    public bool IsClassMemberModifier(TypeScriptSyntaxKind idToken) => idToken switch
    {
        TypeScriptSyntaxKind.PublicKeyword or TypeScriptSyntaxKind.PrivateKeyword or TypeScriptSyntaxKind.ProtectedKeyword or TypeScriptSyntaxKind.StaticKeyword or TypeScriptSyntaxKind.ReadonlyKeyword => true,
        _ => false,
    };


    public bool IsClassMemberStart()
    {
        var idToken = TypeScriptSyntaxKind.Unknown; // null;
        if (Token is TypeScriptSyntaxKind.AtToken)
        {

            return true;
        }
        while (IsModifierKind(Token))
        {

            idToken = Token;
            if (IsClassMemberModifier(idToken))
            {

                return true;
            }


            _ = NextToken;
        }
        if (Token is TypeScriptSyntaxKind.AsteriskToken)
        {

            return true;
        }
        if (IsLiteralPropertyName())
        {

            idToken = Token;

            _ = NextToken;
        }
        if (Token is TypeScriptSyntaxKind.OpenBracketToken)
        {

            return true;
        }
        if (idToken is not TypeScriptSyntaxKind.Unknown)  // null)
        {
            if (!IsKeyword(idToken) || idToken is TypeScriptSyntaxKind.SetKeyword || idToken is TypeScriptSyntaxKind.GetKeyword)
            {

                return true;
            }
            return Token switch
            {
                TypeScriptSyntaxKind.OpenParenToken or TypeScriptSyntaxKind.LessThanToken or TypeScriptSyntaxKind.ColonToken or TypeScriptSyntaxKind.EqualsToken or TypeScriptSyntaxKind.QuestionToken => true,// Not valid, but permitted so that it gets caught later on.
                _ => CanParseSemicolon(),// Covers
                                         //  - Semicolons     (declaration termination)
                                         //  - Closing braces (end-of-class, must be declaration)
                                         //  - End-of-files   (not valid, but permitted so that it gets caught later on)
                                         //  - Line-breaks    (enabling *automatic semicolon insertion*)
            };
        }


        return false;
    }


    public NodeArray<Decorator> ParseDecorators()
    {
        NodeArray<Decorator> decorators = null;
        while (true)
        {
            var decoratorStart = NodePos;
            if (!ParseOptional(TypeScriptSyntaxKind.AtToken))
            {

                break;
            }
            var decorator = new Decorator
            {
                Pos = decoratorStart,
                Expression = DoInDecoratorContext(ParseLeftHandSideExpressionOrHigher)
            };

            FinishNode(decorator);
            if (decorators is null)
            {

                decorators = CreateList<Decorator>();
                decorators.Pos = decoratorStart;
                decorators.Add(decorator);
            }
            else
            {

                decorators.Add(decorator);
            }
        }
        if (decorators != null)
        {

            decorators.End = NodeEnd;
        }

        return decorators;
    }


    public NodeArray<Modifier> ParseModifiers(bool? permitInvalidConstAsModifier = null)
    {
        var modifiers = CreateList<Modifier>();
        while (true)
        {
            var modifierStart = Scanner.StartPos;
            var modifierKind = Token;
            if (Token is TypeScriptSyntaxKind.ConstKeyword && permitInvalidConstAsModifier == true)
            {
                if (!TryParse(NextTokenIsOnSameLineAndCanFollowModifier))
                {

                    break;
                }
            }
            else
            {
                if (!ParseAnyContextualModifier())
                {

                    break;
                }
            }
            var modifier = FinishNode(new Modifier { Kind = modifierKind, Pos = modifierStart }); //(Modifier)createNode(modifierKind, modifierStart)
            if (modifiers is null)
            {

                modifiers = CreateList<Modifier>();
                modifiers.Pos = modifierStart;
                modifiers.Add(modifier);
            }
            else
            {

                modifiers.Add(modifier);
            }
        }
        if (modifiers != null)
        {

            modifiers.End = Scanner.StartPos;
        }

        return modifiers;
    }


    public NodeArray<Modifier> ParseModifiersForArrowFunction()
    {
        NodeArray<Modifier> modifiers = null;
        if (Token is TypeScriptSyntaxKind.AsyncKeyword)
        {
            var modifierStart = Scanner.StartPos;
            var modifierKind = Token;

            _ = NextToken;
            var modifier = FinishNode(new Modifier { Kind = modifierKind, Pos = modifierStart });
            //finishNode((Modifier)createNode(modifierKind, modifierStart));

            modifiers = CreateList<Modifier>();
            modifiers.Pos = modifierStart;
            modifiers.Add(modifier);

            modifiers.End = Scanner.StartPos;
        }


        return modifiers;
    }


    public IClassElement? ParseClassElement()
    {
        if (Token is TypeScriptSyntaxKind.SemicolonToken)
        {
            var result = new SemicolonClassElement() { Pos = Scanner.StartPos };

            _ = NextToken;

            return FinishNode(result);
        }
        var fullStart = NodePos;
        var decorators = ParseDecorators();
        var modifiers = ParseModifiers(/*permitInvalidConstAsModifier*/ true);
        var accessor = TryParseAccessorDeclaration(fullStart, decorators, modifiers);
        if (accessor != null)
        {

            return accessor;
        }
        if (Token is TypeScriptSyntaxKind.ConstructorKeyword)
        {

            return ParseConstructorDeclaration(fullStart, decorators, modifiers);
        }
        if (IsIndexSignature())
        {

            return ParseIndexSignatureDeclaration(fullStart, decorators, modifiers);
        }
        if (TokenIsIdentifierOrKeyword(Token) ||
                        Token is TypeScriptSyntaxKind.StringLiteral ||
                        Token is TypeScriptSyntaxKind.NumericLiteral ||
                        Token is TypeScriptSyntaxKind.AsteriskToken ||
                        Token is TypeScriptSyntaxKind.OpenBracketToken)
        {


            return ParsePropertyOrMethodDeclaration(fullStart, decorators, modifiers);
        }
        if (decorators?.Any() == true || modifiers?.Any() == true)
        {
            var name = (Identifier)CreateMissingNode<Identifier>(TypeScriptSyntaxKind.Identifier, /*reportAtCurrentPosition*/ true, Diagnostics.Declaration_expected);

            return ParsePropertyDeclaration(fullStart, decorators, modifiers, name, /*questionToken*/ null);
        }


        // 'isClassMemberStart' should have hinted not to attempt parsing.
        return null;
    }

    public ClassExpression ParseClassExpression()
    {
        var node = new ClassExpression();
        var declaration = node as IClassLikeDeclaration;

        declaration.Pos = Scanner.StartPos;

        ParseExpected(TypeScriptSyntaxKind.ClassKeyword);

        declaration.Name = ParseNameOfClassDeclarationOrExpression();
        declaration.TypeParameters = ParseTypeParameters();
        declaration.HeritageClauses = ParseHeritageClauses();

        if (ParseExpected(TypeScriptSyntaxKind.OpenBraceToken))
        {
            declaration.Members = ParseClassMembers();

            ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);
        }
        else
        {
            declaration.Members = new NodeArray<IClassElement>();
        }

        return AddJsDocComment(FinishNode(node));
    }

    public ClassDeclaration ParseClassDeclaration(
        int fullStart,
        NodeArray<Decorator> decorators,
        NodeArray<Modifier> modifiers)
    {
        var node = new ClassDeclaration();
        var declaration = node as IClassLikeDeclaration;

        declaration.Pos = fullStart;
        declaration.Decorators = decorators;
        declaration.Modifiers = modifiers;

        ParseExpected(TypeScriptSyntaxKind.ClassKeyword);

        declaration.Name = ParseNameOfClassDeclarationOrExpression();
        declaration.TypeParameters = ParseTypeParameters();
        declaration.HeritageClauses = ParseHeritageClauses();

        if (ParseExpected(TypeScriptSyntaxKind.OpenBraceToken))
        {
            declaration.Members = ParseClassMembers();
            ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);
        }
        else
        {
            declaration.Members = new NodeArray<IClassElement>();
        }

        return AddJsDocComment(FinishNode(node));
    }

    public Identifier? ParseNameOfClassDeclarationOrExpression() =>
        // implements is a future reserved word so
        // 'class implements' might mean either
        // - class expression with omitted name, 'implements' starts heritage clause
        // - class with name 'implements'
        // 'isImplementsClause' helps to disambiguate between these two cases
        IsIdentifier() && !IsImplementsClause()
            ? ParseIdentifier()
            : null;

    public bool IsImplementsClause() =>
        Token is TypeScriptSyntaxKind.ImplementsKeyword &&
        LookAhead(NextTokenIsIdentifierOrKeyword);

    public NodeArray<HeritageClause>? ParseHeritageClauses() =>
        IsHeritageClause()
            ? ParseList(TypeScript.ParsingContext.HeritageClauses, ParseHeritageClause)
            : null;

    public HeritageClause? ParseHeritageClause()
    {
        var syntaxKind = Token;
        if (syntaxKind is TypeScriptSyntaxKind.ExtendsKeyword or TypeScriptSyntaxKind.ImplementsKeyword)
        {
            var node = new HeritageClause
            {
                Token = syntaxKind
            };
            var nodeLike = node as INode;
            nodeLike.Pos = Scanner.StartPos;

            _ = NextToken;

            node.Types = ParseDelimitedList(
                TypeScript.ParsingContext.HeritageClauseElement,
                ParseExpressionWithTypeArguments);

            return FinishNode(node);
        }

        return null;
    }


    public ExpressionWithTypeArguments ParseExpressionWithTypeArguments()
    {
        var node = new ExpressionWithTypeArguments
        {
            Pos = Scanner.StartPos,
            Expression = ParseLeftHandSideExpressionOrHigher()
        };
        if (Token is TypeScriptSyntaxKind.LessThanToken)
        {

            node.TypeArguments = ParseBracketedList(ParsingContext.TypeArguments, ParseType, TypeScriptSyntaxKind.LessThanToken, TypeScriptSyntaxKind.GreaterThanToken);
        }


        return FinishNode(node);
    }


    public bool IsHeritageClause() => Token is TypeScriptSyntaxKind.ExtendsKeyword || Token is TypeScriptSyntaxKind.ImplementsKeyword;


    public NodeArray<IClassElement> ParseClassMembers() => ParseList2(ParsingContext.ClassMembers, ParseClassElement);


    public InterfaceDeclaration ParseInterfaceDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new InterfaceDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };

        ParseExpected(TypeScriptSyntaxKind.InterfaceKeyword);

        node.Name = ParseIdentifier();
        node.TypeParameters = ParseTypeParameters();
        node.HeritageClauses = ParseHeritageClauses();
        node.Members = ParseObjectTypeMembers();

        return AddJsDocComment(FinishNode(node));
    }


    public TypeAliasDeclaration ParseTypeAliasDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new TypeAliasDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };

        ParseExpected(TypeScriptSyntaxKind.TypeKeyword);

        node.Name = ParseIdentifier();
        node.TypeParameters = ParseTypeParameters();

        ParseExpected(TypeScriptSyntaxKind.EqualsToken);

        node.Type = ParseType();

        ParseSemicolon();

        return AddJsDocComment(FinishNode(node));
    }


    public EnumMember ParseEnumMember()
    {
        var node = new EnumMember
        {
            Pos = Scanner.StartPos,
            Name = ParsePropertyName(),
            Initializer = AllowInAnd(ParseNonParameterInitializer)
        };

        return AddJsDocComment(FinishNode(node));
    }


    public EnumDeclaration ParseEnumDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new EnumDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };

        ParseExpected(TypeScriptSyntaxKind.EnumKeyword);

        node.Name = ParseIdentifier();
        if (ParseExpected(TypeScriptSyntaxKind.OpenBraceToken))
        {
            node.Members = ParseDelimitedList(ParsingContext.EnumMembers, ParseEnumMember);
            ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);
        }
        else
        {
            node.Members = CreateMissingList<EnumMember>();
        }

        return AddJsDocComment(FinishNode(node));
    }


    public ModuleBlock ParseModuleBlock()
    {
        var node = new ModuleBlock() { Pos = Scanner.StartPos };
        if (ParseExpected(TypeScriptSyntaxKind.OpenBraceToken))
        {
            node.Statements = ParseList2(ParsingContext.BlockStatements, ParseStatement);

            ParseExpected(TypeScriptSyntaxKind.CloseBraceToken);
        }
        else
        {
            node.Statements = new NodeArray<IStatement>(); // createMissingList<Statement>();
        }

        return FinishNode(node);
    }


    public ModuleDeclaration ParseModuleOrNamespaceDeclaration(
        int fullStart,
        NodeArray<Decorator>? decorators,
        NodeArray<Modifier>? modifiers,
        NodeFlags flags)
    {
        var node = new ModuleDeclaration() { Pos = fullStart };
        var namespaceFlag = flags & NodeFlags.Namespace;

        node.Decorators = decorators;
        node.Modifiers = modifiers;
        node.Flags |= flags;
        node.Name = ParseIdentifier();
        node.Body = ParseOptional(TypeScriptSyntaxKind.DotToken)
            ? (Node)ParseModuleOrNamespaceDeclaration(NodePos, decorators: null, modifiers: null, NodeFlags.NestedNamespace | namespaceFlag)
            : ParseModuleBlock();

        return AddJsDocComment(FinishNode(node));
    }


    public ModuleDeclaration ParseAmbientExternalModuleDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new ModuleDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };

        if (Token is TypeScriptSyntaxKind.GlobalKeyword)
        {

            // parse 'global' as name of global scope augmentation
            node.Name = ParseIdentifier();
            node.Flags |= NodeFlags.GlobalAugmentation;
        }
        else
        {
            node.Name = (StringLiteral)ParseLiteralNode(/*internName*/ true);
        }

        if (Token is TypeScriptSyntaxKind.OpenBraceToken)
        {
            node.Body = ParseModuleBlock();
        }
        else
        {
            ParseSemicolon();
        }

        return FinishNode(node);
    }


    public ModuleDeclaration ParseModuleDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        NodeFlags flags = 0;
        if (Token is TypeScriptSyntaxKind.GlobalKeyword)
        {
            // global augmentation
            return ParseAmbientExternalModuleDeclaration(fullStart, decorators, modifiers);
        }
        else if (ParseOptional(TypeScriptSyntaxKind.NamespaceKeyword))
        {

            flags |= NodeFlags.Namespace;
        }
        else
        {
            ParseExpected(TypeScriptSyntaxKind.ModuleKeyword);
            if (Token is TypeScriptSyntaxKind.StringLiteral)
            {

                return ParseAmbientExternalModuleDeclaration(fullStart, decorators, modifiers);
            }
        }

        return ParseModuleOrNamespaceDeclaration(fullStart, decorators, modifiers, flags);
    }


    public bool IsExternalModuleReference() => Token is TypeScriptSyntaxKind.RequireKeyword &&
            LookAhead(NextTokenIsOpenParen);


    public bool NextTokenIsOpenParen() => NextToken is TypeScriptSyntaxKind.OpenParenToken;


    public bool NextTokenIsSlash() => NextToken is TypeScriptSyntaxKind.SlashToken;


    public NamespaceExportDeclaration ParseNamespaceExportDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var exportDeclaration = new NamespaceExportDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };

        ParseExpected(TypeScriptSyntaxKind.AsKeyword);
        ParseExpected(TypeScriptSyntaxKind.NamespaceKeyword);

        exportDeclaration.Name = ParseIdentifier();

        ParseSemicolon();

        return FinishNode(exportDeclaration);
    }

    public IStatement ParseImportDeclarationOrImportEqualsDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        ParseExpected(TypeScriptSyntaxKind.ImportKeyword);
        var afterImportPos = Scanner.StartPos;
        Identifier? identifier = null;
        if (IsIdentifier())
        {
            identifier = ParseIdentifier();
            if (Token is not TypeScriptSyntaxKind.CommaToken && Token is not TypeScriptSyntaxKind.FromKeyword)
            {
                return ParseImportEqualsDeclaration(fullStart, decorators, modifiers, identifier);
            }
        }
        var importDeclaration = new ImportDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };

        if (identifier != null ||
            Token is TypeScriptSyntaxKind.AsteriskToken ||
            Token is TypeScriptSyntaxKind.OpenBraceToken)
        {
            importDeclaration.ImportClause = ParseImportClause(identifier, afterImportPos);

            ParseExpected(TypeScriptSyntaxKind.FromKeyword);
        }

        importDeclaration.ModuleSpecifier = ParseModuleSpecifier();

        ParseSemicolon();

        return FinishNode(importDeclaration);
    }

    public ImportEqualsDeclaration ParseImportEqualsDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers, Identifier identifier)
    {
        var importEqualsDeclaration = new ImportEqualsDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers,
            Name = identifier
        };

        ParseExpected(TypeScriptSyntaxKind.EqualsToken);

        importEqualsDeclaration.ModuleReference = ParseModuleReference();

        ParseSemicolon();

        return AddJsDocComment(FinishNode(importEqualsDeclaration));
    }


    public ImportClause ParseImportClause(Identifier identifier, int fullStart)
    {
        var importClause = new ImportClause() { Pos = fullStart };
        if (identifier != null)
        {

            // ImportedDefaultBinding:
            //  ImportedBinding
            importClause.Name = identifier;
        }
        if (importClause.Name is null ||
                        ParseOptional(TypeScriptSyntaxKind.CommaToken))
        {

            importClause.NamedBindings = Token is TypeScriptSyntaxKind.AsteriskToken ? ParseNamespaceImport() : (INamedImportBindings)ParseNamedImportsOrExports(TypeScriptSyntaxKind.NamedImports);
        }


        return FinishNode(importClause);
    }


    public INode ParseModuleReference() => IsExternalModuleReference()
            ? ParseExternalModuleReference()
            : ParseEntityName(/*allowReservedWords*/ false);


    public ExternalModuleReference ParseExternalModuleReference()
    {
        var node = new ExternalModuleReference() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.RequireKeyword);

        ParseExpected(TypeScriptSyntaxKind.OpenParenToken);

        node.Expression = ParseModuleSpecifier();

        ParseExpected(TypeScriptSyntaxKind.CloseParenToken);

        return FinishNode(node);
    }


    public IExpression ParseModuleSpecifier()
    {
        if (Token is TypeScriptSyntaxKind.StringLiteral)
        {
            var result = ParseLiteralNode();

            InternIdentifier(((LiteralExpression)result).Text);

            return result;
        }
        else
        {

            // We allow arbitrary expressions here, even though the grammar only allows string
            // literals.  We check to ensure that it is only a string literal later in the grammar
            // check pass.
            return ParseExpression();
        }
    }


    public NamespaceImport ParseNamespaceImport()
    {
        var namespaceImport = new NamespaceImport() { Pos = Scanner.StartPos };

        ParseExpected(TypeScriptSyntaxKind.AsteriskToken);

        ParseExpected(TypeScriptSyntaxKind.AsKeyword);

        namespaceImport.Name = ParseIdentifier();

        return FinishNode(namespaceImport);
    }


    //public NamedImports parseNamedImportsOrExports(TypeScriptSyntaxKind.NamedImports kind)
    //{
    //}


    //public NamedExports parseNamedImportsOrExports(TypeScriptSyntaxKind.NamedExports kind)
    //{
    //}


    public INamedImportsOrExports ParseNamedImportsOrExports(TypeScriptSyntaxKind kind)
    {
        if (Kind is TypeScriptSyntaxKind.NamedImports)
        {
            var node = new NamedImports
            {
                Pos = Scanner.StartPos,
                Elements = ParseBracketedList<ImportSpecifier>(ParsingContext.ImportOrExportSpecifiers, ParseImportSpecifier,
               TypeScriptSyntaxKind.OpenBraceToken, TypeScriptSyntaxKind.CloseBraceToken)
            };

            return FinishNode(node);
        }
        else
        {
            var node = new NamedExports
            {
                Pos = Scanner.StartPos,
                Elements = ParseBracketedList<ExportSpecifier>(ParsingContext.ImportOrExportSpecifiers, ParseExportSpecifier,
               TypeScriptSyntaxKind.OpenBraceToken, TypeScriptSyntaxKind.CloseBraceToken)
            };

            return FinishNode(node);
        }
        //var node = new NamedImports | NamedExports();


        //// NamedImports:
        ////  { }
        ////  { ImportsList }
        ////  { ImportsList, }

        //// ImportsList:
        ////  ImportSpecifier
        ////  ImportsList, ImportSpecifier
        //node.elements = (List<ImportSpecifier> | List<ExportSpecifier>)parseBracketedList(ParsingContext.ImportOrExportSpecifiers,
        //    Kind is TypeScriptSyntaxKind.NamedImports ? parseImportSpecifier : parseExportSpecifier,
        //    TypeScriptSyntaxKind.OpenBraceToken, TypeScriptSyntaxKind.CloseBraceToken);

        //return finishNode(node);
    }


    public ExportSpecifier ParseExportSpecifier()
    {
        var node = new ExportSpecifier { Pos = Scanner.StartPos };
        var checkIdentifierIsKeyword = IsKeyword(Token) && !IsIdentifier();
        var checkIdentifierStart = Scanner.TokenPos;
        var checkIdentifierEnd = Scanner.TextPos;
        var identifierName = ParseIdentifierName();
        if (Token is TypeScriptSyntaxKind.AsKeyword)
        {

            node.PropertyName = identifierName;

            ParseExpected(TypeScriptSyntaxKind.AsKeyword);

            checkIdentifierIsKeyword = IsKeyword(Token) && !IsIdentifier();

            checkIdentifierStart = Scanner.TokenPos;

            checkIdentifierEnd = Scanner.TextPos;

            node.Name = ParseIdentifierName();
        }
        else
        {

            node.Name = identifierName;
        }

        return FinishNode(node);
        //return parseImportOrExportSpecifier(TypeScriptSyntaxKind.ExportSpecifier);
    }


    public ImportSpecifier ParseImportSpecifier()
    {
        var node = new ImportSpecifier() { Pos = Scanner.StartPos };
        var checkIdentifierIsKeyword = IsKeyword(Token) && !IsIdentifier();
        var checkIdentifierStart = Scanner.TokenPos;
        var checkIdentifierEnd = Scanner.TextPos;
        var identifierName = ParseIdentifierName();
        if (Token is TypeScriptSyntaxKind.AsKeyword)
        {

            node.PropertyName = identifierName;

            ParseExpected(TypeScriptSyntaxKind.AsKeyword);

            checkIdentifierIsKeyword = IsKeyword(Token) && !IsIdentifier();

            checkIdentifierStart = Scanner.TokenPos;

            checkIdentifierEnd = Scanner.TextPos;

            node.Name = ParseIdentifierName();
        }
        else
        {

            node.Name = identifierName;
        }
        if (/*Kind is TypeScriptSyntaxKind.ImportSpecifier && */checkIdentifierIsKeyword)
        {

            // Report error identifier expected
            ParseErrorAtPosition(checkIdentifierStart, checkIdentifierEnd - checkIdentifierStart, Diagnostics.Identifier_expected);
        }

        return FinishNode(node);

        //return parseImportOrExportSpecifier(TypeScriptSyntaxKind.ImportSpecifier);
    }


    //public ImportOrExportSpecifier parseImportOrExportSpecifier(TypeScriptSyntaxKind kind)
    //{
    //    var node = new ImportSpecifier { pos = scanner.getStartPos() };
    //    var checkIdentifierIsKeyword = isKeyword(token()) && !isIdentifier();
    //    var checkIdentifierStart = scanner.getTokenPos();
    //    var checkIdentifierEnd = scanner.getTextPos();
    //    var identifierName = parseIdentifierName();
    //    if (token() == TypeScriptSyntaxKind.AsKeyword)
    //    {

    //        node.propertyName = identifierName;

    //        parseExpected(TypeScriptSyntaxKind.AsKeyword);

    //        checkIdentifierIsKeyword = isKeyword(token()) && !isIdentifier();

    //        checkIdentifierStart = scanner.getTokenPos();

    //        checkIdentifierEnd = scanner.getTextPos();

    //        node.name = parseIdentifierName();
    //    }
    //    else
    //    {

    //        node.name = identifierName;
    //    }
    //    if (Kind is TypeScriptSyntaxKind.ImportSpecifier && checkIdentifierIsKeyword)
    //    {

    //        // Report error identifier expected
    //        parseErrorAtPosition(checkIdentifierStart, checkIdentifierEnd - checkIdentifierStart, Diagnostics.Identifier_expected);
    //    }

    //    return finishNode(node);
    //}


    public ExportDeclaration ParseExportDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new ExportDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,

            Modifiers = modifiers
        };
        if (ParseOptional(TypeScriptSyntaxKind.AsteriskToken))
        {

            ParseExpected(TypeScriptSyntaxKind.FromKeyword);

            node.ModuleSpecifier = ParseModuleSpecifier();
        }
        else
        {

            node.ExportClause = (NamedExports)ParseNamedImportsOrExports(TypeScriptSyntaxKind.NamedExports);
            if (Token is TypeScriptSyntaxKind.FromKeyword || (Token is TypeScriptSyntaxKind.StringLiteral && !Scanner.HasPrecedingLineBreak))
            {

                ParseExpected(TypeScriptSyntaxKind.FromKeyword);

                node.ModuleSpecifier = ParseModuleSpecifier();
            }
        }

        ParseSemicolon();

        return FinishNode(node);
    }


    public ExportAssignment ParseExportAssignment(
        int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new ExportAssignment
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };

        if (ParseOptional(TypeScriptSyntaxKind.EqualsToken))
        {
            node.IsExportEquals = true;
        }
        else
        {
            ParseExpected(TypeScriptSyntaxKind.DefaultKeyword);
        }

        node.Expression = ParseAssignmentExpressionOrHigher();
        ParseSemicolon();

        return FinishNode(node);
    }

    public void SetExternalModuleIndicator(SourceFile sourceFile) =>
        sourceFile.ExternalModuleIndicator =
            sourceFile.Statements.FirstOrDefault(node =>
                HasModifier(node, ModifierFlags.Export) ||
            (node.Kind is TypeScriptSyntaxKind.ImportEqualsDeclaration &&
            (node as ImportEqualsDeclaration)?.ModuleReference?.Kind is TypeScriptSyntaxKind.ExternalModuleReference)
            || node.Kind is TypeScriptSyntaxKind.ImportDeclaration
            || node.Kind is TypeScriptSyntaxKind.ExportAssignment
            || node.Kind is TypeScriptSyntaxKind.ExportDeclaration);
}
