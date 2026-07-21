import ts from "typescript";
import { parse } from "webidl2";
import {
  WebIdlArgumentModel,
  WebIdlDeclarationModel,
  WebIdlMemberModel,
  WebIdlSymbolModel,
  WebIdlTypeModel,
} from "./schema.js";
import { extractWebIdlModel } from "./webidl-model.js";

export interface SupplementalTypeScriptInput {
  path: string;
  label: string;
  text: string;
  sha256: string;
  supplemental: true;
}

export interface SupplementalSourceProvenance {
  family: string;
  sourceKind: "webref-idl-generated" | "package-declaration";
  package: string;
  version: string;
  license: string;
  sourceUrl: string;
  inputs: Array<{ name: string; sha256: string }>;
  output: { name: string; sha256: string };
  generationMethod: string;
}

export interface SupplementalBuildResult {
  inputs: SupplementalTypeScriptInput[];
  provenance: SupplementalSourceProvenance[];
}

export interface WebIdlSupplementalSource {
  family: string;
  specification: string;
  sourceUrl: string;
  text: string;
  sha256: string;
}

export function generateWebIdlSupplement(
  source: WebIdlSupplementalSource,
  webrefVersion: string,
  outputPath: string,
  outputSha256: (text: string) => string,
): { input: SupplementalTypeScriptInput; provenance: SupplementalSourceProvenance } {
  const model = extractWebIdlModel([{
    name: source.specification,
    text: source.text,
  }]);
  const text = renderSpecification(
    model.symbols,
    source.specification,
    source.sourceUrl,
    source.sha256,
  );
  const hash = outputSha256(text);
  const label = `supplemental/webref/${source.specification}.d.ts`;
  return {
    input: {
      path: outputPath,
      label,
      text,
      sha256: hash,
      supplemental: true,
    },
    provenance: {
      family: source.family,
      sourceKind: "webref-idl-generated",
      package: "@webref/idl",
      version: webrefVersion,
      license: "MIT",
      sourceUrl: source.sourceUrl,
      inputs: [{ name: `${source.specification}.idl`, sha256: source.sha256 }],
      output: { name: label, sha256: hash },
      generationMethod:
        "Strict deterministic WebIDL-to-TypeScript conversion; unsupported definitions, members, and type forms fail generation.",
    },
  };
}

export function filterWebGpuWindowSource(
  text: string,
  namespaceNames: ReadonlySet<string>,
  constructibleNames: ReadonlySet<string> = new Set(),
): string {
  const source = ts.createSourceFile(
    "webgpu.d.ts",
    text,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TS,
  );
  const workerDeclarations = source.statements.filter((statement) =>
    ts.isInterfaceDeclaration(statement) &&
    statement.name.text === "WorkerNavigator"
  );
  if (workerDeclarations.length !== 1) {
    throw new Error(
      `Expected exactly one WorkerNavigator declaration in @webgpu/types, found ${workerDeclarations.length}.`,
    );
  }
  const namespaceInterfaces = new Map<string, ts.InterfaceDeclaration>();
  const namespaceVariables = new Map<string, ts.VariableStatement>();
  for (const statement of source.statements) {
    if (
      ts.isInterfaceDeclaration(statement) &&
      namespaceNames.has(statement.name.text)
    ) {
      namespaceInterfaces.set(statement.name.text, statement);
    }
    if (ts.isVariableStatement(statement)) {
      for (const declaration of statement.declarationList.declarations) {
        if (
          ts.isIdentifier(declaration.name) &&
          namespaceNames.has(declaration.name.text)
        ) {
          if (statement.declarationList.declarations.length !== 1) {
            throw new Error(
              `@webgpu/types namespace value '${declaration.name.text}' shares a variable statement.`,
            );
          }
          namespaceVariables.set(declaration.name.text, statement);
        }
      }
    }
  }
  for (const name of namespaceNames) {
    if (!namespaceInterfaces.has(name) || !namespaceVariables.has(name)) {
      throw new Error(
        `@webgpu/types does not contain the expected interface/value namespace pair '${name}'.`,
      );
    }
  }

  const removed = new Set<ts.Statement>([
    workerDeclarations[0]!,
    ...namespaceVariables.values(),
  ]);
  const replacements = new Map<ts.Statement, string>();
  for (const [name, declaration] of namespaceInterfaces) {
    replacements.set(declaration, renderWebGpuNamespace(name, declaration, source));
  }

  for (const statement of source.statements) {
    if (
      ts.isInterfaceDeclaration(statement) &&
      statement.heritageClauses?.some((clause) =>
        clause.types.some((type) => type.expression.getText(source) === "Required")
      )
    ) {
      replacements.set(
        statement,
        flattenRequiredHeritage(statement, source),
      );
    }
    if (ts.isVariableStatement(statement)) {
      const replacement = renderWebGpuInterfaceObject(
        statement,
        source,
        constructibleNames,
      );
      if (replacement !== null) {
        replacements.set(statement, replacement);
      }
    }
  }

  const output = source.statements
    .filter((statement) => !removed.has(statement))
    .map((statement) =>
      replacements.get(statement) ?? statement.getFullText(source)
    )
    .join("");
  return normalizeLf(`${output.trimEnd()}\n`);
}

