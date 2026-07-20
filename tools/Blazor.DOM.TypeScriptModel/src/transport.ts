import {
  MemberModel,
  SymbolModel,
  TransportKind,
  TransportModel,
  TypeExpression,
} from "./schema.js";

const arrayBufferViewTypes = new Set([
  "ArrayBufferView",
  "DataView",
  "Int8Array",
  "Uint8Array",
  "Uint8ClampedArray",
  "Int16Array",
  "Uint16Array",
  "Int32Array",
  "Uint32Array",
  "Float16Array",
  "Float32Array",
  "Float64Array",
  "BigInt64Array",
  "BigUint64Array",
]);
const binaryTypes = new Set([
  "ArrayBuffer",
  "ArrayBufferLike",
  "SharedArrayBuffer",
  ...arrayBufferViewTypes,
]);

const promiseTypes = new Set(["Promise", "PromiseLike"]);
const arrayTypes = new Set(["Array", "ReadonlyArray"]);
const dynamicReferenceTypes = new Set(["Object", "object"]);

export function classifyTransports(symbols: SymbolModel[]): void {
  const symbolsByName = new Map(symbols.map((symbol) => [symbol.name, symbol]));

  for (const symbol of symbols) {
    visit(symbol.declarations, (expression) => {
      expression.transport = classify(
        expression,
        symbolsByName,
        new Set<string>(),
      );
    });
  }
}

function visit(value: unknown, classifyExpression: (value: TypeExpression) => void): void {
  if (Array.isArray(value)) {
    value.forEach((item) => visit(item, classifyExpression));
    return;
  }
  if (!isRecord(value)) {
    return;
  }

  for (const [key, child] of Object.entries(value)) {
    if (key !== "transport") {
      visit(child, classifyExpression);
    }
  }
  if (isTypeExpression(value)) {
    classifyExpression(value);
  }
}

function classify(
  expression: TypeExpression,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  switch (expression.kind) {
    case "keyword":
      return classifyKeyword(expression);
    case "literal":
      return classifyLiteral(expression);
    case "templateLiteral":
      return supported("json-value", expression);
    case "reference":
    case "heritageReference":
      return classifyReference(expression, symbols, resolving);
    case "union":
      return classifyUnion(expression, symbols, resolving);
    case "intersection":
      return classifyComposite(expression, symbols, resolving);
    case "array":
      return classifyJsonContainer(
        expression,
        child(expression, "elementType"),
        symbols,
        resolving,
      );
    case "tuple":
      return classifyJsonElements(
        expression,
        children(expression, "elements"),
        symbols,
        resolving,
      );
    case "namedTupleMember":
    case "optional":
    case "parenthesized":
    case "rest":
    case "operator":
      return classifyWrapped(expression, symbols, resolving);
    case "typeLiteral":
      return classifyTypeLiteral(expression, symbols, resolving);
    default:
      return unsupported(
        expression,
        `TypeScript '${expression.checkerType}' has an ambiguous ${expression.kind} shape.`,
      );
  }
}

function classifyKeyword(expression: TypeExpression): TransportModel {
  const name = text(expression, "name");
  switch (name) {
    case "StringKeyword":
    case "NumberKeyword":
    case "BooleanKeyword":
    case "VoidKeyword":
      return supported("json-value", expression);
    case "NullKeyword":
    case "UndefinedKeyword":
      return supported("json-value", expression, true);
    case "AnyKeyword":
    case "UnknownKeyword":
    case "ObjectKeyword":
      return unsupported(
        expression,
        `TypeScript '${expression.checkerType}' requires an explicit runtime transport escape hatch.`,
      );
    case "BigIntKeyword":
    case "SymbolKeyword":
      return unsupported(
        expression,
        `TypeScript '${expression.checkerType}' is not JSON-compatible.`,
      );
    case "NeverKeyword":
      return unsupported(
        expression,
        "TypeScript 'never' has no runtime value to transport.",
      );
    default:
      return unsupported(
        expression,
        `TypeScript '${expression.checkerType}' has no deterministic interop transport.`,
      );
  }
}

