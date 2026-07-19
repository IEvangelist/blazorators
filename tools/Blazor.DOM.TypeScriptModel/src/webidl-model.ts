import { parse } from "webidl2";
import {
  JsonObject,
  JsonValue,
  WebIdlArgumentModel,
  WebIdlCoverage,
  WebIdlDeclarationModel,
  WebIdlExtendedAttributeModel,
  WebIdlMemberModel,
  WebIdlSymbolModel,
  WebIdlTypeModel,
  WebIdlValueModel,
} from "./schema.js";
import { compareOrdinal, increment } from "./stable-json.js";

interface IdlDefinition {
  type: string;
  name?: string;
  partial?: boolean;
  inheritance?: string | null;
  target?: string;
  includes?: string;
  members?: Iterable<IdlMember>;
  arguments?: Iterable<IdlArgument>;
  toJSON(): unknown;
}

interface IdlMember {
  type: string;
  arguments?: Iterable<IdlArgument>;
}

interface IdlArgument {
  type: string;
}

interface MutableWebIdlSymbol {
  name: string;
  classifications: Set<string>;
  specifications: Set<string>;
  partialDeclarationCount: number;
  declarationCount: number;
  inheritance: Set<string>;
  exposures: Set<string>;
  globalNames: Set<string>;
  serializable: boolean;
  transferable: boolean;
  secureContext: boolean;
  extendedAttributes: Set<string>;
  extendedAttributeDetails: Map<string, WebIdlExtendedAttributeModel>;
  includedMixins: Set<string>;
  declarations: Array<{
    specification: string;
    sourceOrdinal: number;
    model: Omit<WebIdlDeclarationModel, "ordinal" | "specification">;
  }>;
  includeStatements: Array<{
    specification: string;
    mixin: string;
    extendedAttributes: WebIdlExtendedAttributeModel[];
  }>;
}

interface PendingInclude {
  target: string;
  mixin: string;
  specification: string;
  extendedAttributes: WebIdlExtendedAttributeModel[];
}

export interface WebIdlExtraction {
  symbols: WebIdlSymbolModel[];
  coverage: WebIdlCoverage;
}

