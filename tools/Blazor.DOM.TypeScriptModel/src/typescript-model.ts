import ts from "typescript";
import path from "node:path";
import {
  DeclarationModel,
  Documentation,
  MemberModel,
  ParameterModel,
  PropertyNameModel,
  SourceLocation,
  SymbolModel,
  TypeExpression,
  TypeParameterModel,
  TypeScriptCoverage,
} from "./schema.js";
import { compareOrdinal, increment } from "./stable-json.js";

interface MutableSymbol {
  name: string;
  symbolFlags: number;
  declarations: DeclarationModel[];
}

interface TypeScriptExtraction {
  symbols: SymbolModel[];
  coverage: TypeScriptCoverage;
}

interface TypeScriptInputFile {
  path: string;
  label: string;
  text?: string;
  supplemental?: boolean;
}

interface ExtractionSource {
  sourceFile: ts.SourceFile;
  label: string;
  ordinal: number;
  supplemental: boolean;
}

const keywordTypeKindNames = new Map<ts.SyntaxKind, string>([
  [ts.SyntaxKind.AnyKeyword, "AnyKeyword"],
  [ts.SyntaxKind.BigIntKeyword, "BigIntKeyword"],
  [ts.SyntaxKind.BooleanKeyword, "BooleanKeyword"],
  [ts.SyntaxKind.IntrinsicKeyword, "IntrinsicKeyword"],
  [ts.SyntaxKind.NeverKeyword, "NeverKeyword"],
  [ts.SyntaxKind.NumberKeyword, "NumberKeyword"],
  [ts.SyntaxKind.ObjectKeyword, "ObjectKeyword"],
  [ts.SyntaxKind.StringKeyword, "StringKeyword"],
  [ts.SyntaxKind.SymbolKeyword, "SymbolKeyword"],
  [ts.SyntaxKind.ThisType, "ThisType"],
  [ts.SyntaxKind.UndefinedKeyword, "UndefinedKeyword"],
  [ts.SyntaxKind.UnknownKeyword, "UnknownKeyword"],
  [ts.SyntaxKind.VoidKeyword, "VoidKeyword"],
]);

export function extractTypeScriptModel(
  files: readonly TypeScriptInputFile[],
): TypeScriptExtraction {
  if (files.length === 0) {
    throw new Error("At least one TypeScript declaration input is required.");
  }
  if (new Set(files.map((file) => file.label)).size !== files.length) {
    throw new Error("TypeScript declaration input labels must be unique.");
  }
  const options: ts.CompilerOptions = {
    noEmit: true,
    skipLibCheck: false,
    strict: true,
    target: ts.ScriptTarget.Latest,
    types: [],
  };
  const standardLibraryPath = path.join(
    path.dirname(ts.getDefaultLibFilePath(options)),
    "lib.esnext.d.ts",
  );
  const virtualFiles = new Map(
    files
      .filter((file) => file.text !== undefined)
      .map((file) => [path.resolve(file.path), file.text!] as const),
  );
  const host = ts.createCompilerHost(options, true);
  const defaultFileExists = host.fileExists.bind(host);
  const defaultReadFile = host.readFile.bind(host);
  const defaultGetSourceFile = host.getSourceFile.bind(host);
  host.fileExists = (fileName) =>
    virtualFiles.has(path.resolve(fileName)) || defaultFileExists(fileName);
  host.readFile = (fileName) =>
    virtualFiles.get(path.resolve(fileName)) ?? defaultReadFile(fileName);
  host.getSourceFile = (fileName, languageVersion, onError, shouldCreateNewSourceFile) => {
    const text = virtualFiles.get(path.resolve(fileName));
    return text === undefined
      ? defaultGetSourceFile(
        fileName,
        languageVersion,
        onError,
        shouldCreateNewSourceFile,
      )
      : ts.createSourceFile(fileName, text, languageVersion, true, ts.ScriptKind.TS);
  };
  const program = ts.createProgram({
    rootNames: [standardLibraryPath, ...files.map((file) => file.path)],
    options,
    host,
  });
  const sources = files.map<ExtractionSource>((file, ordinal) => {
    const sourceFile = program.getSourceFile(file.path);
    if (sourceFile === undefined) {
      throw new Error(`TypeScript did not load '${file.path}'.`);
    }
    return {
      sourceFile,
      label: file.label,
      ordinal,
      supplemental: file.supplemental ?? false,
    };
  });

  const diagnostics = ts.getPreEmitDiagnostics(program);
  if (diagnostics.length > 0) {
    throw new Error(formatDiagnostics(diagnostics));
  }

  const extractor = new TypeScriptExtractor(program.getTypeChecker(), sources);
  return extractor.extract();
}