function classifyLiteral(expression: TypeExpression): TransportModel {
  const kind = text(expression, "literalKind");
  switch (kind) {
    case "StringLiteral":
    case "NumericLiteral":
    case "TrueKeyword":
    case "FalseKeyword":
    case "PrefixUnaryExpression":
      return supported("json-value", expression);
    case "NullKeyword":
      return supported("json-value", expression, true);
    case "BigIntLiteral":
      return unsupported(
        expression,
        `TypeScript literal '${expression.checkerType}' is not JSON-compatible.`,
      );
    default:
      return unsupported(
        expression,
        `TypeScript literal '${expression.checkerType}' has no reviewed JSON transport.`,
      );
  }
}

function classifyReference(
  expression: TypeExpression,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  const name = text(expression, "resolvedSymbol") ?? text(expression, "name");
  if (name === null || dynamicReferenceTypes.has(name)) {
    return unsupported(
      expression,
      `TypeScript '${expression.checkerType}' requires an explicit runtime transport escape hatch.`,
    );
  }

  const typeArguments = children(expression, "typeArguments");
  if (promiseTypes.has(name)) {
    const result = typeArguments[0];
    return result === undefined
      ? unsupported(expression, `Promise '${expression.checkerType}' has no result type.`)
      : withSource(classify(result, symbols, resolving), expression);
  }
  if (arrayTypes.has(name)) {
    return classifyJsonContainer(expression, typeArguments[0], symbols, resolving);
  }
  if (name === "Record") {
    return classifyRecord(expression, typeArguments, symbols, resolving);
  }
  if (binaryTypes.has(name)) {
    return supported(
      "binary",
      expression,
      false,
      isStreamableBinary(name, typeArguments),
      true,
    );
  }

  const symbol = symbols.get(name);
  if (symbol === undefined) {
    return unsupported(
      expression,
      `Named TypeScript type '${name}' has no reviewed DOM transport classification.`,
    );
  }
  if (resolving.has(name)) {
    return unsupported(
      expression,
      `Named type '${name}' forms a transport-classification cycle.`,
    );
  }
  if (symbol.semantic.status === "ambiguous") {
    return unsupported(
      expression,
      `Named TypeScript type '${name}' has ambiguous Web IDL semantics and cannot select a transport.`,
    );
  }

  const classification = symbol.semantic.classifications[0];
  if (symbol.semantic.transferable) {
    return supported("transferable", expression, false, false, true);
  }
  if (classification === "interface" ||
    classification === "mixin" ||
    classification === "callbackInterface") {
    return supported(
      "js-reference",
      expression,
      false,
      name === "Blob" || name === "File",
      symbol.semantic.serializable,
    );
  }
  if (classification === "dictionary") {
    return classifyDictionary(expression, symbol, symbols, resolving);
  }
  if (classification === "enum") {
    return supported("json-value", expression);
  }
  if (classification === "callback") {
    return unsupported(
      expression,
      `Callback type '${name}' requires generated callback marshalling.`,
    );
  }

  const alias = symbol.declarations.find((declaration) =>
    declaration.kind === "typeAlias" && declaration.type !== null
  )?.type;
  if (alias !== undefined && alias !== null) {
    const next = new Set(resolving);
    next.add(name);
    return withSource(classify(alias, symbols, next), expression);
  }

  return unsupported(
    expression,
    `Named TypeScript type '${name}' has no reviewed DOM transport classification.`,
  );
}

function classifyUnion(
  expression: TypeExpression,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  const types = children(expression, "types");
  const nullable = types.some(isNullish);
  const effective = types.filter((type) => !isNullish(type));
  if (effective.length === 0) {
    return supported("json-value", expression, true);
  }

  const transports = effective.map((type) => classify(type, symbols, resolving));
  const first = transports[0]!;
  const unsupportedTransports = transports.filter((transport) =>
    transport.kind === "unsupported"
  );
  if (unsupportedTransports.length > 0) {
    const reasons = [...new Set(unsupportedTransports.map((transport) =>
      transport.reason ?? "unknown transport failure"
    ))].join(" ");
    return unsupported(
      expression,
      `Union '${expression.checkerType}' is unsupported: ${reasons}`,
      nullable,
    );
  }
  if (transports.some((transport) => transport.kind !== first.kind)) {
    const kinds = [...new Set(transports.map((transport) => transport.kind))].join(", ");
    return unsupported(
      expression,
      `Union '${expression.checkerType}' has incompatible transports: ${kinds}.`,
      nullable,
    );
  }

  return {
    ...first,
    nullable: nullable || first.nullable,
    sourceType: expression.checkerType,
    streamable: transports.every((transport) => transport.streamable),
    structuredClone: transports.every((transport) => transport.structuredClone),
  };
}

