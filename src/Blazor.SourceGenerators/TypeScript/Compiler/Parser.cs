// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
using System.Diagnostics;
using Blazor.SourceGenerators.TypeScript.Types;
using static Blazor.SourceGenerators.TypeScript.Compiler.Core;
using static Blazor.SourceGenerators.TypeScript.Compiler.Scanner;
using static Blazor.SourceGenerators.TypeScript.Compiler.Ts;
using static Blazor.SourceGenerators.TypeScript.Compiler.Utilities;
using ParsingContextEnum = Blazor.SourceGenerators.TypeScript.Types.ParsingContext;
using SyntaxKind = Blazor.SourceGenerators.TypeScript.Types.TypeScriptSyntaxKind;

namespace Blazor.SourceGenerators.TypeScript.Compiler;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style", "IDE0007:Use implicit type", Justification = "Leave explicitly typed for future NRT work.")]
public sealed class Parser
{

    public Scanner Scanner = new(ScriptTarget.Latest, skipTrivia: true, LanguageVariant.Standard, null, null);

    public NodeFlags DisallowInAndDecoratorContext = NodeFlags.DisallowInContext | NodeFlags.DecoratorContext;

    public NodeFlags ContextFlags;

    public bool ParseErrorBeforeNextFinishedNode = false;

    public SourceFile SourceFile;

    public List<TypeScriptDiagnostic> ParseDiagnostics;

    public object SyntaxCursor;

    public SyntaxKind CurrentToken;

    public string SourceText;

    public int NodeCount;

    public List<string> Identifiers;

    public int IdentifierCount;

    public int ParsingContext;

    public JsDocParser JsDocParser;

    public Parser() => JsDocParser = new JsDocParser(this);

    public SourceFile ParseSourceFile(
        string fileName,
        string sourceText,
        ScriptTarget? languageVersion,
        object syntaxCursor,
        bool setParentNodes,
        ScriptKind scriptKind)
    {
        scriptKind = EnsureScriptKind(fileName, scriptKind);
        var langVersion = languageVersion ?? ScriptTarget.Latest;
        InitializeState(sourceText, langVersion, syntaxCursor, scriptKind);
        var result = ParseSourceFileWorker(fileName, langVersion, setParentNodes, scriptKind);
        ClearState();
        return result;
    }

    public IEntityName ParseIsolatedEntityName(string content, ScriptTarget languageVersion)
    {
        InitializeState(content, languageVersion, null, ScriptKind.Js);
        // Prime the scanner.
        NextToken();
        var entityName = ParseEntityName(true);
        var isInvalid = CurrentToken == SyntaxKind.EndOfFileToken && !ParseDiagnostics.Any();
        ClearState();
        return isInvalid ? entityName : null;
    }

    public LanguageVariant GetLanguageVariant(ScriptKind scriptKind) =>
        // .tsx and .jsx files are treated as jsx language variant.
        scriptKind is ScriptKind.Tsx or ScriptKind.Jsx or ScriptKind.Js
            ? LanguageVariant.Jsx
            : LanguageVariant.Standard;