export function extractWebIdlModel(
  files: ReadonlyArray<{ name: string; text: string }>,
): WebIdlExtraction {
  const symbols = new Map<string, MutableWebIdlSymbol>();
  const includes: PendingInclude[] = [];
  const definitionKinds: Record<string, number> = {};
  const memberKinds: Record<string, number> = {};
  let definitionCount = 0;
  let memberCount = 0;
  let argumentCount = 0;

  for (const file of [...files].sort((left, right) =>
    compareOrdinal(left.name, right.name)
  )) {
    const definitions = parse(file.text);
    for (const [sourceOrdinal, rawDefinition] of definitions.entries()) {
      const definition = asDefinition(rawDefinition, file.name);
      const ast = normalizeObject(definition.toJSON(), `${file.name} definition`);
      assertKnownKeys(ast, definitionKeys, `${file.name} definition`);
      definitionCount++;
      increment(definitionKinds, definition.type);

      for (const member of definition.members ?? []) {
        if (!supportedMemberKinds.has(member.type)) {
          throw new Error(
            `Unsupported Web IDL member type '${member.type}' in '${file.name}'.`,
          );
        }
        memberCount++;
        increment(memberKinds, member.type);
      }

      if (definition.type === "includes") {
        if (definition.target === undefined || definition.includes === undefined) {
          throw new Error(`Malformed Web IDL includes statement in '${file.name}'.`);
        }
        const extendedAttributes = serializeExtendedAttributes(
          ast.extAttrs,
          `${file.name} includes`,
        );
        argumentCount += countExtendedAttributeArguments(extendedAttributes);
        includes.push({
          target: definition.target,
          mixin: definition.includes,
          specification: file.name,
          extendedAttributes,
        });
        continue;
      }

      if (definition.name === undefined || definition.name.length === 0) {
        throw new Error(
          `Unnamed Web IDL ${definition.type} definition in '${file.name}'.`,
        );
      }

      const declaration = serializeDeclaration(ast, definition.type, file.name);
      argumentCount += countDeclarationArguments(declaration);
      const symbol = symbols.get(definition.name) ??
        createMutableSymbol(definition.name);
      symbols.set(definition.name, symbol);
      symbol.classifications.add(classification(definition.type));
      symbol.specifications.add(file.name);
      symbol.declarations.push({
        specification: file.name,
        sourceOrdinal,
        model: declaration,
      });
      symbol.declarationCount++;
      if (declaration.partial) {
        symbol.partialDeclarationCount++;
      }
      if (declaration.inheritance !== null) {
        symbol.inheritance.add(declaration.inheritance);
      }

      if (!declaration.partial) {
        for (const attribute of declaration.extendedAttributes) {
          symbol.extendedAttributes.add(attribute.name);
          symbol.extendedAttributeDetails.set(JSON.stringify(attribute), attribute);
        }
        for (const exposure of declaration.exposures) {
          symbol.exposures.add(exposure);
        }
        for (const globalName of declaration.globalNames) {
          symbol.globalNames.add(globalName);
        }
        symbol.serializable ||= declaration.serializable;
        symbol.transferable ||= declaration.transferable;
        symbol.secureContext ||= declaration.secureContext;
      }
    }
  }

  for (const include of includes) {
    const target = symbols.get(include.target);
    const mixin = symbols.get(include.mixin);
    if (target === undefined || mixin === undefined) {
      throw new Error(
        `Unresolved Web IDL include '${include.target} includes ${include.mixin}' ` +
        `in '${include.specification}'.`,
      );
    }
    target.includedMixins.add(include.mixin);
    target.includeStatements.push({
      specification: include.specification,
      mixin: include.mixin,
      extendedAttributes: include.extendedAttributes,
    });
  }

  return {
    symbols: [...symbols.values()]
      .sort((left, right) => compareOrdinal(left.name, right.name))
      .map((symbol, ordinal) => ({
        ordinal,
        name: symbol.name,
        classifications: sorted(symbol.classifications),
        specifications: sorted(symbol.specifications),
        partialDeclarationCount: symbol.partialDeclarationCount,
        declarationCount: symbol.declarationCount,
        inheritance: sorted(symbol.inheritance),
        exposures: sorted(symbol.exposures),
        globalNames: sorted(symbol.globalNames),
        serializable: symbol.serializable,
        transferable: symbol.transferable,
        secureContext: symbol.secureContext,
        extendedAttributes: sorted(symbol.extendedAttributes),
        extendedAttributeDetails: [...symbol.extendedAttributeDetails.values()]
          .sort((left, right) =>
            compareOrdinal(JSON.stringify(left), JSON.stringify(right))
          ),
        includedMixins: sorted(symbol.includedMixins),
        declarations: symbol.declarations
          .sort((left, right) => {
            const specificationComparison = compareOrdinal(
              left.specification,
              right.specification,
            );
            return specificationComparison !== 0
              ? specificationComparison
              : left.sourceOrdinal - right.sourceOrdinal;
          })
          .map((declaration, declarationOrdinal) => ({
            ordinal: declarationOrdinal,
            specification: declaration.specification,
            ...declaration.model,
          })),
        includeStatements: symbol.includeStatements.sort((left, right) => {
          const specificationComparison = compareOrdinal(
            left.specification,
            right.specification,
          );
          return specificationComparison !== 0
            ? specificationComparison
            : compareOrdinal(left.mixin, right.mixin);
        }),
      })),
    coverage: {
      specificationCount: files.length,
      definitionCount,
      canonicalSymbolCount: symbols.size,
      definitionKinds: sortRecord(definitionKinds),
      includeStatementCount: includes.length,
      memberCount,
      memberKinds: sortRecord(memberKinds),
      argumentCount,
    },
  };
}