function isStreamableBinary(
  name: string,
  typeArguments: TypeExpression[],
): boolean {
  if (name === "ArrayBuffer") {
    return true;
  }
  if (name === "ArrayBufferLike" || name === "SharedArrayBuffer") {
    return false;
  }
  if (!arrayBufferViewTypes.has(name)) {
    return false;
  }
  if (typeArguments.length === 0) {
    // The DOM declarations default typed-array backing stores to
    // ArrayBufferLike, which also admits SharedArrayBuffer.
    return false;
  }

  return typeArguments.every((argument) =>
    argument.kind === "reference" &&
    (text(argument, "resolvedSymbol") ?? text(argument, "name")) === "ArrayBuffer"
  );
}

function classifyRecord(
  expression: TypeExpression,
  typeArguments: TypeExpression[],
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  if (typeArguments.length !== 2) {
    return unsupported(
      expression,
      `Record '${expression.checkerType}' must have a string key and value type.`,
    );
  }
  if (!isStringKey(typeArguments[0]!, symbols, resolving)) {
    return unsupported(
      expression,
      `Record '${expression.checkerType}' does not have a reviewed string key type.`,
    );
  }
  return classifyJsonElements(
    expression,
    [typeArguments[1]!],
    symbols,
    resolving,
  );
}

function classifyDictionary(
  expression: TypeExpression,
  symbol: SymbolModel,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  const next = new Set(resolving);
  next.add(symbol.name);
  const values: Array<{ name: string; type: TypeExpression }> = [];

  for (const declaration of symbol.declarations) {
    if (declaration.kind === "typeAlias" && declaration.type !== null) {
      values.push({ name: "alias", type: declaration.type });
      continue;
    }
    if (declaration.kind !== "interface") {
      return unsupported(
        expression,
        `Web IDL dictionary '${symbol.name}' has unsupported TypeScript declaration '${declaration.kind}'.`,
      );
    }
    for (const heritage of declaration.heritage) {
      for (const type of heritage.types) {
        values.push({ name: `inherited '${type.checkerType}'`, type });
      }
    }
    for (const member of declaration.members) {
      if (member.kind !== "property" || member.type === null) {
        return unsupported(
          expression,
          `Web IDL dictionary '${symbol.name}' has unsupported member shape '${member.kind}'.`,
        );
      }
      values.push({
        name: member.name?.text ?? `member ${member.ordinal}`,
        type: member.type,
      });
    }
  }

  const transports = values.map(({ name, type }) => ({
    name,
    transport: classify(type, symbols, next),
  }));
  const nonJson = transports.filter(({ transport }) =>
    transport.kind !== "json-value"
  );
  if (nonJson.length === 0) {
    return supported("json-value", expression);
  }

  const details = nonJson.map(({ name, transport }) =>
    `'${name}' uses ${transport.kind}` +
    (transport.reason === null ? "" : ` (${transport.reason})`)
  ).join("; ");
  return unsupported(
    expression,
    `Web IDL dictionary '${symbol.name}' is not a reviewed JSON-only shape: ${details}.`,
  );
}

function isStringKey(
  expression: TypeExpression,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): boolean {
  if (expression.kind === "keyword") {
    return text(expression, "name") === "StringKeyword";
  }
  if (expression.kind === "literal") {
    return text(expression, "literalKind") === "StringLiteral";
  }
  if (expression.kind === "union") {
    return children(expression, "types").every((type) =>
      isStringKey(type, symbols, resolving)
    );
  }
  if (expression.kind === "parenthesized") {
    const wrapped = child(expression, "type");
    return wrapped !== undefined && isStringKey(wrapped, symbols, resolving);
  }
  if (expression.kind !== "reference" &&
    expression.kind !== "heritageReference") {
    return false;
  }

  const name = text(expression, "resolvedSymbol") ?? text(expression, "name");
  if (name === null || resolving.has(name)) {
    return false;
  }
  const symbol = symbols.get(name);
  if (symbol === undefined || symbol.semantic.status === "ambiguous") {
    return false;
  }
  if (symbol.semantic.classifications[0] === "enum") {
    return true;
  }
  const alias = symbol.declarations.find((declaration) =>
    declaration.kind === "typeAlias" && declaration.type !== null
  )?.type;
  if (alias === undefined || alias === null) {
    return false;
  }
  const next = new Set(resolving);
  next.add(name);
  return isStringKey(alias, symbols, next);
}