class TypeScriptExtractor {
  readonly #symbols = new Map<string, MutableSymbol>();
  readonly #declarationKinds: Record<string, number> = {};
  readonly #memberKinds: Record<string, number> = {};
  readonly #typeExpressionKinds: Record<string, number> = {};
  readonly #sourceMetadata = new Map<
    ts.SourceFile,
    { label: string; ordinal: number; supplemental: boolean }
  >();
  readonly #sourceOrdinals = new Map<string, number>();
  #declarationCount = 0;
  #memberCount = 0;
  #typeExpressionCount = 0;

  constructor(
    readonly checker: ts.TypeChecker,
    readonly sources: readonly ExtractionSource[],
  ) {
    for (const source of sources) {
      this.#sourceMetadata.set(source.sourceFile, {
        label: source.label,
        ordinal: source.ordinal,
        supplemental: source.supplemental,
      });
      this.#sourceOrdinals.set(source.label, source.ordinal);
    }
  }

  extract(): TypeScriptExtraction {
    for (const source of this.sources) {
      this.collectStatements(source.sourceFile.statements, "");
    }

    const symbols = [...this.#symbols.values()]
      .sort((left, right) => compareOrdinal(left.name, right.name))
      .map<SymbolModel>((symbol, ordinal) => ({
        ordinal,
        name: symbol.name,
        symbolFlags: symbol.symbolFlags,
        declarations: symbol.declarations
          .sort((left, right) => {
            const sourceComparison =
              this.sourceOrdinal(left.location.source) -
              this.sourceOrdinal(right.location.source);
            return sourceComparison !== 0
              ? sourceComparison
              : left.location.start.offset - right.location.start.offset;
          })
          .map((declaration, declarationOrdinal) => ({
            ...declaration,
            ordinal: declarationOrdinal,
          })),
        isDeclarationMerged: symbol.declarations.length > 1,
        supplemental: symbol.declarations.every((declaration) =>
          declaration.supplemental
        ),
        semantic: {
          status: "unmatched",
          webIdlName: null,
          bindingKind: null,
          webIdlMemberName: null,
          classifications: [],
          specifications: [],
          exposures: [],
          exposedOnWindow: false,
          exposedOnWorker: false,
          globalNames: [],
          serializable: false,
          transferable: false,
          secureContext: false,
          extendedAttributes: [],
          bindings: [],
        },
      }));

    return {
      symbols,
      coverage: {
        symbolCount: symbols.length,
        declarationCount: this.#declarationCount,
        declarationKinds: sortRecord(this.#declarationKinds),
        memberCount: this.#memberCount,
        memberKinds: sortRecord(this.#memberKinds),
        typeExpressionCount: this.#typeExpressionCount,
        typeExpressionKinds: sortRecord(this.#typeExpressionKinds),
        mergedSymbolCount: symbols.filter((symbol) => symbol.isDeclarationMerged).length,
        eventMapCount: symbols.filter((symbol) =>
          symbol.declarations.some((declaration) => declaration.eventMap.isEventMap)
        ).length,
        constructorObjectCount: symbols.filter((symbol) =>
          symbol.declarations.some((declaration) => declaration.constructorObject)
        ).length,
      },
    };
  }

  private collectStatements(
    statements: ts.NodeArray<ts.Statement>,
    qualifier: string,
  ): void {
    for (const statement of statements) {
      if (ts.isInterfaceDeclaration(statement)) {
        this.addDeclaration(
          this.qualifiedName(qualifier, statement.name.text),
          statement.name,
          this.interfaceDeclaration(statement),
        );
      } else if (ts.isTypeAliasDeclaration(statement)) {
        this.addDeclaration(
          this.qualifiedName(qualifier, statement.name.text),
          statement.name,
          this.typeAliasDeclaration(statement),
        );
      } else if (ts.isVariableStatement(statement)) {
        for (const declaration of statement.declarationList.declarations) {
          if (!ts.isIdentifier(declaration.name)) {
            this.unsupported(declaration.name, "global variable binding pattern");
          }
          this.addDeclaration(
            this.qualifiedName(qualifier, declaration.name.text),
            declaration.name,
            this.variableDeclaration(declaration, statement),
          );
        }
      } else if (ts.isFunctionDeclaration(statement)) {
        if (statement.name === undefined) {
          this.unsupported(statement, "anonymous global function");
        }
        this.addDeclaration(
          this.qualifiedName(qualifier, statement.name.text),
          statement.name,
          this.functionDeclaration(statement),
        );
      } else if (ts.isModuleDeclaration(statement)) {
        this.moduleDeclaration(statement, qualifier);
      } else {
        this.unsupported(statement, `top-level ${ts.SyntaxKind[statement.kind]}`);
      }
    }
  }

  private moduleDeclaration(node: ts.ModuleDeclaration, qualifier: string): void {
    const localName = moduleName(node.name);
    const canonicalName = this.qualifiedName(qualifier, localName);
    const body = node.body;
    if (body === undefined) {
      this.unsupported(node, "namespace without module block");
    }
    const declaration = this.baseDeclaration("namespace", localName, node);
    if (ts.isModuleDeclaration(body)) {
      declaration.namespaceMembers = [
        this.qualifiedName(canonicalName, moduleName(body.name)),
      ];
      this.addDeclaration(canonicalName, node.name, declaration);
      this.moduleDeclaration(body, canonicalName);
      return;
    }
    if (!ts.isModuleBlock(body)) {
      this.unsupported(body, "JSDoc namespace body");
    }
    declaration.namespaceMembers = [
      ...new Set(
        body.statements
          .flatMap(statementNames)
          .map((name) => this.qualifiedName(canonicalName, name)),
      ),
    ].sort(compareOrdinal);
    this.addDeclaration(canonicalName, node.name, declaration);
    this.collectStatements(body.statements, canonicalName);
  }

  private interfaceDeclaration(node: ts.InterfaceDeclaration): DeclarationModel {
    const declaration = this.baseDeclaration("interface", node.name.text, node);
    declaration.typeParameters = this.typeParameters(node.typeParameters);
    declaration.heritage = (node.heritageClauses ?? []).map((clause) => ({
      token: clause.token === ts.SyntaxKind.ExtendsKeyword ? "extends" : "implements",
      types: clause.types.map((type) => this.typeExpression(type)),
    }));
    declaration.members = node.members.map((member, ordinal) =>
      this.member(member, ordinal)
    );
    const eventKeys = declaration.members
      .filter((member) => member.kind === "property")
      .map((member) => member.name)
      .filter((name): name is PropertyNameModel =>
        name?.kind === "string" || name?.kind === "identifier"
      )
      .map((name) => name.text)
      .filter((name, index, values) => values.indexOf(name) === index);
    const directMembersAreEventProperties = declaration.members.every((member) =>
      member.kind === "property" &&
      (member.name?.kind === "string" || member.name?.kind === "identifier")
    );
    const inheritsEventMap = declaration.heritage.some((clause) =>
      clause.types.some((type) => {
        const reference = type.expression ?? type.name;
        return typeof reference === "string" && reference.endsWith("EventMap");
      })
    );
    declaration.eventMap = {
      isEventMap: node.name.text.endsWith("EventMap") &&
        directMembersAreEventProperties &&
        (declaration.members.length > 0 || inheritsEventMap),
      keys: eventKeys,
    };
    return declaration;
  }

  private typeAliasDeclaration(node: ts.TypeAliasDeclaration): DeclarationModel {
    const declaration = this.baseDeclaration("typeAlias", node.name.text, node);
    declaration.typeParameters = this.typeParameters(node.typeParameters);
    declaration.type = this.typeExpression(node.type);
    return declaration;
  }

  private variableDeclaration(
    node: ts.VariableDeclaration,
    statement: ts.VariableStatement,
  ): DeclarationModel {
    const declaration = this.baseDeclaration("globalVariable", node.name.getText(), node);
    declaration.modifiers = this.modifiers(statement);
    declaration.variableKind = variableKind(statement.declarationList);
    declaration.type = node.type === undefined ? null : this.typeExpression(node.type);
    declaration.documentation = this.documentation(
      this.documentation(node).text.length > 0 ? node : statement,
    );
    declaration.constructorObject = this.checker.getSignaturesOfType(
      this.checker.getTypeAtLocation(node.name),
      ts.SignatureKind.Construct,
    ).length > 0;
    return declaration;
  }

  private functionDeclaration(node: ts.FunctionDeclaration): DeclarationModel {
    const declaration = this.baseDeclaration(
      "globalFunction",
      node.name?.text ?? "",
      node,
    );
    declaration.typeParameters = this.typeParameters(node.typeParameters);
    declaration.parameters = this.parameters(node.parameters);
    declaration.returnType = node.type === undefined ? null : this.typeExpression(node.type);
    return declaration;
  }

  private baseDeclaration(
    kind: string,
    name: string,
    node: ts.Node,
  ): DeclarationModel {
    this.#declarationCount++;
    increment(this.#declarationKinds, kind);
    return {
      ordinal: 0,
      supplemental: this.location(node).supplemental,
      kind,
      name,
      modifiers: this.modifiers(node),
      typeParameters: [],
      heritage: [],
      members: [],
      type: null,
      parameters: [],
      returnType: null,
      documentation: this.documentation(node),
      location: this.location(node),
      variableKind: null,
      constructorObject: false,
      eventMap: { isEventMap: false, keys: [] },
      namespaceMembers: [],
    };
  }

  private member(node: ts.TypeElement, ordinal: number): MemberModel {
    this.#memberCount++;
    const kind = memberKind(node);
    increment(this.#memberKinds, kind);

    const signature = isSignature(node) ? node : null;
    const named = hasPropertyName(node) ? node.name : undefined;
    let type: TypeExpression | null = null;
    let returnType: TypeExpression | null = null;

    if (ts.isPropertySignature(node)) {
      type = node.type === undefined ? null : this.typeExpression(node.type);
    } else if (
      ts.isMethodSignature(node) ||
      ts.isCallSignatureDeclaration(node) ||
      ts.isConstructSignatureDeclaration(node) ||
      ts.isIndexSignatureDeclaration(node) ||
      ts.isGetAccessorDeclaration(node)
    ) {
      returnType = node.type === undefined ? null : this.typeExpression(node.type);
    }

    return {
      ordinal,
      kind,
      name: named === undefined ? null : this.propertyName(named),
      optional: "questionToken" in node && node.questionToken !== undefined,
      readonly: this.hasModifier(node, ts.SyntaxKind.ReadonlyKeyword),
      static: this.hasModifier(node, ts.SyntaxKind.StaticKeyword),
      typeParameters: signature === null
        ? []
        : this.typeParameters(signature.typeParameters),
      parameters: signature === null ? [] : this.parameters(signature.parameters),
      type,
      returnType,
      documentation: this.documentation(node),
      location: this.location(node),
    };
  }

  private typeParameters(
    nodes: ts.NodeArray<ts.TypeParameterDeclaration> | undefined,
  ): TypeParameterModel[] {
    return (nodes ?? []).map((node) => ({
      name: node.name.text,
      constraint: node.constraint === undefined
        ? null
        : this.typeExpression(node.constraint),
      default: node.default === undefined ? null : this.typeExpression(node.default),
      location: this.location(node),
    }));
  }

  private parameters(nodes: ts.NodeArray<ts.ParameterDeclaration>): ParameterModel[] {
    return nodes.map((node, ordinal) => ({
      ordinal,
      name: this.text(node.name),
      optional: node.questionToken !== undefined || node.initializer !== undefined,
      rest: node.dotDotDotToken !== undefined,
      type: node.type === undefined ? null : this.typeExpression(node.type),
      initializer: node.initializer === undefined ? null : this.text(node.initializer),
      location: this.location(node),
      documentation: this.documentation(node),
    }));
  }

  private typeExpression(node: ts.TypeNode): TypeExpression {
    const syntaxKind = typeSyntaxKind(node);
    this.#typeExpressionCount++;
    increment(this.#typeExpressionKinds, syntaxKind);

    const base = {
      syntaxKind,
      checkerType: this.checker.typeToString(
        this.checker.getTypeAtLocation(node),
        node,
        ts.TypeFormatFlags.NoTruncation,
      ),
      transport: {
        kind: "unsupported" as const,
        nullable: false,
        sourceType: this.checker.typeToString(
          this.checker.getTypeAtLocation(node),
          node,
          ts.TypeFormatFlags.NoTruncation,
        ),
        streamable: false,
        structuredClone: false,
        reason: "Transport classification requires the reconciled semantic model.",
      },
    };

    if (keywordTypeKindNames.has(node.kind)) {
      return { ...base, kind: "keyword", name: syntaxKind };
    }
    if (ts.isTypeReferenceNode(node)) {
      return {
        ...base,
        kind: "reference",
        name: this.text(node.typeName),
        resolvedSymbol: this.resolvedSymbol(node.typeName),
        typeArguments: (node.typeArguments ?? []).map((type) => this.typeExpression(type)),
      };
    }
    if (ts.isUnionTypeNode(node) || ts.isIntersectionTypeNode(node)) {
      return {
        ...base,
        kind: ts.isUnionTypeNode(node) ? "union" : "intersection",
        types: node.types.map((type) => this.typeExpression(type)),
      };
    }
    if (ts.isArrayTypeNode(node)) {
      return { ...base, kind: "array", elementType: this.typeExpression(node.elementType) };
    }
    if (ts.isTupleTypeNode(node)) {
      return {
        ...base,
        kind: "tuple",
        elements: node.elements.map((element) => this.typeExpression(element)),
      };
    }
    if (ts.isNamedTupleMember(node)) {
      return {
        ...base,
        kind: "namedTupleMember",
        name: node.name.text,
        optional: node.questionToken !== undefined,
        rest: node.dotDotDotToken !== undefined,
        type: this.typeExpression(node.type),
      };
    }
    if (ts.isOptionalTypeNode(node) || ts.isRestTypeNode(node)) {
      return {
        ...base,
        kind: ts.isOptionalTypeNode(node) ? "optional" : "rest",
        type: this.typeExpression(node.type),
      };
    }
    if (ts.isParenthesizedTypeNode(node)) {
      return { ...base, kind: "parenthesized", type: this.typeExpression(node.type) };
    }
    if (ts.isFunctionTypeNode(node) || ts.isConstructorTypeNode(node)) {
      return {
        ...base,
        kind: ts.isFunctionTypeNode(node) ? "function" : "constructor",
        abstract: ts.isConstructorTypeNode(node) &&
          this.hasModifier(node, ts.SyntaxKind.AbstractKeyword),
        typeParameters: this.typeParameters(node.typeParameters),
        parameters: this.parameters(node.parameters),
        returnType: this.typeExpression(node.type),
      };
    }
    if (ts.isTypeLiteralNode(node)) {
      return {
        ...base,
        kind: "typeLiteral",
        members: node.members.map((member, ordinal) => this.member(member, ordinal)),
      };
    }
    if (ts.isTypeOperatorNode(node)) {
      return {
        ...base,
        kind: "operator",
        operator: typeOperatorName(node.operator),
        type: this.typeExpression(node.type),
      };
    }
    if (ts.isIndexedAccessTypeNode(node)) {
      return {
        ...base,
        kind: "indexedAccess",
        objectType: this.typeExpression(node.objectType),
        indexType: this.typeExpression(node.indexType),
      };
    }
    if (ts.isMappedTypeNode(node)) {
      return {
        ...base,
        kind: "mapped",
        readonlyToken: node.readonlyToken === undefined
          ? null
          : this.text(node.readonlyToken),
        typeParameter: this.typeParameters(
          ts.factory.createNodeArray([node.typeParameter]),
        )[0],
        nameType: node.nameType === undefined ? null : this.typeExpression(node.nameType),
        optionalToken: node.questionToken === undefined
          ? null
          : this.text(node.questionToken),
        valueType: node.type === undefined ? null : this.typeExpression(node.type),
        members: (node.members ?? []).map((member, ordinal) =>
          this.member(member, ordinal)
        ),
      };
    }
    if (ts.isConditionalTypeNode(node)) {
      return {
        ...base,
        kind: "conditional",
        checkType: this.typeExpression(node.checkType),
        extendsType: this.typeExpression(node.extendsType),
        trueType: this.typeExpression(node.trueType),
        falseType: this.typeExpression(node.falseType),
      };
    }
    if (ts.isInferTypeNode(node)) {
      return {
        ...base,
        kind: "infer",
        typeParameter: this.typeParameters(
          ts.factory.createNodeArray([node.typeParameter]),
        )[0],
      };
    }
    if (ts.isTypeQueryNode(node)) {
      return {
        ...base,
        kind: "query",
        expressionName: this.text(node.exprName),
        resolvedSymbol: this.resolvedSymbol(node.exprName),
        typeArguments: (node.typeArguments ?? []).map((type) => this.typeExpression(type)),
      };
    }
    if (ts.isTypePredicateNode(node)) {
      return {
        ...base,
        kind: "predicate",
        asserts: node.assertsModifier !== undefined,
        parameterName: this.text(node.parameterName),
        type: node.type === undefined ? null : this.typeExpression(node.type),
      };
    }
    if (ts.isLiteralTypeNode(node)) {
      return {
        ...base,
        kind: "literal",
        literalKind: literalKindName(node.literal),
        text: this.text(node.literal),
      };
    }
    if (ts.isTemplateLiteralTypeNode(node)) {
      return {
        ...base,
        kind: "templateLiteral",
        head: node.head.text,
        spans: node.templateSpans.map((span) => ({
          type: this.typeExpression(span.type),
          literal: span.literal.text,
        })),
      };
    }
    if (ts.isImportTypeNode(node)) {
      return {
        ...base,
        kind: "import",
        argument: this.typeExpression(node.argument),
        qualifier: node.qualifier === undefined ? null : this.text(node.qualifier),
        typeArguments: (node.typeArguments ?? []).map((type) => this.typeExpression(type)),
        isTypeOf: node.isTypeOf,
        attributes: node.attributes === undefined ? null : this.text(node.attributes),
      };
    }
    if (ts.isExpressionWithTypeArguments(node)) {
      return {
        ...base,
        kind: "heritageReference",
        expression: this.text(node.expression),
        resolvedSymbol: this.resolvedSymbol(node.expression),
        typeArguments: (node.typeArguments ?? []).map((type) => this.typeExpression(type)),
      };
    }

    this.unsupported(node, `type expression ${syntaxKind}`);
  }

  private propertyName(node: ts.PropertyName): PropertyNameModel {
    if (ts.isIdentifier(node)) {
      return { kind: "identifier", text: node.text };
    }
    if (ts.isStringLiteral(node)) {
      return { kind: "string", text: node.text };
    }
    if (ts.isNumericLiteral(node)) {
      return { kind: "number", text: node.text };
    }
    if (ts.isComputedPropertyName(node)) {
      return { kind: "computed", text: this.text(node.expression) };
    }
    if (ts.isPrivateIdentifier(node)) {
      return { kind: "private", text: node.text };
    }
    this.unsupported(node, `property name ${ts.SyntaxKind[node.kind]}`);
  }

  private documentation(node: ts.Node): Documentation {
    const tags = ts.getJSDocTags(node).map((tag) => ({
      name: tag.tagName.text,
      text: ts.getTextOfJSDocComment(tag.comment) ?? "",
      raw: this.text(tag),
    }));
    const comments = ts.getJSDocCommentsAndTags(node)
      .filter(ts.isJSDoc)
      .map((doc) => ts.getTextOfJSDocComment(doc.comment) ?? "")
      .filter((text) => text.length > 0);
    return {
      text: comments.join("\n\n"),
      tags,
      deprecated: tags.some((tag) => tag.name === "deprecated"),
    };
  }

  private modifiers(node: ts.Node): string[] {
    if (!ts.canHaveModifiers(node)) {
      return [];
    }
    return (ts.getModifiers(node) ?? []).map((modifier) =>
      modifierKindName(modifier.kind)
    );
  }

  private location(node: ts.Node): SourceLocation {
    const sourceFile = node.getSourceFile();
    const metadata = this.#sourceMetadata.get(sourceFile);
    if (metadata === undefined) {
      throw new Error(`Unexpected TypeScript source '${sourceFile.fileName}'.`);
    }
    const startOffset = node.getStart(sourceFile, false);
    const endOffset = node.getEnd();
    const start = sourceFile.getLineAndCharacterOfPosition(startOffset);
    const end = sourceFile.getLineAndCharacterOfPosition(endOffset);
    return {
      source: metadata.label,
      sourceOrdinal: metadata.ordinal,
      supplemental: metadata.supplemental,
      start: {
        line: start.line + 1,
        column: start.character + 1,
        offset: startOffset,
      },
      end: {
        line: end.line + 1,
        column: end.character + 1,
        offset: endOffset,
      },
    };
  }

  private resolvedSymbol(node: ts.Node): string | null {
    let symbol = this.checker.getSymbolAtLocation(node);
    if (symbol === undefined) {
      return null;
    }

    if ((symbol.flags & ts.SymbolFlags.Alias) !== 0) {
      symbol = this.checker.getAliasedSymbol(symbol);
    }
    return this.checker.getFullyQualifiedName(symbol).replace(/^".*"\./, "");
  }

  private text(node: ts.Node): string {
    return node.getText(node.getSourceFile());
  }

  private hasModifier(node: ts.Node, kind: ts.SyntaxKind): boolean {
    if (!ts.canHaveModifiers(node)) {
      return false;
    }
    return (ts.getModifiers(node) ?? []).some((modifier) => modifier.kind === kind);
  }

  private sourceOrdinal(label: string): number {
    const ordinal = this.#sourceOrdinals.get(label);
    if (ordinal === undefined) {
      throw new Error(`Unknown TypeScript source label '${label}'.`);
    }
    return ordinal;
  }

  private addDeclaration(
    canonicalName: string,
    nameNode: ts.Node,
    declaration: DeclarationModel,
  ): void {
    const compilerSymbol = this.checker.getSymbolAtLocation(nameNode);
    const existing = this.#symbols.get(canonicalName);
    if (existing === undefined) {
      this.#symbols.set(canonicalName, {
        name: canonicalName,
        symbolFlags: compilerSymbol?.flags ?? 0,
        declarations: [declaration],
      });
    } else {
      existing.symbolFlags |= compilerSymbol?.flags ?? 0;
      existing.declarations.push(declaration);
    }
  }

  private qualifiedName(qualifier: string, name: string): string {
    return qualifier.length === 0 ? name : `${qualifier}.${name}`;
  }

  private unsupported(node: ts.Node, description: string): never {
    const location = this.location(node);
    throw new Error(
      `Unsupported TypeScript ${description} at ${location.source}:` +
      `${location.start.line}:${location.start.column}.`,
    );
  }
}

function memberKind(node: ts.TypeElement): string {
  if (ts.isPropertySignature(node)) return "property";
  if (ts.isMethodSignature(node)) return "method";
  if (ts.isCallSignatureDeclaration(node)) return "callSignature";
  if (ts.isConstructSignatureDeclaration(node)) return "constructSignature";
  if (ts.isIndexSignatureDeclaration(node)) return "indexSignature";
  if (ts.isGetAccessorDeclaration(node)) return "getter";
  if (ts.isSetAccessorDeclaration(node)) return "setter";
  throw new Error(`Unsupported TypeScript member ${ts.SyntaxKind[node.kind]}.`);
}

function variableKind(
  declarationList: ts.VariableDeclarationList,
): "var" | "let" | "const" {
  if ((declarationList.flags & ts.NodeFlags.Const) !== 0) {
    return "const";
  }
  if ((declarationList.flags & ts.NodeFlags.Let) !== 0) {
    return "let";
  }
  return "var";
}

function typeSyntaxKind(node: ts.TypeNode): string {
  const keyword = keywordTypeKindNames.get(node.kind);
  if (keyword !== undefined) return keyword;
  if (ts.isTypeReferenceNode(node)) return "TypeReference";
  if (ts.isUnionTypeNode(node)) return "UnionType";
  if (ts.isIntersectionTypeNode(node)) return "IntersectionType";
  if (ts.isArrayTypeNode(node)) return "ArrayType";
  if (ts.isTupleTypeNode(node)) return "TupleType";
  if (ts.isNamedTupleMember(node)) return "NamedTupleMember";
  if (ts.isOptionalTypeNode(node)) return "OptionalType";
  if (ts.isRestTypeNode(node)) return "RestType";
  if (ts.isParenthesizedTypeNode(node)) return "ParenthesizedType";
  if (ts.isFunctionTypeNode(node)) return "FunctionType";
  if (ts.isConstructorTypeNode(node)) return "ConstructorType";
  if (ts.isTypeLiteralNode(node)) return "TypeLiteral";
  if (ts.isTypeOperatorNode(node)) return "TypeOperator";
  if (ts.isIndexedAccessTypeNode(node)) return "IndexedAccessType";
  if (ts.isMappedTypeNode(node)) return "MappedType";
  if (ts.isConditionalTypeNode(node)) return "ConditionalType";
  if (ts.isInferTypeNode(node)) return "InferType";
  if (ts.isTypeQueryNode(node)) return "TypeQuery";
  if (ts.isTypePredicateNode(node)) return "TypePredicate";
  if (ts.isLiteralTypeNode(node)) return "LiteralType";
  if (ts.isTemplateLiteralTypeNode(node)) return "TemplateLiteralType";
  if (ts.isImportTypeNode(node)) return "ImportType";
  if (ts.isExpressionWithTypeArguments(node)) return "ExpressionWithTypeArguments";
  return `SyntaxKind(${node.kind})`;
}

function literalKindName(node: ts.LiteralTypeNode["literal"]): string {
  if (ts.isNumericLiteral(node)) return "NumericLiteral";
  if (ts.isBigIntLiteral(node)) return "BigIntLiteral";
  if (ts.isStringLiteral(node)) return "StringLiteral";
  if (ts.isNoSubstitutionTemplateLiteral(node)) {
    return "NoSubstitutionTemplateLiteral";
  }
  if (ts.isPrefixUnaryExpression(node)) return "PrefixUnaryExpression";
  switch (node.kind) {
    case ts.SyntaxKind.TrueKeyword:
      return "TrueKeyword";
    case ts.SyntaxKind.FalseKeyword:
      return "FalseKeyword";
    case ts.SyntaxKind.NullKeyword:
      return "NullKeyword";
    default:
      throw new Error(`Unsupported TypeScript literal kind ${node.kind}.`);
  }
}

function typeOperatorName(operator: ts.TypeOperatorNode["operator"]): string {
  switch (operator) {
    case ts.SyntaxKind.KeyOfKeyword:
      return "KeyOfKeyword";
    case ts.SyntaxKind.UniqueKeyword:
      return "UniqueKeyword";
    case ts.SyntaxKind.ReadonlyKeyword:
      return "ReadonlyKeyword";
  }
}

function modifierKindName(kind: ts.Modifier["kind"]): string {
  switch (kind) {
    case ts.SyntaxKind.AbstractKeyword:
      return "AbstractKeyword";
    case ts.SyntaxKind.AccessorKeyword:
      return "AccessorKeyword";
    case ts.SyntaxKind.AsyncKeyword:
      return "AsyncKeyword";
    case ts.SyntaxKind.ConstKeyword:
      return "ConstKeyword";
    case ts.SyntaxKind.DeclareKeyword:
      return "DeclareKeyword";
    case ts.SyntaxKind.DefaultKeyword:
      return "DefaultKeyword";
    case ts.SyntaxKind.ExportKeyword:
      return "ExportKeyword";
    case ts.SyntaxKind.InKeyword:
      return "InKeyword";
    case ts.SyntaxKind.OutKeyword:
      return "OutKeyword";
    case ts.SyntaxKind.OverrideKeyword:
      return "OverrideKeyword";
    case ts.SyntaxKind.PrivateKeyword:
      return "PrivateKeyword";
    case ts.SyntaxKind.ProtectedKeyword:
      return "ProtectedKeyword";
    case ts.SyntaxKind.PublicKeyword:
      return "PublicKeyword";
    case ts.SyntaxKind.ReadonlyKeyword:
      return "ReadonlyKeyword";
    case ts.SyntaxKind.StaticKeyword:
      return "StaticKeyword";
    default:
      throw new Error(`Unsupported TypeScript modifier kind ${kind}.`);
  }
}

type TypeElementSignature =
  | ts.MethodSignature
  | ts.CallSignatureDeclaration
  | ts.ConstructSignatureDeclaration
  | ts.IndexSignatureDeclaration
  | ts.GetAccessorDeclaration
  | ts.SetAccessorDeclaration;

function isSignature(node: ts.TypeElement): node is TypeElementSignature {
  return ts.isMethodSignature(node) ||
    ts.isCallSignatureDeclaration(node) ||
    ts.isConstructSignatureDeclaration(node) ||
    ts.isIndexSignatureDeclaration(node) ||
    ts.isGetAccessorDeclaration(node) ||
    ts.isSetAccessorDeclaration(node);
}

function hasPropertyName(
  node: ts.TypeElement,
): node is ts.TypeElement & { name: ts.PropertyName } {
  return "name" in node && node.name !== undefined;
}

function moduleName(name: ts.ModuleName): string {
  return name.text;
}

function statementNames(statement: ts.Statement): string[] {
  if (
    ts.isInterfaceDeclaration(statement) ||
    ts.isTypeAliasDeclaration(statement) ||
    ts.isFunctionDeclaration(statement) ||
    ts.isModuleDeclaration(statement)
  ) {
    return statement.name === undefined ? [] : [moduleName(statement.name)];
  }
  if (ts.isVariableStatement(statement)) {
    return statement.declarationList.declarations
      .map((declaration) => declaration.name.getText(declaration.getSourceFile()));
  }
  return [];
}

function sortRecord(record: Record<string, number>): Record<string, number> {
  return Object.fromEntries(
    Object.entries(record).sort(([left], [right]) => compareOrdinal(left, right)),
  );
}

function formatDiagnostics(diagnostics: readonly ts.Diagnostic[]): string {
  return diagnostics.map((diagnostic) => {
    const text = ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n");
    if (diagnostic.file === undefined || diagnostic.start === undefined) {
      return text;
    }
    const location = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start);
    return `${diagnostic.file.fileName}:${location.line + 1}:${location.character + 1}: ${text}`;
  }).join("\n");
}