function serializeDeclaration(
  ast: JsonObject,
  kind: string,
  context: string,
): Omit<WebIdlDeclarationModel, "ordinal" | "specification"> {
  const members = arrayValue(ast.members, `${context} members`).map(
    (member, ordinal) => serializeMember(member, ordinal, context),
  );
  const args = arrayValue(ast.arguments, `${context} arguments`).map(
    (argument, ordinal) => serializeArgument(argument, ordinal, context),
  );
  const enumValues = arrayValue(ast.values, `${context} enum values`).map(
    (value, index) => {
      const enumValue = objectValue(value, `${context} enum value ${index}`);
      assertKnownKeys(enumValue, valueKeys, `${context} enum value ${index}`);
      return requiredString(enumValue.value, `${context} enum value ${index}`);
    },
  );
  const extendedAttributes = serializeExtendedAttributes(
    ast.extAttrs,
    `${context} extended attributes`,
  );
  const semantics = extendedAttributeSemantics(extendedAttributes);

  return {
    kind,
    partial: booleanValue(ast.partial, false, `${context} partial`),
    inheritance: nullableString(ast.inheritance, `${context} inheritance`),
    members,
    type: ast.idlType === undefined
      ? null
      : serializeType(ast.idlType, `${context} type`),
    arguments: args,
    enumValues,
    ...semantics,
    extendedAttributes,
  };
}

function serializeMember(
  value: JsonValue,
  ordinal: number,
  context: string,
): WebIdlMemberModel {
  const member = objectValue(value, `${context} member ${ordinal}`);
  assertKnownKeys(member, memberKeys, `${context} member ${ordinal}`);
  const kind = requiredString(member.type, `${context} member ${ordinal} type`);
  if (!supportedMemberKinds.has(kind)) {
    throw new Error(`Unsupported Web IDL member type '${kind}' in '${context}'.`);
  }
  const extendedAttributes = serializeExtendedAttributes(
    member.extAttrs,
    `${context} member ${ordinal} extended attributes`,
  );
  const semantics = extendedAttributeSemantics(extendedAttributes);
  return {
    ordinal,
    kind,
    name: nullableString(member.name, `${context} member ${ordinal} name`),
    types: member.idlType === undefined
      ? []
      : Array.isArray(member.idlType)
      ? member.idlType.map((type, typeOrdinal) =>
        serializeType(type, `${context} member ${ordinal} type ${typeOrdinal}`)
      )
      : [serializeType(member.idlType, `${context} member ${ordinal} type 0`)],
    arguments: arrayValue(
      member.arguments,
      `${context} member ${ordinal} arguments`,
    ).map((argument, argumentOrdinal) =>
      serializeArgument(argument, argumentOrdinal, context)
    ),
    required: booleanValue(
      member.required,
      false,
      `${context} member ${ordinal} required`,
    ),
    readonly: booleanValue(
      member.readonly,
      false,
      `${context} member ${ordinal} readonly`,
    ),
    asynchronous: booleanValue(
      member.async,
      false,
      `${context} member ${ordinal} async`,
    ),
    special: nullableString(
      member.special,
      `${context} member ${ordinal} special`,
    ),
    default: serializeValue(
      member.default,
      `${context} member ${ordinal} default`,
    ),
    value: serializeValue(member.value, `${context} member ${ordinal} value`),
    exposures: semantics.exposures,
    secureContext: semantics.secureContext,
    extendedAttributes,
  };
}

function serializeArgument(
  value: JsonValue,
  ordinal: number,
  context: string,
): WebIdlArgumentModel {
  const argument = objectValue(value, `${context} argument ${ordinal}`);
  assertKnownKeys(argument, argumentKeys, `${context} argument ${ordinal}`);
  if (
    requiredString(argument.type, `${context} argument ${ordinal} kind`) !==
      "argument"
  ) {
    throw new Error(`Unsupported Web IDL argument in '${context}'.`);
  }
  return {
    ordinal,
    name: requiredString(argument.name, `${context} argument ${ordinal} name`),
    type: serializeType(argument.idlType, `${context} argument ${ordinal} type`),
    optional: booleanValue(
      argument.optional,
      false,
      `${context} argument ${ordinal} optional`,
    ),
    variadic: booleanValue(
      argument.variadic,
      false,
      `${context} argument ${ordinal} variadic`,
    ),
    default: serializeValue(
      argument.default,
      `${context} argument ${ordinal} default`,
    ),
    extendedAttributes: serializeExtendedAttributes(
      argument.extAttrs,
      `${context} argument ${ordinal} extended attributes`,
    ),
  };
}