function classifyComposite(
  expression: TypeExpression,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  const transports = children(expression, "types")
    .map((type) => classify(type, symbols, resolving));
  if (transports.length > 0 &&
    transports.every((transport) => transport.kind === "json-value")) {
    return supported("json-value", expression);
  }
  return unsupported(
    expression,
    `Intersection '${expression.checkerType}' is not a reviewed JSON value shape.`,
  );
}

function classifyWrapped(
  expression: TypeExpression,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  const wrapped = child(expression, "type");
  if (wrapped === undefined) {
    return unsupported(
      expression,
      `Wrapped type '${expression.checkerType}' has no inner type.`,
    );
  }
  const transport = withSource(classify(wrapped, symbols, resolving), expression);
  return expression.kind === "optional"
    ? { ...transport, nullable: true }
    : transport;
}

function classifyJsonContainer(
  expression: TypeExpression,
  element: TypeExpression | undefined,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  if (element === undefined) {
    return unsupported(
      expression,
      `Collection '${expression.checkerType}' has no element type.`,
    );
  }
  return classifyJsonElements(expression, [element], symbols, resolving);
}

function classifyJsonElements(
  expression: TypeExpression,
  elements: TypeExpression[],
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  const transports = elements.map((element) => classify(element, symbols, resolving));
  if (transports.every((transport) => transport.kind === "json-value")) {
    return supported("json-value", expression);
  }
  return unsupported(
    expression,
    `Collection '${expression.checkerType}' contains a non-JSON transport.`,
  );
}

function classifyTypeLiteral(
  expression: TypeExpression,
  symbols: ReadonlyMap<string, SymbolModel>,
  resolving: ReadonlySet<string>,
): TransportModel {
  const members = value<MemberModel[]>(expression, "members") ?? [];
  const reviewed = members.every((member) => {
    if (member.kind !== "property" && member.kind !== "getter") {
      return false;
    }
    const memberType = member.type ?? member.returnType;
    return memberType !== null &&
      classify(memberType, symbols, resolving).kind === "json-value";
  });
  return reviewed
    ? supported("json-value", expression)
    : unsupported(
      expression,
      `Object shape '${expression.checkerType}' is not a reviewed JSON value shape.`,
    );
}

function supported(
  kind: Exclude<TransportKind, "unsupported">,
  expression: TypeExpression,
  nullable = false,
  streamable = false,
  structuredClone = kind === "json-value",
): TransportModel {
  return {
    kind,
    nullable,
    sourceType: expression.checkerType,
    streamable,
    structuredClone,
    reason: null,
  };
}

function unsupported(
  expression: TypeExpression,
  reason: string,
  nullable = false,
): TransportModel {
  return {
    kind: "unsupported",
    nullable,
    sourceType: expression.checkerType,
    streamable: false,
    structuredClone: false,
    reason,
  };
}

function withSource(
  transport: TransportModel,
  expression: TypeExpression,
): TransportModel {
  return { ...transport, sourceType: expression.checkerType };
}

function isNullish(expression: TypeExpression): boolean {
  if (expression.kind === "literal") {
    return text(expression, "literalKind") === "NullKeyword";
  }
  return expression.kind === "keyword" &&
    text(expression, "name") === "UndefinedKeyword";
}

function isTypeExpression(value: Record<string, unknown>): value is TypeExpression {
  return typeof value.kind === "string" &&
    typeof value.syntaxKind === "string" &&
    typeof value.checkerType === "string";
}

function child(
  expression: TypeExpression,
  key: string,
): TypeExpression | undefined {
  const candidate = expression[key];
  return isRecord(candidate) && isTypeExpression(candidate)
    ? candidate
    : undefined;
}

function children(expression: TypeExpression, key: string): TypeExpression[] {
  const candidate = expression[key];
  return Array.isArray(candidate)
    ? candidate.filter((item): item is TypeExpression =>
      isRecord(item) && isTypeExpression(item)
    )
    : [];
}

function text(expression: TypeExpression, key: string): string | null {
  const candidate = expression[key];
  return typeof candidate === "string" ? candidate : null;
}

function value<T>(expression: TypeExpression, key: string): T | undefined {
  return expression[key] as T | undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