export function webIdlWindowConstructors(text: string): ReadonlySet<string> {
  const names = new Set<string>();
  for (const raw of parse(text)) {
    if (
      typeof raw !== "object" ||
      raw === null ||
      !("toJSON" in raw) ||
      typeof raw.toJSON !== "function"
    ) {
      throw new Error("Malformed WebGPU Web IDL definition.");
    }
    const value: unknown = raw.toJSON();
    if (
      typeof value !== "object" ||
      value === null ||
      !("type" in value) ||
      value.type !== "interface" ||
      !("name" in value) ||
      typeof value.name !== "string" ||
      !("members" in value) ||
      !Array.isArray(value.members)
    ) {
      continue;
    }
    if (value.members.some((member: unknown) =>
      typeof member === "object" &&
      member !== null &&
      "type" in member &&
      member.type === "constructor"
    )) {
      names.add(value.name);
    }
  }
  return names;
}

function renderWebGpuInterfaceObject(
  statement: ts.VariableStatement,
  source: ts.SourceFile,
  constructibleNames: ReadonlySet<string>,
): string | null {
  if (statement.declarationList.declarations.length !== 1) {
    return null;
  }
  const declaration = statement.declarationList.declarations[0]!;
  if (
    !ts.isIdentifier(declaration.name) ||
    declaration.type === undefined ||
    !ts.isTypeLiteralNode(declaration.type)
  ) {
    return null;
  }
  const constructs = declaration.type.members.filter(
    ts.isConstructSignatureDeclaration,
  );
  if (constructs.length === 0 || constructibleNames.has(declaration.name.text)) {
    return null;
  }
  for (const construct of constructs) {
    if (construct.type?.kind !== ts.SyntaxKind.NeverKeyword) {
      throw new Error(
        `@webgpu/types exposes non-WebIDL constructor '${declaration.name.text}' with return type '${construct.type?.getText(source) ?? "(missing)"}'.`,
      );
    }
  }
  const members = declaration.type.members
    .filter((member) => !ts.isConstructSignatureDeclaration(member))
    .map((member) =>
      member.getFullText(source).trim().split("\n")
        .map((line) => `  ${line}`)
        .join("\n")
    );
  return [
    `\ndeclare var ${declaration.name.text}: {`,
    ...members,
    "};",
  ].join("\n");
}

export function webIdlWindowNamespaces(text: string): ReadonlySet<string> {
  const names = new Set<string>();
  for (const raw of parse(text)) {
    if (
      typeof raw !== "object" ||
      raw === null ||
      !("toJSON" in raw) ||
      typeof raw.toJSON !== "function"
    ) {
      throw new Error("Malformed WebGPU Web IDL definition.");
    }
    const value: unknown = raw.toJSON();
    if (
      typeof value !== "object" ||
      value === null ||
      !("type" in value) ||
      value.type !== "namespace"
    ) {
      continue;
    }
    if (
      !("name" in value) ||
      typeof value.name !== "string" ||
      !("extAttrs" in value) ||
      !Array.isArray(value.extAttrs)
    ) {
      throw new Error("Malformed WebGPU namespace definition.");
    }
    const exposed = value.extAttrs.find((attribute: unknown) =>
      typeof attribute === "object" &&
      attribute !== null &&
      "name" in attribute &&
      attribute.name === "Exposed"
    );
    if (
      typeof exposed !== "object" ||
      exposed === null ||
      !("rhs" in exposed) ||
      typeof exposed.rhs !== "object" ||
      exposed.rhs === null ||
      !("value" in exposed.rhs)
    ) {
      throw new Error(`WebGPU namespace '${value.name}' has no Exposed value.`);
    }
    const exposureValue = exposed.rhs.value;
    const exposures = Array.isArray(exposureValue)
      ? exposureValue.map((item: unknown) =>
        typeof item === "object" && item !== null && "value" in item
          ? item.value
          : item
      )
      : [exposureValue];
    if (exposures.includes("Window") || exposures.includes("*")) {
      names.add(value.name);
    }
  }
  return names;
}

