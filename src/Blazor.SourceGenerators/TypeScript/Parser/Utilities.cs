// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using static Blazor.SourceGenerators.TypeScript.Parser.Factory;
using static Blazor.SourceGenerators.TypeScript.Parser.Scanner;
using static Blazor.SourceGenerators.TypeScript.Parser.Ts;

namespace Blazor.SourceGenerators.TypeScript.Parser;

internal sealed class Utilities
{
    internal static int GetFullWidth(INode node) => (node.End ?? 0) - (node.Pos ?? 0);


    internal static INode? ContainsParseError(INode node)
    {
        AggregateChildData(node);

        return (node.Flags & NodeFlags.ThisNodeOrAnySubNodesHasError) != 0 ? node : null;
    }


    internal static void AggregateChildData(INode node)
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

    internal static bool NodeIsMissing(INode node) =>
        node is null ||
        (node.Pos == node.End && node.Pos >= 0 && node.Kind != SyntaxKind.EndOfFileToken);

    internal static string GetTextOfNodeFromSourceText(
        string sourceText, INode node) =>
        NodeIsMissing(node)
        ? ""
        : sourceText.SubString(SkipTriviaM(sourceText, node.Pos ?? 0), node.End);

    internal static string EscapeIdentifier(
        string identifier) =>
        identifier.Length >= 2 &&
        identifier.CharCodeAt(0) == CharacterCode._ &&
        identifier.CharCodeAt(1) == CharacterCode._
            ? $"_{identifier}"
            : identifier;

    internal static List<CommentRange> GetLeadingCommentRangesOfNodeFromText(INode node, string text) =>
        GetLeadingCommentRanges(text, node.Pos ?? 0);

    internal static List<CommentRange> GetJsDocCommentRanges(
        INode node, string text)
    {
        var commentRanges =
            node.Kind == SyntaxKind.Parameter ||
            node.Kind == SyntaxKind.TypeParameter ||
            node.Kind == SyntaxKind.FunctionExpression ||
            node.Kind == SyntaxKind.ArrowFunction
                ? GetTrailingCommentRanges(text, node.Pos ?? 0).Concat(GetLeadingCommentRanges(text, node.Pos ?? 0))
                : GetLeadingCommentRangesOfNodeFromText(node, text);

        commentRanges ??= new List<CommentRange>();

        return commentRanges.Where(comment =>
                text.CharCodeAt((comment.Pos ?? 0) + 1) == CharacterCode.Asterisk &&
                text.CharCodeAt((comment.Pos ?? 0) + 2) == CharacterCode.Asterisk &&
                text.CharCodeAt((comment.Pos ?? 0) + 3) != CharacterCode.Slash)
            .ToList();
    }

    internal static bool IsKeyword(SyntaxKind token) =>
        SyntaxKind.FirstKeyword <= token &&
        token <= SyntaxKind.LastKeyword;

    internal static bool IsTrivia(SyntaxKind token) =>
        SyntaxKind.FirstTriviaToken <= token &&
        token <= SyntaxKind.LastTriviaToken;

    internal static bool IsModifierKind(SyntaxKind token) => token switch
    {
        SyntaxKind.AbstractKeyword or
        SyntaxKind.AsyncKeyword or
        SyntaxKind.ConstKeyword or
        SyntaxKind.DeclareKeyword or
        SyntaxKind.DefaultKeyword or
        SyntaxKind.ExportKeyword or
        SyntaxKind.PublicKeyword or
        SyntaxKind.PrivateKeyword or
        SyntaxKind.ProtectedKeyword or
        SyntaxKind.ReadonlyKeyword or
        SyntaxKind.StaticKeyword => true,

        _ => false,
    };


    internal static bool IsParameterDeclaration(IVariableLikeDeclaration node)
    {
        var root = GetRootDeclaration(node);
        return root.Kind is SyntaxKind.Parameter;
    }

    internal static INode GetRootDeclaration(INode node)
    {
        while (node.Kind is SyntaxKind.BindingElement)
        {
            node = node.Parent.Parent;
        }

        return node;
    }


    internal static bool HasModifiers(Node node) =>
        GetModifierFlags(node) is not ModifierFlags.None;

    internal static bool HasModifier(INode node, ModifierFlags flags) =>
        (GetModifierFlags(node) & flags) is not 0;

    internal static ModifierFlags GetModifierFlags(INode node)
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


    internal static ModifierFlags ModifierToFlag(SyntaxKind token) => token switch
    {
        SyntaxKind.StaticKeyword => ModifierFlags.Static,
        SyntaxKind.PublicKeyword => ModifierFlags.Public,
        SyntaxKind.ProtectedKeyword => ModifierFlags.Protected,
        SyntaxKind.PrivateKeyword => ModifierFlags.Private,
        SyntaxKind.AbstractKeyword => ModifierFlags.Abstract,
        SyntaxKind.ExportKeyword => ModifierFlags.Export,
        SyntaxKind.DeclareKeyword => ModifierFlags.Ambient,
        SyntaxKind.ConstKeyword => ModifierFlags.Const,
        SyntaxKind.DefaultKeyword => ModifierFlags.Default,
        SyntaxKind.AsyncKeyword => ModifierFlags.Async,
        SyntaxKind.ReadonlyKeyword => ModifierFlags.Readonly,
        _ => ModifierFlags.None,
    };

    internal static bool IsLogicalOperator(SyntaxKind token) =>
        token == SyntaxKind.BarBarToken ||
        token == SyntaxKind.AmpersandAmpersandToken ||
        token == SyntaxKind.ExclamationToken;

    internal static bool IsAssignmentOperator(SyntaxKind token) =>
        token >= SyntaxKind.FirstAssignment &&
        token <= SyntaxKind.LastAssignment;

    internal static bool IsLeftHandSideExpressionKind(SyntaxKind kind) =>
        kind == SyntaxKind.PropertyAccessExpression ||
        kind == SyntaxKind.ElementAccessExpression ||
        kind == SyntaxKind.NewExpression ||
        kind == SyntaxKind.CallExpression ||
        kind == SyntaxKind.JsxElement ||
        kind == SyntaxKind.JsxSelfClosingElement ||
        kind == SyntaxKind.TaggedTemplateExpression ||
        kind == SyntaxKind.ArrayLiteralExpression ||
        kind == SyntaxKind.ParenthesizedExpression ||
        kind == SyntaxKind.ObjectLiteralExpression ||
        kind == SyntaxKind.ClassExpression ||
        kind == SyntaxKind.FunctionExpression ||
        kind == SyntaxKind.Identifier ||
        kind == SyntaxKind.RegularExpressionLiteral ||
        kind == SyntaxKind.NumericLiteral ||
        kind == SyntaxKind.StringLiteral ||
        kind == SyntaxKind.NoSubstitutionTemplateLiteral ||
        kind == SyntaxKind.TemplateExpression ||
        kind == SyntaxKind.FalseKeyword ||
        kind == SyntaxKind.NullKeyword ||
        kind == SyntaxKind.ThisKeyword ||
        kind == SyntaxKind.TrueKeyword ||
        kind == SyntaxKind.SuperKeyword ||
        kind == SyntaxKind.NonNullExpression ||
        kind == SyntaxKind.MetaProperty;

    internal static bool IsLeftHandSideExpression(IExpression node) =>
        IsLeftHandSideExpressionKind(
            SkipPartiallyEmittedExpressions(node).Kind);
}