function serializeType(value: JsonValue | undefined, context: string): WebIdlTypeModel {
  const type = objectValue(value, context);
  assertKnownKeys(type, typeKeys, context);
  const idlType = type.idlType;
  let name: string | null = null;
  let typeArguments: WebIdlTypeModel[] = [];
  if (typeof idlType === "string") {
    name = idlType;
  } else if (Array.isArray(idlType)) {
    typeArguments = idlType.map((item, index) =>
      serializeType(item, `${context} argument ${index}`)
    );
  } else if (idlType !== undefined && idlType !== null) {
    typeArguments = [serializeType(idlType, `${context} argument 0`)];
  } else {
    throw new Error(`Web IDL type is missing idlType in '${context}'.`);
  }
  const genericValue = requiredString(type.generic, `${context} generic`);
  return {
    context: nullableString(type.type, `${context} context`),
    name,
    generic: genericValue.length === 0 ? null : genericValue,
    nullable: booleanValue(type.nullable, false, `${context} nullable`),
    union: booleanValue(type.union, false, `${context} union`),
    typeArguments,
    extendedAttributes: serializeExtendedAttributes(
      type.extAttrs,
      `${context} extended attributes`,
    ),
  };
}

function serializeExtendedAttributes(
  value: JsonValue | undefined,
  context: string,
): WebIdlExtendedAttributeModel[] {
  return arrayValue(value, context).map((attributeValue, attributeOrdinal) => {
    const attribute = objectValue(
      attributeValue,
      `${context} ${attributeOrdinal}`,
    );
    assertKnownKeys(
      attribute,
      extendedAttributeKeys,
      `${context} ${attributeOrdinal}`,
    );
    const rhs = attribute.rhs === null || attribute.rhs === undefined
      ? null
      : objectValue(attribute.rhs, `${context} ${attributeOrdinal} rhs`);
    if (rhs !== null) {
      assertKnownKeys(rhs, rhsKeys, `${context} ${attributeOrdinal} rhs`);
    }
    const values = rhs === null
      ? []
      : extendedAttributeValues(rhs, `${context} ${attributeOrdinal} rhs`);
    return {
      name: requiredString(
        attribute.name,
        `${context} ${attributeOrdinal} name`,
      ),
      rhsType: rhs === null
        ? null
        : requiredString(rhs.type, `${context} ${attributeOrdinal} rhs type`),
      values,
      arguments: arrayValue(
        attribute.arguments,
        `${context} ${attributeOrdinal} arguments`,
      ).map((argument, argumentOrdinal) =>
        serializeArgument(argument, argumentOrdinal, context)
      ),
    };
  });
}

function extendedAttributeValues(rhs: JsonObject, context: string): string[] {
  if (rhs.type === "*" && rhs.value === null) {
    return ["*"];
  }
  if (typeof rhs.value === "string") {
    return [extendedAttributeValue(rhs.type, rhs.value, context)];
  }
  return arrayValue(rhs.value, `${context} value`).map((item, index) => {
    const value = objectValue(item, `${context} value ${index}`);
    assertKnownKeys(value, identifierValueKeys, `${context} value ${index}`);
    return extendedAttributeValue(
      rhs.type,
      requiredString(value.value, `${context} value ${index}`),
      `${context} value ${index}`,
    );
  });
}

function extendedAttributeValue(
  type: JsonValue | undefined,
  value: string,
  context: string,
): string {
  if (type !== "string" && type !== "string-list") {
    return value;
  }
  if (value.length < 2 || !value.startsWith("\"") || !value.endsWith("\"")) {
    throw new Error(`Malformed Web IDL string token '${value}' in '${context}'.`);
  }
  return value.slice(1, -1);
}

function serializeValue(
  value: JsonValue | undefined,
  context: string,
): WebIdlValueModel | null {
  if (value === undefined || value === null) {
    return null;
  }
  const model = objectValue(value, context);
  assertKnownKeys(model, valueKeys, context);
  const kind = requiredString(model.type, `${context} kind`);
  if (kind === "Infinity") {
    return {
      kind: booleanValue(model.negative, false, `${context} negative`)
        ? "-Infinity"
        : "Infinity",
      value: null,
    };
  }
  if (model.negative !== undefined) {
    throw new Error(`Unexpected Web IDL negative flag for '${kind}' in '${context}'.`);
  }
  if (kind === "NaN") {
    return { kind, value: null };
  }
  return {
    kind,
    value: model.value ?? null,
  };
}