function renderWebGpuNamespace(
  name: string,
  declaration: ts.InterfaceDeclaration,
  source: ts.SourceFile,
): string {
  const members = declaration.members.map((member) => {
    if (
      !ts.isPropertySignature(member) ||
      member.type === undefined ||
      member.questionToken !== undefined ||
      member.modifiers?.some((modifier) =>
        modifier.kind !== ts.SyntaxKind.ReadonlyKeyword
      )
    ) {
      throw new Error(
        `@webgpu/types namespace interface '${name}' has a non-constant member.`,
      );
    }
    return `  const ${member.name.getText(source)}: ${member.type.getText(source)};`;
  });
  return [
    `\ndeclare namespace ${name} {`,
    ...members,
    "}",
  ].join("\n");
}

function flattenRequiredHeritage(
  declaration: ts.InterfaceDeclaration,
  source: ts.SourceFile,
): string {
  const requiredClause = declaration.heritageClauses?.flatMap((clause) =>
    [...clause.types]
  ).find((type) => type.expression.getText(source) === "Required");
  if (
    requiredClause === undefined ||
    requiredClause.typeArguments?.length !== 1
  ) {
    throw new Error(
      `Mapped heritage on '${declaration.name.text}' is not Required<T>.`,
    );
  }
  const inherited = resolveRequiredMembers(
    requiredClause.typeArguments[0]!,
    source,
  );
  const ownNames = new Set(declaration.members.map((member) =>
    member.name?.getText(source).replace(/^["']|["']$/g, "")
  ));
  const own = declaration.members.map((member) => member.getText(source));
  return [
    `\ninterface ${declaration.name.text} {`,
    ...[
      ...inherited.filter((member) => !ownNames.has(propertyNameFromText(member))),
      ...own,
    ].map((member) =>
      member.split("\n").map((line) => `  ${line}`).join("\n")
    ),
    "}",
  ].join("\n");
}

function resolveRequiredMembers(
  type: ts.TypeNode,
  source: ts.SourceFile,
): string[] {
  if (!ts.isTypeReferenceNode(type) || !ts.isIdentifier(type.typeName)) {
    throw new Error("Required<T> target is not a supported type reference.");
  }
  if (type.typeName.text === "Omit") {
    if (type.typeArguments?.length !== 2) {
      throw new Error("Omit<T, K> in Required heritage must have two arguments.");
    }
    const excluded = finiteStringLiterals(type.typeArguments[1]!);
    return resolveInterfaceMembers(type.typeArguments[0]!, source)
      .filter(({ name }) => !excluded.has(name))
      .map(({ text }) => requireProperty(text));
  }
  return resolveInterfaceMembers(type, source)
    .map(({ text }) => requireProperty(text));
}

function resolveInterfaceMembers(
  type: ts.TypeNode,
  source: ts.SourceFile,
): Array<{ name: string; text: string }> {
  if (!ts.isTypeReferenceNode(type) || !ts.isIdentifier(type.typeName)) {
    throw new Error("Mapped heritage target is not a named interface.");
  }
  const targetName = type.typeName.text;
  const declarations = source.statements.filter(
    (statement): statement is ts.InterfaceDeclaration =>
    ts.isInterfaceDeclaration(statement) &&
    statement.name.text === targetName,
  );
  if (declarations.length !== 1) {
    throw new Error(
      `Mapped heritage target '${targetName}' must have exactly one interface declaration.`,
    );
  }
  return declarations[0]!.members.map((member) => {
    if (!ts.isPropertySignature(member) || member.type === undefined) {
      throw new Error(
        `Mapped heritage target '${targetName}' contains a non-property member.`,
      );
    }
    return {
      name: member.name.getText(source).replace(/^["']|["']$/g, ""),
      text: member.getText(source),
    };
  });
}

function requireProperty(text: string): string {
  return text.replace(/^(\s*(?:readonly\s+)?(?:[A-Za-z_$][A-Za-z0-9_$]*|["'][^"']+["']))\?(\s*:)/, "$1$2");
}

function propertyNameFromText(text: string): string {
  const match = /^(?:readonly\s+)?([A-Za-z_$][A-Za-z0-9_$]*|["'][^"']+["'])\??\s*:/.exec(
    text.trim(),
  );
  if (match?.[1] === undefined) {
    throw new Error(`Cannot read property name from '${text}'.`);
  }
  return match[1].replace(/^["']|["']$/g, "");
}

function finiteStringLiterals(type: ts.TypeNode): ReadonlySet<string> {
  const nodes = ts.isUnionTypeNode(type) ? [...type.types] : [type];
  const values = nodes.map((node) => {
    if (!ts.isLiteralTypeNode(node) || !ts.isStringLiteral(node.literal)) {
      throw new Error("Omit<T, K> key domain must contain only string literals.");
    }
    return node.literal.text;
  });
  return new Set(values);
}

function renderSpecification(
  symbols: readonly WebIdlSymbolModel[],
  specification: string,
  sourceUrl: string,
  sourceHash: string,
): string {
  const lines = [
    `/**`,
    ` * Generated from @webref/idl ${specification}.idl.`,
    ` * Source: ${sourceUrl}`,
    ` * SHA-256: ${sourceHash}`,
    ` */`,
    "",
  ];

  for (const symbol of symbols) {
    for (const declaration of symbol.declarations.filter((item) =>
      item.specification === specification && includeDeclaration(symbol, item)
    )) {
      lines.push(...renderDeclaration(symbol, declaration, sourceUrl), "");
    }
    for (const include of symbol.includeStatements.filter((item) =>
      item.specification === specification && symbol.name !== "WorkerNavigator"
    )) {
      lines.push(
        provenanceDoc(sourceUrl),
        `interface ${identifier(symbol.name)} extends ${identifier(include.mixin)} {}`,
        "",
      );
    }
  }

  return normalizeLf(`${lines.join("\n").trimEnd()}\n`);
}

function includeDeclaration(
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
): boolean {
  if (symbol.name === "WorkerNavigator") {
    return false;
  }
  return declaration.exposures.length === 0 ||
    declaration.exposures.includes("Window") ||
    declaration.exposures.includes("*");
}

function renderDeclaration(
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
  sourceUrl: string,
): string[] {
  const doc = provenanceDoc(sourceUrl);
  switch (declaration.kind) {
    case "dictionary":
      return [
        doc,
        renderDictionary(symbol.name, declaration),
      ];
    case "enum":
      return [
        doc,
        `type ${identifier(symbol.name)} = ${declaration.enumValues.map(quote).join(" | ")};`,
      ];
    case "typedef":
      if (declaration.type === null) {
        throw unsupported(symbol, declaration, "typedef without a type");
      }
      return [
        doc,
        `type ${identifier(symbol.name)} = ${renderType(declaration.type, symbol.name)};`,
      ];
    case "callback":
      if (declaration.type === null) {
        throw unsupported(symbol, declaration, "callback without a return type");
      }
      return [
        doc,
        `type ${identifier(symbol.name)} = (${renderArguments(declaration.arguments, symbol.name)}) => ` +
        `${renderType(declaration.type, symbol.name)};`,
      ];
    case "interface":
    case "interface mixin":
      return renderInterface(symbol, declaration, doc);
    case "namespace":
      return [doc, renderNamespace(symbol, declaration)];
    default:
      throw unsupported(
        symbol,
        declaration,
        `definition kind '${declaration.kind}'`,
      );
  }
}

function renderDictionary(
  name: string,
  declaration: WebIdlDeclarationModel,
): string {
  const heritage = declaration.inheritance === null
    ? ""
    : ` extends ${identifier(declaration.inheritance)}`;
  const members = declaration.members.map((member) => {
    if (member.kind !== "field" || member.name === null || member.types.length !== 1) {
      throw new Error(`Unsupported dictionary member in '${name}'.`);
    }
    return `  ${propertyName(member.name)}${member.required ? "" : "?"}: ` +
      `${renderType(member.types[0]!, name)};`;
  });
  return [`interface ${identifier(name)}${heritage} {`, ...members, "}"].join("\n");
}

function renderInterface(
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
  doc: string,
): string[] {
  const heritage = declaration.inheritance === null
    ? ""
    : ` extends ${identifier(declaration.inheritance)}`;
  const instanceMembers = declaration.members.filter((member) =>
    member.kind !== "constructor" && member.special !== "static"
  );
  const body = instanceMembers.flatMap((member) =>
    renderInterfaceMember(member, symbol.name)
  );

  const result = [
    doc,
    [`interface ${identifier(symbol.name)}${heritage} {`, ...body, "}"].join("\n"),
  ];

  if (declaration.kind !== "interface mixin" && !declaration.partial) {
    result.push("", renderInterfaceObject(symbol, declaration));
  }
  return result;
}

function renderInterfaceMember(
  member: WebIdlMemberModel,
  owner: string,
): string[] {
  switch (member.kind) {
    case "attribute":
      if (member.name === null || member.types.length !== 1) {
        throw new Error(`Malformed attribute in '${owner}'.`);
      }
      return [
        `  ${member.readonly ? "readonly " : ""}${propertyName(member.name)}: ` +
        `${renderType(member.types[0]!, owner)};`,
      ];
    case "operation":
      if (member.name === null || member.types.length !== 1) {
        throw new Error(`Malformed operation in '${owner}'.`);
      }
      return [
        `  ${propertyName(member.name)}(${renderArguments(member.arguments, owner)}): ` +
        `${renderType(member.types[0]!, owner)};`,
      ];
    case "maplike":
      return renderMaplike(member, owner);
    case "iterable":
      return renderIterable(member, owner);
    default:
      throw new Error(`Unsupported Web IDL member '${member.kind}' in '${owner}'.`);
  }
}

function renderMaplike(member: WebIdlMemberModel, owner: string): string[] {
  if (member.types.length !== 2) {
    throw new Error(`Maplike '${owner}' must have key and value types.`);
  }
  const key = renderType(member.types[0]!, owner);
  const value = renderType(member.types[1]!, owner);
  const lines = [
    `  readonly size: number;`,
    `  entries(): MapIterator<[${key}, ${value}]>;`,
    `  forEach(callbackfn: (value: ${value}, key: ${key}, parent: ${identifier(owner)}) => void, thisArg?: any): void;`,
    `  get(key: ${key}): ${value} | undefined;`,
    `  has(key: ${key}): boolean;`,
    `  keys(): MapIterator<${key}>;`,
    `  values(): MapIterator<${value}>;`,
    `  [Symbol.iterator](): MapIterator<[${key}, ${value}]>;`,
  ];
  if (!member.readonly) {
    lines.push(
      `  clear(): void;`,
      `  delete(key: ${key}): boolean;`,
      `  set(key: ${key}, value: ${value}): ${identifier(owner)};`,
    );
  }
  return lines;
}

function renderIterable(member: WebIdlMemberModel, owner: string): string[] {
  if (member.types.length === 1) {
    const value = renderType(member.types[0]!, owner);
    return [`  [Symbol.iterator](): Iterator<${value}>;`];
  }
  if (member.types.length === 2) {
    const key = renderType(member.types[0]!, owner);
    const value = renderType(member.types[1]!, owner);
    return [
      `  entries(): Iterator<[${key}, ${value}]>;`,
      `  keys(): Iterator<${key}>;`,
      `  values(): Iterator<${value}>;`,
      `  [Symbol.iterator](): Iterator<[${key}, ${value}]>;`,
    ];
  }
  throw new Error(`Iterable '${owner}' has unsupported arity ${member.types.length}.`);
}

function renderInterfaceObject(
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
): string {
  const constructors = declaration.members.filter((member) =>
    member.kind === "constructor"
  );
  const statics = declaration.members.filter((member) =>
    member.kind === "operation" && member.special === "static"
  );
  const members = [`  prototype: ${identifier(symbol.name)};`];
  if (constructors.length > 0) {
    members.push(...constructors.map((member) =>
      `  new(${renderArguments(member.arguments, symbol.name)}): ${identifier(symbol.name)};`
    ));
  }
  members.push(...statics.map((member) => {
    if (member.name === null || member.types.length !== 1) {
      throw new Error(`Malformed static operation in '${symbol.name}'.`);
    }
    return `  ${propertyName(member.name)}(${renderArguments(member.arguments, symbol.name)}): ` +
      `${renderType(member.types[0]!, symbol.name)};`;
  }));
  return [
    `declare var ${identifier(symbol.name)}: {`,
    ...members,
    "};",
  ].join("\n");
}

function renderNamespace(
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
): string {
  const members = declaration.members.flatMap((member) => {
    if (member.kind === "operation" && member.name !== null && member.types.length === 1) {
      return [
        `  function ${propertyName(member.name)}(${renderArguments(member.arguments, symbol.name)}): ` +
        `${renderType(member.types[0]!, symbol.name)};`,
      ];
    }
    if (member.kind === "attribute" && member.name !== null && member.types.length === 1) {
      return [
        `  const ${propertyName(member.name)}: ${renderType(member.types[0]!, symbol.name)};`,
      ];
    }
    throw new Error(`Unsupported namespace member '${member.kind}' in '${symbol.name}'.`);
  });
  return [
    `declare namespace ${identifier(symbol.name)} {`,
    ...members,
    "}",
  ].join("\n");
}

function renderArguments(
  args: readonly WebIdlArgumentModel[],
  owner: string,
): string {
  return args.map((argument) => {
    const optional = argument.optional ? "?" : "";
    const rest = argument.variadic ? "..." : "";
    const type = renderType(argument.type, owner);
    return `${rest}${identifier(argument.name)}${optional}: ` +
      `${argument.variadic ? `Array<${type}>` : type}`;
  }).join(", ");
}

function renderType(type: WebIdlTypeModel, owner: string): string {
  let rendered: string;
  if (type.union) {
    if (type.typeArguments.length < 2) {
      throw new Error(`Union in '${owner}' must contain at least two members.`);
    }
    rendered = type.typeArguments.map((item) => renderType(item, owner)).join(" | ");
  } else if (type.generic !== null) {
    const args = type.typeArguments.map((item) => renderType(item, owner));
    switch (type.generic) {
      case "Promise":
        if (args.length !== 1) throw new Error(`Promise in '${owner}' must have one argument.`);
        rendered = `Promise<${args[0]}>`;
        break;
      case "sequence":
        if (args.length !== 1) throw new Error(`sequence in '${owner}' must have one argument.`);
        rendered = `Array<${args[0]}>`;
        break;
      case "FrozenArray":
        if (args.length !== 1) throw new Error(`FrozenArray in '${owner}' must have one argument.`);
        rendered = `ReadonlyArray<${args[0]}>`;
        break;
      case "record":
        if (args.length !== 2) throw new Error(`record in '${owner}' must have two arguments.`);
        if (args[0] !== "string") {
          throw new Error(`record key in '${owner}' must project to string.`);
        }
        rendered = `Record<string, ${args[1]}>`;
        break;
      default:
        throw new Error(`Unsupported Web IDL generic '${type.generic}' in '${owner}'.`);
    }
  } else if (type.name !== null) {
    rendered = renderNamedType(type.name, owner);
  } else {
    throw new Error(`Web IDL type in '${owner}' has no name, union, or generic.`);
  }
  return type.nullable ? `(${rendered}) | null` : rendered;
}

function renderNamedType(name: string, owner: string): string {
  if (name === "EventHandler") {
    return `((this: ${identifier(owner)}, ev: Event) => any) | null`;
  }
  if (name === "ArrayBufferView") {
    return "ArrayBufferView<ArrayBuffer>";
  }
  if (name === "DOMString" || name === "USVString" || name === "ByteString") {
    return "string";
  }
  if (name === "undefined") return "void";
  if (name === "boolean" || name === "any" || name === "object") return name;
  if (
    /^(byte|octet|short|unsigned short|long|unsigned long|long long|unsigned long long|float|unrestricted float|double|unrestricted double)$/
      .test(name)
  ) {
    return "number";
  }
  if (!/^[A-Za-z_$][A-Za-z0-9_$]*$/.test(name)) {
    throw new Error(`Unsupported Web IDL named type '${name}' in '${owner}'.`);
  }
  return name;
}

function identifier(value: string): string {
  if (!/^[A-Za-z_$][A-Za-z0-9_$]*$/.test(value)) {
    throw new Error(`Invalid TypeScript identifier '${value}'.`);
  }
  return value;
}

function propertyName(value: string): string {
  return /^[A-Za-z_$][A-Za-z0-9_$]*$/.test(value) ? value : quote(value);
}

function quote(value: string): string {
  return JSON.stringify(value);
}

function provenanceDoc(sourceUrl: string): string {
  return `/** @see ${sourceUrl} */`;
}

function unsupported(
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
  description: string,
): Error {
  return new Error(
    `Unsupported ${description} for '${symbol.name}' in '${declaration.specification}'.`,
  );
}

function normalizeLf(text: string): string {
  return text.replace(/\r\n?/g, "\n");
}