    public void InitializeState(string sourceText, ScriptTarget languageVersion, object _syntaxCursor, ScriptKind scriptKind)
    {
        SourceText = sourceText;
        SyntaxCursor = _syntaxCursor;
        ParseDiagnostics = new();
        ParsingContext = 0;
        Identifiers = new();
        IdentifierCount = 0;
        NodeCount = 0;
        ContextFlags = scriptKind is ScriptKind.Js or ScriptKind.Jsx ? NodeFlags.JavaScriptFile : NodeFlags.None;
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

    public SourceFile ParseSourceFileWorker(string fileName, ScriptTarget languageVersion, bool setParentNodes, ScriptKind scriptKind)
    {
        SourceFile = CreateSourceFile(fileName, languageVersion, scriptKind);
        SourceFile.Flags = ContextFlags;
        // Prime the scanner.
        NextToken();
        ProcessReferenceComments(SourceFile);
        SourceFile.Statements = ParseList2(ParsingContextEnum.SourceElements, ParseStatement);
        Debug.Assert(CurrentToken == SyntaxKind.EndOfFileToken);
        SourceFile.EndOfFileToken = ParseTokenNode<EndOfFileToken>(CurrentToken);
        SetExternalModuleIndicator(SourceFile);
        SourceFile.NodeCount = NodeCount;
        SourceFile.IdentifierCount = IdentifierCount;
        SourceFile.Identifiers = Identifiers;
        SourceFile.ParseDiagnostics = ParseDiagnostics;
        if (setParentNodes)
        {
            FixupParentReferences(SourceFile);
        }
        return SourceFile;
    }

    public T AddJsDocComment<T>(T node) where T : INode
    {
        var comments = GetJsDocCommentRanges(node, SourceFile.Text);
        if (comments.Any())
        {
            foreach (var comment in comments)
            {
                var jsDoc = JsDocParser.ParseJsDocComment(node, comment.Pos, comment.End - comment.Pos);
                if (jsDoc == null)
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
        INode parent = rootNode;
        ForEachChild(rootNode, visitNode);
        return;
        INode visitNode(INode n)
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

    public SourceFile CreateSourceFile(string fileName, ScriptTarget languageVersion, ScriptKind scriptKind)
    {
        //var sourceFile = (SourceFile)new SourceFileConstructor(SyntaxKind.SourceFile,  0,  sourceText.length);
        var sourceFile = new SourceFile { Pos = 0, End = SourceText.Length };
        NodeCount++;
        sourceFile.Text = SourceText;
        sourceFile.BindDiagnostics = new();
        sourceFile.LanguageVersion = languageVersion;
        sourceFile.FileName = NormalizePath(fileName);
        sourceFile.LanguageVariant = GetLanguageVariant(scriptKind);
        sourceFile.IsDeclarationFile = FileExtensionIs(sourceFile.FileName, ".d.ts");
        sourceFile.ScriptKind = scriptKind;
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

    public void SetDisallowInContext(bool val) => SetContextFlag(val, NodeFlags.DisallowInContext);

    public void SetYieldContext(bool val) => SetContextFlag(val, NodeFlags.YieldContext);

    public void SetDecoratorContext(bool val) => SetContextFlag(val, NodeFlags.DecoratorContext);

    public void SetAwaitContext(bool val) => SetContextFlag(val, NodeFlags.AwaitContext);

    public T DoOutsideOfContext<T>(NodeFlags context, Func<T> func)
    {
        var contextFlagsToClear = context & ContextFlags;
        if (contextFlagsToClear != 0)
        {
            // clear the requested context flags
            SetContextFlag(false, contextFlagsToClear);
            var result = func();
            // restore the context flags we just cleared
            SetContextFlag(true, contextFlagsToClear);
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
            SetContextFlag(true, contextFlagsToSet);
            var result = func();
            // reset the context flags we just set
            SetContextFlag(false, contextFlagsToSet);
            return result;
        }
        // no need to do anything special as we are already in all of the requested contexts
        return func();
    }

    public T AllowInAnd<T>(Func<T> func) => DoOutsideOfContext(NodeFlags.DisallowInContext, func);

    public T DisallowInAnd<T>(Func<T> func) => DoInsideOfContext(NodeFlags.DisallowInContext, func);

    public T DoInYieldContext<T>(Func<T> func) => DoInsideOfContext(NodeFlags.YieldContext, func);

    public T DoInDecoratorContext<T>(Func<T> func) => DoInsideOfContext(NodeFlags.DecoratorContext, func);

    public T DoInAwaitContext<T>(Func<T> func) => DoInsideOfContext(NodeFlags.AwaitContext, func);

    public T DoOutsideOfAwaitContext<T>(Func<T> func) => DoOutsideOfContext(NodeFlags.AwaitContext, func);

    public T DoInYieldAndAwaitContext<T>(Func<T> func) => DoInsideOfContext(NodeFlags.YieldContext | NodeFlags.AwaitContext, func);

    public bool InContext(NodeFlags flags) => (ContextFlags & flags) != 0;

    public bool InYieldContext() => InContext(NodeFlags.YieldContext);

    public bool InDisallowInContext() => InContext(NodeFlags.DisallowInContext);

    public bool InDecoratorContext() => InContext(NodeFlags.DecoratorContext);

    public bool InAwaitContext() => InContext(NodeFlags.AwaitContext);

    public void ParseErrorAtCurrentToken(DiagnosticMessage? message, object arg0 = null)
    {
        var start = Scanner.TokenPos;
        var length = Scanner.TextPos - start;
        ParseErrorAtPosition(start, length, message, arg0);
    }

    public void ParseErrorAtPosition(int start, int length, DiagnosticMessage? message, object arg0 = null)
    {
        var lastError = LastOrUndefined(ParseDiagnostics);
        if (lastError == null || start != lastError.Start)
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

    public int GetNodePos() => Scanner.StartPos;

    public int GetNodeEnd() => Scanner.StartPos;

    public SyntaxKind NextToken() =>
        CurrentToken = Scanner.Scan();

    public SyntaxKind ReScanGreaterToken() =>
        CurrentToken = Scanner.ReScanGreaterToken();

    public SyntaxKind ReScanSlashToken() =>
        CurrentToken = Scanner.ReScanSlashToken();

    public SyntaxKind ReScanTemplateToken() =>
        CurrentToken = Scanner.ReScanTemplateToken();

    public SyntaxKind ScanJsxIdentifier() =>
        CurrentToken = Scanner.ScanJsxIdentifier();

    public SyntaxKind ScanJsxText() =>
        CurrentToken = Scanner.ScanJsxToken();

    public SyntaxKind ScanJsxAttributeValue() =>
        CurrentToken = Scanner.ScanJsxAttributeValue();

    public T SpeculationHelper<T>(Func<T> callback, bool isLookAhead)
    {
        var saveToken = CurrentToken;
        var saveParseDiagnosticsLength = ParseDiagnostics.Count;
        var saveParseErrorBeforeNextFinishedNode = ParseErrorBeforeNextFinishedNode;
        var saveContextFlags = ContextFlags;
        var result = isLookAhead
                        ? Scanner.LookAhead(callback)
                        : Scanner.TryScan(callback);
        Debug.Assert(saveContextFlags == ContextFlags);
        if (result == null || ((result is bool) && Convert.ToBoolean(result) == false) || isLookAhead)
        {
            CurrentToken = saveToken;
            //parseDiagnostics.Count = saveParseDiagnosticsLength;
            ParseDiagnostics = ParseDiagnostics.Take(saveParseDiagnosticsLength).ToList();
            ParseErrorBeforeNextFinishedNode = saveParseErrorBeforeNextFinishedNode;
        }
        return result;
    }

    public T LookAhead<T>(Func<T> callback) => SpeculationHelper(callback, true);

    public T TryParse<T>(Func<T> callback) => SpeculationHelper(callback, false);

    public bool IsIdentifier() => CurrentToken switch
    {
        SyntaxKind.Identifier => true,
        SyntaxKind.YieldKeyword when InYieldContext() => false,
        SyntaxKind.AwaitKeyword when InAwaitContext() => false,
        > SyntaxKind.LastReservedWord => true,
        _ => false
    };

    public bool ParseExpected(SyntaxKind kind, DiagnosticMessage? diagnosticMessage = null, bool shouldAdvance = true)
    {
        if (CurrentToken == kind)
        {
            if (shouldAdvance)
            {
                NextToken();
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

    public bool ParseOptional(SyntaxKind t)
    {
        if (CurrentToken == t)
        {
            NextToken();
            return true;
        }
        return false;
    }
    //public GetCurrentToken<TKind> parseOptionalToken<TKind>(TKind t) where TKind : SyntaxKind
    //{
    //}

    public Node ParseOptionalToken<T>(SyntaxKind t) where T : Node => CurrentToken == t ? ParseTokenNode<T>(CurrentToken) : (Node)null;
    //public GetCurrentToken<TKind> parseExpectedToken<TKind>(TKind t, bool reportAtCurrentPosition, DiagnosticMessage diagnosticMessage, object arg0 = null) where TKind : SyntaxKind
    //{
    //}

    public Node ParseExpectedToken<T>(SyntaxKind t, bool reportAtCurrentPosition, DiagnosticMessage diagnosticMessage, object arg0 = null) where T : Node => ParseOptionalToken<T>(t) ??
            CreateMissingNode<T>(t, reportAtCurrentPosition, diagnosticMessage, arg0);

    public T ParseTokenNode<T>(SyntaxKind sk) where T : Node
    {
        var node = (T)Activator.CreateInstance(typeof(T));// new T();
        node.Pos = Scanner.StartPos;
        node.Kind = sk;
        NextToken();
        return FinishNode(node);
    }

    public bool CanParseSemicolon()
    {
        if (CurrentToken == SyntaxKind.SemicolonToken)
        {
            return true;
        }
        // We can parse out an optional semicolon in ASI cases in the following cases.
        return CurrentToken == SyntaxKind.CloseBraceToken || CurrentToken == SyntaxKind.EndOfFileToken || Scanner.HasPrecedingLineBreak;
    }

    public bool ParseSemicolon()
    {
        if (CanParseSemicolon())
        {
            if (CurrentToken == SyntaxKind.SemicolonToken)
            {
                // consume the semicolon if it was explicitly provided.
                NextToken();
            }
            return true;
        }
        else
        {
            return ParseExpected(SyntaxKind.SemicolonToken);
        }
    }

    public NodeArray<T> CreateList<T>(T[] elements = null, int? pos = null)
    {
        var array = elements == null ? new NodeArray<T>() : new NodeArray<T>(elements); // (List<T>)(elements || []);
        if (!(pos >= 0))
        {
            pos = GetNodePos();
        }
        array.Pos = pos;
        array.End = pos;
        return array;
    }

    public T FinishNode<T>(T node, int? end = null) where T : INode
    {
        node.End = end == null ? Scanner.StartPos : (int)end;
        if (ContextFlags != NodeFlags.None)
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

    public Node CreateMissingNode<T>(SyntaxKind kind, bool reportAtCurrentPosition, DiagnosticMessage? diagnosticMessage = null, object arg0 = null) where T : Node
    {
        if (reportAtCurrentPosition)
        {
            ParseErrorAtPosition(Scanner.StartPos, 0, diagnosticMessage, arg0);
        }
        else
        {
            ParseErrorAtCurrentToken(diagnosticMessage, arg0);
        }
        var result = (T)Activator.CreateInstance(typeof(T));
        result.Kind = SyntaxKind.MissingDeclaration;
        result.Pos = Scanner.StartPos;
        //var result = new MissingNode { kind = kind, pos = scanner.StartPos, text = "" }; // createNode(kind, scanner.StartPos);
        //(< Identifier > result).text = ";
        return FinishNode(result);
    }

    public string InternIdentifier(string text)
    {
        text = EscapeIdentifier(text);
        //var identifier = identifiers.get(text);
        if (!Identifiers.Contains(text))// identifier == null)
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
            if (CurrentToken != SyntaxKind.Identifier)
            {
                node.OriginalKeywordKind = CurrentToken;
            }
            node.Text = InternIdentifier(Scanner.TokenValue);
            NextToken();
            return FinishNode(node);
        }
        return (Identifier)CreateMissingNode<Identifier>(SyntaxKind.Identifier, false, diagnosticMessage ?? Diagnostics.Identifier_expected);
    }

    public Identifier ParseIdentifier(DiagnosticMessage? diagnosticMessage = null) => CreateIdentifier(IsIdentifier(), diagnosticMessage);

    public Identifier ParseIdentifierName() => CreateIdentifier(TokenIsIdentifierOrKeyword(CurrentToken));

    public bool IsLiteralPropertyName() => TokenIsIdentifierOrKeyword(CurrentToken) ||
            CurrentToken == SyntaxKind.StringLiteral ||
            CurrentToken == SyntaxKind.NumericLiteral;

    public IPropertyName ParsePropertyNameWorker(bool allowComputedPropertyNames)
    {
        if (CurrentToken == SyntaxKind.StringLiteral || CurrentToken == SyntaxKind.NumericLiteral)
        {
            var le = ParseLiteralNode(internName: true);
            if (le is StringLiteral literal) return literal;
            else if (le is NumericLiteral numericLiteral) return numericLiteral;
            return null;
        }
        return allowComputedPropertyNames && CurrentToken == SyntaxKind.OpenBracketToken
            ? ParseComputedPropertyName()
            : ParseIdentifierName();
    }

    public IPropertyName ParsePropertyName() => ParsePropertyNameWorker(true);

    public IPropertyName ParseSimplePropertyName() => ParsePropertyNameWorker(false);

    public bool IsSimplePropertyName() => CurrentToken == SyntaxKind.StringLiteral || CurrentToken == SyntaxKind.NumericLiteral || TokenIsIdentifierOrKeyword(CurrentToken);

    public ComputedPropertyName ParseComputedPropertyName()
    {
        var node = new ComputedPropertyName() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBracketToken);
        // We parse any expression (including a comma expression). But the grammar
        // says that only an assignment expression is allowed, so the grammar checker
        // will error if it sees a comma expression.
        node.Expression = AllowInAnd(ParseExpression);
        ParseExpected(SyntaxKind.CloseBracketToken);
        return FinishNode(node);
    }

    public bool ParseContextualModifier(SyntaxKind t) => CurrentToken == t && TryParse(NextTokenCanFollowModifier);

    public bool NextTokenIsOnSameLineAndCanFollowModifier()
    {
        NextToken();
        return !Scanner.HasPrecedingLineBreak && CanFollowModifier();
    }

    public bool NextTokenCanFollowModifier()
    {
        if (CurrentToken == SyntaxKind.ConstKeyword)
        {
            // 'const' is only a modifier if followed by 'enum'.
            return NextToken() == SyntaxKind.EnumKeyword;
        }
        if (CurrentToken == SyntaxKind.ExportKeyword)
        {
            NextToken();
            return CurrentToken == SyntaxKind.DefaultKeyword
                ? LookAhead(NextTokenIsClassOrFunctionOrAsync)
                : CurrentToken != SyntaxKind.AsteriskToken && CurrentToken != SyntaxKind.AsKeyword && CurrentToken != SyntaxKind.OpenBraceToken && CanFollowModifier();
        }
        if (CurrentToken == SyntaxKind.DefaultKeyword)
        {
            return NextTokenIsClassOrFunctionOrAsync();
        }
        if (CurrentToken == SyntaxKind.StaticKeyword)
        {
            NextToken();
            return CanFollowModifier();
        }
        return NextTokenIsOnSameLineAndCanFollowModifier();
    }

    public bool ParseAnyContextualModifier() => IsModifierKind(CurrentToken) && TryParse(NextTokenCanFollowModifier);

    public bool CanFollowModifier() => CurrentToken == SyntaxKind.OpenBracketToken
            || CurrentToken == SyntaxKind.OpenBraceToken
            || CurrentToken == SyntaxKind.AsteriskToken
            || CurrentToken == SyntaxKind.DotDotDotToken
            || IsLiteralPropertyName();

    public bool NextTokenIsClassOrFunctionOrAsync()
    {
        NextToken();
        return CurrentToken == SyntaxKind.ClassKeyword || CurrentToken == SyntaxKind.FunctionKeyword ||
            (CurrentToken == SyntaxKind.AsyncKeyword && LookAhead(NextTokenIsFunctionKeywordOnSameLine));
    }

    public bool IsListElement(ParsingContextEnum parsingContext, bool inErrorRecovery)
    {
        var node = CurrentNode(parsingContext);
        if (node != null)
        {
            return true;
        }
        switch (parsingContext)
        {
            case ParsingContextEnum.SourceElements:
            case ParsingContextEnum.BlockStatements:
            case ParsingContextEnum.SwitchClauseStatements:
                // If we're in error recovery, then we don't want to treat ';' as an empty statement.
                // The problem is that ';' can show up in far too many contexts, and if we see one
                // and assume it's a statement, then we may bail out inappropriately from whatever
                // we're parsing.  For example, if we have a semicolon in the middle of a class, then
                // we really don't want to assume the class is over and we're on a statement in the
                // outer module.  We just want to consume and move on.
                return !(CurrentToken == SyntaxKind.SemicolonToken && inErrorRecovery) && IsStartOfStatement();
            case ParsingContextEnum.SwitchClauses:
                return CurrentToken == SyntaxKind.CaseKeyword || CurrentToken == SyntaxKind.DefaultKeyword;
            case ParsingContextEnum.TypeMembers:
                return LookAhead(IsTypeMemberStart);
            case ParsingContextEnum.ClassMembers:
                // We allow semicolons as class elements (as specified by ES6) as long as we're
                // not in error recovery.  If we're in error recovery, we don't want an errant
                // semicolon to be treated as a class member (since they're almost always used
                // for statements.
                return LookAhead(IsClassMemberStart) || (CurrentToken == SyntaxKind.SemicolonToken && !inErrorRecovery);
            case ParsingContextEnum.EnumMembers:
                // Include open bracket computed properties. This technically also lets in indexers,
                // which would be a candidate for improved error reporting.
                return CurrentToken == SyntaxKind.OpenBracketToken || IsLiteralPropertyName();
            case ParsingContextEnum.ObjectLiteralMembers:
                return CurrentToken == SyntaxKind.OpenBracketToken || CurrentToken == SyntaxKind.AsteriskToken || CurrentToken == SyntaxKind.DotDotDotToken || IsLiteralPropertyName();
            case ParsingContextEnum.RestProperties:
                return IsLiteralPropertyName();
            case ParsingContextEnum.ObjectBindingElements:
                return CurrentToken == SyntaxKind.OpenBracketToken || CurrentToken == SyntaxKind.DotDotDotToken || IsLiteralPropertyName();
            case ParsingContextEnum.HeritageClauseElement:
                if (CurrentToken == SyntaxKind.OpenBraceToken)
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
            case ParsingContextEnum.VariableDeclarations:
                //caseLabel12:
                return IsIdentifierOrPattern();
            case ParsingContextEnum.ArrayBindingElements:
                return CurrentToken == SyntaxKind.CommaToken || CurrentToken == SyntaxKind.DotDotDotToken || IsIdentifierOrPattern();
            case ParsingContextEnum.TypeParameters:
                return IsIdentifier();
            case ParsingContextEnum.ArgumentExpressions:
            case ParsingContextEnum.ArrayLiteralMembers:
                return CurrentToken == SyntaxKind.CommaToken || CurrentToken == SyntaxKind.DotDotDotToken || IsStartOfExpression();
            case ParsingContextEnum.Parameters:
                return IsStartOfParameter();
            case ParsingContextEnum.TypeArguments:
            case ParsingContextEnum.TupleElementTypes:
                return CurrentToken == SyntaxKind.CommaToken || IsStartOfType();
            case ParsingContextEnum.HeritageClauses:
                return IsHeritageClause();
            case ParsingContextEnum.ImportOrExportSpecifiers:
                return TokenIsIdentifierOrKeyword(CurrentToken);
            case ParsingContextEnum.JsxAttributes:
                return TokenIsIdentifierOrKeyword(CurrentToken) || CurrentToken == SyntaxKind.OpenBraceToken;
            case ParsingContextEnum.JsxChildren:
                return true;
            case ParsingContextEnum.JSDocFunctionParameters:
            case ParsingContextEnum.JSDocTypeArguments:
            case ParsingContextEnum.JSDocTupleTypes:
                return JsDocParser.IsJsDocType();
            case ParsingContextEnum.JSDocRecordMembers:
                return IsSimplePropertyName();
        }
        Debug.Fail("Non-exhaustive case in 'isListElement'.");
        return false;
    }

    public bool IsValidHeritageClauseObjectLiteral()
    {
        Debug.Assert(CurrentToken == SyntaxKind.OpenBraceToken);
        if (NextToken() == SyntaxKind.CloseBraceToken)
        {
            var next = NextToken();
            return next == SyntaxKind.CommaToken || next == SyntaxKind.OpenBraceToken || next == SyntaxKind.ExtendsKeyword || next == SyntaxKind.ImplementsKeyword;
        }
        return true;
    }

    public bool NextTokenIsIdentifier()
    {
        NextToken();
        return IsIdentifier();
    }

    public bool NextTokenIsIdentifierOrKeyword()
    {
        NextToken();
        return TokenIsIdentifierOrKeyword(CurrentToken);
    }

    public bool IsHeritageClauseExtendsOrImplementsKeyword() => (CurrentToken == SyntaxKind.ImplementsKeyword ||
                        CurrentToken == SyntaxKind.ExtendsKeyword)
&& LookAhead(NextTokenIsStartOfExpression);

    public bool NextTokenIsStartOfExpression()
    {
        NextToken();
        return IsStartOfExpression();
    }

    public bool IsListTerminator(ParsingContextEnum kind)
    {
        if (CurrentToken == SyntaxKind.EndOfFileToken)
        {
            // Being at the end of the file ends all lists.
            return true;
        }
        return kind switch
        {
            ParsingContextEnum.BlockStatements or ParsingContextEnum.SwitchClauses or ParsingContextEnum.TypeMembers or ParsingContextEnum.ClassMembers or ParsingContextEnum.EnumMembers or ParsingContextEnum.ObjectLiteralMembers or ParsingContextEnum.ObjectBindingElements or ParsingContextEnum.ImportOrExportSpecifiers => CurrentToken == SyntaxKind.CloseBraceToken,
            ParsingContextEnum.SwitchClauseStatements => CurrentToken == SyntaxKind.CloseBraceToken || CurrentToken == SyntaxKind.CaseKeyword || CurrentToken == SyntaxKind.DefaultKeyword,
            ParsingContextEnum.HeritageClauseElement => CurrentToken == SyntaxKind.OpenBraceToken || CurrentToken == SyntaxKind.ExtendsKeyword || CurrentToken == SyntaxKind.ImplementsKeyword,
            ParsingContextEnum.VariableDeclarations => IsVariableDeclaratorListTerminator(),
            ParsingContextEnum.TypeParameters => CurrentToken == SyntaxKind.GreaterThanToken || CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.OpenBraceToken || CurrentToken == SyntaxKind.ExtendsKeyword || CurrentToken == SyntaxKind.ImplementsKeyword,// Tokens other than '>' are here for better error recovery
            ParsingContextEnum.ArgumentExpressions => CurrentToken == SyntaxKind.CloseParenToken || CurrentToken == SyntaxKind.SemicolonToken,// Tokens other than ')' are here for better error recovery
            ParsingContextEnum.ArrayLiteralMembers or ParsingContextEnum.TupleElementTypes or ParsingContextEnum.ArrayBindingElements => CurrentToken == SyntaxKind.CloseBracketToken,
            ParsingContextEnum.Parameters or ParsingContextEnum.RestProperties => CurrentToken == SyntaxKind.CloseParenToken || CurrentToken == SyntaxKind.CloseBracketToken,// Tokens other than ')' and ']' (the latter for index signatures) are here for better error recovery
            ParsingContextEnum.TypeArguments => CurrentToken != SyntaxKind.CommaToken,// All other tokens should cause the type-argument to terminate except comma token
            ParsingContextEnum.HeritageClauses => CurrentToken == SyntaxKind.OpenBraceToken || CurrentToken == SyntaxKind.CloseBraceToken,
            ParsingContextEnum.JsxAttributes => CurrentToken == SyntaxKind.GreaterThanToken || CurrentToken == SyntaxKind.SlashToken,
            ParsingContextEnum.JsxChildren => CurrentToken == SyntaxKind.LessThanToken && LookAhead(NextTokenIsSlash),
            ParsingContextEnum.JSDocFunctionParameters => CurrentToken == SyntaxKind.CloseParenToken || CurrentToken == SyntaxKind.ColonToken || CurrentToken == SyntaxKind.CloseBraceToken,
            ParsingContextEnum.JSDocTypeArguments => CurrentToken == SyntaxKind.GreaterThanToken || CurrentToken == SyntaxKind.CloseBraceToken,
            ParsingContextEnum.JSDocTupleTypes => CurrentToken == SyntaxKind.CloseBracketToken || CurrentToken == SyntaxKind.CloseBraceToken,
            ParsingContextEnum.JSDocRecordMembers => CurrentToken == SyntaxKind.CloseBraceToken,
            _ => false,// ?
        };
    }

    public bool IsVariableDeclaratorListTerminator()
    {
        if (CanParseSemicolon())
        {
            return true;
        }
        if (IsInOrOfKeyword(CurrentToken))
        {
            return true;
        }
        if (CurrentToken == SyntaxKind.EqualsGreaterThanToken)
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
        foreach (ParsingContextEnum kind in Enum.GetValues(typeof(ParsingContextEnum)))
        {
            if ((ParsingContext & (1 << (int)kind)) != 0)
            {
                if (IsListElement(kind, true) || IsListTerminator(kind))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public NodeArray<T> ParseList<T>(ParsingContextEnum kind, Func<T> parseElement) where T : INode
    {
        var saveParsingContext = ParsingContext;
        ParsingContext |= 1 << (int)kind;
        var result = CreateList<T>();
        while (!IsListTerminator(kind))
        {
            if (IsListElement(kind, false))
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
        result.End = GetNodeEnd();
        ParsingContext = saveParsingContext;
        return result;
    }

    public NodeArray<T> ParseList2<T>(ParsingContextEnum kind, Func<T> parseElement) where T : INode
    {
        var saveParsingContext = ParsingContext;
        ParsingContext |= 1 << (int)kind;
        var result = CreateList<T>();
        while (!IsListTerminator(kind))
        {
            if (IsListElement(kind, false))
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
        result.End = GetNodeEnd();
        ParsingContext = saveParsingContext;
        return result;
    }

    public T ParseListElement<T>(ParsingContextEnum parsingContext, Func<T> parseElement) where T : INode
    {
        var node = CurrentNode(parsingContext);
        return node != null ? (T)ConsumeNode(node) : parseElement();
    }

    public T ParseListElement2<T>(ParsingContextEnum parsingContext, Func<T> parseElement) where T : INode
    {
        var node = CurrentNode2(parsingContext);
        return node != null ? (T)ConsumeNode(node) : parseElement();
    }

    public Node CurrentNode(ParsingContextEnum parsingContext) => ParseErrorBeforeNextFinishedNode ? null : null;//if (syntaxCursor == null)//{//    // if we don't have a cursor, we could never return a node from the old tree.//    return null;//}//var node = syntaxCursor.currentNode(scanner.StartPos);//if (nodeIsMissing(node))//{//    return null;//}//if (node.intersectsChange)//{//    return null;//}//if (containsParseError(node) != null)//{//    return null;//}//var nodeContextFlags = node.flags & NodeFlags.ContextFlags;//if (nodeContextFlags != contextFlags)//{//    return null;//}//if (!canReuseNode(node, parsingContext))//{//    return null;//}//return node;

    public INode CurrentNode2(ParsingContextEnum parsingContext) => ParseErrorBeforeNextFinishedNode ? null : null;

    public INode ConsumeNode(INode node)
    {
        // Move the scanner so it is after the node we just consumed.
        Scanner.SetTextPos(node.End ?? 0);
        NextToken();
        return node;
    }
    //public INode consumeNode(INode node)
    //{
    //    // Move the scanner so it is after the node we just consumed.
    //    scanner.setTextPos(node.end);
    //    nextToken();
    //    return node;
    //}

    public bool CanReuseNode(Node node, ParsingContextEnum parsingContext)
    {
        switch (parsingContext)
        {
            case ParsingContextEnum.ClassMembers:
                return IsReusableClassMember(node);
            case ParsingContextEnum.SwitchClauses:
                return IsReusableSwitchClause(node);
            case ParsingContextEnum.SourceElements:
            case ParsingContextEnum.BlockStatements:
            case ParsingContextEnum.SwitchClauseStatements:
                return IsReusableStatement(node);
            case ParsingContextEnum.EnumMembers:
                return IsReusableEnumMember(node);
            case ParsingContextEnum.TypeMembers:
                return IsReusableTypeMember(node);
            case ParsingContextEnum.VariableDeclarations:
                return IsReusableVariableDeclaration(node);
            case ParsingContextEnum.Parameters:
                return IsReusableParameter(node);
            case ParsingContextEnum.RestProperties:
                return false;
            case ParsingContextEnum.HeritageClauses:
            case ParsingContextEnum.TypeParameters:
            case ParsingContextEnum.TupleElementTypes:
            case ParsingContextEnum.TypeArguments:
            case ParsingContextEnum.ArgumentExpressions:
            case ParsingContextEnum.ObjectLiteralMembers:
            case ParsingContextEnum.HeritageClauseElement:
            case ParsingContextEnum.JsxAttributes:
            case ParsingContextEnum.JsxChildren:
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
                case SyntaxKind.Constructor:
                case SyntaxKind.IndexSignature:
                case SyntaxKind.GetAccessor:
                case SyntaxKind.SetAccessor:
                case SyntaxKind.PropertyDeclaration:
                case SyntaxKind.SemicolonClassElement:
                    return true;
                case SyntaxKind.MethodDeclaration:
                    var methodDeclaration = (MethodDeclaration)node;
                    var nameIsConstructor = methodDeclaration.Name.Kind == SyntaxKind.Identifier &&
                                                ((Identifier)methodDeclaration.Name).OriginalKeywordKind == SyntaxKind.ConstructorKeyword;
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
                case SyntaxKind.CaseClause:
                case SyntaxKind.DefaultClause:
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
                case SyntaxKind.FunctionDeclaration:
                case SyntaxKind.VariableStatement:
                case SyntaxKind.Block:
                case SyntaxKind.IfStatement:
                case SyntaxKind.ExpressionStatement:
                case SyntaxKind.ThrowStatement:
                case SyntaxKind.ReturnStatement:
                case SyntaxKind.SwitchStatement:
                case SyntaxKind.BreakStatement:
                case SyntaxKind.ContinueStatement:
                case SyntaxKind.ForInStatement:
                case SyntaxKind.ForOfStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.WithStatement:
                case SyntaxKind.EmptyStatement:
                case SyntaxKind.TryStatement:
                case SyntaxKind.LabeledStatement:
                case SyntaxKind.DoStatement:
                case SyntaxKind.DebuggerStatement:
                case SyntaxKind.ImportDeclaration:
                case SyntaxKind.ImportEqualsDeclaration:
                case SyntaxKind.ExportDeclaration:
                case SyntaxKind.ExportAssignment:
                case SyntaxKind.ModuleDeclaration:
                case SyntaxKind.ClassDeclaration:
                case SyntaxKind.InterfaceDeclaration:
                case SyntaxKind.EnumDeclaration:
                case SyntaxKind.TypeAliasDeclaration:
                    return true;
            }
        }
        return false;
    }

    public bool IsReusableEnumMember(Node node) => node.Kind == SyntaxKind.EnumMember;

    public bool IsReusableTypeMember(Node node)
    {
        if (node != null)
        {
            switch (node.Kind)
            {
                case SyntaxKind.ConstructSignature:
                case SyntaxKind.MethodSignature:
                case SyntaxKind.IndexSignature:
                case SyntaxKind.PropertySignature:
                case SyntaxKind.CallSignature:
                    return true;
            }
        }
        return false;
    }

    public bool IsReusableVariableDeclaration(Node node)
    {
        if (node.Kind != SyntaxKind.VariableDeclaration)
        {
            return false;
        }
        var variableDeclarator = (VariableDeclaration)node;
        return variableDeclarator.Initializer == null;
    }

    public bool IsReusableParameter(Node node)
    {
        if (node.Kind != SyntaxKind.Parameter)
        {
            return false;
        }
        var parameter = (ParameterDeclaration)node;
        return parameter.Initializer == null;
    }

    public bool AbortParsingListOrMoveToNextToken(ParsingContextEnum kind)
    {
        ParseErrorAtCurrentToken(ParsingContextErrors(kind));
        if (IsInSomeParsingContext())
        {
            return true;
        }
        NextToken();
        return false;
    }

    public DiagnosticMessage ParsingContextErrors(ParsingContextEnum context) => context switch
    {
        ParsingContextEnum.SourceElements => Diagnostics.Declaration_or_statement_expected,
        ParsingContextEnum.BlockStatements => Diagnostics.Declaration_or_statement_expected,
        ParsingContextEnum.SwitchClauses => Diagnostics.case_or_default_expected,
        ParsingContextEnum.SwitchClauseStatements => Diagnostics.Statement_expected,
        ParsingContextEnum.RestProperties or ParsingContextEnum.TypeMembers => Diagnostics.Property_or_signature_expected,
        ParsingContextEnum.ClassMembers => Diagnostics.Unexpected_token_A_constructor_method_accessor_or_property_was_expected,
        ParsingContextEnum.EnumMembers => Diagnostics.Enum_member_expected,
        ParsingContextEnum.HeritageClauseElement => Diagnostics.Expression_expected,
        ParsingContextEnum.VariableDeclarations => Diagnostics.Variable_declaration_expected,
        ParsingContextEnum.ObjectBindingElements => Diagnostics.Property_destructuring_pattern_expected,
        ParsingContextEnum.ArrayBindingElements => Diagnostics.Array_element_destructuring_pattern_expected,
        ParsingContextEnum.ArgumentExpressions => Diagnostics.Argument_expression_expected,
        ParsingContextEnum.ObjectLiteralMembers => Diagnostics.Property_assignment_expected,
        ParsingContextEnum.ArrayLiteralMembers => Diagnostics.Expression_or_comma_expected,
        ParsingContextEnum.Parameters => Diagnostics.Parameter_declaration_expected,
        ParsingContextEnum.TypeParameters => Diagnostics.Type_parameter_declaration_expected,
        ParsingContextEnum.TypeArguments => Diagnostics.Type_argument_expected,
        ParsingContextEnum.TupleElementTypes => Diagnostics.Type_expected,
        ParsingContextEnum.HeritageClauses => Diagnostics.Unexpected_token_expected,
        ParsingContextEnum.ImportOrExportSpecifiers => Diagnostics.Identifier_expected,
        ParsingContextEnum.JsxAttributes => Diagnostics.Identifier_expected,
        ParsingContextEnum.JsxChildren => Diagnostics.Identifier_expected,
        ParsingContextEnum.JSDocFunctionParameters => Diagnostics.Parameter_declaration_expected,
        ParsingContextEnum.JSDocTypeArguments => Diagnostics.Type_argument_expected,
        ParsingContextEnum.JSDocTupleTypes => Diagnostics.Type_expected,
        ParsingContextEnum.JSDocRecordMembers => Diagnostics.Property_assignment_expected,
        _ => new DiagnosticMessage()
        {
            Key = "Unknown parsing context",
            Category = DiagnosticCategory.Unknown,
            Message = "Unknown parsing context",
            Code = -1
        },
    };

    public NodeArray<T> ParseDelimitedList<T>(ParsingContextEnum kind, Func<T> parseElement, bool? considerSemicolonAsDelimiter = null) where T : INode
    {
        var saveParsingContext = ParsingContext;
        ParsingContext |= 1 << (int)kind;
        var result = CreateList<T>();
        var commaStart = -1;
        while (true)
        {
            if (IsListElement(kind, false))
            {
                result.Add(ParseListElement(kind, parseElement));
                commaStart = Scanner.TokenPos;
                if (ParseOptional(SyntaxKind.CommaToken))
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
                ParseExpected(SyntaxKind.CommaToken);
                if (considerSemicolonAsDelimiter == true && CurrentToken == SyntaxKind.SemicolonToken && !Scanner.HasPrecedingLineBreak)
                {
                    NextToken();
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
        result.End = GetNodeEnd();
        ParsingContext = saveParsingContext;
        return result;
    }

    public NodeArray<T> CreateMissingList<T>() where T : INode => CreateList<T>();

    public NodeArray<T> ParseBracketedList<T>(ParsingContextEnum kind, Func<T> parseElement, SyntaxKind open, SyntaxKind close) where T : INode
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
        while (ParseOptional(SyntaxKind.DotToken))
        {
            QualifiedName node = new QualifiedName
            {
                Pos = entity.Pos,
                Left = entity,
                Right = ParseRightSideOfDot(allowReservedWords)
            };
            entity = FinishNode(node);
        }
        return entity;
    }

    public Identifier ParseRightSideOfDot(bool allowIdentifierNames)
    {
        if (Scanner.HasPrecedingLineBreak && TokenIsIdentifierOrKeyword(CurrentToken))
        {
            var matchesPattern = LookAhead(NextTokenIsIdentifierOrKeywordOnSameLine);
            if (matchesPattern)
            {
                // Report that we need an identifier.  However, report it right after the dot,
                // and not on the next token.  This is because the next token might actually
                // be an identifier and the error would be quite confusing.
                return (Identifier)CreateMissingNode<Identifier>(SyntaxKind.Identifier, true, Diagnostics.Identifier_expected);
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
        Debug.Assert(template.Head.Kind == SyntaxKind.TemplateHead, "Template head has wrong token kind");
        var templateSpans = CreateList<TemplateSpan>();
        do
        {
            templateSpans.Add(ParseTemplateSpan());
        }
        while (LastOrUndefined(templateSpans).Literal.Kind == SyntaxKind.TemplateMiddle);
        templateSpans.End = GetNodeEnd();
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
        if (CurrentToken == SyntaxKind.CloseBraceToken)
        {
            ReScanTemplateToken();
            span.Literal = ParseTemplateMiddleOrTemplateTail();
        }
        else
        {
            span.Literal = (TemplateTail)ParseExpectedToken<TemplateTail>(SyntaxKind.TemplateTail, false, Diagnostics._0_expected, TokenToString(SyntaxKind.CloseBraceToken));
        }
        //span.literal = literal;
        return FinishNode(span);
    }

    public ILiteralExpression ParseLiteralNode(bool? internName = null)
    {
        var literal = CurrentToken switch
        {
            SyntaxKind.StringLiteral => ParseLiteralLikeNode(new StringLiteral(), internName == true),
            SyntaxKind.RegularExpressionLiteral => ParseLiteralLikeNode(new RegularExpressionLiteral(), internName == true),
            SyntaxKind.NoSubstitutionTemplateLiteral => ParseLiteralLikeNode(new NoSubstitutionTemplateLiteral(), internName == true),
            SyntaxKind.NumericLiteral => ParseLiteralLikeNode(new NumericLiteral(), internName == true),
            _ => throw new NotSupportedException("parseLiteralNode")
        };

        return (ILiteralExpression)literal;
    }

    public TemplateHead ParseTemplateHead()
    {
        var fragment = new TemplateHead();
        ParseLiteralLikeNode(fragment, internName: false);
        Debug.Assert(fragment.Kind is SyntaxKind.TemplateHead, "Template head has wrong token kind");
        return fragment;
    }

    public ILiteralLikeNode ParseTemplateMiddleOrTemplateTail()
    {
        var t = CurrentToken;
        ILiteralLikeNode fragment = null;
        if (t == SyntaxKind.TemplateMiddle)
        {
            fragment = ParseLiteralLikeNode(new TemplateMiddle(), internName: false);
        }
        else if (t == SyntaxKind.TemplateTail)
        {
            fragment = ParseLiteralLikeNode(new TemplateTail(), internName: false);
        }
        Debug.Assert(fragment.Kind == SyntaxKind.TemplateMiddle || fragment.Kind == SyntaxKind.TemplateTail, "Template fragment has wrong token kind");
        return fragment;
    }

    public ILiteralLikeNode ParseLiteralLikeNode(ILiteralLikeNode node, bool internName)
    {
        node.Pos = Scanner.StartPos;
        //var node = new LiteralLikeNode { pos = scanner.StartPos }; // LiteralExpression();
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
        NextToken();
        FinishNode(node);
        if (node.Kind == SyntaxKind.NumericLiteral
                        && SourceText.CharCodeAt(tokenPos) == CharacterCode._0
                        && IsOctalDigit(SourceText.CharCodeAt(tokenPos + 1)))
        {
            node.IsOctalLiteral = true;
        }
        return node;
    }

    public TypeReferenceNode ParseTypeReference()
    {
        var typeName = ParseEntityName(false, Diagnostics.Type_expected);
        var node = new TypeReferenceNode
        {
            Pos = typeName.Pos,
            TypeName = typeName
        };
        if (!Scanner.HasPrecedingLineBreak && CurrentToken == SyntaxKind.LessThanToken)
        {
            node.TypeArguments = ParseBracketedList(ParsingContextEnum.TypeArguments, ParseType, SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken);
        }
        return FinishNode(node);
    }

    public TypePredicateNode ParseThisTypePredicate(ThisTypeNode lhs)
    {
        NextToken();
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
        NextToken();
        return FinishNode(node);
    }

    public TypeQueryNode ParseTypeQuery()
    {
        var node = new TypeQueryNode() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.TypeOfKeyword);
        node.ExprName = ParseEntityName(true);
        return FinishNode(node);
    }

    public TypeParameterDeclaration ParseTypeParameter()
    {
        var node = new TypeParameterDeclaration
        {
            Pos = Scanner.StartPos,
            Name = ParseIdentifier()
        };
        if (ParseOptional(SyntaxKind.ExtendsKeyword))
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
        if (ParseOptional(SyntaxKind.EqualsToken))
        {
            node.Default = ParseType();
        }
        return FinishNode(node);
    }

    public NodeArray<TypeParameterDeclaration> ParseTypeParameters() =>
        CurrentToken == SyntaxKind.LessThanToken
            ? ParseBracketedList(ParsingContextEnum.TypeParameters, ParseTypeParameter, SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken)
            : null;

    public ITypeNode ParseParameterType() =>
        ParseOptional(SyntaxKind.ColonToken) ? ParseType() : null;

    public bool IsStartOfParameter() =>
        CurrentToken == SyntaxKind.DotDotDotToken || IsIdentifierOrPattern() || IsModifierKind(CurrentToken) || CurrentToken == SyntaxKind.AtToken || CurrentToken == SyntaxKind.ThisKeyword;

    public ParameterDeclaration ParseParameter()
    {
        var node = new ParameterDeclaration() { Pos = Scanner.StartPos };
        if (CurrentToken == SyntaxKind.ThisKeyword)
        {
            node.Name = CreateIdentifier(true, null);
            node.Type = ParseParameterType();
            return FinishNode(node);
        }
        node.Decorators = ParseDecorators();
        node.Modifiers = ParseModifiers();
        node.DotDotDotToken = (DotDotDotToken)ParseOptionalToken<DotDotDotToken>(SyntaxKind.DotDotDotToken);
        // FormalParameter [Yield,Await]:
        //      BindingElement[?Yield,?Await]
        node.Name = ParseIdentifierOrPattern();
        if (GetFullWidth(node.Name) == 0 && !HasModifiers(node) && IsModifierKind(CurrentToken))
        {
            // in cases like
            // 'use strict'
            // function foo(static)
            // isParameter('static') == true, because of isModifier('static')
            // however 'static' is not a legal identifier in a strict mode.
            // so result of this function will be ParameterDeclaration (flags = 0, name = missing, type = null, initializer = null)
            // and current token will not change => parsing of the enclosing parameter list will last till the end of time (or OOM)
            // to avoid this we'll advance cursor to the next token.
            NextToken();
        }
        node.QuestionToken = (QuestionToken)ParseOptionalToken<QuestionToken>(SyntaxKind.QuestionToken);
        node.Type = ParseParameterType();
        node.Initializer = ParseBindingElementInitializer(true);
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

    public IExpression ParseParameterInitializer() => ParseInitializer(true);

    public void FillSignature(SyntaxKind returnToken, bool
                yieldContext, bool
                awaitContext, bool
                requireCompleteParameterList, ISignatureDeclaration
                signature)
    {
        var returnTokenRequired = returnToken == SyntaxKind.EqualsGreaterThanToken;
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

    public void FillSignatureEqualsGreaterThanToken(SyntaxKind returnToken, bool
                yieldContext, bool
                awaitContext, bool
                requireCompleteParameterList, SignatureDeclaration
                signature)
    {
        var returnTokenRequired = returnToken == SyntaxKind.EqualsGreaterThanToken;
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

    public void FillSignatureColonToken(SyntaxKind
                returnToken, bool
                yieldContext, bool
                awaitContext, bool
                requireCompleteParameterList, SignatureDeclaration
                signature)
    {
        var returnTokenRequired = returnToken == SyntaxKind.EqualsGreaterThanToken;
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

    public NodeArray<ParameterDeclaration> ParseParameterList(bool yieldContext, bool awaitContext, bool requireCompleteParameterList)
    {
        if (ParseExpected(SyntaxKind.OpenParenToken))
        {
            var savedYieldContext = InYieldContext();
            var savedAwaitContext = InAwaitContext();
            SetYieldContext(yieldContext);
            SetAwaitContext(awaitContext);
            var result = ParseDelimitedList(ParsingContextEnum.Parameters, ParseParameter);
            SetYieldContext(savedYieldContext);
            SetAwaitContext(savedAwaitContext);
            if (!ParseExpected(SyntaxKind.CloseParenToken) && requireCompleteParameterList)
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
        if (ParseOptional(SyntaxKind.CommaToken))
        {
            return;
        }
        // Didn't have a comma.  We must have a (possible ASI) semicolon.
        ParseSemicolon();
    }

    public ITypeElement ParseSignatureMember(SyntaxKind kind)
    {
        //var node = new CallSignatureDeclaration | ConstructSignatureDeclaration();
        if (kind == SyntaxKind.ConstructSignature)
        {
            var node = new ConstructSignatureDeclaration { Pos = Scanner.StartPos };
            ParseExpected(SyntaxKind.NewKeyword);
            FillSignature(SyntaxKind.ColonToken, false, false, false, node);
            ParseTypeMemberSemicolon();
            return AddJsDocComment(FinishNode(node));
        }
        else
        {
            var node = new CallSignatureDeclaration { Pos = Scanner.StartPos };
            FillSignature(SyntaxKind.ColonToken, false, false, false, node);
            ParseTypeMemberSemicolon();
            return AddJsDocComment(FinishNode(node));
        }
        //fillSignature(SyntaxKind.ColonToken,  false,  false,  false, node);
        //parseTypeMemberSemicolon();
        //return addJSDocComment(finishNode(node));
    }

    public bool IsIndexSignature() => CurrentToken == SyntaxKind.OpenBracketToken && LookAhead(IsUnambiguouslyIndexSignature);

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
        NextToken();
        if (CurrentToken == SyntaxKind.DotDotDotToken || CurrentToken == SyntaxKind.CloseBracketToken)
        {
            return true;
        }
        if (IsModifierKind(CurrentToken))
        {
            NextToken();
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
            NextToken();
        }
        if (CurrentToken == SyntaxKind.ColonToken || CurrentToken == SyntaxKind.CommaToken)
        {
            return true;
        }
        if (CurrentToken != SyntaxKind.QuestionToken)
        {
            return false;
        }
        // If any of the following tokens are after the question mark, it cannot
        // be a conditional expression, so treat it as an indexer.
        NextToken();
        return CurrentToken == SyntaxKind.ColonToken || CurrentToken == SyntaxKind.CommaToken || CurrentToken == SyntaxKind.CloseBracketToken;
    }

    public IndexSignatureDeclaration ParseIndexSignatureDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new IndexSignatureDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers,
            Parameters = ParseBracketedList(ParsingContextEnum.Parameters, ParseParameter, SyntaxKind.OpenBracketToken, SyntaxKind.CloseBracketToken),
            Type = ParseTypeAnnotation()
        };
        ParseTypeMemberSemicolon();
        return FinishNode(node);
    }

    public ITypeElement ParsePropertyOrMethodSignature(int fullStart, NodeArray<Modifier> modifiers)
    {
        var name = ParsePropertyName();
        var questionToken = (QuestionToken)ParseOptionalToken<QuestionToken>(SyntaxKind.QuestionToken);
        if (CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.LessThanToken)
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
            FillSignature(SyntaxKind.ColonToken, false, false, false, method);
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
            if (CurrentToken == SyntaxKind.EqualsToken)
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
        if (CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.LessThanToken)
        {
            return true;
        }
        bool idToken = false;
        while (IsModifierKind(CurrentToken))
        {
            idToken = true;
            NextToken();
        }
        if (CurrentToken == SyntaxKind.OpenBracketToken)
        {
            return true;
        }
        if (IsLiteralPropertyName())
        {
            idToken = true;
            NextToken();
        }
        return idToken
&& (CurrentToken == SyntaxKind.OpenParenToken ||
                CurrentToken == SyntaxKind.LessThanToken ||
                CurrentToken == SyntaxKind.QuestionToken ||
                CurrentToken == SyntaxKind.ColonToken ||
                CurrentToken == SyntaxKind.CommaToken ||
                CanParseSemicolon());
    }

    public ITypeElement ParseTypeMember()
    {
        if (CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.LessThanToken)
        {
            return ParseSignatureMember(SyntaxKind.CallSignature);
        }
        if (CurrentToken == SyntaxKind.NewKeyword && LookAhead(IsStartOfConstructSignature))
        {
            return ParseSignatureMember(SyntaxKind.ConstructSignature);
        }
        var fullStart = GetNodePos();
        var modifiers = ParseModifiers();
        return IsIndexSignature()
            ? ParseIndexSignatureDeclaration(fullStart, null, modifiers)
            : ParsePropertyOrMethodSignature(fullStart, modifiers);
    }

    public bool IsStartOfConstructSignature()
    {
        NextToken();
        return CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.LessThanToken;
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
        if (ParseExpected(SyntaxKind.OpenBraceToken))
        {
            members = ParseList(ParsingContextEnum.TypeMembers, ParseTypeMember);
            ParseExpected(SyntaxKind.CloseBraceToken);
        }
        else
        {
            members = CreateMissingList<ITypeElement>();
        }
        return members;
    }

    public bool IsStartOfMappedType()
    {
        NextToken();
        if (CurrentToken == SyntaxKind.ReadonlyKeyword)
        {
            NextToken();
        }
        return CurrentToken == SyntaxKind.OpenBracketToken && NextTokenIsIdentifier() && NextToken() == SyntaxKind.InKeyword;
    }

    public TypeParameterDeclaration ParseMappedTypeParameter()
    {
        var node = new TypeParameterDeclaration
        {
            Pos = Scanner.StartPos,
            Name = ParseIdentifier()
        };
        ParseExpected(SyntaxKind.InKeyword);
        node.Constraint = ParseType();
        return FinishNode(node);
    }

    public MappedTypeNode ParseMappedType()
    {
        var node = new MappedTypeNode() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBraceToken);
        node.ReadonlyToken = (ReadonlyToken)ParseOptionalToken<ReadonlyToken>(SyntaxKind.ReadonlyKeyword);
        ParseExpected(SyntaxKind.OpenBracketToken);
        node.TypeParameter = ParseMappedTypeParameter();
        ParseExpected(SyntaxKind.CloseBracketToken);
        node.QuestionToken = (QuestionToken)ParseOptionalToken<QuestionToken>(SyntaxKind.QuestionToken);
        node.Type = ParseTypeAnnotation();
        ParseSemicolon();
        ParseExpected(SyntaxKind.CloseBraceToken);
        return FinishNode(node);
    }

    public TupleTypeNode ParseTupleType()
    {
        var node = new TupleTypeNode
        {
            Pos = Scanner.StartPos,
            ElementTypes = ParseBracketedList(ParsingContextEnum.TupleElementTypes, ParseType, SyntaxKind.OpenBracketToken, SyntaxKind.CloseBracketToken)
        };
        return FinishNode(node);
    }

    public ParenthesizedTypeNode ParseParenthesizedType()
    {
        var node = new ParenthesizedTypeNode() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenParenToken);
        node.Type = ParseType();
        ParseExpected(SyntaxKind.CloseParenToken);
        return FinishNode(node);
    }

    public IFunctionOrConstructorTypeNode ParseFunctionOrConstructorType(SyntaxKind kind)
    {
        var node = kind == SyntaxKind.FunctionType ?
            (IFunctionOrConstructorTypeNode)new FunctionTypeNode { Kind = SyntaxKind.FunctionType } :
            kind == SyntaxKind.ConstructorType ?
            new ConstructorTypeNode { Kind = SyntaxKind.ConstructorType } :
            throw new NotSupportedException("parseFunctionOrConstructorType");
        node.Pos = Scanner.StartPos;
        //new FunctionOrConstructorTypeNode { kind = kind, pos = scanner.StartPos };
        if (kind == SyntaxKind.ConstructorType)
        {
            ParseExpected(SyntaxKind.NewKeyword);
        }
        FillSignature(SyntaxKind.EqualsGreaterThanToken, false, false, false, node);
        return FinishNode(node);
    }

    public TypeNode ParseKeywordAndNoDot()
    {
        var node = ParseTokenNode<TypeNode>(CurrentToken);
        return CurrentToken == SyntaxKind.DotToken ? null : node;
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

    public bool NextTokenIsNumericLiteral() => NextToken() == SyntaxKind.NumericLiteral;

    public ITypeNode ParseNonArrayType()
    {
        switch (CurrentToken)
        {
            case SyntaxKind.AnyKeyword:
            case SyntaxKind.StringKeyword:
            case SyntaxKind.NumberKeyword:
            case SyntaxKind.BooleanKeyword:
            case SyntaxKind.SymbolKeyword:
            case SyntaxKind.UndefinedKeyword:
            case SyntaxKind.NeverKeyword:
            case SyntaxKind.ObjectKeyword:
                var node = TryParse(ParseKeywordAndNoDot);
                return node ?? ParseTypeReference();
            case SyntaxKind.StringLiteral:
            case SyntaxKind.NumericLiteral:
            case SyntaxKind.TrueKeyword:
            case SyntaxKind.FalseKeyword:
                return ParseLiteralTypeNode();
            case SyntaxKind.MinusToken:
                return LookAhead(NextTokenIsNumericLiteral) ? ParseLiteralTypeNode() : ParseTypeReference();
            case SyntaxKind.VoidKeyword:
            case SyntaxKind.NullKeyword:
                return ParseTokenNode<TypeNode>(CurrentToken);
            case SyntaxKind.ThisKeyword:
                {
                    var thisKeyword = ParseThisTypeNode();
                    return CurrentToken == SyntaxKind.IsKeyword && !Scanner.HasPrecedingLineBreak ? ParseThisTypePredicate(thisKeyword) : thisKeyword;
                }
            //goto caseLabel17;
            case SyntaxKind.TypeOfKeyword:
                //caseLabel17:
                return ParseTypeQuery();
            case SyntaxKind.OpenBraceToken:
                return LookAhead(IsStartOfMappedType) ? ParseMappedType() : ParseTypeLiteral();
            case SyntaxKind.OpenBracketToken:
                return ParseTupleType();
            case SyntaxKind.OpenParenToken:
                return ParseParenthesizedType();
            default:
                return ParseTypeReference();
        }
    }

    public bool IsStartOfType() => CurrentToken switch
    {
        SyntaxKind.AnyKeyword or SyntaxKind.StringKeyword or SyntaxKind.NumberKeyword or SyntaxKind.BooleanKeyword or SyntaxKind.SymbolKeyword or SyntaxKind.VoidKeyword or SyntaxKind.UndefinedKeyword or SyntaxKind.NullKeyword or SyntaxKind.ThisKeyword or SyntaxKind.TypeOfKeyword or SyntaxKind.NeverKeyword or SyntaxKind.OpenBraceToken or SyntaxKind.OpenBracketToken or SyntaxKind.LessThanToken or SyntaxKind.BarToken or SyntaxKind.AmpersandToken or SyntaxKind.NewKeyword or SyntaxKind.StringLiteral or SyntaxKind.NumericLiteral or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword or SyntaxKind.ObjectKeyword => true,
        SyntaxKind.MinusToken => LookAhead(NextTokenIsNumericLiteral),
        SyntaxKind.OpenParenToken => LookAhead(IsStartOfParenthesizedOrFunctionType),// Only consider '(' the start of a type if followed by ')', '...', an identifier, a modifier,
                                                                                     // or something that starts a type. We don't want to consider things like '(1)' a type.
        _ => IsIdentifier(),
    };

    public bool IsStartOfParenthesizedOrFunctionType()
    {
        NextToken();
        return CurrentToken == SyntaxKind.CloseParenToken || IsStartOfParameter() || IsStartOfType();
    }

    public ITypeNode ParseArrayTypeOrHigher()
    {
        var type = ParseNonArrayType();
        while (!Scanner.HasPrecedingLineBreak && ParseOptional(SyntaxKind.OpenBracketToken))
        {
            if (IsStartOfType())
            {
                var node = new IndexedAccessTypeNode
                {
                    Pos = type.Pos,
                    ObjectType = type,
                    IndexType = ParseType()
                };
                ParseExpected(SyntaxKind.CloseBracketToken);
                type = FinishNode(node);
            }
            else
            {
                var node = new ArrayTypeNode
                {
                    Pos = type.Pos,
                    ElementType = type
                };
                ParseExpected(SyntaxKind.CloseBracketToken);
                type = FinishNode(node);
            }
        }
        return type;
    }

    public TypeOperatorNode ParseTypeOperator(SyntaxKind @operator)
    {
        var node = new TypeOperatorNode() { Pos = Scanner.StartPos };
        ParseExpected(@operator);
        node.Operator = @operator;
        node.Type = ParseTypeOperatorOrHigher();
        return FinishNode(node);
    }

    public ITypeNode ParseTypeOperatorOrHigher() => CurrentToken switch
    {
        SyntaxKind.KeyOfKeyword => ParseTypeOperator(SyntaxKind.KeyOfKeyword),
        _ => ParseArrayTypeOrHigher(),
    };

    public ITypeNode ParseUnionOrIntersectionType(SyntaxKind kind, Func<ITypeNode> parseConstituentType, SyntaxKind @operator)
    {
        ParseOptional(@operator);
        var type = parseConstituentType();
        if (CurrentToken == @operator)
        {
            var types = CreateList<ITypeNode>(); //[type], type.pos);
            types.Pos = type.Pos;
            types.Add(type);
            while (ParseOptional(@operator))
            {
                types.Add(parseConstituentType());
            }
            types.End = GetNodeEnd();
            var node = kind == SyntaxKind.UnionType ?
                (IUnionOrIntersectionTypeNode)new UnionTypeNode { Kind = kind, Pos = type.Pos } :
                kind == SyntaxKind.IntersectionType ? new IntersectionTypeNode { Kind = kind, Pos = type.Pos }
                : throw new NotSupportedException("parseUnionOrIntersectionType");
            node.Types = types;
            type = FinishNode(node);
        }
        return type;
    }

    public ITypeNode ParseIntersectionTypeOrHigher() => ParseUnionOrIntersectionType(SyntaxKind.IntersectionType, ParseTypeOperatorOrHigher, SyntaxKind.AmpersandToken);

    public ITypeNode ParseUnionTypeOrHigher() => ParseUnionOrIntersectionType(SyntaxKind.UnionType, ParseIntersectionTypeOrHigher, SyntaxKind.BarToken);

    public bool IsStartOfFunctionType() => CurrentToken == SyntaxKind.LessThanToken
|| CurrentToken == SyntaxKind.OpenParenToken && LookAhead(IsUnambiguouslyStartOfFunctionType);

    public bool SkipParameterStart()
    {
        if (IsModifierKind(CurrentToken))
        {
            // Skip modifiers
            ParseModifiers();
        }
        if (IsIdentifier() || CurrentToken == SyntaxKind.ThisKeyword)
        {
            NextToken();
            return true;
        }
        if (CurrentToken == SyntaxKind.OpenBracketToken || CurrentToken == SyntaxKind.OpenBraceToken)
        {
            var previousErrorCount = ParseDiagnostics.Count;
            ParseIdentifierOrPattern();
            return previousErrorCount == ParseDiagnostics.Count;
        }
        return false;
    }

    public bool IsUnambiguouslyStartOfFunctionType()
    {
        NextToken();
        if (CurrentToken == SyntaxKind.CloseParenToken || CurrentToken == SyntaxKind.DotDotDotToken)
        {
            // ( )
            // ( ...
            return true;
        }
        if (SkipParameterStart())
        {
            if (CurrentToken == SyntaxKind.ColonToken || CurrentToken == SyntaxKind.CommaToken ||
                                CurrentToken == SyntaxKind.QuestionToken || CurrentToken == SyntaxKind.EqualsToken)
            {
                // ( xxx :
                // ( xxx ,
                // ( xxx ?
                // ( xxx =
                return true;
            }
            if (CurrentToken == SyntaxKind.CloseParenToken)
            {
                NextToken();
                if (CurrentToken == SyntaxKind.EqualsGreaterThanToken)
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

    public Identifier ParseTypePredicatePrefix()
    {
        var id = ParseIdentifier();
        if (CurrentToken == SyntaxKind.IsKeyword && !Scanner.HasPrecedingLineBreak)
        {
            NextToken();
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
            return ParseFunctionOrConstructorType(SyntaxKind.FunctionType);
        }
        return CurrentToken == SyntaxKind.NewKeyword ? ParseFunctionOrConstructorType(SyntaxKind.ConstructorType) : ParseUnionTypeOrHigher();
    }

    public ITypeNode ParseTypeAnnotation() => ParseOptional(SyntaxKind.ColonToken) ? ParseType() : null;

    public bool IsStartOfLeftHandSideExpression() => CurrentToken switch
    {
        SyntaxKind.ThisKeyword or SyntaxKind.SuperKeyword or SyntaxKind.NullKeyword or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword or SyntaxKind.NumericLiteral or SyntaxKind.StringLiteral or SyntaxKind.NoSubstitutionTemplateLiteral or SyntaxKind.TemplateHead or SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken or SyntaxKind.OpenBraceToken or SyntaxKind.FunctionKeyword or SyntaxKind.ClassKeyword or SyntaxKind.NewKeyword or SyntaxKind.SlashToken or SyntaxKind.SlashEqualsToken or SyntaxKind.Identifier => true,
        _ => IsIdentifier(),
    };

    public bool IsStartOfExpression()
    {
        if (IsStartOfLeftHandSideExpression())
        {
            return true;
        }
        switch (CurrentToken)
        {
            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
            case SyntaxKind.TildeToken:
            case SyntaxKind.ExclamationToken:
            case SyntaxKind.DeleteKeyword:
            case SyntaxKind.TypeOfKeyword:
            case SyntaxKind.VoidKeyword:
            case SyntaxKind.PlusPlusToken:
            case SyntaxKind.MinusMinusToken:
            case SyntaxKind.LessThanToken:
            case SyntaxKind.AwaitKeyword:
            case SyntaxKind.YieldKeyword:
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
        CurrentToken != SyntaxKind.OpenBraceToken &&
            CurrentToken != SyntaxKind.FunctionKeyword &&
            CurrentToken != SyntaxKind.ClassKeyword &&
            CurrentToken != SyntaxKind.AtToken &&
            IsStartOfExpression();

    public IExpression ParseExpression()
    {
        var saveDecoratorContext = InDecoratorContext();
        if (saveDecoratorContext)
        {
            SetDecoratorContext(false);
        }
        var expr = ParseAssignmentExpressionOrHigher();

        Token operatorToken = null;
        while ((operatorToken = (Token)ParseOptionalToken<Token>(SyntaxKind.CommaToken)) != null)
        {
            expr = MakeBinaryExpression(expr, operatorToken, ParseAssignmentExpressionOrHigher());
        }
        if (saveDecoratorContext)
        {
            SetDecoratorContext(true);
        }
        return expr;
    }

    public IExpression ParseInitializer(bool inParameter)
    {
        if (CurrentToken != SyntaxKind.EqualsToken)
        {
            if (Scanner.HasPrecedingLineBreak || (inParameter && CurrentToken == SyntaxKind.OpenBraceToken) || !IsStartOfExpression())
            {
                // preceding line break, open brace in a parameter (likely a function body) or current token is not an expression -
                // do not try to parse initializer
                return null;
            }
        }
        // Initializer[In, Yield] :
        //     = AssignmentExpression[?In, ?Yield]
        ParseExpected(SyntaxKind.EqualsToken);
        return ParseAssignmentExpressionOrHigher();
    }

    public IExpression ParseAssignmentExpressionOrHigher()
    {
        if (IsYieldExpression())
        {
            return ParseYieldExpression();
        }
        var arrowExpression = TryParseParenthesizedArrowFunctionExpression() ?? TryParseAsyncSimpleArrowFunctionExpression();
        if (arrowExpression != null)
        {
            return arrowExpression;
        }
        var expr = ParseBinaryExpressionOrHigher(0);
        if (expr.Kind == SyntaxKind.Identifier && CurrentToken == SyntaxKind.EqualsGreaterThanToken)
        {
            return ParseSimpleArrowFunctionExpression((Identifier)expr);
        }
        if (IsLeftHandSideExpression(expr) && IsAssignmentOperator(ReScanGreaterToken()))
        {
            return MakeBinaryExpression(expr, ParseTokenNode<Token>(CurrentToken), ParseAssignmentExpressionOrHigher());
        }
        // It wasn't an assignment or a lambda.  This is a conditional expression:
        return ParseConditionalExpressionRest(expr);
    }

    public bool IsYieldExpression()
    {
        if (CurrentToken == SyntaxKind.YieldKeyword)
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
        NextToken();
        return !Scanner.HasPrecedingLineBreak && IsIdentifier();
    }

    public YieldExpression ParseYieldExpression()
    {
        var node = new YieldExpression() { Pos = Scanner.StartPos };
        // YieldExpression[In] :
        //      yield
        //      yield [no LineTerminator here] [Lexical goal InputElementRegExp]AssignmentExpression[?In, Yield]
        //      yield [no LineTerminator here] * [Lexical goal InputElementRegExp]AssignmentExpression[?In, Yield]
        NextToken();
        if (!Scanner.HasPrecedingLineBreak &&
                        (CurrentToken == SyntaxKind.AsteriskToken || IsStartOfExpression()))
        {
            node.AsteriskToken = (AsteriskToken)ParseOptionalToken<AsteriskToken>(SyntaxKind.AsteriskToken);
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

    public ArrowFunction ParseSimpleArrowFunctionExpression(Identifier identifier, NodeArray<Modifier> asyncModifier = null)
    {
        Debug.Assert(CurrentToken == SyntaxKind.EqualsGreaterThanToken, "parseSimpleArrowFunctionExpression should only have been called if we had a =>");
        ArrowFunction node = null;
        if (asyncModifier != null)
        {
            node = new ArrowFunction
            {
                Pos = (int)asyncModifier.Pos,
                Modifiers = asyncModifier
            }; // (ArrowFunction)createNode(SyntaxKind.ArrowFunction, asyncModifier.pos);
        }
        else
        {
            node = new ArrowFunction { Pos = identifier.Pos }; // (ArrowFunction)createNode(SyntaxKind.ArrowFunction, identifier.pos);
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
        node.EqualsGreaterThanToken = (EqualsGreaterThanToken)ParseExpectedToken<EqualsGreaterThanToken>(SyntaxKind.EqualsGreaterThanToken, false, Diagnostics._0_expected, "=>");
        node.Body = ParseArrowFunctionExpressionBody(asyncModifier?.Any() == true);
        return AddJsDocComment(FinishNode(node));
    }

    public ArrowFunction TryParseParenthesizedArrowFunctionExpression()
    {
        var triState = IsParenthesizedArrowFunctionExpression();
        if (triState == Tristate.False)
        {
            // It's definitely not a parenthesized arrow function expression.
            return null;
        }
        var arrowFunction = triState == Tristate.True
                        ? ParseParenthesizedArrowFunctionExpressionHead(true)
                        : TryParse(ParsePossibleParenthesizedArrowFunctionExpressionHead);
        if (arrowFunction == null)
        {
            // Didn't appear to actually be a parenthesized arrow function.  Just bail out.
            return null;
        }
        var isAsync = (GetModifierFlags(arrowFunction) & ModifierFlags.Async) != 0;
        var lastToken = CurrentToken;
        arrowFunction.EqualsGreaterThanToken = (EqualsGreaterThanToken)ParseExpectedToken<EqualsGreaterThanToken>(SyntaxKind.EqualsGreaterThanToken, false, Diagnostics._0_expected, "=>");
        arrowFunction.Body = (lastToken == SyntaxKind.EqualsGreaterThanToken || lastToken == SyntaxKind.OpenBraceToken)
            ? ParseArrowFunctionExpressionBody(isAsync)
            : ParseIdentifier();
        return AddJsDocComment(FinishNode(arrowFunction));
    }

    public Tristate IsParenthesizedArrowFunctionExpression()
    {
        if (CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.LessThanToken || CurrentToken == SyntaxKind.AsyncKeyword)
        {
            return LookAhead(IsParenthesizedArrowFunctionExpressionWorker);
        }
        if (CurrentToken == SyntaxKind.EqualsGreaterThanToken)
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
        if (CurrentToken == SyntaxKind.AsyncKeyword)
        {
            NextToken();
            if (Scanner.HasPrecedingLineBreak)
            {
                return Tristate.False;
            }
            if (CurrentToken != SyntaxKind.OpenParenToken && CurrentToken != SyntaxKind.LessThanToken)
            {
                return Tristate.False;
            }
        }
        var first = CurrentToken;
        var second = NextToken();
        if (first == SyntaxKind.OpenParenToken)
        {
            if (second == SyntaxKind.CloseParenToken)
            {
                var third = NextToken();
                return third switch
                {
                    SyntaxKind.EqualsGreaterThanToken or SyntaxKind.ColonToken or SyntaxKind.OpenBraceToken => Tristate.True,
                    _ => Tristate.False,
                };
            }
            if (second == SyntaxKind.OpenBracketToken || second == SyntaxKind.OpenBraceToken)
            {
                return Tristate.Unknown;
            }
            if (second == SyntaxKind.DotDotDotToken)
            {
                return Tristate.True;
            }
            if (!IsIdentifier())
            {
                return Tristate.False;
            }
            if (NextToken() == SyntaxKind.ColonToken)
            {
                return Tristate.True;
            }
            // This *could* be a parenthesized arrow function.
            // Return Unknown to let the caller know.
            return Tristate.Unknown;
        }
        else
        {
            Debug.Assert(first == SyntaxKind.LessThanToken);
            if (!IsIdentifier())
            {
                return Tristate.False;
            }
            if (SourceFile.LanguageVariant == LanguageVariant.Jsx)
            {
                var isArrowFunctionInJsx = LookAhead(() =>
                {
                    var third = NextToken();
                    if (third == SyntaxKind.ExtendsKeyword)
                    {
                        var fourth = NextToken();
                        return fourth switch
                        {
                            SyntaxKind.EqualsToken or SyntaxKind.GreaterThanToken => false,
                            _ => true,
                        };
                    }
                    else if (third == SyntaxKind.CommaToken)
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

    public ArrowFunction ParsePossibleParenthesizedArrowFunctionExpressionHead() => ParseParenthesizedArrowFunctionExpressionHead(false);

    public ArrowFunction TryParseAsyncSimpleArrowFunctionExpression()
    {
        if (CurrentToken == SyntaxKind.AsyncKeyword)
        {
            var isUnParenthesizedAsyncArrowFunction = LookAhead(IsUnParenthesizedAsyncArrowFunctionWorker);
            if (isUnParenthesizedAsyncArrowFunction == Tristate.True)
            {
                var asyncModifier = ParseModifiersForArrowFunction();
                var expr = ParseBinaryExpressionOrHigher(0);
                return ParseSimpleArrowFunctionExpression((Identifier)expr, asyncModifier);
            }
        }
        return null;
    }

    public Tristate IsUnParenthesizedAsyncArrowFunctionWorker()
    {
        if (CurrentToken == SyntaxKind.AsyncKeyword)
        {
            NextToken();
            if (Scanner.HasPrecedingLineBreak || CurrentToken == SyntaxKind.EqualsGreaterThanToken)
            {
                return Tristate.False;
            }
            var expr = ParseBinaryExpressionOrHigher(0);
            if (!Scanner.HasPrecedingLineBreak && expr.Kind == SyntaxKind.Identifier && CurrentToken == SyntaxKind.EqualsGreaterThanToken)
            {
                return Tristate.True;
            }
        }
        return Tristate.False;
    }

    public ArrowFunction ParseParenthesizedArrowFunctionExpressionHead(bool allowAmbiguity)
    {
        var node = new ArrowFunction
        {
            Pos = Scanner.StartPos,
            Modifiers = ParseModifiersForArrowFunction()
        };
        var isAsync = (GetModifierFlags(node) & ModifierFlags.Async) != 0;
        // Arrow functions are never generators.
        //
        // If we're speculatively parsing a signature for a parenthesized arrow function, then
        // we have to have a complete parameter list.  Otherwise we might see something like
        // a => (b => c)
        // And think that "(b =>" was actually a parenthesized arrow function with a missing
        // close paren.
        FillSignature(SyntaxKind.ColonToken, false, isAsync, !allowAmbiguity, node);
        if (node.Parameters == null)
        {
            return null;
        }
        if (!allowAmbiguity && CurrentToken != SyntaxKind.EqualsGreaterThanToken && CurrentToken != SyntaxKind.OpenBraceToken)
        {
            // Returning null here will cause our caller to rewind to where we started from.
            return null;
        }
        return node;
    }

    public IBlockOrExpression ParseArrowFunctionExpressionBody(bool isAsync)
    {
        if (CurrentToken == SyntaxKind.OpenBraceToken)
        {
            return ParseFunctionBlock(false, isAsync, false);
        }
        if (CurrentToken != SyntaxKind.SemicolonToken &&
                        CurrentToken != SyntaxKind.FunctionKeyword &&
                        CurrentToken != SyntaxKind.ClassKeyword &&
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
            return ParseFunctionBlock(false, isAsync, true);
        }
        return isAsync
            ? DoInAwaitContext(ParseAssignmentExpressionOrHigher)
            : DoOutsideOfAwaitContext(ParseAssignmentExpressionOrHigher);
    }

    public IExpression ParseConditionalExpressionRest(IExpression leftOperand)
    {
        var questionToken = (QuestionToken)ParseOptionalToken<QuestionToken>(SyntaxKind.QuestionToken);
        if (questionToken == null)
        {
            return leftOperand;
        }
        var node = new ConditionalExpression
        {
            Pos = leftOperand.Pos,
            Condition = leftOperand,
            QuestionToken = questionToken,
            WhenTrue = DoOutsideOfContext(DisallowInAndDecoratorContext, ParseAssignmentExpressionOrHigher),
            ColonToken = (ColonToken)ParseExpectedToken<ColonToken>(SyntaxKind.ColonToken, false,
            Diagnostics._0_expected, TokenToString(SyntaxKind.ColonToken)),
            WhenFalse = ParseAssignmentExpressionOrHigher()
        };
        return FinishNode(node);
    }

    public IExpression ParseBinaryExpressionOrHigher(int precedence)
    {
        var leftOperand = ParseUnaryExpressionOrHigher();
        return leftOperand == null ? throw new NullReferenceException() : ParseBinaryExpressionRest(precedence, leftOperand);
    }

    public bool IsInOrOfKeyword(SyntaxKind t) => t == SyntaxKind.InKeyword || t == SyntaxKind.OfKeyword;

    public IExpression ParseBinaryExpressionRest(int precedence, IExpression leftOperand)
    {
        while (true)
        {
            // We either have a binary operator here, or we're finished.  We call
            // reScanGreaterToken so that we merge token sequences like > and = into >=
            ReScanGreaterToken();
            var newPrecedence = GetBinaryOperatorPrecedence();
            var consumeCurrentOperator = CurrentToken == SyntaxKind.AsteriskAsteriskToken ?
                                newPrecedence >= precedence :
                                newPrecedence > precedence;
            if (!consumeCurrentOperator)
            {
                break;
            }
            if (CurrentToken == SyntaxKind.InKeyword && InDisallowInContext())
            {
                break;
            }
            if (CurrentToken == SyntaxKind.AsKeyword)
            {
                if (Scanner.HasPrecedingLineBreak)
                {
                    break;
                }
                else
                {
                    NextToken();
                    leftOperand = MakeAsExpression(leftOperand, ParseType());
                }
            }
            else
            {
                leftOperand = MakeBinaryExpression(leftOperand, ParseTokenNode<Token>(CurrentToken), ParseBinaryExpressionOrHigher(newPrecedence));
            }
        }
        return leftOperand;
    }

    public bool IsBinaryOperator() => (!InDisallowInContext() || CurrentToken != SyntaxKind.InKeyword) && GetBinaryOperatorPrecedence() > 0;

    public int GetBinaryOperatorPrecedence() => CurrentToken switch
    {
        SyntaxKind.BarBarToken => 1,
        SyntaxKind.AmpersandAmpersandToken => 2,
        SyntaxKind.BarToken => 3,
        SyntaxKind.CaretToken => 4,
        SyntaxKind.AmpersandToken => 5,
        SyntaxKind.EqualsEqualsToken or SyntaxKind.ExclamationEqualsToken or SyntaxKind.EqualsEqualsEqualsToken or SyntaxKind.ExclamationEqualsEqualsToken => 6,
        SyntaxKind.LessThanToken or SyntaxKind.GreaterThanToken or SyntaxKind.LessThanEqualsToken or SyntaxKind.GreaterThanEqualsToken or SyntaxKind.InstanceOfKeyword or SyntaxKind.InKeyword or SyntaxKind.AsKeyword => 7,
        SyntaxKind.LessThanLessThanToken or SyntaxKind.GreaterThanGreaterThanToken or SyntaxKind.GreaterThanGreaterThanGreaterThanToken => 8,
        SyntaxKind.PlusToken or SyntaxKind.MinusToken => 9,
        SyntaxKind.AsteriskToken or SyntaxKind.SlashToken or SyntaxKind.PercentToken => 10,
        SyntaxKind.AsteriskAsteriskToken => 11,
        // -1 is lower than all other precedences.  Returning it will cause binary expression
        // parsing to stop.
        _ => -1,
    };

    public BinaryExpression MakeBinaryExpression(IExpression left, Token operatorToken, IExpression right)
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
            Operator = CurrentToken
        };
        NextToken();
        node.Operand = ParseSimpleUnaryExpression();
        return FinishNode(node);
    }

    public DeleteExpression ParseDeleteExpression()
    {
        var node = new DeleteExpression() { Pos = Scanner.StartPos };
        NextToken();
        node.Expression = ParseSimpleUnaryExpression(); // as UnaryExpression;
        return FinishNode(node);
    }

    public TypeOfExpression ParseTypeOfExpression()
    {
        var node = new TypeOfExpression() { Pos = Scanner.StartPos };
        NextToken();
        node.Expression = ParseSimpleUnaryExpression(); //  as UnaryExpression;
        return FinishNode(node);
    }

    public VoidExpression ParseVoidExpression()
    {
        var node = new VoidExpression() { Pos = Scanner.StartPos };
        NextToken();
        node.Expression = ParseSimpleUnaryExpression(); //  as UnaryExpression;
        return FinishNode(node);
    }

    public bool IsAwaitExpression()
    {
        if (CurrentToken == SyntaxKind.AwaitKeyword)
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
        NextToken();
        node.Expression = ParseSimpleUnaryExpression(); // as UnaryExpression;
        return FinishNode(node);
    }
    //UnaryExpression | BinaryExpression

    public IExpression ParseUnaryExpressionOrHigher()
    {
        if (IsUpdateExpression())
        {
            var incrementExpression = ParseIncrementExpression();
            return CurrentToken == SyntaxKind.AsteriskAsteriskToken ?
                ParseBinaryExpressionRest(GetBinaryOperatorPrecedence(), incrementExpression) :
                incrementExpression;
        }
        var unaryOperator = CurrentToken;
        var simpleUnaryExpression = ParseSimpleUnaryExpression();
        if (CurrentToken == SyntaxKind.AsteriskAsteriskToken)
        {
            var start = Scanner.SkipTriviaM(SourceText, simpleUnaryExpression.Pos ?? 0);
            if (simpleUnaryExpression.Kind == SyntaxKind.TypeAssertionExpression)
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

    public IExpression ParseSimpleUnaryExpression()
    {
        switch (CurrentToken)
        {
            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
            case SyntaxKind.TildeToken:
            case SyntaxKind.ExclamationToken:
                return ParsePrefixUnaryExpression();
            case SyntaxKind.DeleteKeyword:
                return ParseDeleteExpression();
            case SyntaxKind.TypeOfKeyword:
                return ParseTypeOfExpression();
            case SyntaxKind.VoidKeyword:
                return ParseVoidExpression();
            case SyntaxKind.LessThanToken:
                // This is modified UnaryExpression grammar in TypeScript
                //  UnaryExpression (modified):
                //      < type > UnaryExpression
                return ParseTypeAssertion();
            case SyntaxKind.AwaitKeyword:
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
        switch (CurrentToken)
        {
            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
            case SyntaxKind.TildeToken:
            case SyntaxKind.ExclamationToken:
            case SyntaxKind.DeleteKeyword:
            case SyntaxKind.TypeOfKeyword:
            case SyntaxKind.VoidKeyword:
            case SyntaxKind.AwaitKeyword:
                return false;
            case SyntaxKind.LessThanToken:
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

    public IExpression ParseIncrementExpression()
    {
        if (CurrentToken == SyntaxKind.PlusPlusToken || CurrentToken == SyntaxKind.MinusMinusToken)
        {
            var node = new PrefixUnaryExpression
            {
                Pos = Scanner.StartPos,
                Operator = CurrentToken
            };
            NextToken();
            node.Operand = ParseLeftHandSideExpressionOrHigher();
            return FinishNode(node);
        }
        else
        if (SourceFile.LanguageVariant == LanguageVariant.Jsx && CurrentToken == SyntaxKind.LessThanToken && LookAhead(NextTokenIsIdentifierOrKeyword))
        {
            // JSXElement is part of primaryExpression
            return ParseJsxElementOrSelfClosingElement(true);
        }
        var expression = ParseLeftHandSideExpressionOrHigher();
        //Debug.assert(isLeftHandSideExpression(expression));
        if ((CurrentToken == SyntaxKind.PlusPlusToken || CurrentToken == SyntaxKind.MinusMinusToken) && !Scanner.HasPrecedingLineBreak)
        {
            var node = new PostfixUnaryExpression
            {
                Pos = expression.Pos,
                Operand = expression,
                Operator = CurrentToken
            };
            NextToken();
            return FinishNode(node);
        }
        return expression;
    }

    public IExpression ParseLeftHandSideExpressionOrHigher()
    {
        var expression = CurrentToken == SyntaxKind.SuperKeyword
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
        var expression = ParseTokenNode<PrimaryExpression>(CurrentToken);
        if (CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.DotToken || CurrentToken == SyntaxKind.OpenBracketToken)
        {
            return expression;
        }
        var node = new PropertyAccessExpression
        {
            Pos = expression.Pos,
            Expression = expression
        };
        ParseExpectedToken<DotToken>(SyntaxKind.DotToken, false, Diagnostics.super_must_be_followed_by_an_argument_list_or_member_access);
        node.Name = ParseRightSideOfDot(true);
        return FinishNode(node);
    }

    public bool TagNamesAreEquivalent(IJsxTagNameExpression lhs, IJsxTagNameExpression rhs)
    {
        if (lhs.Kind != rhs.Kind)
        {
            return false;
        }
        if (lhs.Kind == SyntaxKind.Identifier)
        {
            return (lhs as Identifier).Text == (rhs as Identifier).Text;
        }
        if (lhs.Kind == SyntaxKind.ThisKeyword)
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

    public PrimaryExpression ParseJsxElementOrSelfClosingElement(bool inExpressionContext)
    {
        var opening = ParseJsxOpeningOrSelfClosingElement(inExpressionContext);
        //var result = JsxElement | JsxSelfClosingElement;
        if (opening.Kind == SyntaxKind.JsxOpeningElement)
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
            if (inExpressionContext && CurrentToken == SyntaxKind.LessThanToken)
            {
                var invalidElement = TryParse(() => ParseJsxElementOrSelfClosingElement(true));
                if (invalidElement != null)
                {
                    ParseErrorAtCurrentToken(Diagnostics.JSX_expressions_must_have_one_parent_element);
                    var badNode = new BinaryExpression
                    {
                        Pos = result.Pos,
                        End = invalidElement.End,
                        Left = result,
                        Right = invalidElement,
                        OperatorToken = (Token)CreateMissingNode<Token>(SyntaxKind.CommaToken, false, null)
                    };
                    badNode.OperatorToken.Pos = badNode.OperatorToken.End = badNode.Right.Pos;
                    return (JsxElement)(Node)badNode;
                }
            }
            return result;
        }
        else
        {
            Debug.Assert(opening.Kind == SyntaxKind.JsxSelfClosingElement);
            // Nothing else to do for self-closing elements
            var result = (JsxSelfClosingElement)opening;
            if (inExpressionContext && CurrentToken == SyntaxKind.LessThanToken)
            {
                var invalidElement = TryParse(() => ParseJsxElementOrSelfClosingElement(true));
                if (invalidElement != null)
                {
                    ParseErrorAtCurrentToken(Diagnostics.JSX_expressions_must_have_one_parent_element);
                    var badNode = new BinaryExpression
                    {
                        Pos = result.Pos,
                        End = invalidElement.End,
                        Left = result,
                        Right = invalidElement,
                        OperatorToken = (Token)CreateMissingNode<Token>(SyntaxKind.CommaToken, false, null)
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

    public Node ParseJsxChild()
    {
        switch (CurrentToken)
        {
            case SyntaxKind.JsxText:
                return ParseJsxText();
            case SyntaxKind.OpenBraceToken:
                return ParseJsxExpression(false);
            case SyntaxKind.LessThanToken:
                return ParseJsxElementOrSelfClosingElement(false);
        }
        Debug.Fail("Unknown JSX child kind " + CurrentToken);
        return null;
    }

    public NodeArray<IJsxChild> ParseJsxChildren(IExpression openingTagName)
    {
        var result = CreateList<IJsxChild>(); //List<IJsxChild>(); // 
        var saveParsingContext = ParsingContext;
        ParsingContext |= 1 << (int)ParsingContextEnum.JsxChildren;
        while (true)
        {
            CurrentToken = Scanner.ReScanJsxToken();
            if (CurrentToken == SyntaxKind.LessThanSlashToken)
            {
                // Closing tag
                break;
            }
            else
            if (CurrentToken == SyntaxKind.EndOfFileToken)
            {
                // If we hit EOF, issue the error at the tag that lacks the closing element
                // rather than at the end of the file (which is useless)
                ParseErrorAtPosition(openingTagName.Pos ?? 0, (openingTagName.End ?? 0) - (openingTagName.Pos ?? 0), Diagnostics.JSX_element_0_has_no_corresponding_closing_tag, GetTextOfNodeFromSourceText(SourceText, openingTagName));
                break;
            }
            else
            if (CurrentToken == SyntaxKind.ConflictMarkerTrivia)
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
            Properties = ParseList(ParsingContextEnum.JsxAttributes, ParseJsxAttribute)
        };
        return FinishNode(jsxAttributes);
    }
    //JsxOpeningElement | JsxSelfClosingElement

    public Expression ParseJsxOpeningOrSelfClosingElement(bool inExpressionContext)
    {
        var fullStart = Scanner.StartPos;
        ParseExpected(SyntaxKind.LessThanToken);
        var tagName = ParseJsxElementName();
        var attributes = ParseJsxAttributes();
        //JsxOpeningLikeElement node = null;
        if (CurrentToken == SyntaxKind.GreaterThanToken)
        {
            // Closing tag, so scan the immediately-following text with the JSX scanning instead
            // of regular scanning to avoid treating illegal characters (e.g. '#') as immediate
            // scanning errors
            var node = new JsxOpeningElement
            {
                Pos = fullStart,
                TagName = tagName,
                Attributes = attributes
            }; //(JsxOpeningElement)createNode(SyntaxKind.JsxOpeningElement, fullStart);
            ScanJsxText();
            return FinishNode(node);
        }
        else
        {
            ParseExpected(SyntaxKind.SlashToken);
            if (inExpressionContext)
            {
                ParseExpected(SyntaxKind.GreaterThanToken);
            }
            else
            {
                ParseExpected(SyntaxKind.GreaterThanToken, null, false);
                ScanJsxText();
            }
            var node = new JsxSelfClosingElement
            {
                Pos = fullStart,
                TagName = tagName,
                Attributes = attributes
            }; //(JsxSelfClosingElement)createNode(SyntaxKind.JsxSelfClosingElement, fullStart);
            return FinishNode(node);
        }
        //node.tagName = tagName;
        //node.attributes = attributes;
        //return finishNode(node);
    }

    public IJsxTagNameExpression ParseJsxElementName()
    {
        ScanJsxIdentifier();
        IJsxTagNameExpression expression = CurrentToken == SyntaxKind.ThisKeyword ?
                        ParseTokenNode<PrimaryExpression>(CurrentToken) : ParseIdentifierName();
        if (CurrentToken == SyntaxKind.ThisKeyword)
        {
            IJsxTagNameExpression expression2 = ParseTokenNode<PrimaryExpression>(CurrentToken);
            while (ParseOptional(SyntaxKind.DotToken))
            {
                PropertyAccessExpression propertyAccess = new PropertyAccessExpression
                {
                    Pos = expression2.Pos,
                    Expression = expression2,
                    Name = ParseRightSideOfDot(true)
                }; //(PropertyAccessExpression)createNode(SyntaxKind.PropertyAccessExpression, expression.pos);
                expression2 = FinishNode(propertyAccess);
            }
            return expression2;
        }
        else
        {
            IJsxTagNameExpression expression2 = ParseIdentifierName();
            while (ParseOptional(SyntaxKind.DotToken))
            {
                PropertyAccessExpression propertyAccess = new PropertyAccessExpression
                {
                    Pos = expression2.Pos,
                    Expression = expression2,
                    Name = ParseRightSideOfDot(true)
                }; //(PropertyAccessExpression)createNode(SyntaxKind.PropertyAccessExpression, expression.pos);
                expression2 = FinishNode(propertyAccess);
            }
            return expression2;
        }
    }

    public JsxExpression ParseJsxExpression(bool inExpressionContext)
    {
        var node = new JsxExpression() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBraceToken);
        if (CurrentToken != SyntaxKind.CloseBraceToken)
        {
            node.DotDotDotToken = (DotDotDotToken)ParseOptionalToken<DotDotDotToken>(SyntaxKind.DotDotDotToken);
            node.Expression = ParseAssignmentExpressionOrHigher();
        }
        if (inExpressionContext)
        {
            ParseExpected(SyntaxKind.CloseBraceToken);
        }
        else
        {
            ParseExpected(SyntaxKind.CloseBraceToken, null, false);
            ScanJsxText();
        }
        return FinishNode(node);
    }
    //JsxAttribute | JsxSpreadAttribute

    public ObjectLiteralElement ParseJsxAttribute()
    {
        if (CurrentToken == SyntaxKind.OpenBraceToken)
        {
            return ParseJsxSpreadAttribute();
        }
        ScanJsxIdentifier();
        var node = new JsxAttribute
        {
            Pos = Scanner.StartPos,
            Name = ParseIdentifierName()
        };
        if (CurrentToken == SyntaxKind.EqualsToken)
        {
            node.Initializer = ScanJsxAttributeValue() switch
            {
                SyntaxKind.StringLiteral => (StringLiteral)ParseLiteralNode(),
                _ => ParseJsxExpression(true),
            };
        }
        return FinishNode(node);
    }

    public JsxSpreadAttribute ParseJsxSpreadAttribute()
    {
        var node = new JsxSpreadAttribute() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBraceToken);
        ParseExpected(SyntaxKind.DotDotDotToken);
        node.Expression = ParseExpression();
        ParseExpected(SyntaxKind.CloseBraceToken);
        return FinishNode(node);
    }

    public JsxClosingElement ParseJsxClosingElement(bool inExpressionContext)
    {
        var node = new JsxClosingElement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.LessThanSlashToken);
        node.TagName = ParseJsxElementName();
        if (inExpressionContext)
        {
            ParseExpected(SyntaxKind.GreaterThanToken);
        }
        else
        {
            ParseExpected(SyntaxKind.GreaterThanToken, null, false);
            ScanJsxText();
        }
        return FinishNode(node);
    }

    public TypeAssertion ParseTypeAssertion()
    {
        var node = new TypeAssertion() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.LessThanToken);
        node.Type = ParseType();
        ParseExpected(SyntaxKind.GreaterThanToken);
        node.Expression = ParseSimpleUnaryExpression(); // as UnaryExpression;
        return FinishNode(node);
    }

    public IMemberExpression ParseMemberExpressionRest(IMemberExpression expression)
    {
        while (true)
        {
            var dotToken = (DotToken)ParseOptionalToken<DotToken>(SyntaxKind.DotToken);
            if (dotToken != null)
            {
                var propertyAccess = new PropertyAccessExpression
                {
                    Pos = expression.Pos,
                    Expression = expression,
                    Name = ParseRightSideOfDot(true)
                };
                expression = FinishNode(propertyAccess);
                continue;
            }
            if (CurrentToken == SyntaxKind.ExclamationToken && !Scanner.HasPrecedingLineBreak)
            {
                NextToken();
                var nonNullExpression = new NonNullExpression
                {
                    Pos = expression.Pos,
                    Expression = expression
                };
                expression = FinishNode(nonNullExpression);
                continue;
            }
            if (!InDecoratorContext() && ParseOptional(SyntaxKind.OpenBracketToken))
            {
                var indexedAccess = new ElementAccessExpression
                {
                    Pos = expression.Pos,
                    Expression = expression
                };
                if (CurrentToken != SyntaxKind.CloseBracketToken)
                {
                    indexedAccess.ArgumentExpression = AllowInAnd(ParseExpression);
                    if (indexedAccess.ArgumentExpression.Kind == SyntaxKind.StringLiteral ||
                        indexedAccess.ArgumentExpression.Kind == SyntaxKind.NumericLiteral)
                    {
                        var literal = (LiteralExpression)indexedAccess.ArgumentExpression;//(LiteralExpression)
                        literal.Text = InternIdentifier(literal.Text);
                    }
                }
                ParseExpected(SyntaxKind.CloseBracketToken);
                expression = FinishNode(indexedAccess);
                continue;
            }
            if (CurrentToken == SyntaxKind.NoSubstitutionTemplateLiteral || CurrentToken == SyntaxKind.TemplateHead)
            {
                var tagExpression = new TaggedTemplateExpression
                {
                    Pos = expression.Pos,
                    Tag = expression,
                    Template = CurrentToken == SyntaxKind.NoSubstitutionTemplateLiteral
                    ? (Node)ParseLiteralNode()
                    : ParseTemplateExpression()
                };
                expression = FinishNode(tagExpression);
                continue;
            }
            return expression;
        }
    }

    public IMemberExpression ParseCallExpressionRest(IMemberExpression expression)
    {
        while (true)
        {
            expression = ParseMemberExpressionRest(expression);
            if (CurrentToken == SyntaxKind.LessThanToken)
            {
                var typeArguments = TryParse(ParseTypeArgumentsInExpression);
                if (typeArguments == null)
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
            if (CurrentToken == SyntaxKind.OpenParenToken)
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
        ParseExpected(SyntaxKind.OpenParenToken);
        var result = ParseDelimitedList(ParsingContextEnum.ArgumentExpressions, ParseArgumentExpression);
        ParseExpected(SyntaxKind.CloseParenToken);
        return result;
    }

    public NodeArray<ITypeNode> ParseTypeArgumentsInExpression()
    {
        if (!ParseOptional(SyntaxKind.LessThanToken))
        {
            return null;
        }
        var typeArguments = ParseDelimitedList(ParsingContextEnum.TypeArguments, ParseType);
        if (!ParseExpected(SyntaxKind.GreaterThanToken))
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

    public bool CanFollowTypeArgumentsInExpression() => CurrentToken switch
    {
        SyntaxKind.OpenParenToken or SyntaxKind.DotToken or SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken or SyntaxKind.ColonToken or SyntaxKind.SemicolonToken or SyntaxKind.QuestionToken or SyntaxKind.EqualsEqualsToken or SyntaxKind.EqualsEqualsEqualsToken or SyntaxKind.ExclamationEqualsToken or SyntaxKind.ExclamationEqualsEqualsToken or SyntaxKind.AmpersandAmpersandToken or SyntaxKind.BarBarToken or SyntaxKind.CaretToken or SyntaxKind.AmpersandToken or SyntaxKind.BarToken or SyntaxKind.CloseBraceToken or SyntaxKind.EndOfFileToken => true,// foo<literal>
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               // these cases can't legally follow a type arg list.  However, they're not legal
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               // expressions either.  The user is probably in the middle of a generic type. So
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               // treat it as such.
        _ => false,// Anything else treat as an expression.
    };

    public IPrimaryExpression ParsePrimaryExpression()
    {
        switch (CurrentToken)
        {
            case SyntaxKind.NumericLiteral:
            case SyntaxKind.StringLiteral:
            case SyntaxKind.NoSubstitutionTemplateLiteral:
                return ParseLiteralNode();
            case SyntaxKind.ThisKeyword:
            case SyntaxKind.SuperKeyword:
            case SyntaxKind.NullKeyword:
            case SyntaxKind.TrueKeyword:
            case SyntaxKind.FalseKeyword:
                return ParseTokenNode<PrimaryExpression>(CurrentToken);
            case SyntaxKind.OpenParenToken:
                return ParseParenthesizedExpression();
            case SyntaxKind.OpenBracketToken:
                return ParseArrayLiteralExpression();
            case SyntaxKind.OpenBraceToken:
                return ParseObjectLiteralExpression();
            case SyntaxKind.AsyncKeyword:
                if (!LookAhead(NextTokenIsFunctionKeywordOnSameLine))
                {
                    break;
                }
                return ParseFunctionExpression();
            case SyntaxKind.ClassKeyword:
                return ParseClassExpression();
            case SyntaxKind.FunctionKeyword:
                return ParseFunctionExpression();
            case SyntaxKind.NewKeyword:
                return ParseNewExpression();
            case SyntaxKind.SlashToken:
            case SyntaxKind.SlashEqualsToken:
                if (ReScanSlashToken() == SyntaxKind.RegularExpressionLiteral)
                {
                    return ParseLiteralNode();
                }
                break;
            case SyntaxKind.TemplateHead:
                return ParseTemplateExpression();
        }
        return ParseIdentifier(Diagnostics.Expression_expected);
    }

    public ParenthesizedExpression ParseParenthesizedExpression()
    {
        var node = new ParenthesizedExpression() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenParenToken);
        node.Expression = AllowInAnd(ParseExpression);
        ParseExpected(SyntaxKind.CloseParenToken);
        return FinishNode(node);
    }

    public Expression ParseSpreadElement()
    {
        var node = new SpreadElement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.DotDotDotToken);
        node.Expression = ParseAssignmentExpressionOrHigher();
        return FinishNode(node);
    }

    public IExpression ParseArgumentOrArrayLiteralElement() => CurrentToken == SyntaxKind.DotDotDotToken ? ParseSpreadElement() :
            CurrentToken == SyntaxKind.CommaToken
            ? new OmittedExpression() { Pos = Scanner.StartPos }
            : ParseAssignmentExpressionOrHigher();

    public IExpression ParseArgumentExpression() => DoOutsideOfContext(DisallowInAndDecoratorContext, ParseArgumentOrArrayLiteralElement);

    public ArrayLiteralExpression ParseArrayLiteralExpression()
    {
        var node = new ArrayLiteralExpression() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBracketToken);
        if (Scanner.HasPrecedingLineBreak)
        {
            node.MultiLine = true;
        }
        node.Elements = ParseDelimitedList(ParsingContextEnum.ArrayLiteralMembers, ParseArgumentOrArrayLiteralElement);
        ParseExpected(SyntaxKind.CloseBracketToken);
        return FinishNode(node);
    }

    public IAccessorDeclaration TryParseAccessorDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        if (ParseContextualModifier(SyntaxKind.GetKeyword))
        {
            return ParseAccessorDeclaration(SyntaxKind.GetAccessor, fullStart, decorators, modifiers);
        }
        else
        if (ParseContextualModifier(SyntaxKind.SetKeyword))
        {
            return ParseAccessorDeclaration(SyntaxKind.SetAccessor, fullStart, decorators, modifiers);
        }
        return null;
    }

    public IObjectLiteralElementLike ParseObjectLiteralElement()
    {
        var fullStart = Scanner.StartPos;
        var dotDotDotToken = (DotDotDotToken)ParseOptionalToken<DotDotDotToken>(SyntaxKind.DotDotDotToken);
        if (dotDotDotToken != null)
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
        var asteriskToken = (AsteriskToken)ParseOptionalToken<AsteriskToken>(SyntaxKind.AsteriskToken);
        var tokenIsIdentifier = IsIdentifier();
        var propertyName = ParsePropertyName(); // parseIdentifierName(); // 
        var questionToken = (QuestionToken)ParseOptionalToken<QuestionToken>(SyntaxKind.QuestionToken);
        if (asteriskToken != null || CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.LessThanToken)
        {
            return ParseMethodDeclaration(fullStart, decorators, modifiers, asteriskToken, propertyName, questionToken);
        }
        var isShorthandPropertyAssignment =
                        tokenIsIdentifier && (CurrentToken == SyntaxKind.CommaToken || CurrentToken == SyntaxKind.CloseBraceToken || CurrentToken == SyntaxKind.EqualsToken);
        if (isShorthandPropertyAssignment)
        {
            var shorthandDeclaration = new ShorthandPropertyAssignment
            {
                Pos = fullStart,
                Name = (Identifier)propertyName,
                QuestionToken = questionToken
            };
            var equalsToken = (EqualsToken)ParseOptionalToken<EqualsToken>(SyntaxKind.EqualsToken);
            if (equalsToken != null)
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
            ParseExpected(SyntaxKind.ColonToken);
            propertyAssignment.Initializer = AllowInAnd(ParseAssignmentExpressionOrHigher);
            return AddJsDocComment(FinishNode(propertyAssignment));
        }
    }

    public ObjectLiteralExpression ParseObjectLiteralExpression()
    {
        var node = new ObjectLiteralExpression() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBraceToken);
        if (Scanner.HasPrecedingLineBreak)
        {
            node.MultiLine = true;
        }
        node.Properties = ParseDelimitedList(ParsingContextEnum.ObjectLiteralMembers, ParseObjectLiteralElement, true);
        ParseExpected(SyntaxKind.CloseBraceToken);
        return FinishNode(node);
    }

    public FunctionExpression ParseFunctionExpression()
    {
        var saveDecoratorContext = InDecoratorContext();
        if (saveDecoratorContext)
        {
            SetDecoratorContext(false);
        }
        var node = new FunctionExpression
        {
            Pos = Scanner.StartPos,
            Modifiers = ParseModifiers()
        };
        ParseExpected(SyntaxKind.FunctionKeyword);
        node.AsteriskToken = (AsteriskToken)ParseOptionalToken<AsteriskToken>(SyntaxKind.AsteriskToken);
        var isGenerator = node.AsteriskToken != null;
        var isAsync = (GetModifierFlags(node) & ModifierFlags.Async) != 0;
        node.Name =
            isGenerator && isAsync ? DoInYieldAndAwaitContext(ParseOptionalIdentifier) :
                isGenerator ? DoInYieldContext(ParseOptionalIdentifier) :
                    isAsync ? DoInAwaitContext(ParseOptionalIdentifier) :
                        ParseOptionalIdentifier();
        FillSignature(SyntaxKind.ColonToken, isGenerator, isAsync, false, node);
        node.Body = ParseFunctionBlock(isGenerator, isAsync, false);
        if (saveDecoratorContext)
        {
            SetDecoratorContext(true);
        }
        return AddJsDocComment(FinishNode(node));
    }

    public Identifier ParseOptionalIdentifier() => IsIdentifier() ? ParseIdentifier() : null;

    public IPrimaryExpression ParseNewExpression()
    {
        var fullStart = Scanner.StartPos;
        ParseExpected(SyntaxKind.NewKeyword);
        if (ParseOptional(SyntaxKind.DotToken))
        {
            var node = new MetaProperty
            {
                Pos = fullStart,
                KeywordToken = SyntaxKind.NewKeyword,
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
            if (node.TypeArguments != null || CurrentToken == SyntaxKind.OpenParenToken)
            {
                node.Arguments = ParseArgumentList();
            }
            return FinishNode(node);
        }
    }

    public Block ParseBlock(bool ignoreMissingOpenBrace, DiagnosticMessage? diagnosticMessage = null)
    {
        var node = new Block() { Pos = Scanner.StartPos };
        if (ParseExpected(SyntaxKind.OpenBraceToken, diagnosticMessage) || ignoreMissingOpenBrace)
        {
            if (Scanner.HasPrecedingLineBreak)
            {
                node.MultiLine = true;
            }
            node.Statements = ParseList2(ParsingContextEnum.BlockStatements, ParseStatement);
            ParseExpected(SyntaxKind.CloseBraceToken);
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
            SetDecoratorContext(false);
        }
        var block = ParseBlock(ignoreMissingOpenBrace, diagnosticMessage);
        if (saveDecoratorContext)
        {
            SetDecoratorContext(true);
        }
        SetYieldContext(savedYieldContext);
        SetAwaitContext(savedAwaitContext);
        return block;
    }

    public EmptyStatement ParseEmptyStatement()
    {
        var node = new EmptyStatement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.SemicolonToken);
        return FinishNode(node);
    }

    public IfStatement ParseIfStatement()
    {
        var node = new IfStatement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.IfKeyword);
        ParseExpected(SyntaxKind.OpenParenToken);
        node.Expression = AllowInAnd(ParseExpression);
        ParseExpected(SyntaxKind.CloseParenToken);
        node.ThenStatement = ParseStatement();
        node.ElseStatement = ParseOptional(SyntaxKind.ElseKeyword) ? ParseStatement() : null;
        return FinishNode(node);
    }

    public DoStatement ParseDoStatement()
    {
        var node = new DoStatement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.DoKeyword);
        node.Statement = ParseStatement();
        ParseExpected(SyntaxKind.WhileKeyword);
        ParseExpected(SyntaxKind.OpenParenToken);
        node.Expression = AllowInAnd(ParseExpression);
        ParseExpected(SyntaxKind.CloseParenToken);
        // From: https://mail.mozilla.org/pipermail/es-discuss/2011-August/016188.html
        // 157 min --- All allen at wirfs-brock.com CONF --- "do{;}while(false)false" prohibited in
        // spec but allowed in consensus reality. Approved -- this is the de-facto standard whereby
        //  do;while(0)literal will have a semicolon inserted before literal.
        ParseOptional(SyntaxKind.SemicolonToken);
        return FinishNode(node);
    }

    public WhileStatement ParseWhileStatement()
    {
        var node = new WhileStatement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.WhileKeyword);
        ParseExpected(SyntaxKind.OpenParenToken);
        node.Expression = AllowInAnd(ParseExpression);
        ParseExpected(SyntaxKind.CloseParenToken);
        node.Statement = ParseStatement();
        return FinishNode(node);
    }

    public Statement ParseForOrForInOrForOfStatement()
    {
        var pos = GetNodePos();
        ParseExpected(SyntaxKind.ForKeyword);
        var awaitToken = (AwaitKeywordToken)ParseOptionalToken<AwaitKeywordToken>(SyntaxKind.AwaitKeyword);
        ParseExpected(SyntaxKind.OpenParenToken);
        IVariableDeclarationListOrExpression initializer = null;
        //Node initializer = null;
        if (CurrentToken != SyntaxKind.SemicolonToken)
        {
            if (CurrentToken == SyntaxKind.VarKeyword || CurrentToken == SyntaxKind.LetKeyword || CurrentToken == SyntaxKind.ConstKeyword)
            {
                initializer = ParseVariableDeclarationList(true);
            }
            else
            {
                initializer = DisallowInAnd(ParseExpression);
            }
        }
        IterationStatement forOrForInOrForOfStatement = null;
        if (awaitToken != null ? ParseExpected(SyntaxKind.OfKeyword) : ParseOptional(SyntaxKind.OfKeyword))
        {
            var forOfStatement = new ForOfStatement
            {
                Pos = pos,
                AwaitModifier = awaitToken,
                Initializer = initializer,
                Expression = AllowInAnd(ParseAssignmentExpressionOrHigher)
            };
            ParseExpected(SyntaxKind.CloseParenToken);
            forOrForInOrForOfStatement = forOfStatement;
        }
        else
        if (ParseOptional(SyntaxKind.InKeyword))
        {
            var forInStatement = new ForInStatement
            {
                Pos = pos,
                Initializer = initializer,
                Expression = AllowInAnd(ParseExpression)
            };
            ParseExpected(SyntaxKind.CloseParenToken);
            forOrForInOrForOfStatement = forInStatement;
        }
        else
        {
            var forStatement = new ForStatement
            {
                Pos = pos,
                Initializer = initializer
            };
            ParseExpected(SyntaxKind.SemicolonToken);
            if (CurrentToken != SyntaxKind.SemicolonToken && CurrentToken != SyntaxKind.CloseParenToken)
            {
                forStatement.Condition = AllowInAnd(ParseExpression);
            }
            ParseExpected(SyntaxKind.SemicolonToken);
            if (CurrentToken != SyntaxKind.CloseParenToken)
            {
                forStatement.Incrementor = AllowInAnd(ParseExpression);
            }
            ParseExpected(SyntaxKind.CloseParenToken);
            forOrForInOrForOfStatement = forStatement;
        }
        forOrForInOrForOfStatement.Statement = ParseStatement();
        return FinishNode(forOrForInOrForOfStatement);
    }

    public IBreakOrContinueStatement ParseBreakOrContinueStatement(SyntaxKind kind)
    {
        var node = kind == SyntaxKind.ContinueStatement ? (IBreakOrContinueStatement)new ContinueStatement { Pos = Scanner.StartPos } : kind == SyntaxKind.BreakStatement ? new BreakStatement { Pos = Scanner.StartPos } : throw new NotSupportedException("parseBreakOrContinueStatement");
        ParseExpected(kind == SyntaxKind.BreakStatement ? SyntaxKind.BreakKeyword : SyntaxKind.ContinueKeyword);
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
        ParseExpected(SyntaxKind.ReturnKeyword);
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
        ParseExpected(SyntaxKind.WithKeyword);
        ParseExpected(SyntaxKind.OpenParenToken);
        node.Expression = AllowInAnd(ParseExpression);
        ParseExpected(SyntaxKind.CloseParenToken);
        node.Statement = ParseStatement();
        return FinishNode(node);
    }

    public CaseClause ParseCaseClause()
    {
        var node = new CaseClause() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.CaseKeyword);
        node.Expression = AllowInAnd(ParseExpression);
        ParseExpected(SyntaxKind.ColonToken);
        node.Statements = ParseList2(ParsingContextEnum.SwitchClauseStatements, ParseStatement);
        return FinishNode(node);
    }

    public DefaultClause ParseDefaultClause()
    {
        var node = new DefaultClause() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.DefaultKeyword);
        ParseExpected(SyntaxKind.ColonToken);
        node.Statements = ParseList2(ParsingContextEnum.SwitchClauseStatements, ParseStatement);
        return FinishNode(node);
    }

    public ICaseOrDefaultClause ParseCaseOrDefaultClause() => CurrentToken == SyntaxKind.CaseKeyword ? ParseCaseClause() : ParseDefaultClause();

    public SwitchStatement ParseSwitchStatement()
    {
        var node = new SwitchStatement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.SwitchKeyword);
        ParseExpected(SyntaxKind.OpenParenToken);
        node.Expression = AllowInAnd(ParseExpression);
        ParseExpected(SyntaxKind.CloseParenToken);
        var caseBlock = new CaseBlock() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBraceToken);
        caseBlock.Clauses = ParseList(ParsingContextEnum.SwitchClauses, ParseCaseOrDefaultClause);
        ParseExpected(SyntaxKind.CloseBraceToken);
        node.CaseBlock = FinishNode(caseBlock);
        return FinishNode(node);
    }

    public ThrowStatement ParseThrowStatement()
    {
        var node = new ThrowStatement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.ThrowKeyword);
        node.Expression = Scanner.HasPrecedingLineBreak ? null : AllowInAnd(ParseExpression);
        ParseSemicolon();
        return FinishNode(node);
    }

    public TryStatement ParseTryStatement()
    {
        var node = new TryStatement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.TryKeyword);
        node.TryBlock = ParseBlock(false);
        node.CatchClause = CurrentToken == SyntaxKind.CatchKeyword ? ParseCatchClause() : null;
        if (node.CatchClause == null || CurrentToken == SyntaxKind.FinallyKeyword)
        {
            ParseExpected(SyntaxKind.FinallyKeyword);
            node.FinallyBlock = ParseBlock(false);
        }
        return FinishNode(node);
    }

    public CatchClause ParseCatchClause()
    {
        var result = new CatchClause() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.CatchKeyword);
        if (ParseExpected(SyntaxKind.OpenParenToken))
        {
            result.VariableDeclaration = ParseVariableDeclaration();
        }
        ParseExpected(SyntaxKind.CloseParenToken);
        result.Block = ParseBlock(false);
        return FinishNode(result);
    }

    public DebuggerStatement ParseDebuggerStatement()
    {
        var node = new DebuggerStatement() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.DebuggerKeyword);
        ParseSemicolon();
        return FinishNode(node);
    }

    public Statement ParseExpressionOrLabeledStatement()
    {
        var fullStart = Scanner.StartPos;
        var expression = AllowInAnd(ParseExpression);
        if (expression.Kind == SyntaxKind.Identifier && ParseOptional(SyntaxKind.ColonToken))
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
        NextToken();
        return TokenIsIdentifierOrKeyword(CurrentToken) && !Scanner.HasPrecedingLineBreak;
    }

    public bool NextTokenIsFunctionKeywordOnSameLine()
    {
        NextToken();
        return CurrentToken == SyntaxKind.FunctionKeyword && !Scanner.HasPrecedingLineBreak;
    }

    public bool NextTokenIsIdentifierOrKeywordOrNumberOnSameLine()
    {
        NextToken();
        return (TokenIsIdentifierOrKeyword(CurrentToken) || CurrentToken == SyntaxKind.NumericLiteral) && !Scanner.HasPrecedingLineBreak;
    }

    public bool IsDeclaration()
    {
        while (true)
        {
            switch (CurrentToken)
            {
                case SyntaxKind.VarKeyword:
                case SyntaxKind.LetKeyword:
                case SyntaxKind.ConstKeyword:
                case SyntaxKind.FunctionKeyword:
                case SyntaxKind.ClassKeyword:
                case SyntaxKind.EnumKeyword:
                    return true;
                case SyntaxKind.InterfaceKeyword:
                case SyntaxKind.TypeKeyword:
                    return NextTokenIsIdentifierOnSameLine();
                case SyntaxKind.ModuleKeyword:
                case SyntaxKind.NamespaceKeyword:
                    return NextTokenIsIdentifierOrStringLiteralOnSameLine();
                case SyntaxKind.AbstractKeyword:
                case SyntaxKind.AsyncKeyword:
                case SyntaxKind.DeclareKeyword:
                case SyntaxKind.PrivateKeyword:
                case SyntaxKind.ProtectedKeyword:
                case SyntaxKind.PublicKeyword:
                case SyntaxKind.ReadonlyKeyword:
                    NextToken();
                    if (Scanner.HasPrecedingLineBreak)
                    {
                        return false;
                    }
                    continue;
                case SyntaxKind.GlobalKeyword:
                    NextToken();
                    return CurrentToken == SyntaxKind.OpenBraceToken || CurrentToken == SyntaxKind.Identifier || CurrentToken == SyntaxKind.ExportKeyword;
                case SyntaxKind.ImportKeyword:
                    NextToken();
                    return CurrentToken == SyntaxKind.StringLiteral || CurrentToken == SyntaxKind.AsteriskToken ||
                        CurrentToken == SyntaxKind.OpenBraceToken || TokenIsIdentifierOrKeyword(CurrentToken);
                case SyntaxKind.ExportKeyword:
                    NextToken();
                    if (CurrentToken == SyntaxKind.EqualsToken || CurrentToken == SyntaxKind.AsteriskToken ||
                                                CurrentToken == SyntaxKind.OpenBraceToken || CurrentToken == SyntaxKind.DefaultKeyword ||
                                                CurrentToken == SyntaxKind.AsKeyword)
                    {
                        return true;
                    }
                    continue;
                case SyntaxKind.StaticKeyword:
                    NextToken();
                    continue;
                default:
                    return false;
            }
        }
    }

    public bool IsStartOfDeclaration() => LookAhead(IsDeclaration);

    public bool IsStartOfStatement() => CurrentToken switch
    {
        SyntaxKind.AtToken or SyntaxKind.SemicolonToken or SyntaxKind.OpenBraceToken or SyntaxKind.VarKeyword or SyntaxKind.LetKeyword or SyntaxKind.FunctionKeyword or SyntaxKind.ClassKeyword or SyntaxKind.EnumKeyword or SyntaxKind.IfKeyword or SyntaxKind.DoKeyword or SyntaxKind.WhileKeyword or SyntaxKind.ForKeyword or SyntaxKind.ContinueKeyword or SyntaxKind.BreakKeyword or SyntaxKind.ReturnKeyword or SyntaxKind.WithKeyword or SyntaxKind.SwitchKeyword or SyntaxKind.ThrowKeyword or SyntaxKind.TryKeyword or SyntaxKind.DebuggerKeyword or SyntaxKind.CatchKeyword or SyntaxKind.FinallyKeyword => true,
        SyntaxKind.ConstKeyword or SyntaxKind.ExportKeyword or SyntaxKind.ImportKeyword => IsStartOfDeclaration(),
        SyntaxKind.AsyncKeyword or SyntaxKind.DeclareKeyword or SyntaxKind.InterfaceKeyword or SyntaxKind.ModuleKeyword or SyntaxKind.NamespaceKeyword or SyntaxKind.TypeKeyword or SyntaxKind.GlobalKeyword => true,// When these don't start a declaration, they're an identifier in an expression statement
        SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.StaticKeyword or SyntaxKind.ReadonlyKeyword => IsStartOfDeclaration() || !LookAhead(NextTokenIsIdentifierOrKeywordOnSameLine),// When these don't start a declaration, they may be the start of a class member if an identifier
                                                                                                                                                                                                                                         // immediately follows. Otherwise they're an identifier in an expression statement.
        _ => IsStartOfExpression(),
    };

    public bool NextTokenIsIdentifierOrStartOfDestructuring()
    {
        NextToken();
        return IsIdentifier() || CurrentToken == SyntaxKind.OpenBraceToken || CurrentToken == SyntaxKind.OpenBracketToken;
    }

    public bool IsLetDeclaration() =>
        // In ES6 'let' always starts a lexical declaration if followed by an identifier or {
        // or [.
        LookAhead(NextTokenIsIdentifierOrStartOfDestructuring);

    public IStatement ParseStatement()
    {
        switch (CurrentToken)
        {
            case SyntaxKind.SemicolonToken:
                return ParseEmptyStatement();
            case SyntaxKind.OpenBraceToken:
                return ParseBlock(false);
            case SyntaxKind.VarKeyword:
                return ParseVariableStatement(Scanner.StartPos, null, null);
            case SyntaxKind.LetKeyword:
                if (IsLetDeclaration())
                {
                    return ParseVariableStatement(Scanner.StartPos, null, null);
                }
                break;
            case SyntaxKind.FunctionKeyword:
                return ParseFunctionDeclaration(Scanner.StartPos, null, null);
            case SyntaxKind.ClassKeyword:
                return ParseClassDeclaration(Scanner.StartPos, null, null);
            case SyntaxKind.IfKeyword:
                return ParseIfStatement();
            case SyntaxKind.DoKeyword:
                return ParseDoStatement();
            case SyntaxKind.WhileKeyword:
                return ParseWhileStatement();
            case SyntaxKind.ForKeyword:
                return ParseForOrForInOrForOfStatement();
            case SyntaxKind.ContinueKeyword:
                return ParseBreakOrContinueStatement(SyntaxKind.ContinueStatement);
            case SyntaxKind.BreakKeyword:
                return ParseBreakOrContinueStatement(SyntaxKind.BreakStatement);
            case SyntaxKind.ReturnKeyword:
                return ParseReturnStatement();
            case SyntaxKind.WithKeyword:
                return ParseWithStatement();
            case SyntaxKind.SwitchKeyword:
                return ParseSwitchStatement();
            case SyntaxKind.ThrowKeyword:
                return ParseThrowStatement();
            case SyntaxKind.TryKeyword:
            case SyntaxKind.CatchKeyword:
            case SyntaxKind.FinallyKeyword:
                return ParseTryStatement();
            case SyntaxKind.DebuggerKeyword:
                return ParseDebuggerStatement();
            case SyntaxKind.AtToken:
                return ParseDeclaration();
            case SyntaxKind.AsyncKeyword:
            case SyntaxKind.InterfaceKeyword:
            case SyntaxKind.TypeKeyword:
            case SyntaxKind.ModuleKeyword:
            case SyntaxKind.NamespaceKeyword:
            case SyntaxKind.DeclareKeyword:
            case SyntaxKind.ConstKeyword:
            case SyntaxKind.EnumKeyword:
            case SyntaxKind.ExportKeyword:
            case SyntaxKind.ImportKeyword:
            case SyntaxKind.PrivateKeyword:
            case SyntaxKind.ProtectedKeyword:
            case SyntaxKind.PublicKeyword:
            case SyntaxKind.AbstractKeyword:
            case SyntaxKind.StaticKeyword:
            case SyntaxKind.ReadonlyKeyword:
            case SyntaxKind.GlobalKeyword:
                if (IsStartOfDeclaration())
                {
                    return ParseDeclaration();
                }
                break;
        }
        return ParseExpressionOrLabeledStatement();
    }

    public IStatement ParseDeclaration()
    {
        var fullStart = GetNodePos();
        var decorators = ParseDecorators();
        var modifiers = ParseModifiers();
        switch (CurrentToken)
        {
            case SyntaxKind.VarKeyword:
            case SyntaxKind.LetKeyword:
            case SyntaxKind.ConstKeyword:
                return ParseVariableStatement(fullStart, decorators, modifiers);
            case SyntaxKind.FunctionKeyword:
                return ParseFunctionDeclaration(fullStart, decorators, modifiers);
            case SyntaxKind.ClassKeyword:
                return ParseClassDeclaration(fullStart, decorators, modifiers);
            case SyntaxKind.InterfaceKeyword:
                return ParseInterfaceDeclaration(fullStart, decorators, modifiers);
            case SyntaxKind.TypeKeyword:
                return ParseTypeAliasDeclaration(fullStart, decorators, modifiers);
            case SyntaxKind.EnumKeyword:
                return ParseEnumDeclaration(fullStart, decorators, modifiers);
            case SyntaxKind.GlobalKeyword:
            case SyntaxKind.ModuleKeyword:
            case SyntaxKind.NamespaceKeyword:
                return ParseModuleDeclaration(fullStart, decorators, modifiers);
            case SyntaxKind.ImportKeyword:
                return ParseImportDeclarationOrImportEqualsDeclaration(fullStart, decorators, modifiers);
            case SyntaxKind.ExportKeyword:
                NextToken();
                return CurrentToken switch
                {
                    SyntaxKind.DefaultKeyword or SyntaxKind.EqualsToken => ParseExportAssignment(fullStart, decorators, modifiers),
                    SyntaxKind.AsKeyword => ParseNamespaceExportDeclaration(fullStart, decorators, modifiers),
                    _ => ParseExportDeclaration(fullStart, decorators, modifiers),
                };
            default:
                if (decorators?.Any() == true || modifiers?.Any() == true)
                {
                    // We reached this point because we encountered decorators and/or modifiers and assumed a declaration
                    // would follow. For recovery and error reporting purposes, return an incomplete declaration.
                    var node = (Statement)CreateMissingNode<Statement>(SyntaxKind.MissingDeclaration, true, Diagnostics.Declaration_expected);
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
        NextToken();
        return !Scanner.HasPrecedingLineBreak && (IsIdentifier() || CurrentToken == SyntaxKind.StringLiteral);
    }

    public Block ParseFunctionBlockOrSemicolon(bool isGenerator, bool isAsync, DiagnosticMessage? diagnosticMessage = null)
    {
        if (CurrentToken != SyntaxKind.OpenBraceToken && CanParseSemicolon())
        {
            ParseSemicolon();
            return null;
        }
        return ParseFunctionBlock(isGenerator, isAsync, false, diagnosticMessage);
    }

    public IArrayBindingElement ParseArrayBindingElement()
    {
        if (CurrentToken == SyntaxKind.CommaToken)
        {
            return new OmittedExpression { Pos = Scanner.StartPos }; //(OmittedExpression)createNode(SyntaxKind.OmittedExpression);
        }
        var node = new BindingElement
        {
            Pos = Scanner.StartPos,
            DotDotDotToken = (DotDotDotToken)ParseOptionalToken<DotDotDotToken>(SyntaxKind.DotDotDotToken),
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
            DotDotDotToken = (DotDotDotToken)ParseOptionalToken<DotDotDotToken>(SyntaxKind.DotDotDotToken)
        };
        var tokenIsIdentifier = IsIdentifier();
        var propertyName = ParsePropertyName();
        if (tokenIsIdentifier && CurrentToken != SyntaxKind.ColonToken)
        {
            node.Name = (Identifier)propertyName;
        }
        else
        {
            ParseExpected(SyntaxKind.ColonToken);
            node.PropertyName = propertyName;
            node.Name = ParseIdentifierOrPattern();
        }
        node.Initializer = ParseBindingElementInitializer(false);
        return FinishNode(node);
    }

    public ObjectBindingPattern ParseObjectBindingPattern()
    {
        var node = new ObjectBindingPattern() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBraceToken);
        node.Elements = ParseDelimitedList(ParsingContextEnum.ObjectBindingElements, ParseObjectBindingElement);
        ParseExpected(SyntaxKind.CloseBraceToken);
        return FinishNode(node);
    }

    public ArrayBindingPattern ParseArrayBindingPattern()
    {
        var node = new ArrayBindingPattern() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.OpenBracketToken);
        node.Elements = ParseDelimitedList(ParsingContextEnum.ArrayBindingElements, ParseArrayBindingElement);
        ParseExpected(SyntaxKind.CloseBracketToken);
        return FinishNode(node);
    }

    public bool IsIdentifierOrPattern() => CurrentToken == SyntaxKind.OpenBraceToken || CurrentToken == SyntaxKind.OpenBracketToken || IsIdentifier();

    public Node ParseIdentifierOrPattern()
    {
        if (CurrentToken == SyntaxKind.OpenBracketToken)
        {
            return ParseArrayBindingPattern();
        }
        return CurrentToken == SyntaxKind.OpenBraceToken ? ParseObjectBindingPattern() : ParseIdentifier();
    }

    public VariableDeclaration ParseVariableDeclaration()
    {
        var node = new VariableDeclaration
        {
            Pos = Scanner.StartPos,
            Name = ParseIdentifierOrPattern(),
            Type = ParseTypeAnnotation()
        };
        if (!IsInOrOfKeyword(CurrentToken))
        {
            node.Initializer = ParseInitializer(false);
        }
        return FinishNode(node);
    }

    public IVariableDeclarationList ParseVariableDeclarationList(bool inForStatementInitializer)
    {
        var node = new VariableDeclarationList() { Pos = Scanner.StartPos };
        switch (CurrentToken)
        {
            case SyntaxKind.VarKeyword:
                break;
            case SyntaxKind.LetKeyword:
                node.Flags |= NodeFlags.Let;
                break;
            case SyntaxKind.ConstKeyword:
                node.Flags |= NodeFlags.Const;
                break;
            default:
                Debug.Fail("This should never happen");
                break;
        }
        NextToken();
        if (CurrentToken == SyntaxKind.OfKeyword && LookAhead(CanFollowContextualOfKeyword))
        {
            node.Declarations = CreateMissingList<VariableDeclaration>();
        }
        else
        {
            var savedDisallowIn = InDisallowInContext();
            SetDisallowInContext(inForStatementInitializer);
            node.Declarations = ParseDelimitedList(ParsingContextEnum.VariableDeclarations, ParseVariableDeclaration);
            SetDisallowInContext(savedDisallowIn);
        }
        return FinishNode(node);
    }

    public bool CanFollowContextualOfKeyword() => NextTokenIsIdentifier() && NextToken() == SyntaxKind.CloseParenToken;

    public VariableStatement ParseVariableStatement(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new VariableStatement
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers,
            DeclarationList = ParseVariableDeclarationList(false)
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
        ParseExpected(SyntaxKind.FunctionKeyword);
        node.AsteriskToken = (AsteriskToken)ParseOptionalToken<AsteriskToken>(SyntaxKind.AsteriskToken);
        node.Name = HasModifier(node, ModifierFlags.Default) ? ParseOptionalIdentifier() : ParseIdentifier();
        var isGenerator = node.AsteriskToken != null;
        var isAsync = HasModifier(node, ModifierFlags.Async);
        FillSignature(SyntaxKind.ColonToken, isGenerator, isAsync, false, node);
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
        ParseExpected(SyntaxKind.ConstructorKeyword);
        FillSignature(SyntaxKind.ColonToken, false, false, false, node);
        node.Body = ParseFunctionBlockOrSemicolon(false, false, Diagnostics.or_expected);
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
        var isGenerator = asteriskToken != null;
        var isAsync = HasModifier(method, ModifierFlags.Async);
        FillSignature(SyntaxKind.ColonToken, isGenerator, isAsync, false, method);
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
        var asteriskToken = (AsteriskToken)ParseOptionalToken<AsteriskToken>(SyntaxKind.AsteriskToken);
        var name = ParsePropertyName();
        var questionToken = (QuestionToken)ParseOptionalToken<QuestionToken>(SyntaxKind.QuestionToken);
        return asteriskToken != null || CurrentToken == SyntaxKind.OpenParenToken || CurrentToken == SyntaxKind.LessThanToken
            ? ParseMethodDeclaration(fullStart, decorators, modifiers, asteriskToken, name, questionToken, Diagnostics.or_expected)
            : ParsePropertyDeclaration(fullStart, decorators, modifiers, name, questionToken);
    }

    public IExpression ParseNonParameterInitializer() => ParseInitializer(false);

    public IAccessorDeclaration ParseAccessorDeclaration(SyntaxKind kind, int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        IAccessorDeclaration node = kind == SyntaxKind.GetAccessor ? new GetAccessorDeclaration() { Kind = kind, Pos = fullStart } : kind == SyntaxKind.SetAccessor ? new SetAccessorDeclaration() { Kind = kind, Pos = fullStart } : throw new NotSupportedException("parseAccessorDeclaration");
        node.Decorators = decorators;
        node.Modifiers = modifiers;
        node.Name = ParsePropertyName();
        FillSignature(SyntaxKind.ColonToken, false, false, false, node);
        node.Body = ParseFunctionBlockOrSemicolon(false, false);
        return AddJsDocComment(FinishNode(node));
    }

    public bool IsClassMemberModifier(SyntaxKind idToken) => idToken switch
    {
        SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.StaticKeyword or SyntaxKind.ReadonlyKeyword => true,
        _ => false,
    };

    public bool IsClassMemberStart()
    {
        SyntaxKind idToken = SyntaxKind.Unknown; // null;
        if (CurrentToken == SyntaxKind.AtToken)
        {
            return true;
        }
        while (IsModifierKind(CurrentToken))
        {
            idToken = CurrentToken;
            if (IsClassMemberModifier(idToken))
            {
                return true;
            }
            NextToken();
        }
        if (CurrentToken == SyntaxKind.AsteriskToken)
        {
            return true;
        }
        if (IsLiteralPropertyName())
        {
            idToken = CurrentToken;
            NextToken();
        }
        if (CurrentToken == SyntaxKind.OpenBracketToken)
        {
            return true;
        }
        if (idToken != SyntaxKind.Unknown)  // null)
        {
            return !IsKeyword(idToken) || idToken == SyntaxKind.SetKeyword || idToken == SyntaxKind.GetKeyword
|| CurrentToken switch
{
    SyntaxKind.OpenParenToken or SyntaxKind.LessThanToken or SyntaxKind.ColonToken or SyntaxKind.EqualsToken or SyntaxKind.QuestionToken => true,// Not valid, but permitted so that it gets caught later on.
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
            var decoratorStart = GetNodePos();
            if (!ParseOptional(SyntaxKind.AtToken))
            {
                break;
            }
            var decorator = new Decorator
            {
                Pos = decoratorStart,
                Expression = DoInDecoratorContext(ParseLeftHandSideExpressionOrHigher)
            };
            FinishNode(decorator);
            if (decorators == null)
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
            decorators.End = GetNodeEnd();
        }
        return decorators;
    }

    public NodeArray<Modifier> ParseModifiers(bool? permitInvalidConstAsModifier = null)
    {
        var modifiers = CreateList<Modifier>();
        while (true)
        {
            var modifierStart = Scanner.StartPos;
            var modifierKind = CurrentToken;
            if (CurrentToken == SyntaxKind.ConstKeyword && permitInvalidConstAsModifier == true)
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
            if (modifiers == null)
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
        if (CurrentToken == SyntaxKind.AsyncKeyword)
        {
            var modifierStart = Scanner.StartPos;
            var modifierKind = CurrentToken;
            NextToken();
            var modifier = FinishNode(new Modifier { Kind = modifierKind, Pos = modifierStart });
            //finishNode((Modifier)createNode(modifierKind, modifierStart));
            modifiers = CreateList<Modifier>();
            modifiers.Pos = modifierStart;
            modifiers.Add(modifier);
            modifiers.End = Scanner.StartPos;
        }
        return modifiers;
    }

    public IClassElement ParseClassElement()
    {
        if (CurrentToken == SyntaxKind.SemicolonToken)
        {
            var result = new SemicolonClassElement() { Pos = Scanner.StartPos };
            NextToken();
            return FinishNode(result);
        }
        var fullStart = GetNodePos();
        var decorators = ParseDecorators();
        var modifiers = ParseModifiers(true);
        var accessor = TryParseAccessorDeclaration(fullStart, decorators, modifiers);
        if (accessor != null)
        {
            return accessor;
        }
        if (CurrentToken == SyntaxKind.ConstructorKeyword)
        {
            return ParseConstructorDeclaration(fullStart, decorators, modifiers);
        }
        if (IsIndexSignature())
        {
            return ParseIndexSignatureDeclaration(fullStart, decorators, modifiers);
        }
        if (TokenIsIdentifierOrKeyword(CurrentToken) ||
                        CurrentToken == SyntaxKind.StringLiteral ||
                        CurrentToken == SyntaxKind.NumericLiteral ||
                        CurrentToken == SyntaxKind.AsteriskToken ||
                        CurrentToken == SyntaxKind.OpenBracketToken)
        {
            return ParsePropertyOrMethodDeclaration(fullStart, decorators, modifiers);
        }
        if (decorators?.Any() == true || modifiers?.Any() == true)
        {
            var name = (Identifier)CreateMissingNode<Identifier>(SyntaxKind.Identifier, true, Diagnostics.Declaration_expected);
            return ParsePropertyDeclaration(fullStart, decorators, modifiers, name, null);
        }
        // 'isClassMemberStart' should have hinted not to attempt parsing.
        Debug.Fail("Should not have attempted to parse class member declaration.");
        return null;
    }

    public ClassExpression ParseClassExpression()
    {
        var node = new ClassExpression { Pos = Scanner.StartPos };
        node.Pos = Scanner.StartPos;
        //node.decorators = decorators;
        //node.modifiers = modifiers;
        ParseExpected(SyntaxKind.ClassKeyword);
        node.Name = ParseNameOfClassDeclarationOrExpression();
        node.TypeParameters = ParseTypeParameters();
        node.HeritageClauses = ParseHeritageClauses();
        if (ParseExpected(SyntaxKind.OpenBraceToken))
        {
            // ClassTail[Yield,Await] : (Modified) See 14.5
            //      ClassHeritage[?Yield,?Await]opt { ClassBody[?Yield,?Await]opt }
            node.Members = ParseClassMembers();
            ParseExpected(SyntaxKind.CloseBraceToken);
        }
        else
        {
            node.Members = new NodeArray<IClassElement>(); // createMissingList<ClassElement>();
        }
        return AddJsDocComment(FinishNode(node));
        //return (ClassExpression)parseClassDeclarationOrExpression(
        //     scanner.StartPos,
        //     null,
        //     null,
        //    SyntaxKind.ClassExpression);
    }

    public ClassDeclaration ParseClassDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new ClassDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };
        ParseExpected(SyntaxKind.ClassKeyword);
        node.Name = ParseNameOfClassDeclarationOrExpression();
        node.TypeParameters = ParseTypeParameters();
        node.HeritageClauses = ParseHeritageClauses();
        if (ParseExpected(SyntaxKind.OpenBraceToken))
        {
            // ClassTail[Yield,Await] : (Modified) See 14.5
            //      ClassHeritage[?Yield,?Await]opt { ClassBody[?Yield,?Await]opt }
            node.Members = ParseClassMembers();
            ParseExpected(SyntaxKind.CloseBraceToken);
        }
        else
        {
            node.Members = new NodeArray<IClassElement>(); // createMissingList<ClassElement>();
        }
        return AddJsDocComment(FinishNode(node));
        //return (ClassDeclaration)parseClassDeclarationOrExpression(fullStart, decorators, modifiers, SyntaxKind.ClassDeclaration);
    }
    //public ClassLikeDeclaration parseClassDeclarationOrExpression(int fullStart, ListWithPos<Decorator> decorators, ListWithPos<Modifier> modifiers, SyntaxKind kind)
    //{
    //    var node = new ClassLikeDeclaration() { pos = fullStart };
    //    node.decorators = decorators;
    //    node.modifiers = modifiers;
    //    parseExpected(SyntaxKind.ClassKeyword);
    //    node.name = parseNameOfClassDeclarationOrExpression();
    //    node.typeParameters = parseTypeParameters();
    //    node.heritageClauses = parseHeritageClauses();
    //    if (parseExpected(SyntaxKind.OpenBraceToken))
    //    {
    //        // ClassTail[Yield,Await] : (Modified) See 14.5
    //        //      ClassHeritage[?Yield,?Await]opt { ClassBody[?Yield,?Await]opt }
    //        node.members = parseClassMembers();
    //        parseExpected(SyntaxKind.CloseBraceToken);
    //    }
    //    else
    //    {
    //        node.members = new NodeArray<Node>(); createMissingList<ClassElement>();
    //    }
    //    return addJSDocComment(finishNode(node));
    //}

    public Identifier ParseNameOfClassDeclarationOrExpression() =>
        // implements is a future reserved word so
        // 'class implements' might mean either
        // - class expression with omitted name, 'implements' starts heritage clause
        // - class with name 'implements'
        // 'isImplementsClause' helps to disambiguate between these two cases
        IsIdentifier() && !IsImplementsClause()
            ? ParseIdentifier()
            : null;

    public bool IsImplementsClause() => CurrentToken == SyntaxKind.ImplementsKeyword && LookAhead(NextTokenIsIdentifierOrKeyword);

    public NodeArray<HeritageClause> ParseHeritageClauses() => IsHeritageClause() ? ParseList(ParsingContextEnum.HeritageClauses, ParseHeritageClause) : null;

    public HeritageClause ParseHeritageClause()
    {
        var tok = CurrentToken;
        if (tok == SyntaxKind.ExtendsKeyword || tok == SyntaxKind.ImplementsKeyword)
        {
            var node = new HeritageClause
            {
                Pos = Scanner.StartPos,
                Token = tok
            };
            NextToken();
            node.Types = ParseDelimitedList(ParsingContextEnum.HeritageClauseElement, ParseExpressionWithTypeArguments);
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
        if (CurrentToken == SyntaxKind.LessThanToken)
        {
            node.TypeArguments = ParseBracketedList(ParsingContextEnum.TypeArguments, ParseType, SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken);
        }
        return FinishNode(node);
    }

    public bool IsHeritageClause() => CurrentToken == SyntaxKind.ExtendsKeyword || CurrentToken == SyntaxKind.ImplementsKeyword;

    public NodeArray<IClassElement> ParseClassMembers() => ParseList2(ParsingContextEnum.ClassMembers, ParseClassElement);

    public InterfaceDeclaration ParseInterfaceDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new InterfaceDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };
        ParseExpected(SyntaxKind.InterfaceKeyword);
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
        ParseExpected(SyntaxKind.TypeKeyword);
        node.Name = ParseIdentifier();
        node.TypeParameters = ParseTypeParameters();
        ParseExpected(SyntaxKind.EqualsToken);
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
        ParseExpected(SyntaxKind.EnumKeyword);
        node.Name = ParseIdentifier();
        if (ParseExpected(SyntaxKind.OpenBraceToken))
        {
            node.Members = ParseDelimitedList(ParsingContextEnum.EnumMembers, ParseEnumMember);
            ParseExpected(SyntaxKind.CloseBraceToken);
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
        if (ParseExpected(SyntaxKind.OpenBraceToken))
        {
            node.Statements = ParseList2(ParsingContextEnum.BlockStatements, ParseStatement);
            ParseExpected(SyntaxKind.CloseBraceToken);
        }
        else
        {
            node.Statements = new NodeArray<IStatement>(); // createMissingList<Statement>();
        }
        return FinishNode(node);
    }

    public ModuleDeclaration ParseModuleOrNamespaceDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers, NodeFlags flags)
    {
        var node = new ModuleDeclaration() { Pos = fullStart };
        var namespaceFlag = flags & NodeFlags.Namespace;
        node.Decorators = decorators;
        node.Modifiers = modifiers;
        node.Flags |= flags;
        node.Name = ParseIdentifier();
        node.Body = ParseOptional(SyntaxKind.DotToken)
            ? (Node)ParseModuleOrNamespaceDeclaration(GetNodePos(), null, null, NodeFlags.NestedNamespace | namespaceFlag)
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
        if (CurrentToken == SyntaxKind.GlobalKeyword)
        {
            // parse 'global' as name of global scope augmentation
            node.Name = ParseIdentifier();
            node.Flags |= NodeFlags.GlobalAugmentation;
        }
        else
        {
            node.Name = (StringLiteral)ParseLiteralNode(true);
        }
        if (CurrentToken == SyntaxKind.OpenBraceToken)
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
        if (CurrentToken == SyntaxKind.GlobalKeyword)
        {
            // global augmentation
            return ParseAmbientExternalModuleDeclaration(fullStart, decorators, modifiers);
        }
        else
    if (ParseOptional(SyntaxKind.NamespaceKeyword))
        {
            flags |= NodeFlags.Namespace;
        }
        else
        {
            ParseExpected(SyntaxKind.ModuleKeyword);
            if (CurrentToken == SyntaxKind.StringLiteral)
            {
                return ParseAmbientExternalModuleDeclaration(fullStart, decorators, modifiers);
            }
        }
        return ParseModuleOrNamespaceDeclaration(fullStart, decorators, modifiers, flags);
    }

    public bool IsExternalModuleReference() => CurrentToken == SyntaxKind.RequireKeyword &&
            LookAhead(NextTokenIsOpenParen);

    public bool NextTokenIsOpenParen() => NextToken() == SyntaxKind.OpenParenToken;

    public bool NextTokenIsSlash() => NextToken() == SyntaxKind.SlashToken;

    public NamespaceExportDeclaration ParseNamespaceExportDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var exportDeclaration = new NamespaceExportDeclaration
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };
        ParseExpected(SyntaxKind.AsKeyword);
        ParseExpected(SyntaxKind.NamespaceKeyword);
        exportDeclaration.Name = ParseIdentifier();
        ParseSemicolon();
        return FinishNode(exportDeclaration);
    }

    public IStatement ParseImportDeclarationOrImportEqualsDeclaration(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        ParseExpected(SyntaxKind.ImportKeyword);
        var afterImportPos = Scanner.StartPos;
        Identifier identifier = null;
        if (IsIdentifier())
        {
            identifier = ParseIdentifier();
            if (CurrentToken != SyntaxKind.CommaToken && CurrentToken != SyntaxKind.FromKeyword)
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
        if (identifier != null || // import id
                        CurrentToken == SyntaxKind.AsteriskToken || // import *
                        CurrentToken == SyntaxKind.OpenBraceToken)
        {
            // import {
            importDeclaration.ImportClause = ParseImportClause(identifier, afterImportPos);
            ParseExpected(SyntaxKind.FromKeyword);
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
        ParseExpected(SyntaxKind.EqualsToken);
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
        if (importClause.Name == null ||
                        ParseOptional(SyntaxKind.CommaToken))
        {
            importClause.NamedBindings = CurrentToken == SyntaxKind.AsteriskToken ? ParseNamespaceImport() : (INamedImportBindings)ParseNamedImportsOrExports(SyntaxKind.NamedImports);
        }
        return FinishNode(importClause);
    }

    public INode ParseModuleReference() => IsExternalModuleReference()
            ? ParseExternalModuleReference()
            : ParseEntityName(false);

    public ExternalModuleReference ParseExternalModuleReference()
    {
        var node = new ExternalModuleReference() { Pos = Scanner.StartPos };
        ParseExpected(SyntaxKind.RequireKeyword);
        ParseExpected(SyntaxKind.OpenParenToken);
        node.Expression = ParseModuleSpecifier();
        ParseExpected(SyntaxKind.CloseParenToken);
        return FinishNode(node);
    }

    public IExpression ParseModuleSpecifier()
    {
        if (CurrentToken == SyntaxKind.StringLiteral)
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
        ParseExpected(SyntaxKind.AsteriskToken);
        ParseExpected(SyntaxKind.AsKeyword);
        namespaceImport.Name = ParseIdentifier();
        return FinishNode(namespaceImport);
    }
    //public NamedImports parseNamedImportsOrExports(SyntaxKind.NamedImports kind)
    //{
    //}
    //public NamedExports parseNamedImportsOrExports(SyntaxKind.NamedExports kind)
    //{
    //}

    public INamedImportsOrExports ParseNamedImportsOrExports(SyntaxKind kind)
    {
        if (kind == SyntaxKind.NamedImports)
        {
            var node = new NamedImports
            {
                Pos = Scanner.StartPos,
                Elements = ParseBracketedList(ParsingContextEnum.ImportOrExportSpecifiers, ParseImportSpecifier,
               SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken)
            };
            return FinishNode(node);
        }
        else
        {
            var node = new NamedExports
            {
                Pos = Scanner.StartPos,
                Elements = ParseBracketedList(ParsingContextEnum.ImportOrExportSpecifiers, ParseExportSpecifier,
               SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken)
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
        //    kind == SyntaxKind.NamedImports ? parseImportSpecifier : parseExportSpecifier,
        //    SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken);
        //return finishNode(node);
    }

    public ExportSpecifier ParseExportSpecifier()
    {
        var node = new ExportSpecifier { Pos = Scanner.StartPos };
        var checkIdentifierIsKeyword = IsKeyword(CurrentToken) && !IsIdentifier();
        var checkIdentifierStart = Scanner.TokenPos;
        var checkIdentifierEnd = Scanner.TextPos;
        var identifierName = ParseIdentifierName();
        if (CurrentToken == SyntaxKind.AsKeyword)
        {
            node.PropertyName = identifierName;
            ParseExpected(SyntaxKind.AsKeyword);
            checkIdentifierIsKeyword = IsKeyword(CurrentToken) && !IsIdentifier();
            checkIdentifierStart = Scanner.TokenPos;
            checkIdentifierEnd = Scanner.TextPos;
            node.Name = ParseIdentifierName();
        }
        else
        {
            node.Name = identifierName;
        }
        return FinishNode(node);
        //return parseImportOrExportSpecifier(SyntaxKind.ExportSpecifier);
    }

    public ImportSpecifier ParseImportSpecifier()
    {
        var node = new ImportSpecifier() { Pos = Scanner.StartPos };
        var checkIdentifierIsKeyword = IsKeyword(CurrentToken) && !IsIdentifier();
        var checkIdentifierStart = Scanner.TokenPos;
        var checkIdentifierEnd = Scanner.TextPos;
        var identifierName = ParseIdentifierName();
        if (CurrentToken == SyntaxKind.AsKeyword)
        {
            node.PropertyName = identifierName;
            ParseExpected(SyntaxKind.AsKeyword);
            checkIdentifierIsKeyword = IsKeyword(CurrentToken) && !IsIdentifier();
            checkIdentifierStart = Scanner.TokenPos;
            checkIdentifierEnd = Scanner.TextPos;
            node.Name = ParseIdentifierName();
        }
        else
        {
            node.Name = identifierName;
        }
        if (checkIdentifierIsKeyword)
        {
            // Report error identifier expected
            ParseErrorAtPosition(checkIdentifierStart, checkIdentifierEnd - checkIdentifierStart, Diagnostics.Identifier_expected);
        }
        return FinishNode(node);
        //return parseImportOrExportSpecifier(SyntaxKind.ImportSpecifier);
    }
    //public ImportOrExportSpecifier parseImportOrExportSpecifier(SyntaxKind kind)
    //{
    //    var node = new ImportSpecifier { pos = scanner.StartPos };
    //    var checkIdentifierIsKeyword = isKeyword(token()) && !isIdentifier();
    //    var checkIdentifierStart = scanner.TokenPos;
    //    var checkIdentifierEnd = scanner.getTextPos();
    //    var identifierName = parseIdentifierName();
    //    if (token() == SyntaxKind.AsKeyword)
    //    {
    //        node.propertyName = identifierName;
    //        parseExpected(SyntaxKind.AsKeyword);
    //        checkIdentifierIsKeyword = isKeyword(token()) && !isIdentifier();
    //        checkIdentifierStart = scanner.TokenPos;
    //        checkIdentifierEnd = scanner.getTextPos();
    //        node.name = parseIdentifierName();
    //    }
    //    else
    //    {
    //        node.name = identifierName;
    //    }
    //    if (kind == SyntaxKind.ImportSpecifier && checkIdentifierIsKeyword)
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
        if (ParseOptional(SyntaxKind.AsteriskToken))
        {
            ParseExpected(SyntaxKind.FromKeyword);
            node.ModuleSpecifier = ParseModuleSpecifier();
        }
        else
        {
            node.ExportClause = (NamedExports)ParseNamedImportsOrExports(SyntaxKind.NamedExports);
            if (CurrentToken == SyntaxKind.FromKeyword || (CurrentToken == SyntaxKind.StringLiteral && !Scanner.HasPrecedingLineBreak))
            {
                ParseExpected(SyntaxKind.FromKeyword);
                node.ModuleSpecifier = ParseModuleSpecifier();
            }
        }
        ParseSemicolon();
        return FinishNode(node);
    }

    public ExportAssignment ParseExportAssignment(int fullStart, NodeArray<Decorator> decorators, NodeArray<Modifier> modifiers)
    {
        var node = new ExportAssignment
        {
            Pos = fullStart,
            Decorators = decorators,
            Modifiers = modifiers
        };
        if (ParseOptional(SyntaxKind.EqualsToken))
        {
            node.IsExportEquals = true;
        }
        else
        {
            ParseExpected(SyntaxKind.DefaultKeyword);
        }
        node.Expression = ParseAssignmentExpressionOrHigher();
        ParseSemicolon();
        return FinishNode(node);
    }

    public void ProcessReferenceComments(SourceFile sourceFile)
    {
        //var triviaScanner = new Scanner(sourceFile._languageVersion, false, LanguageVariant.Standard, sourceText);
        //List<FileReference> referencedFiles = new List<FileReference>();
        //List<FileReference> typeReferenceDirectives = new List<FileReference>();
        ////(string path, string name)[] amdDependencies =  [];
        //List<AmdDependency> amdDependencies = new List<AmdDependency>();
        //string amdModuleName = null;
        //CheckJsDirective checkJsDirective = null;
        //while (true)
        //{
        //    var kind = triviaScanner.scan();
        //    if (kind != SyntaxKind.SingleLineCommentTrivia)
        //    {
        //        if (isTrivia(kind))
        //        {
        //            continue;
        //        }
        //        else
        //        {
        //            break;
        //        }
        //    }
        //    var range = new
        //    {
        //        kind = triviaScanner.getToken(),
        //        pos = triviaScanner.getTokenPos(),
        //        end = triviaScanner.getTextPos()
        //    };
        //    var comment = sourceText.substring(range.pos, range.end);
        //    var referencePathMatchResult = getFileReferenceFromReferencePath(comment, range);
        //    if (referencePathMatchResult)
        //    {
        //        var fileReference = referencePathMatchResult.fileReference;
        //        sourceFile.hasNoDefaultLib = referencePathMatchResult.isNoDefaultLib;
        //        var diagnosticMessage = referencePathMatchResult.diagnosticMessage;
        //        if (fileReference)
        //        {
        //            if (referencePathMatchResult.isTypeReferenceDirective)
        //            {
        //                typeReferenceDirectives.Add(fileReference);
        //            }
        //            else
        //            {
        //                referencedFiles.Add(fileReference);
        //            }
        //        }
        //        if (diagnosticMessage)
        //        {
        //            parseDiagnostics.Add(createFileDiagnostic(sourceFile, range.pos, range.end - range.pos, diagnosticMessage));
        //        }
        //    }
        //    else
        //    {
        //        var amdModuleNameRegEx = new Regex(@" /^\/\/\/\s*<amd-module\s+name\s*=\s*('|"")(.+?)\1/gim");
        //        var amdModuleNameMatchResult = amdModuleNameRegEx.exec(comment);
        //        if (amdModuleNameMatchResult)
        //        {
        //            if (amdModuleName)
        //            {
        //                parseDiagnostics.Add(createFileDiagnostic(sourceFile, range.pos, range.end - range.pos, Diagnostics.An_AMD_module_cannot_have_multiple_name_assignments));
        //            }
        //            amdModuleName = amdModuleNameMatchResult[2];
        //        }
        //        var amdDependencyRegEx = new Regex(@" /^\/\/\/\s*<amd-dependency\s/gim");
        //        var pathRegex = new Regex(@" /\spath\s*=\s*('|"")(.+?)\1/gim");
        //        var nameRegex = new Regex(@" /\sname\s*=\s*('|"")(.+?)\1/gim");
        //        var amdDependencyMatchResult = amdDependencyRegEx.exec(comment);
        //        if (amdDependencyMatchResult.Any())
        //        {
        //            var pathMatchResult = pathRegex.exec(comment);
        //            var nameMatchResult = nameRegex.exec(comment);
        //            if (pathMatchResult.Any())
        //            {
        //                var amdDependency = new { path = pathMatchResult[2], name = nameMatchResult.Any() ? nameMatchResult[2] : null };
        //                amdDependencies.Add(new AmdDependency { name = amdDependency.name, path = amdDependency.path });
        //            }
        //        }
        //        var checkJsDirectiveRegEx = new Regex(@" /^\/\/\/?\s*(@ts-check|@ts-nocheck)\s*$/gim");
        //        var checkJsDirectiveMatchResult = checkJsDirectiveRegEx.exec(comment);
        //        if (checkJsDirectiveMatchResult.Any())
        //        {
        //            checkJsDirective = new CheckJsDirective
        //            {
        //                enabled = checkJsDirectiveMatchResult[1].ToLower() == @"ts-check",
        //                //compareStrings(checkJsDirectiveMatchResult[1], "@ts-check",  true) == Comparison.EqualTo,
        //                end = range.end,
        //                pos = range.pos
        //            };
        //        }
        //    }
        //}
        //sourceFile.referencedFiles = referencedFiles;
        //sourceFile.typeReferenceDirectives = typeReferenceDirectives;
        //sourceFile.amdDependencies = amdDependencies;
        //sourceFile.moduleName = amdModuleName;
        //sourceFile.checkJsDirective = checkJsDirective;
    }

    public void SetExternalModuleIndicator(SourceFile sourceFile) => sourceFile.ExternalModuleIndicator = sourceFile.Statements.FirstOrDefault(node =>
                                                                     {
                                                                         return HasModifier(node, ModifierFlags.Export)
                                                                             || (node.Kind == SyntaxKind.ImportEqualsDeclaration && (node as ImportEqualsDeclaration)?.ModuleReference?.Kind == SyntaxKind.ExternalModuleReference)
                                                                             || node.Kind == SyntaxKind.ImportDeclaration
                                                                             || node.Kind == SyntaxKind.ExportAssignment
                                                                             || node.Kind == SyntaxKind.ExportDeclaration;
                                                                         //?  node : null;
                                                                     }
);
}