function extendedAttributeSemantics(
  attributes: readonly WebIdlExtendedAttributeModel[],
): {
  exposures: string[];
  globalNames: string[];
  serializable: boolean;
  transferable: boolean;
  secureContext: boolean;
} {
  const exposures = new Set<string>();
  const globalNames = new Set<string>();
  let serializable = false;
  let transferable = false;
  let secureContext = false;

  for (const attribute of attributes) {
    switch (attribute.name) {
      case "Exposed":
        attribute.values.forEach((value) => exposures.add(value));
        break;
      case "Global":
        attribute.values.forEach((value) => globalNames.add(value));
        break;
      case "Serializable":
        serializable = true;
        break;
      case "Transferable":
        transferable = true;
        break;
      case "SecureContext":
        secureContext = true;
        break;
    }
  }

  return {
    exposures: sorted(exposures),
    globalNames: sorted(globalNames),
    serializable,
    transferable,
    secureContext,
  };
}

function asDefinition(value: unknown, specification: string): IdlDefinition {
  if (typeof value !== "object" || value === null || !("type" in value)) {
    throw new Error(`Invalid Web IDL AST definition in '${specification}'.`);
  }
  const candidate = value as Record<string, unknown>;
  if (typeof candidate.type !== "string") {
    throw new Error(`Web IDL AST definition without a type in '${specification}'.`);
  }
  return value as IdlDefinition;
}

function classification(type: string): string {
  switch (type) {
    case "interface":
    case "dictionary":
    case "callback":
    case "enum":
    case "namespace":
    case "typedef":
      return type;
    case "interface mixin":
      return "mixin";
    case "callback interface":
      return "callbackInterface";
    default:
      throw new Error(`Unsupported Web IDL definition type '${type}'.`);
  }
}

function createMutableSymbol(name: string): MutableWebIdlSymbol {
  return {
    name,
    classifications: new Set(),
    specifications: new Set(),
    partialDeclarationCount: 0,
    declarationCount: 0,
    inheritance: new Set(),
    exposures: new Set(),
    globalNames: new Set(),
    serializable: false,
    transferable: false,
    secureContext: false,
    extendedAttributes: new Set(),
    extendedAttributeDetails: new Map(),
    includedMixins: new Set(),
    declarations: [],
    includeStatements: [],
  };
}

const supportedMemberKinds = new Set([
  "async_iterable",
  "attribute",
  "const",
  "constructor",
  "field",
  "iterable",
  "maplike",
  "operation",
  "setlike",
]);

const definitionKeys = new Set([
  "arguments",
  "extAttrs",
  "idlType",
  "includes",
  "inheritance",
  "members",
  "name",
  "partial",
  "target",
  "type",
  "values",
]);
const memberKeys = new Set([
  "arguments",
  "async",
  "default",
  "extAttrs",
  "idlType",
  "inheritance",
  "name",
  "readonly",
  "required",
  "special",
  "type",
  "value",
]);
const argumentKeys = new Set([
  "default",
  "extAttrs",
  "idlType",
  "name",
  "optional",
  "type",
  "variadic",
]);
const typeKeys = new Set([
  "extAttrs",
  "generic",
  "idlType",
  "inheritance",
  "name",
  "nullable",
  "type",
  "union",
]);
const extendedAttributeKeys = new Set(["arguments", "name", "rhs", "type"]);
const rhsKeys = new Set(["type", "value"]);
const valueKeys = new Set(["negative", "type", "value"]);
const identifierValueKeys = new Set(["value"]);

function countDeclarationArguments(
  declaration: Omit<WebIdlDeclarationModel, "ordinal" | "specification">,
): number {
  return declaration.arguments.reduce(
    (count, argument) => count + countArgument(argument),
    0,
  ) +
    declaration.members.reduce(
      (count, member) => count + countMemberArguments(member),
      0,
    ) +
    (declaration.type === null ? 0 : countTypeArguments(declaration.type)) +
    countExtendedAttributeArguments(declaration.extendedAttributes);
}

