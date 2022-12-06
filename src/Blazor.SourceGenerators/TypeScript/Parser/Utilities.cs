// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using static Blazor.SourceGenerators.TypeScript.Parser.Factory;
using static Blazor.SourceGenerators.TypeScript.Parser.Scanner;
using static Blazor.SourceGenerators.TypeScript.Parser.Ts;

namespace Blazor.SourceGenerators.TypeScript.Parser;

public sealed class Utilities
{
    public static int GetFullWidth(INode node) => (node.End ?? 0) - (node.Pos ?? 0);


    public static INode? ContainsParseError(INode node)
    {
        AggregateChildData(node);

        return (node.Flags & NodeFlags.ThisNodeOrAnySubNodesHasError) != 0 ? node : null;
    }


    public static void AggregateChildData(INode node)
    {
        if ((node.Flags & NodeFlags.HasAggregatedChildData) != 0)
        {
            var thisNodeOrAnySubNodesHasError =
                (node.Flags & NodeFlags.ThisNodeHasError) is not 0 ||
                ForEachChild(node, ContainsParseError) != null;

            if (thisNodeOrAnySubNodesHasError)
            {
                node.Flags |= NodeFlags.ThisNodeOrAnySubNodesHasError;
            }

            node.Flags |= NodeFlags.HasAggregatedChildData;
        }
    }

    public static bool NodeIsMissing(INode node) =>
        node is null ||
        (node.Pos == node.End && node.Pos >= 0 && node.Kind != TypeScriptSyntaxKind.EndOfFileToken);

    public static string GetTextOfNodeFromSourceText(
        string sourceText, INode node) =>
        NodeIsMissing(node)
        ? ""
        : sourceText.SubString(SkipTriviaM(sourceText, node.Pos ?? 0), node.End);

    public static string EscapeIdentifier(
        string identifier) =>
        identifier.Length >= 2 &&
        identifier.CharCodeAt(0) == CharacterCode._ &&
        identifier.CharCodeAt(1) == CharacterCode._
            ? $"_{identifier}"
            : identifier;

    public static List<CommentRange> GetLeadingCommentRangesOfNodeFromText(INode node, string text) =>
        GetLeadingCommentRanges(text, node.Pos ?? 0);

    public static List<CommentRange> GetJsDocCommentRanges(
        INode node, string text)
    {
        var commentRanges =
            node.Kind is TypeScriptSyntaxKind.Parameter ||
            node.Kind is TypeScriptSyntaxKind.TypeParameter ||
            node.Kind is TypeScriptSyntaxKind.FunctionExpression ||
            node.Kind is TypeScriptSyntaxKind.ArrowFunction
                ? GetTrailingCommentRanges(text, node.Pos ?? 0).Concat(GetLeadingCommentRanges(text, node.Pos ?? 0))
                : GetLeadingCommentRangesOfNodeFromText(node, text);

        commentRanges ??= new List<CommentRange>();

        return commentRanges.Where(comment =>
                text.CharCodeAt((comment.Pos ?? 0) + 1) == CharacterCode.Asterisk &&
                text.CharCodeAt((comment.Pos ?? 0) + 2) == CharacterCode.Asterisk &&
                text.CharCodeAt((comment.Pos ?? 0) + 3) != CharacterCode.Slash)
            .ToList();
    }

    public static bool IsKeyword(TypeScriptSyntaxKind token) =>
        TypeScriptSyntaxKind.FirstKeyword <= token &&
        token <= TypeScriptSyntaxKind.LastKeyword;

    public static bool IsTrivia(TypeScriptSyntaxKind token) =>
        TypeScriptSyntaxKind.FirstTriviaToken <= token &&
        token <= TypeScriptSyntaxKind.LastTriviaToken;

    public static bool IsModifierKind(TypeScriptSyntaxKind token) => token switch
    {
        TypeScriptSyntaxKind.AbstractKeyword or
        TypeScriptSyntaxKind.AsyncKeyword or
        TypeScriptSyntaxKind.ConstKeyword or
        TypeScriptSyntaxKind.DeclareKeyword or
        TypeScriptSyntaxKind.DefaultKeyword or
        TypeScriptSyntaxKind.ExportKeyword or
        TypeScriptSyntaxKind.PublicKeyword or
        TypeScriptSyntaxKind.PrivateKeyword or
        TypeScriptSyntaxKind.ProtectedKeyword or
        TypeScriptSyntaxKind.ReadonlyKeyword or
        TypeScriptSyntaxKind.StaticKeyword => true,

        _ => false,
    };


    public static bool IsParameterDeclaration(IVariableLikeDeclaration node)
    {
        var root = GetRootDeclaration(node);
        return root.Kind is TypeScriptSyntaxKind.Parameter;
    }

    public static INode GetRootDeclaration(INode node)
    {
        while (node.Kind is TypeScriptSyntaxKind.BindingElement)
        {
            node = node.Parent.Parent;
        }

        return node;
    }


