// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Compiler;

public sealed class Ts
{
    public static INode VisitNode(
        Func<INode, INode> nodeCallback, INode node) =>
        node != null ? nodeCallback(node) : null;

    public static T VisitList<T>(
        Func<INode[], T> nodeArrayCallback, INode[] nodes) =>
        nodes != null ? nodeArrayCallback(nodes) : default;

    public static INode VisitNodeArray(
        Func<INode[], INode> nodeArrayCallback, INode[] nodes) =>
        nodes != null ? nodeArrayCallback(nodes) : null;

    public static INode VisitEachNode(
        Func<INode, INode> nodeCallback, List<INode> nodes)
    {
        foreach (var node in nodes ?? Enumerable.Empty<INode>())
        {
            if (nodeCallback(node) is { } result)
            {
                return result;
            }
        }

        return null;
    }

    public static INode ForEachChild(
        INode node,
        Func<INode, INode> nodeCallback,
        Func<INode[], INode> nodeArrayCallback = null)
    {
        if (node is null)
        {
            return null;
        }

        INode visitNodes(IEnumerable<INode> nodes)
        {
            var nodeList = nodes?.Cast<INode>()?.ToList();
            return nodeList is not null
                ? nodeArrayCallback is null
                    ? VisitEachNode(nodeCallback, nodeList)
                    : nodeArrayCallback(nodeList.ToArray())
                : null;
        }

        return node.Kind switch
        {
            TypeScriptSyntaxKind.QualifiedName => VisitNode(nodeCallback, (node as QualifiedName)?.Left) ??
                VisitNode(nodeCallback, (node as QualifiedName)?.Right),
            TypeScriptSyntaxKind.TypeParameter => VisitNode(nodeCallback, (node as TypeParameterDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as TypeParameterDeclaration)?.Constraint) ??
                VisitNode(nodeCallback, (node as TypeParameterDeclaration)?.Default) ??
                VisitNode(nodeCallback, (node as TypeParameterDeclaration)?.Expression),
            TypeScriptSyntaxKind.ShorthandPropertyAssignment => visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ShorthandPropertyAssignment)?.Name) ??
                VisitNode(nodeCallback, (node as ShorthandPropertyAssignment)?.QuestionToken) ??
                VisitNode(nodeCallback, (node as ShorthandPropertyAssignment)?.EqualsToken) ??
                VisitNode(nodeCallback, (node as ShorthandPropertyAssignment)?.ObjectAssignmentInitializer),
            TypeScriptSyntaxKind.SpreadAssignment => VisitNode(nodeCallback, (node as SpreadAssignment)?.Expression),
            TypeScriptSyntaxKind.Parameter or
            TypeScriptSyntaxKind.PropertyDeclaration or
            TypeScriptSyntaxKind.PropertySignature or
            TypeScriptSyntaxKind.PropertyAssignment or
            TypeScriptSyntaxKind.VariableDeclaration or
            TypeScriptSyntaxKind.BindingElement =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.PropertyName) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.DotDotDotToken) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.QuestionToken) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.Type) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.Initializer),
            TypeScriptSyntaxKind.FunctionType or
            TypeScriptSyntaxKind.ConstructorType or
            TypeScriptSyntaxKind.CallSignature or
            TypeScriptSyntaxKind.ConstructSignature or
            TypeScriptSyntaxKind.IndexSignature =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                visitNodes((node as ISignatureDeclaration)?.TypeParameters) ??
                visitNodes((node as ISignatureDeclaration)?.Parameters) ??
                VisitNode(nodeCallback, (node as ISignatureDeclaration)?.Type),
            TypeScriptSyntaxKind.MethodDeclaration or
            TypeScriptSyntaxKind.MethodSignature or
            TypeScriptSyntaxKind.Constructor or
            TypeScriptSyntaxKind.GetAccessor or
            TypeScriptSyntaxKind.SetAccessor or
            TypeScriptSyntaxKind.FunctionExpression or
            TypeScriptSyntaxKind.FunctionDeclaration or
            TypeScriptSyntaxKind.ArrowFunction =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as IFunctionLikeDeclaration)?.AsteriskToken) ??
                VisitNode(nodeCallback, (node as IFunctionLikeDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as IFunctionLikeDeclaration)?.QuestionToken) ??
                visitNodes((node as IFunctionLikeDeclaration)?.TypeParameters) ??
                visitNodes((node as IFunctionLikeDeclaration)?.Parameters) ??
                VisitNode(nodeCallback, (node as IFunctionLikeDeclaration)?.Type) ??
                VisitNode(nodeCallback, (node as ArrowFunction)?.EqualsGreaterThanToken) ??
                VisitNode(nodeCallback, (node as IFunctionLikeDeclaration)?.Body),
            TypeScriptSyntaxKind.TypeReference =>
                VisitNode(nodeCallback, (node as TypeReferenceNode)?.TypeName) ??
                visitNodes((node as TypeReferenceNode)?.TypeArguments),
            TypeScriptSyntaxKind.TypePredicate =>
                VisitNode(nodeCallback, (node as TypePredicateNode)?.ParameterName) ??
                VisitNode(nodeCallback, (node as TypePredicateNode)?.Type),
            TypeScriptSyntaxKind.TypeQuery => VisitNode(nodeCallback, (node as TypeQueryNode)?.ExprName),
            TypeScriptSyntaxKind.TypeLiteral => visitNodes((node as TypeLiteralNode)?.Members),
            TypeScriptSyntaxKind.ArrayType => VisitNode(nodeCallback, (node as ArrayTypeNode)?.ElementType),
            TypeScriptSyntaxKind.TupleType => visitNodes((node as TupleTypeNode)?.ElementTypes),
            TypeScriptSyntaxKind.UnionType or
            TypeScriptSyntaxKind.IntersectionType =>
                visitNodes((node as IUnionOrIntersectionTypeNode)?.Types),
            TypeScriptSyntaxKind.ParenthesizedType or
            TypeScriptSyntaxKind.TypeOperator =>
                VisitNode(nodeCallback, (node as ParenthesizedTypeNode)?.Type ?? (node as TypeOperatorNode)?.Type),
            TypeScriptSyntaxKind.IndexedAccessType =>
                VisitNode(nodeCallback, (node as IndexedAccessTypeNode)?.ObjectType) ??
                VisitNode(nodeCallback, (node as IndexedAccessTypeNode)?.IndexType),
            TypeScriptSyntaxKind.MappedType =>
                VisitNode(nodeCallback, (node as MappedTypeNode)?.ReadonlyToken) ??
                VisitNode(nodeCallback, (node as MappedTypeNode)?.TypeParameter) ??
                VisitNode(nodeCallback, (node as MappedTypeNode)?.QuestionToken) ??
                VisitNode(nodeCallback, (node as MappedTypeNode)?.Type),
            TypeScriptSyntaxKind.LiteralType => VisitNode(nodeCallback, (node as LiteralTypeNode)?.Literal),
            TypeScriptSyntaxKind.ObjectBindingPattern or
            TypeScriptSyntaxKind.ArrayBindingPattern =>
                visitNodes(((IBindingPattern)node).Elements),
            TypeScriptSyntaxKind.ArrayLiteralExpression => visitNodes((node as ArrayLiteralExpression)?.Elements),
            TypeScriptSyntaxKind.ObjectLiteralExpression => visitNodes((node as ObjectLiteralExpression)?.Properties),
            TypeScriptSyntaxKind.PropertyAccessExpression =>
                VisitNode(nodeCallback, (node as PropertyAccessExpression)?.Expression) ??
                VisitNode(nodeCallback, (node as PropertyAccessExpression)?.Name),
            TypeScriptSyntaxKind.ElementAccessExpression =>
                VisitNode(nodeCallback, (node as ElementAccessExpression)?.Expression) ??
                VisitNode(nodeCallback, (node as ElementAccessExpression)?.ArgumentExpression),
            TypeScriptSyntaxKind.CallExpression or
            TypeScriptSyntaxKind.NewExpression =>
                VisitNode(nodeCallback, (node as CallExpression)?.Expression) ??
                visitNodes((node as CallExpression)?.TypeArguments) ??
                visitNodes((node as CallExpression)?.Arguments),
            TypeScriptSyntaxKind.TaggedTemplateExpression =>
                VisitNode(nodeCallback, (node as TaggedTemplateExpression)?.Tag) ??
                VisitNode(nodeCallback, (node as TaggedTemplateExpression)?.Template),
            TypeScriptSyntaxKind.TypeAssertionExpression =>
                VisitNode(nodeCallback, (node as TypeAssertion)?.Type) ??
                VisitNode(nodeCallback, (node as TypeAssertion)?.Expression),
            TypeScriptSyntaxKind.ParenthesizedExpression => VisitNode(nodeCallback, (node as ParenthesizedExpression)?.Expression),
            TypeScriptSyntaxKind.DeleteExpression => VisitNode(nodeCallback, (node as DeleteExpression)?.Expression),
            TypeScriptSyntaxKind.TypeOfExpression => VisitNode(nodeCallback, (node as TypeOfExpression)?.Expression),
            TypeScriptSyntaxKind.VoidExpression => VisitNode(nodeCallback, (node as VoidExpression)?.Expression),
            TypeScriptSyntaxKind.PrefixUnaryExpression => VisitNode(nodeCallback, (node as PrefixUnaryExpression)?.Operand),
            TypeScriptSyntaxKind.YieldExpression =>
                VisitNode(nodeCallback, (node as YieldExpression)?.AsteriskToken) ??
                VisitNode(nodeCallback, (node as YieldExpression)?.Expression),
            TypeScriptSyntaxKind.AwaitExpression => VisitNode(nodeCallback, (node as AwaitExpression)?.Expression),
            TypeScriptSyntaxKind.PostfixUnaryExpression => VisitNode(nodeCallback, (node as PostfixUnaryExpression)?.Operand),
            TypeScriptSyntaxKind.BinaryExpression =>
                VisitNode(nodeCallback, (node as BinaryExpression)?.Left) ??
                VisitNode(nodeCallback, (node as BinaryExpression)?.OperatorToken) ??
                VisitNode(nodeCallback, (node as BinaryExpression)?.Right),
            TypeScriptSyntaxKind.AsExpression =>
                VisitNode(nodeCallback, (node as AsExpression)?.Expression) ??
                VisitNode(nodeCallback, (node as AsExpression)?.Type),
            TypeScriptSyntaxKind.NonNullExpression => VisitNode(nodeCallback, (node as NonNullExpression)?.Expression),
            TypeScriptSyntaxKind.MetaProperty => VisitNode(nodeCallback, (node as MetaProperty)?.Name),
            TypeScriptSyntaxKind.ConditionalExpression =>
                VisitNode(nodeCallback, (node as ConditionalExpression)?.Condition) ??
                VisitNode(nodeCallback, (node as ConditionalExpression)?.QuestionToken) ??
                VisitNode(nodeCallback, (node as ConditionalExpression)?.WhenTrue) ??
                VisitNode(nodeCallback, (node as ConditionalExpression)?.ColonToken) ??
                VisitNode(nodeCallback, (node as ConditionalExpression)?.WhenFalse),
            TypeScriptSyntaxKind.SpreadElement => VisitNode(nodeCallback, (node as SpreadElement)?.Expression),
            TypeScriptSyntaxKind.Block or
            TypeScriptSyntaxKind.ModuleBlock =>
                visitNodes((node as Block)?.Statements),
            TypeScriptSyntaxKind.SourceFile =>
                visitNodes((node as SourceFile)?.Statements) ??
                VisitNode(nodeCallback, (node as SourceFile)?.EndOfFileToken),
            TypeScriptSyntaxKind.VariableStatement =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as VariableStatement)?.DeclarationList),
            TypeScriptSyntaxKind.VariableDeclarationList =>
                visitNodes((node as VariableDeclarationList)?.Declarations),
            TypeScriptSyntaxKind.ExpressionStatement => VisitNode(nodeCallback, (node as ExpressionStatement)?.Expression),
            TypeScriptSyntaxKind.IfStatement =>
                VisitNode(nodeCallback, (node as IfStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as IfStatement)?.ThenStatement) ??
                VisitNode(nodeCallback, (node as IfStatement)?.ElseStatement),
            TypeScriptSyntaxKind.DoStatement =>
                VisitNode(nodeCallback, (node as DoStatement)?.Statement) ??
                VisitNode(nodeCallback, (node as DoStatement)?.Expression),
            TypeScriptSyntaxKind.WhileStatement =>
                VisitNode(nodeCallback, (node as WhileStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as WhileStatement)?.Statement),
            TypeScriptSyntaxKind.ForStatement =>
                VisitNode(nodeCallback, (node as ForStatement)?.Initializer) ??
                VisitNode(nodeCallback, (node as ForStatement)?.Condition) ??
                VisitNode(nodeCallback, (node as ForStatement)?.Incrementor) ??
                VisitNode(nodeCallback, (node as ForStatement)?.Statement),
            TypeScriptSyntaxKind.ForInStatement =>
                VisitNode(nodeCallback, (node as ForInStatement)?.Initializer) ??
                VisitNode(nodeCallback, (node as ForInStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as ForInStatement)?.Statement),
            TypeScriptSyntaxKind.ForOfStatement =>
                VisitNode(nodeCallback, (node as ForOfStatement)?.AwaitModifier) ??
                VisitNode(nodeCallback, (node as ForOfStatement)?.Initializer) ??
                VisitNode(nodeCallback, (node as ForOfStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as ForOfStatement)?.Statement),
            TypeScriptSyntaxKind.ContinueStatement or
            TypeScriptSyntaxKind.BreakStatement =>
                VisitNode(nodeCallback, (node as IBreakOrContinueStatement)?.Label),
            TypeScriptSyntaxKind.ReturnStatement => VisitNode(nodeCallback, (node as ReturnStatement)?.Expression),
            TypeScriptSyntaxKind.WithStatement =>
                VisitNode(nodeCallback, (node as WithStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as WithStatement)?.Statement),
            TypeScriptSyntaxKind.SwitchStatement =>
                VisitNode(nodeCallback, (node as SwitchStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as SwitchStatement)?.CaseBlock),
            TypeScriptSyntaxKind.CaseBlock => visitNodes((node as CaseBlock)?.Clauses),
            TypeScriptSyntaxKind.CaseClause =>
                VisitNode(nodeCallback, (node as CaseClause)?.Expression) ??
                visitNodes((node as CaseClause)?.Statements),
            TypeScriptSyntaxKind.DefaultClause => visitNodes((node as DefaultClause)?.Statements),
            TypeScriptSyntaxKind.LabeledStatement =>
                VisitNode(nodeCallback, (node as LabeledStatement)?.Label) ??
                VisitNode(nodeCallback, (node as LabeledStatement)?.Statement),
            TypeScriptSyntaxKind.ThrowStatement => VisitNode(nodeCallback, (node as ThrowStatement)?.Expression),
            TypeScriptSyntaxKind.TryStatement =>
                VisitNode(nodeCallback, (node as TryStatement)?.TryBlock) ??
                VisitNode(nodeCallback, (node as TryStatement)?.CatchClause) ??
                VisitNode(nodeCallback, (node as TryStatement)?.FinallyBlock),
            TypeScriptSyntaxKind.CatchClause =>
                VisitNode(nodeCallback, (node as CatchClause)?.VariableDeclaration) ??
                VisitNode(nodeCallback, (node as CatchClause)?.Block),
            TypeScriptSyntaxKind.Decorator => VisitNode(nodeCallback, (node as Decorator)?.Expression),
            TypeScriptSyntaxKind.ClassDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ClassDeclaration)?.Name) ??
                visitNodes((node as ClassDeclaration)?.TypeParameters) ??
                visitNodes((node as ClassDeclaration)?.HeritageClauses) ??
                visitNodes((node as ClassDeclaration)?.Members),
            TypeScriptSyntaxKind.ClassExpression =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ClassExpression)?.Name) ??
                visitNodes((node as ClassExpression)?.TypeParameters) ??
                visitNodes((node as ClassExpression)?.HeritageClauses) ??
                visitNodes((node as ClassExpression)?.Members),

            TypeScriptSyntaxKind.InterfaceDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as InterfaceDeclaration)?.Name) ??
                visitNodes((node as InterfaceDeclaration)?.TypeParameters) ??
                visitNodes((node as InterfaceDeclaration)?.HeritageClauses) ??
                visitNodes((node as InterfaceDeclaration)?.Members),
            TypeScriptSyntaxKind.TypeAliasDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as TypeAliasDeclaration)?.Name) ??
                visitNodes((node as TypeAliasDeclaration)?.TypeParameters) ??
                VisitNode(nodeCallback, (node as TypeAliasDeclaration)?.Type),
            TypeScriptSyntaxKind.EnumDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as EnumDeclaration)?.Name) ??
                visitNodes((node as EnumDeclaration)?.Members),
            TypeScriptSyntaxKind.EnumMember =>
                VisitNode(nodeCallback, (node as EnumMember)?.Name) ??
                VisitNode(nodeCallback, (node as EnumMember)?.Initializer),
            TypeScriptSyntaxKind.ModuleDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ModuleDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as ModuleDeclaration)?.Body),
            TypeScriptSyntaxKind.ImportEqualsDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ImportEqualsDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as ImportEqualsDeclaration)?.ModuleReference),
            TypeScriptSyntaxKind.ImportDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ImportDeclaration)?.ImportClause) ??
                VisitNode(nodeCallback, (node as ImportDeclaration)?.ModuleSpecifier),
            TypeScriptSyntaxKind.ImportClause => VisitNode(nodeCallback, (node as ImportClause)?.Name) ??
                VisitNode(nodeCallback, (node as ImportClause)?.NamedBindings),
            TypeScriptSyntaxKind.NamespaceExportDeclaration => VisitNode(nodeCallback, (node as NamespaceExportDeclaration)?.Name),
            TypeScriptSyntaxKind.NamespaceImport =>
                VisitNode(nodeCallback, (node as NamespaceImport)?.Name),
            TypeScriptSyntaxKind.NamedImports or
            TypeScriptSyntaxKind.NamedExports => node is NamedImports
                ? visitNodes((node as NamedImports)?.Elements)
                : visitNodes((node as NamedExports)?.Elements),
            TypeScriptSyntaxKind.ExportDeclaration => visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ExportDeclaration)?.ExportClause) ??
                VisitNode(nodeCallback, (node as ExportDeclaration)?.ModuleSpecifier),
            TypeScriptSyntaxKind.ImportSpecifier or TypeScriptSyntaxKind.ExportSpecifier =>
                VisitNode(nodeCallback, (node as IImportOrExportSpecifier)?.PropertyName ??
                VisitNode(nodeCallback, (node as IImportOrExportSpecifier)?.Name)),
            TypeScriptSyntaxKind.ExportAssignment =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ExportAssignment)?.Expression),
            TypeScriptSyntaxKind.TemplateExpression => VisitNode(nodeCallback, (node as TemplateExpression)?.Head) ??
                visitNodes((node as TemplateExpression)?.TemplateSpans),
            TypeScriptSyntaxKind.TemplateSpan => VisitNode(nodeCallback, (node as TemplateSpan)?.Expression) ??
                VisitNode(nodeCallback, (node as TemplateSpan)?.Literal),
            TypeScriptSyntaxKind.ComputedPropertyName => VisitNode(nodeCallback, (node as ComputedPropertyName)?.Expression),
            TypeScriptSyntaxKind.HeritageClause => visitNodes((node as HeritageClause)?.Types),
            TypeScriptSyntaxKind.ExpressionWithTypeArguments =>
                VisitNode(nodeCallback, (node as ExpressionWithTypeArguments)?.Expression) ??
                visitNodes((node as ExpressionWithTypeArguments)?.TypeArguments),
            TypeScriptSyntaxKind.ExternalModuleReference => VisitNode(nodeCallback, (node as ExternalModuleReference)?.Expression),
            TypeScriptSyntaxKind.MissingDeclaration => visitNodes(node.Decorators),
            TypeScriptSyntaxKind.JsxElement => VisitNode(nodeCallback, (node as JsxElement)?.OpeningElement) ??
                visitNodes((node as JsxElement)?.JsxChildren) ??
                VisitNode(nodeCallback, (node as JsxElement)?.ClosingElement),
            TypeScriptSyntaxKind.JsxSelfClosingElement or TypeScriptSyntaxKind.JsxOpeningElement =>
                VisitNode(nodeCallback, (node as JsxSelfClosingElement)?.TagName ?? (node as JsxOpeningElement)?.TagName) ??
                VisitNode(nodeCallback, (node as JsxSelfClosingElement)?.Attributes ?? (node as JsxOpeningElement)?.Attributes),
            TypeScriptSyntaxKind.JsxAttributes => visitNodes((node as JsxAttributes)?.Properties),
            TypeScriptSyntaxKind.JsxAttribute => VisitNode(nodeCallback, (node as JsxAttribute)?.Name) ??
                VisitNode(nodeCallback, (node as JsxAttribute)?.Initializer),
            TypeScriptSyntaxKind.JsxSpreadAttribute => VisitNode(nodeCallback, (node as JsxSpreadAttribute)?.Expression),
            TypeScriptSyntaxKind.JsxExpression => VisitNode(nodeCallback, (node as JsxExpression)?.DotDotDotToken) ??
                VisitNode(nodeCallback, (node as JsxExpression)?.Expression),
            TypeScriptSyntaxKind.JsxClosingElement => VisitNode(nodeCallback, (node as JsxClosingElement)?.TagName),
            TypeScriptSyntaxKind.JsDocTypeExpression => VisitNode(nodeCallback, (node as JsDocTypeExpression)?.Type),
            TypeScriptSyntaxKind.JsDocUnionType => visitNodes((node as JsDocUnionType)?.Types),
            TypeScriptSyntaxKind.JsDocTupleType => visitNodes((node as JsDocTupleType)?.Types),
            TypeScriptSyntaxKind.JsDocArrayType => VisitNode(nodeCallback, (node as JsDocArrayType)?.ElementType),
            TypeScriptSyntaxKind.JsDocNonNullableType => VisitNode(nodeCallback, (node as JsDocNonNullableType)?.Type),
            TypeScriptSyntaxKind.JsDocNullableType => VisitNode(nodeCallback, (node as JsDocNullableType)?.Type),
            TypeScriptSyntaxKind.JsDocRecordType => VisitNode(nodeCallback, (node as JsDocRecordType)?.Literal),
            TypeScriptSyntaxKind.JsDocTypeReference => VisitNode(nodeCallback, (node as JsDocTypeReference)?.Name) ??
                visitNodes((node as JsDocTypeReference)?.TypeArguments),
            TypeScriptSyntaxKind.JsDocOptionalType => VisitNode(nodeCallback, (node as JsDocOptionalType)?.Type),
            TypeScriptSyntaxKind.JsDocFunctionType => visitNodes((node as JsDocFunctionType)?.Parameters) ??
                VisitNode(nodeCallback, (node as JsDocFunctionType)?.Type),
            TypeScriptSyntaxKind.JsDocVariadicType => VisitNode(nodeCallback, (node as JsDocVariadicType)?.Type),
            TypeScriptSyntaxKind.JsDocConstructorType => VisitNode(nodeCallback, (node as JsDocConstructorType)?.Type),
            TypeScriptSyntaxKind.JsDocThisType => VisitNode(nodeCallback, (node as JsDocThisType)?.Type),
            TypeScriptSyntaxKind.JsDocRecordMember => VisitNode(nodeCallback, (node as JsDocRecordMember)?.Name) ??
                VisitNode(nodeCallback, (node as JsDocRecordMember)?.Type),
            TypeScriptSyntaxKind.JsDocComment => visitNodes((node as JsDoc)?.Tags),
            TypeScriptSyntaxKind.JsDocParameterTag => VisitNode(nodeCallback, (node as JsDocParameterTag)?.PreParameterName) ??
                VisitNode(nodeCallback, (node as JsDocParameterTag)?.TypeExpression) ??
                VisitNode(nodeCallback, (node as JsDocParameterTag)?.PostParameterName),
            TypeScriptSyntaxKind.JsDocReturnTag => VisitNode(nodeCallback, (node as JsDocReturnTag)?.TypeExpression),
            TypeScriptSyntaxKind.JsDocTypeTag => VisitNode(nodeCallback, (node as JsDocTypeTag)?.TypeExpression),
            TypeScriptSyntaxKind.JsDocAugmentsTag => VisitNode(nodeCallback, (node as JsDocAugmentsTag)?.TypeExpression),
            TypeScriptSyntaxKind.JsDocTemplateTag => visitNodes((node as JsDocTemplateTag)?.TypeParameters),
            TypeScriptSyntaxKind.JsDocTypedefTag => VisitNode(nodeCallback, (node as JsDocTypedefTag)?.TypeExpression) ??
                VisitNode(nodeCallback, (node as JsDocTypedefTag)?.FullName) ??
                VisitNode(nodeCallback, (node as JsDocTypedefTag)?.Name) ??
                VisitNode(nodeCallback, (node as JsDocTypedefTag)?.JsDocTypeLiteral),
            TypeScriptSyntaxKind.JsDocTypeLiteral => visitNodes((node as JsDocTypeLiteral)?.JsDocPropertyTags),
            TypeScriptSyntaxKind.JsDocPropertyTag => VisitNode(nodeCallback, (node as JsDocPropertyTag)?.TypeExpression) ??
                VisitNode(nodeCallback, (node as JsDocPropertyTag)?.Name),
            TypeScriptSyntaxKind.PartiallyEmittedExpression => VisitNode(nodeCallback, (node as PartiallyEmittedExpression)?.Expression),
            TypeScriptSyntaxKind.JsDocLiteralType => VisitNode(nodeCallback, (node as JsDocLiteralType)?.Literal),
            _ => null,
        };
    }
}
