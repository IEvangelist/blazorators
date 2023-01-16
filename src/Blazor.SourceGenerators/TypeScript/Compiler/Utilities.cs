// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;
using static Blazor.SourceGenerators.TypeScript.Compiler.Factory;
using static Blazor.SourceGenerators.TypeScript.Compiler.Scanner;
using static Blazor.SourceGenerators.TypeScript.Compiler.Ts;

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Compiler;

public sealed class Utilities
{
    public static int GetFullWidth(INode node) => (node.End ?? 0) - (node.Pos ?? 0);


    public static INode ContainsParseError(INode node)
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
            identifier.CharCodeAt(0) is CharacterCode._ &&
            identifier.CharCodeAt(1) is CharacterCode._
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
                text.CharCodeAt((comment.Pos ?? 0) + 1) is CharacterCode.Asterisk &&
                text.CharCodeAt((comment.Pos ?? 0) + 2) is CharacterCode.Asterisk &&
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
        token is TypeScriptSyntaxKind.BarBarToken ||
        token is TypeScriptSyntaxKind.AmpersandAmpersandToken ||
        token is TypeScriptSyntaxKind.ExclamationToken;

    public static bool IsAssignmentOperator(TypeScriptSyntaxKind token) =>
        token >= TypeScriptSyntaxKind.FirstAssignment &&
        token <= TypeScriptSyntaxKind.LastAssignment;

    public static bool IsLeftHandSideExpressionKind(TypeScriptSyntaxKind kind) =>
        kind is TypeScriptSyntaxKind.PropertyAccessExpression ||
        kind is TypeScriptSyntaxKind.ElementAccessExpression ||
        kind is TypeScriptSyntaxKind.NewExpression ||
        kind is TypeScriptSyntaxKind.CallExpression ||
        kind is TypeScriptSyntaxKind.JsxElement ||
        kind is TypeScriptSyntaxKind.JsxSelfClosingElement ||
        kind is TypeScriptSyntaxKind.TaggedTemplateExpression ||
        kind is TypeScriptSyntaxKind.ArrayLiteralExpression ||
        kind is TypeScriptSyntaxKind.ParenthesizedExpression ||
        kind is TypeScriptSyntaxKind.ObjectLiteralExpression ||
        kind is TypeScriptSyntaxKind.ClassExpression ||
        kind is TypeScriptSyntaxKind.FunctionExpression ||
        kind is TypeScriptSyntaxKind.Identifier ||
        kind is TypeScriptSyntaxKind.RegularExpressionLiteral ||
        kind is TypeScriptSyntaxKind.NumericLiteral ||
        kind is TypeScriptSyntaxKind.StringLiteral ||
        kind is TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral ||
        kind is TypeScriptSyntaxKind.TemplateExpression ||
        kind is TypeScriptSyntaxKind.FalseKeyword ||
        kind is TypeScriptSyntaxKind.NullKeyword ||
        kind is TypeScriptSyntaxKind.ThisKeyword ||
        kind is TypeScriptSyntaxKind.TrueKeyword ||
        kind is TypeScriptSyntaxKind.SuperKeyword ||
        kind is TypeScriptSyntaxKind.NonNullExpression ||
        kind is TypeScriptSyntaxKind.MetaProperty;

    public static bool IsLeftHandSideExpression(IExpression node) =>
        IsLeftHandSideExpressionKind(
            SkipPartiallyEmittedExpressions(node).Kind);
}