    public static bool HasModifiers(Node node) =>
        GetModifierFlags(node) is not ModifierFlags.None;

    public static bool HasModifier(INode node, ModifierFlags flags) =>
        (GetModifierFlags(node) & flags) is not 0;

    public static ModifierFlags GetModifierFlags(INode node)
    {
        if ((node.ModifierFlagsCache & ModifierFlags.HasComputedFlags) != 0)
        {
            return node.ModifierFlagsCache & ~ModifierFlags.HasComputedFlags;
        }

        var flags = ModifierFlags.None;
        if (node.Modifiers != null)
        {
            foreach (var modifier in node.Modifiers)
            {
                flags |= ModifierToFlag(modifier.Kind);
            }
        }

        if (node.Flags.HasFlag(NodeFlags.NestedNamespace) ||
            (node is Identifier identifier && identifier.IsInJsDocNamespace))
        {
            flags |= ModifierFlags.Export;
        }

        node.ModifierFlagsCache = flags | ModifierFlags.HasComputedFlags;

        return flags;
    }


    public static ModifierFlags ModifierToFlag(TypeScriptSyntaxKind token) => token switch
    {
        TypeScriptSyntaxKind.StaticKeyword => ModifierFlags.Static,
        TypeScriptSyntaxKind.PublicKeyword => ModifierFlags.Public,
        TypeScriptSyntaxKind.ProtectedKeyword => ModifierFlags.Protected,
        TypeScriptSyntaxKind.PrivateKeyword => ModifierFlags.Private,
        TypeScriptSyntaxKind.AbstractKeyword => ModifierFlags.Abstract,
        TypeScriptSyntaxKind.ExportKeyword => ModifierFlags.Export,
        TypeScriptSyntaxKind.DeclareKeyword => ModifierFlags.Ambient,
        TypeScriptSyntaxKind.ConstKeyword => ModifierFlags.Const,
        TypeScriptSyntaxKind.DefaultKeyword => ModifierFlags.Default,
        TypeScriptSyntaxKind.AsyncKeyword => ModifierFlags.Async,
        TypeScriptSyntaxKind.ReadonlyKeyword => ModifierFlags.Readonly,
        _ => ModifierFlags.None,
    };

    public static bool IsLogicalOperator(TypeScriptSyntaxKind token) =>
        Token is TypeScriptSyntaxKind.BarBarToken ||
        Token is TypeScriptSyntaxKind.AmpersandAmpersandToken ||
        Token is TypeScriptSyntaxKind.ExclamationToken;

    public static bool IsAssignmentOperator(TypeScriptSyntaxKind token) =>
        token >= TypeScriptSyntaxKind.FirstAssignment &&
        token <= TypeScriptSyntaxKind.LastAssignment;

    public static bool IsLeftHandSideExpressionKind(TypeScriptSyntaxKind kind) =>
        Kind is TypeScriptSyntaxKind.PropertyAccessExpression ||
        Kind is TypeScriptSyntaxKind.ElementAccessExpression ||
        Kind is TypeScriptSyntaxKind.NewExpression ||
        Kind is TypeScriptSyntaxKind.CallExpression ||
        Kind is TypeScriptSyntaxKind.JsxElement ||
        Kind is TypeScriptSyntaxKind.JsxSelfClosingElement ||
        Kind is TypeScriptSyntaxKind.TaggedTemplateExpression ||
        Kind is TypeScriptSyntaxKind.ArrayLiteralExpression ||
        Kind is TypeScriptSyntaxKind.ParenthesizedExpression ||
        Kind is TypeScriptSyntaxKind.ObjectLiteralExpression ||
        Kind is TypeScriptSyntaxKind.ClassExpression ||
        Kind is TypeScriptSyntaxKind.FunctionExpression ||
        Kind is TypeScriptSyntaxKind.Identifier ||
        Kind is TypeScriptSyntaxKind.RegularExpressionLiteral ||
        Kind is TypeScriptSyntaxKind.NumericLiteral ||
        Kind is TypeScriptSyntaxKind.StringLiteral ||
        Kind is TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral ||
        Kind is TypeScriptSyntaxKind.TemplateExpression ||
        Kind is TypeScriptSyntaxKind.FalseKeyword ||
        Kind is TypeScriptSyntaxKind.NullKeyword ||
        Kind is TypeScriptSyntaxKind.ThisKeyword ||
        Kind is TypeScriptSyntaxKind.TrueKeyword ||
        Kind is TypeScriptSyntaxKind.SuperKeyword ||
        Kind is TypeScriptSyntaxKind.NonNullExpression ||
        Kind is TypeScriptSyntaxKind.MetaProperty;

    public static bool IsLeftHandSideExpression(IExpression node) =>
        IsLeftHandSideExpressionKind(
            SkipPartiallyEmittedExpressions(node).Kind);
}