// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript.Parser;

internal sealed class Ts
{
    internal static INode? VisitNode(
        Func<INode, INode?> nodeCallback, INode? node) =>
        node != null ? nodeCallback(node) : null;

    internal static T? VisitList<T>(
        Func<INode[], T> nodeArrayCallback, INode[] nodes) =>
        nodes != null ? nodeArrayCallback(nodes) : default;

    internal static INode? VisitNodeArray(
        Func<INode[], INode> nodeArrayCallback, INode[] nodes) =>
        nodes != null ? nodeArrayCallback(nodes) : null;

    internal static INode? VisitEachNode(
        Func<INode, INode?> nodeCallback, List<INode> nodes)
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

    internal static INode? ForEachChild(
        INode node,
        Func<INode, INode?> nodeCallback,
        Func<INode[], INode>? nodeArrayCallback = null)
    {
        if (node is null)
        {
            return null;
        }

        INode? visitNodes(IEnumerable<INode>? nodes)
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
            SyntaxKind.QualifiedName => VisitNode(nodeCallback, (node as QualifiedName)?.Left) ??
                VisitNode(nodeCallback, (node as QualifiedName)?.Right),
            SyntaxKind.TypeParameter => VisitNode(nodeCallback, (node as TypeParameterDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as TypeParameterDeclaration)?.Constraint) ??
                VisitNode(nodeCallback, (node as TypeParameterDeclaration)?.Default) ??
                VisitNode(nodeCallback, (node as TypeParameterDeclaration)?.Expression),
            SyntaxKind.ShorthandPropertyAssignment => visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ShorthandPropertyAssignment)?.Name) ??
                VisitNode(nodeCallback, (node as ShorthandPropertyAssignment)?.QuestionToken) ??
                VisitNode(nodeCallback, (node as ShorthandPropertyAssignment)?.EqualsToken) ??
                VisitNode(nodeCallback, (node as ShorthandPropertyAssignment)?.ObjectAssignmentInitializer),
            SyntaxKind.SpreadAssignment => VisitNode(nodeCallback, (node as SpreadAssignment)?.Expression),
            SyntaxKind.Parameter or
            SyntaxKind.PropertyDeclaration or
            SyntaxKind.PropertySignature or
            SyntaxKind.PropertyAssignment or
            SyntaxKind.VariableDeclaration or
            SyntaxKind.BindingElement =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.PropertyName) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.DotDotDotToken) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.QuestionToken) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.Type) ??
                VisitNode(nodeCallback, (node as IVariableLikeDeclaration)?.Initializer),
            SyntaxKind.FunctionType or
            SyntaxKind.ConstructorType or
            SyntaxKind.CallSignature or
            SyntaxKind.ConstructSignature or
            SyntaxKind.IndexSignature =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                visitNodes((node as ISignatureDeclaration)?.TypeParameters) ??
                visitNodes((node as ISignatureDeclaration)?.Parameters) ??
                VisitNode(nodeCallback, (node as ISignatureDeclaration)?.Type),
            SyntaxKind.MethodDeclaration or
            SyntaxKind.MethodSignature or
            SyntaxKind.Constructor or
            SyntaxKind.GetAccessor or
            SyntaxKind.SetAccessor or
            SyntaxKind.FunctionExpression or
            SyntaxKind.FunctionDeclaration or
            SyntaxKind.ArrowFunction =>
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
            SyntaxKind.TypeReference =>
                VisitNode(nodeCallback, (node as TypeReferenceNode)?.TypeName) ??
                visitNodes((node as TypeReferenceNode)?.TypeArguments),
            SyntaxKind.TypePredicate =>
                VisitNode(nodeCallback, (node as TypePredicateNode)?.ParameterName) ??
                VisitNode(nodeCallback, (node as TypePredicateNode)?.Type),
            SyntaxKind.TypeQuery => VisitNode(nodeCallback, (node as TypeQueryNode)?.ExprName),
            SyntaxKind.TypeLiteral => visitNodes((node as TypeLiteralNode)?.Members),
            SyntaxKind.ArrayType => VisitNode(nodeCallback, (node as ArrayTypeNode)?.ElementType),
            SyntaxKind.TupleType => visitNodes((node as TupleTypeNode)?.ElementTypes),
            SyntaxKind.UnionType or
            SyntaxKind.IntersectionType =>
                visitNodes((node as IUnionOrIntersectionTypeNode)?.Types),
            SyntaxKind.ParenthesizedType or
            SyntaxKind.TypeOperator =>
                VisitNode(nodeCallback, (node as ParenthesizedTypeNode)?.Type ?? (node as TypeOperatorNode)?.Type),
            SyntaxKind.IndexedAccessType =>
                VisitNode(nodeCallback, (node as IndexedAccessTypeNode)?.ObjectType) ??
                VisitNode(nodeCallback, (node as IndexedAccessTypeNode)?.IndexType),
            SyntaxKind.MappedType =>
                VisitNode(nodeCallback, (node as MappedTypeNode)?.ReadonlyToken) ??
                VisitNode(nodeCallback, (node as MappedTypeNode)?.TypeParameter) ??
                VisitNode(nodeCallback, (node as MappedTypeNode)?.QuestionToken) ??
                VisitNode(nodeCallback, (node as MappedTypeNode)?.Type),
            SyntaxKind.LiteralType => VisitNode(nodeCallback, (node as LiteralTypeNode)?.Literal),
            SyntaxKind.ObjectBindingPattern or
            SyntaxKind.ArrayBindingPattern =>
                visitNodes(((IBindingPattern)node).Elements),
            SyntaxKind.ArrayLiteralExpression => visitNodes((node as ArrayLiteralExpression)?.Elements),
            SyntaxKind.ObjectLiteralExpression => visitNodes((node as ObjectLiteralExpression)?.Properties),
            SyntaxKind.PropertyAccessExpression =>
                VisitNode(nodeCallback, (node as PropertyAccessExpression)?.Expression) ??
                VisitNode(nodeCallback, (node as PropertyAccessExpression)?.Name),
            SyntaxKind.ElementAccessExpression =>
                VisitNode(nodeCallback, (node as ElementAccessExpression)?.Expression) ??
                VisitNode(nodeCallback, (node as ElementAccessExpression)?.ArgumentExpression),
            SyntaxKind.CallExpression or
            SyntaxKind.NewExpression =>
                VisitNode(nodeCallback, (node as CallExpression)?.Expression) ??
                visitNodes((node as CallExpression)?.TypeArguments) ??
                visitNodes((node as CallExpression)?.Arguments),
            SyntaxKind.TaggedTemplateExpression =>
                VisitNode(nodeCallback, (node as TaggedTemplateExpression)?.Tag) ??
                VisitNode(nodeCallback, (node as TaggedTemplateExpression)?.Template),
            SyntaxKind.TypeAssertionExpression =>
                VisitNode(nodeCallback, (node as TypeAssertion)?.Type) ??
                VisitNode(nodeCallback, (node as TypeAssertion)?.Expression),
            SyntaxKind.ParenthesizedExpression => VisitNode(nodeCallback, (node as ParenthesizedExpression)?.Expression),
            SyntaxKind.DeleteExpression => VisitNode(nodeCallback, (node as DeleteExpression)?.Expression),
            SyntaxKind.TypeOfExpression => VisitNode(nodeCallback, (node as TypeOfExpression)?.Expression),
            SyntaxKind.VoidExpression => VisitNode(nodeCallback, (node as VoidExpression)?.Expression),
            SyntaxKind.PrefixUnaryExpression => VisitNode(nodeCallback, (node as PrefixUnaryExpression)?.Operand),
            SyntaxKind.YieldExpression =>
                VisitNode(nodeCallback, (node as YieldExpression)?.AsteriskToken) ??
                VisitNode(nodeCallback, (node as YieldExpression)?.Expression),
            SyntaxKind.AwaitExpression => VisitNode(nodeCallback, (node as AwaitExpression)?.Expression),
            SyntaxKind.PostfixUnaryExpression => VisitNode(nodeCallback, (node as PostfixUnaryExpression)?.Operand),
            SyntaxKind.BinaryExpression =>
                VisitNode(nodeCallback, (node as BinaryExpression)?.Left) ??
                VisitNode(nodeCallback, (node as BinaryExpression)?.OperatorToken) ??
                VisitNode(nodeCallback, (node as BinaryExpression)?.Right),
            SyntaxKind.AsExpression =>
                VisitNode(nodeCallback, (node as AsExpression)?.Expression) ??
                VisitNode(nodeCallback, (node as AsExpression)?.Type),
            SyntaxKind.NonNullExpression => VisitNode(nodeCallback, (node as NonNullExpression)?.Expression),
            SyntaxKind.MetaProperty => VisitNode(nodeCallback, (node as MetaProperty)?.Name),
            SyntaxKind.ConditionalExpression =>
                VisitNode(nodeCallback, (node as ConditionalExpression)?.Condition) ??
                VisitNode(nodeCallback, (node as ConditionalExpression)?.QuestionToken) ??
                VisitNode(nodeCallback, (node as ConditionalExpression)?.WhenTrue) ??
                VisitNode(nodeCallback, (node as ConditionalExpression)?.ColonToken) ??
                VisitNode(nodeCallback, (node as ConditionalExpression)?.WhenFalse),
            SyntaxKind.SpreadElement => VisitNode(nodeCallback, (node as SpreadElement)?.Expression),
            SyntaxKind.Block or
            SyntaxKind.ModuleBlock =>
                visitNodes((node as Block)?.Statements),
            SyntaxKind.SourceFile =>
                visitNodes((node as SourceFile)?.Statements) ??
                VisitNode(nodeCallback, (node as SourceFile)?.EndOfFileToken),
            SyntaxKind.VariableStatement =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as VariableStatement)?.DeclarationList),
            SyntaxKind.VariableDeclarationList =>
                visitNodes((node as VariableDeclarationList)?.Declarations),
            SyntaxKind.ExpressionStatement => VisitNode(nodeCallback, (node as ExpressionStatement)?.Expression),
            SyntaxKind.IfStatement =>
                VisitNode(nodeCallback, (node as IfStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as IfStatement)?.ThenStatement) ??
                VisitNode(nodeCallback, (node as IfStatement)?.ElseStatement),
            SyntaxKind.DoStatement =>
                VisitNode(nodeCallback, (node as DoStatement)?.Statement) ??
                VisitNode(nodeCallback, (node as DoStatement)?.Expression),
            SyntaxKind.WhileStatement =>
                VisitNode(nodeCallback, (node as WhileStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as WhileStatement)?.Statement),
            SyntaxKind.ForStatement =>
                VisitNode(nodeCallback, (node as ForStatement)?.Initializer) ??
                VisitNode(nodeCallback, (node as ForStatement)?.Condition) ??
                VisitNode(nodeCallback, (node as ForStatement)?.Incrementor) ??
                VisitNode(nodeCallback, (node as ForStatement)?.Statement),
            SyntaxKind.ForInStatement =>
                VisitNode(nodeCallback, (node as ForInStatement)?.Initializer) ??
                VisitNode(nodeCallback, (node as ForInStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as ForInStatement)?.Statement),
            SyntaxKind.ForOfStatement =>
                VisitNode(nodeCallback, (node as ForOfStatement)?.AwaitModifier) ??
                VisitNode(nodeCallback, (node as ForOfStatement)?.Initializer) ??
                VisitNode(nodeCallback, (node as ForOfStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as ForOfStatement)?.Statement),
            SyntaxKind.ContinueStatement or
            SyntaxKind.BreakStatement =>
                VisitNode(nodeCallback, (node as IBreakOrContinueStatement)?.Label),
            SyntaxKind.ReturnStatement => VisitNode(nodeCallback, (node as ReturnStatement)?.Expression),
            SyntaxKind.WithStatement =>
                VisitNode(nodeCallback, (node as WithStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as WithStatement)?.Statement),
            SyntaxKind.SwitchStatement =>
                VisitNode(nodeCallback, (node as SwitchStatement)?.Expression) ??
                VisitNode(nodeCallback, (node as SwitchStatement)?.CaseBlock),
            SyntaxKind.CaseBlock => visitNodes((node as CaseBlock)?.Clauses),
            SyntaxKind.CaseClause =>
                VisitNode(nodeCallback, (node as CaseClause)?.Expression) ??
                visitNodes((node as CaseClause)?.Statements),
            SyntaxKind.DefaultClause => visitNodes((node as DefaultClause)?.Statements),
            SyntaxKind.LabeledStatement =>
                VisitNode(nodeCallback, (node as LabeledStatement)?.Label) ??
                VisitNode(nodeCallback, (node as LabeledStatement)?.Statement),
            SyntaxKind.ThrowStatement => VisitNode(nodeCallback, (node as ThrowStatement)?.Expression),
            SyntaxKind.TryStatement =>
                VisitNode(nodeCallback, (node as TryStatement)?.TryBlock) ??
                VisitNode(nodeCallback, (node as TryStatement)?.CatchClause) ??
                VisitNode(nodeCallback, (node as TryStatement)?.FinallyBlock),
            SyntaxKind.CatchClause =>
                VisitNode(nodeCallback, (node as CatchClause)?.VariableDeclaration) ??
                VisitNode(nodeCallback, (node as CatchClause)?.Block),
            SyntaxKind.Decorator => VisitNode(nodeCallback, (node as Decorator)?.Expression),
            SyntaxKind.ClassDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ClassDeclaration)?.Name) ??
                visitNodes((node as ClassDeclaration)?.TypeParameters) ??
                visitNodes((node as ClassDeclaration)?.HeritageClauses) ??
                visitNodes((node as ClassDeclaration)?.Members),
            SyntaxKind.ClassExpression =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ClassExpression)?.Name) ??
                visitNodes((node as ClassExpression)?.TypeParameters) ??
                visitNodes((node as ClassExpression)?.HeritageClauses) ??
                visitNodes((node as ClassExpression)?.Members),

            SyntaxKind.InterfaceDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as InterfaceDeclaration)?.Name) ??
                visitNodes((node as InterfaceDeclaration)?.TypeParameters) ??
                visitNodes((node as InterfaceDeclaration)?.HeritageClauses) ??
                visitNodes((node as InterfaceDeclaration)?.Members),
            SyntaxKind.TypeAliasDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as TypeAliasDeclaration)?.Name) ??
                visitNodes((node as TypeAliasDeclaration)?.TypeParameters) ??
                VisitNode(nodeCallback, (node as TypeAliasDeclaration)?.Type),
            SyntaxKind.EnumDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as EnumDeclaration)?.Name) ??
                visitNodes((node as EnumDeclaration)?.Members),
            SyntaxKind.EnumMember =>
                VisitNode(nodeCallback, (node as EnumMember)?.Name) ??
                VisitNode(nodeCallback, (node as EnumMember)?.Initializer),
            SyntaxKind.ModuleDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ModuleDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as ModuleDeclaration)?.Body),
            SyntaxKind.ImportEqualsDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ImportEqualsDeclaration)?.Name) ??
                VisitNode(nodeCallback, (node as ImportEqualsDeclaration)?.ModuleReference),
            SyntaxKind.ImportDeclaration =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ImportDeclaration)?.ImportClause) ??
                VisitNode(nodeCallback, (node as ImportDeclaration)?.ModuleSpecifier),
            SyntaxKind.ImportClause => VisitNode(nodeCallback, (node as ImportClause)?.Name) ??
                VisitNode(nodeCallback, (node as ImportClause)?.NamedBindings),
            SyntaxKind.NamespaceExportDeclaration => VisitNode(nodeCallback, (node as NamespaceExportDeclaration)?.Name),
            SyntaxKind.NamespaceImport =>
                VisitNode(nodeCallback, (node as NamespaceImport)?.Name),
            SyntaxKind.NamedImports or
            SyntaxKind.NamedExports => node is NamedImports
                ? visitNodes((node as NamedImports)?.Elements)
                : visitNodes((node as NamedExports)?.Elements),
            SyntaxKind.ExportDeclaration => visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ExportDeclaration)?.ExportClause) ??
                VisitNode(nodeCallback, (node as ExportDeclaration)?.ModuleSpecifier),
            SyntaxKind.ImportSpecifier or SyntaxKind.ExportSpecifier =>
                VisitNode(nodeCallback, (node as ImportOrExportSpecifier)?.PropertyName ??
                VisitNode(nodeCallback, (node as ImportOrExportSpecifier)?.Name)),
            SyntaxKind.ExportAssignment =>
                visitNodes(node.Decorators) ??
                visitNodes(node.Modifiers) ??
                VisitNode(nodeCallback, (node as ExportAssignment)?.Expression),
            SyntaxKind.TemplateExpression => VisitNode(nodeCallback, (node as TemplateExpression)?.Head) ??
                visitNodes((node as TemplateExpression)?.TemplateSpans),
            SyntaxKind.TemplateSpan => VisitNode(nodeCallback, (node as TemplateSpan)?.Expression) ??
                VisitNode(nodeCallback, (node as TemplateSpan)?.Literal),
            SyntaxKind.ComputedPropertyName => VisitNode(nodeCallback, (node as ComputedPropertyName)?.Expression),
            SyntaxKind.HeritageClause => visitNodes((node as HeritageClause)?.Types),
            SyntaxKind.ExpressionWithTypeArguments =>
                VisitNode(nodeCallback, (node as ExpressionWithTypeArguments)?.Expression) ??
                visitNodes((node as ExpressionWithTypeArguments)?.TypeArguments),
            SyntaxKind.ExternalModuleReference => VisitNode(nodeCallback, (node as ExternalModuleReference)?.Expression),
            SyntaxKind.MissingDeclaration => visitNodes(node.Decorators),
            SyntaxKind.JsxElement => VisitNode(nodeCallback, (node as JsxElement)?.OpeningElement) ??
                visitNodes((node as JsxElement)?.JsxChildren) ??
                VisitNode(nodeCallback, (node as JsxElement)?.ClosingElement),
            SyntaxKind.JsxSelfClosingElement or SyntaxKind.JsxOpeningElement =>
                VisitNode(nodeCallback, (node as JsxSelfClosingElement)?.TagName ?? (node as JsxOpeningElement)?.TagName) ??
                VisitNode(nodeCallback, (node as JsxSelfClosingElement)?.Attributes ?? (node as JsxOpeningElement)?.Attributes),
            SyntaxKind.JsxAttributes => visitNodes((node as JsxAttributes)?.Properties),
            SyntaxKind.JsxAttribute => VisitNode(nodeCallback, (node as JsxAttribute)?.Name) ??
                VisitNode(nodeCallback, (node as JsxAttribute)?.Initializer),
            SyntaxKind.JsxSpreadAttribute => VisitNode(nodeCallback, (node as JsxSpreadAttribute)?.Expression),
            SyntaxKind.JsxExpression => VisitNode(nodeCallback, (node as JsxExpression)?.DotDotDotToken) ??
                VisitNode(nodeCallback, (node as JsxExpression)?.Expression),
            SyntaxKind.JsxClosingElement => VisitNode(nodeCallback, (node as JsxClosingElement)?.TagName),
            SyntaxKind.JsDocTypeExpression => VisitNode(nodeCallback, (node as JsDocTypeExpression)?.Type),
            SyntaxKind.JsDocUnionType => visitNodes((node as JsDocUnionType)?.Types),
            SyntaxKind.JsDocTupleType => visitNodes((node as JsDocTupleType)?.Types),
            SyntaxKind.JsDocArrayType => VisitNode(nodeCallback, (node as JsDocArrayType)?.ElementType),
            SyntaxKind.JsDocNonNullableType => VisitNode(nodeCallback, (node as JsDocNonNullableType)?.Type),
            SyntaxKind.JsDocNullableType => VisitNode(nodeCallback, (node as JsDocNullableType)?.Type),
            SyntaxKind.JsDocRecordType => VisitNode(nodeCallback, (node as JsDocRecordType)?.Literal),
            SyntaxKind.JsDocTypeReference => VisitNode(nodeCallback, (node as JsDocTypeReference)?.Name) ??
                visitNodes((node as JsDocTypeReference)?.TypeArguments),
            SyntaxKind.JsDocOptionalType => VisitNode(nodeCallback, (node as JsDocOptionalType)?.Type),
            SyntaxKind.JsDocFunctionType => visitNodes((node as JsDocFunctionType)?.Parameters) ??
                VisitNode(nodeCallback, (node as JsDocFunctionType)?.Type),
            SyntaxKind.JsDocVariadicType => VisitNode(nodeCallback, (node as JsDocVariadicType)?.Type),
            SyntaxKind.JsDocConstructorType => VisitNode(nodeCallback, (node as JsDocConstructorType)?.Type),
            SyntaxKind.JsDocThisType => VisitNode(nodeCallback, (node as JsDocThisType)?.Type),
            SyntaxKind.JsDocRecordMember => VisitNode(nodeCallback, (node as JsDocRecordMember)?.Name) ??
                VisitNode(nodeCallback, (node as JsDocRecordMember)?.Type),
            SyntaxKind.JsDocComment => visitNodes((node as JsDoc)?.Tags),
            SyntaxKind.JsDocParameterTag => VisitNode(nodeCallback, (node as JsDocParameterTag)?.PreParameterName) ??
                VisitNode(nodeCallback, (node as JsDocParameterTag)?.TypeExpression) ??
                VisitNode(nodeCallback, (node as JsDocParameterTag)?.PostParameterName),
            SyntaxKind.JsDocReturnTag => VisitNode(nodeCallback, (node as JsDocReturnTag)?.TypeExpression),
            SyntaxKind.JsDocTypeTag => VisitNode(nodeCallback, (node as JsDocTypeTag)?.TypeExpression),
            SyntaxKind.JsDocAugmentsTag => VisitNode(nodeCallback, (node as JsDocAugmentsTag)?.TypeExpression),
            SyntaxKind.JsDocTemplateTag => visitNodes((node as JsDocTemplateTag)?.TypeParameters),
            SyntaxKind.JsDocTypedefTag => VisitNode(nodeCallback, (node as JsDocTypedefTag)?.TypeExpression) ??
                VisitNode(nodeCallback, (node as JsDocTypedefTag)?.FullName) ??
                VisitNode(nodeCallback, (node as JsDocTypedefTag)?.Name) ??
                VisitNode(nodeCallback, (node as JsDocTypedefTag)?.JsDocTypeLiteral),
            SyntaxKind.JsDocTypeLiteral => visitNodes((node as JsDocTypeLiteral)?.JsDocPropertyTags),
            SyntaxKind.JsDocPropertyTag => VisitNode(nodeCallback, (node as JsDocPropertyTag)?.TypeExpression) ??
                VisitNode(nodeCallback, (node as JsDocPropertyTag)?.Name),
            SyntaxKind.PartiallyEmittedExpression => VisitNode(nodeCallback, (node as PartiallyEmittedExpression)?.Expression),
            SyntaxKind.JsDocLiteralType => VisitNode(nodeCallback, (node as JsDocLiteralType)?.Literal),
            _ => null,
        };
    }
}