// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;
using static Blazor.SourceGenerators.TypeScript.Compiler.Factory;
using static Blazor.SourceGenerators.TypeScript.Compiler.Scanner;
using static Blazor.SourceGenerators.TypeScript.Compiler.Ts;

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Compiler;

internal static class Utilities
{
    internal static int GetFullWidth(INode node) => (node.End ?? 0) - (node.Pos ?? 0);

    internal static INode ContainsParseError(INode node)
    {
        AggregateChildData(node);

        return (node.Flags & NodeFlags.ThisNodeOrAnySubNodesHasError) != 0 ? node : null;
    }


    internal static void AggregateChildData(INode node)
    {
        if ((node.Flags & NodeFlags.HasAggregatedChildData) == 0) return;

        var thisNodeOrAnySubNodesHasError =
            (node.Flags & NodeFlags.ThisNodeHasError) is not 0 ||
            ForEachChild(node, ContainsParseError) != null;

        if (thisNodeOrAnySubNodesHasError)
        {
            node.Flags |= NodeFlags.ThisNodeOrAnySubNodesHasError;
        }

        node.Flags |= NodeFlags.HasAggregatedChildData;
    }

    internal static bool NodeIsMissing(INode node) =>
        node is null ||
        (node.Pos == node.End && node.Pos >= 0 && node.Kind != TypeScriptSyntaxKind.EndOfFileToken);

    internal static string GetTextOfNodeFromSourceText(
        string sourceText, INode node) =>
        NodeIsMissing(node)
        ? ""
        : sourceText.SubString(SkipTriviaM(sourceText, node.Pos ?? 0), node.End);

    internal static string EscapeIdentifier(
        string identifier) =>
        identifier.Length >= 2 &&
            identifier.CharCodeAt(0) is CharacterCode._ &&
            identifier.CharCodeAt(1) is CharacterCode._
                ? $"_{identifier}"
                : identifier;

    internal static List<CommentRange> GetLeadingCommentRangesOfNodeFromText(
        INode node, string text) =>
        GetLeadingCommentRanges(text, node.Pos ?? 0);

    internal static List<CommentRange> GetJsDocCommentRanges(
        INode node, string text)
    {
        var commentRanges =
            node.Kind is TypeScriptSyntaxKind.Parameter or
                TypeScriptSyntaxKind.TypeParameter or
                TypeScriptSyntaxKind.FunctionExpression or
                TypeScriptSyntaxKind.ArrowFunction
                ? GetTrailingCommentRanges(text, node.Pos ?? 0).Concat(GetLeadingCommentRanges(text, node.Pos ?? 0))
                : GetLeadingCommentRangesOfNodeFromText(node, text);

        commentRanges ??= new List<CommentRange>();
        return commentRanges.Where(comment =>
                text.CharCodeAt((comment.Pos ?? 0) + 1) is CharacterCode.Asterisk &&
                text.CharCodeAt((comment.Pos ?? 0) + 2) is CharacterCode.Asterisk &&
                text.CharCodeAt((comment.Pos ?? 0) + 3) != CharacterCode.Slash)
            .ToList();
    }

    internal static bool IsKeyword(TypeScriptSyntaxKind token) =>
        token is >= TypeScriptSyntaxKind.FirstKeyword and <= TypeScriptSyntaxKind.LastKeyword;

    internal static bool IsTrivia(TypeScriptSyntaxKind token) =>
        token is >= TypeScriptSyntaxKind.FirstTriviaToken and <= TypeScriptSyntaxKind.LastTriviaToken;

    internal static bool IsModifierKind(TypeScriptSyntaxKind token) => token switch
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

    internal static bool IsParameterDeclaration(IVariableLikeDeclaration node)
    {
        var root = GetRootDeclaration(node);
        return root.Kind is TypeScriptSyntaxKind.Parameter;
    }

    internal static INode GetRootDeclaration(INode node)
    {
        while (node.Kind is TypeScriptSyntaxKind.BindingElement)
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
            flags = node.Modifiers.Aggregate(flags, (current, modifier) => current | ModifierToFlag(modifier.Kind));
        }

        if (node.Flags.HasFlag(NodeFlags.NestedNamespace) ||
            node is Identifier { IsInJsDocNamespace: true })
        {
            flags |= ModifierFlags.Export;
        }

        node.ModifierFlagsCache = flags | ModifierFlags.HasComputedFlags;

        return flags;
    }


    internal static ModifierFlags ModifierToFlag(TypeScriptSyntaxKind token) => token switch
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

    internal static bool IsLogicalOperator(TypeScriptSyntaxKind token) =>
        token is TypeScriptSyntaxKind.BarBarToken or
            TypeScriptSyntaxKind.AmpersandAmpersandToken or
            TypeScriptSyntaxKind.ExclamationToken;

    internal static bool IsAssignmentOperator(TypeScriptSyntaxKind token) =>
        token is >= TypeScriptSyntaxKind.FirstAssignment and <= TypeScriptSyntaxKind.LastAssignment;

    internal static bool IsLeftHandSideExpressionKind(TypeScriptSyntaxKind kind) =>
        kind is TypeScriptSyntaxKind.PropertyAccessExpression or
            TypeScriptSyntaxKind.ElementAccessExpression or
            TypeScriptSyntaxKind.NewExpression or
            TypeScriptSyntaxKind.CallExpression or
            TypeScriptSyntaxKind.JsxElement or
            TypeScriptSyntaxKind.JsxSelfClosingElement or
            TypeScriptSyntaxKind.TaggedTemplateExpression or
            TypeScriptSyntaxKind.ArrayLiteralExpression or
            TypeScriptSyntaxKind.ParenthesizedExpression or
            TypeScriptSyntaxKind.ObjectLiteralExpression or
            TypeScriptSyntaxKind.ClassExpression or
            TypeScriptSyntaxKind.FunctionExpression or
            TypeScriptSyntaxKind.Identifier or
            TypeScriptSyntaxKind.RegularExpressionLiteral or
            TypeScriptSyntaxKind.NumericLiteral or
            TypeScriptSyntaxKind.StringLiteral or
            TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral or
            TypeScriptSyntaxKind.TemplateExpression or
            TypeScriptSyntaxKind.FalseKeyword or
            TypeScriptSyntaxKind.NullKeyword or
            TypeScriptSyntaxKind.ThisKeyword or
            TypeScriptSyntaxKind.TrueKeyword or
            TypeScriptSyntaxKind.SuperKeyword or
            TypeScriptSyntaxKind.NonNullExpression or
            TypeScriptSyntaxKind.MetaProperty;

    internal static bool IsLeftHandSideExpression(IExpression node) =>
        IsLeftHandSideExpressionKind(
            SkipPartiallyEmittedExpressions(node).Kind);
}