function countMemberArguments(member: WebIdlMemberModel): number {
  return member.arguments.reduce(
    (count, argument) => count + countArgument(argument),
    0,
  ) +
    member.types.reduce(
      (count, type) => count + countTypeArguments(type),
      0,
    ) +
    countExtendedAttributeArguments(member.extendedAttributes);
}

function countArgument(argument: WebIdlArgumentModel): number {
  return 1 +
    countTypeArguments(argument.type) +
    countExtendedAttributeArguments(argument.extendedAttributes);
}

function countTypeArguments(type: WebIdlTypeModel): number {
  return type.typeArguments.reduce(
    (count, argument) => count + countTypeArguments(argument),
    0,
  ) +
    countExtendedAttributeArguments(type.extendedAttributes);
}

function countExtendedAttributeArguments(
  attributes: readonly WebIdlExtendedAttributeModel[],
): number {
  return attributes.reduce(
    (attributeCount, attribute) =>
      attributeCount +
      attribute.arguments.reduce(
        (argumentCount, argument) => argumentCount + countArgument(argument),
        0,
      ),
    0,
  );
}

function normalizeObject(value: unknown, context: string): JsonObject {
  const normalized = normalizeJson(value, context);
  if (
    typeof normalized !== "object" ||
    normalized === null ||
    Array.isArray(normalized)
  ) {
    throw new Error(`Expected Web IDL object for ${context}.`);
  }
  return normalized;
}

function normalizeJson(value: unknown, context: string): JsonValue {
  if (
    value === null ||
    typeof value === "string" ||
    typeof value === "boolean"
  ) {
    return value;
  }
  if (typeof value === "number") {
    if (!Number.isFinite(value)) {
      throw new Error(`Non-finite Web IDL number in ${context}.`);
    }
    return value;
  }
  if (Array.isArray(value)) {
    return value.map((item, index) => normalizeJson(item, `${context}[${index}]`));
  }
  if (typeof value === "object") {
    if ("toJSON" in value && typeof value.toJSON === "function") {
      return normalizeJson(value.toJSON(), context);
    }
    const normalized: JsonObject = {};
    for (const [key, child] of Object.entries(value)) {
      if (child !== undefined) {
        normalized[key] = normalizeJson(child, `${context}.${key}`);
      }
    }
    return normalized;
  }
  throw new Error(`Unsupported Web IDL JSON value '${typeof value}' in ${context}.`);
}

function objectValue(value: JsonValue | undefined, context: string): JsonObject {
  if (
    typeof value !== "object" ||
    value === null ||
    Array.isArray(value)
  ) {
    throw new Error(`Expected Web IDL object in '${context}'.`);
  }
  return value;
}

function arrayValue(value: JsonValue | undefined, context: string): JsonValue[] {
  if (value === undefined) {
    return [];
  }
  if (!Array.isArray(value)) {
    throw new Error(`Expected Web IDL array in '${context}'.`);
  }
  return value;
}

function requiredString(value: JsonValue | undefined, context: string): string {
  if (typeof value !== "string") {
    throw new Error(`Expected Web IDL string in '${context}'.`);
  }
  return value;
}

function nullableString(
  value: JsonValue | undefined,
  context: string,
): string | null {
  if (value === undefined || value === null || value === "") {
    return null;
  }
  return requiredString(value, context);
}

function booleanValue(
  value: JsonValue | undefined,
  defaultValue: boolean,
  context: string,
): boolean {
  if (value === undefined) {
    return defaultValue;
  }
  if (typeof value !== "boolean") {
    throw new Error(`Expected Web IDL boolean in '${context}'.`);
  }
  return value;
}

function assertKnownKeys(
  value: JsonObject,
  knownKeys: ReadonlySet<string>,
  context: string,
): void {
  const unknownKeys = Object.keys(value).filter((key) => !knownKeys.has(key));
  if (unknownKeys.length > 0) {
    throw new Error(
      `Unsupported Web IDL fields in '${context}': ${unknownKeys.join(", ")}.`,
    );
  }
}

function sorted(values: ReadonlySet<string>): string[] {
  return [...values].sort(compareOrdinal);
}

function sortRecord(record: Record<string, number>): Record<string, number> {
  return Object.fromEntries(
    Object.entries(record).sort(([left], [right]) => compareOrdinal(left, right)),
  );
}
