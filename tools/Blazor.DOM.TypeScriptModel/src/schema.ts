export const MODEL_SCHEMA_VERSION = 2;

export type TransportKind =
  | "json-value"
  | "js-reference"
  | "js-stream"
  | "binary"
  | "transferable"
  | "unsupported";

export interface TransportModel {
  kind: TransportKind;
  nullable: boolean;
  sourceType: string;
  streamable: boolean;
  structuredClone: boolean;
  reason: string | null;
}

export interface SourceLocation {
  source: string;
  sourceOrdinal: number;
  supplemental: boolean;
  start: { line: number; column: number; offset: number };
  end: { line: number; column: number; offset: number };
}

export interface Documentation {
  text: string;
  tags: Array<{ name: string; text: string; raw: string }>;
  deprecated: boolean;
}

export interface TypeExpression {
  kind: string;
  syntaxKind: string;
  checkerType: string;
  transport: TransportModel;
  [key: string]: unknown;
}

export interface TypeParameterModel {
  name: string;
  constraint: TypeExpression | null;
  default: TypeExpression | null;
  location: SourceLocation;
}

export interface ParameterModel {
  ordinal: number;
  name: string;
  optional: boolean;
  rest: boolean;
  type: TypeExpression | null;
  initializer: string | null;
  location: SourceLocation;
  documentation: Documentation;
}

export interface MemberModel {
  ordinal: number;
  kind: string;
  name: PropertyNameModel | null;
  optional: boolean;
  readonly: boolean;
  static: boolean;
  typeParameters: TypeParameterModel[];
  parameters: ParameterModel[];
  type: TypeExpression | null;
  returnType: TypeExpression | null;
  documentation: Documentation;
  location: SourceLocation;
}

export interface PropertyNameModel {
  kind: "identifier" | "string" | "number" | "computed" | "private";
  text: string;
}

export interface DeclarationModel {
  ordinal: number;
  supplemental: boolean;
  kind: string;
  name: string;
  modifiers: string[];
  typeParameters: TypeParameterModel[];
  heritage: Array<{
    token: "extends" | "implements";
    types: TypeExpression[];
  }>;
  members: MemberModel[];
  type: TypeExpression | null;
  parameters: ParameterModel[];
  returnType: TypeExpression | null;
  documentation: Documentation;
  location: SourceLocation;
  variableKind: "var" | "let" | "const" | null;
  constructorObject: boolean;
  eventMap: { isEventMap: boolean; keys: string[] };
  namespaceMembers: string[];
}

export interface SemanticModel {
  status: "matched" | "unmatched" | "ambiguous" | "overridden";
  webIdlName: string | null;
  bindingKind: WebIdlBindingKind | null;
  webIdlMemberName: string | null;
  classifications: string[];
  specifications: string[];
  exposures: string[];
  exposedOnWindow: boolean;
  exposedOnWorker: boolean;
  globalNames: string[];
  serializable: boolean;
  transferable: boolean;
  secureContext: boolean;
  extendedAttributes: string[];
  bindings: WebIdlBindingModel[];
}

export type WebIdlBindingKind =
  | "definition"
  | "legacyNamespace"
  | "namespaceMember"
  | "globalMember"
  | "legacyWindowAlias"
  | "legacyFactoryFunction";

export interface WebIdlBindingModel {
  kind: WebIdlBindingKind;
  webIdlName: string;
  webIdlMemberName: string | null;
  specification: string;
  declarationOrdinal: number;
  memberOrdinal: number | null;
  exposures: string[];
  globalNames: string[];
  secureContext: boolean;
}

export interface SymbolModel {
  ordinal: number;
  name: string;
  symbolFlags: number;
  declarations: DeclarationModel[];
  isDeclarationMerged: boolean;
  supplemental: boolean;
  semantic: SemanticModel;
}

export interface SupplementalSourceProvenance {
  family: string;
  sourceKind: "webref-idl-generated" | "package-declaration";
  package: string;
  version: string;
  license: string;
  sourceUrl: string;
  inputs: ProvenanceFile[];
  output: ProvenanceFile;
  generationMethod: string;
}

export interface WebIdlExtendedAttributeModel {
  name: string;
  rhsType: string | null;
  values: string[];
  arguments: WebIdlArgumentModel[];
}

export interface WebIdlTypeModel {
  context: string | null;
  name: string | null;
  generic: string | null;
  nullable: boolean;
  union: boolean;
  typeArguments: WebIdlTypeModel[];
  extendedAttributes: WebIdlExtendedAttributeModel[];
}

export interface WebIdlValueModel {
  kind: string;
  value: JsonValue | null;
}

export interface WebIdlArgumentModel {
  ordinal: number;
  name: string;
  type: WebIdlTypeModel;
  optional: boolean;
  variadic: boolean;
  default: WebIdlValueModel | null;
  extendedAttributes: WebIdlExtendedAttributeModel[];
}

export interface WebIdlMemberModel {
  ordinal: number;
  kind: string;
  name: string | null;
  types: WebIdlTypeModel[];
  arguments: WebIdlArgumentModel[];
  required: boolean;
  readonly: boolean;
  asynchronous: boolean;
  special: string | null;
  default: WebIdlValueModel | null;
  value: WebIdlValueModel | null;
  exposures: string[];
  secureContext: boolean;
  extendedAttributes: WebIdlExtendedAttributeModel[];
}

export interface WebIdlDeclarationModel {
  ordinal: number;
  specification: string;
  kind: string;
  partial: boolean;
  inheritance: string | null;
  members: WebIdlMemberModel[];
  type: WebIdlTypeModel | null;
  arguments: WebIdlArgumentModel[];
  enumValues: string[];
  exposures: string[];
  globalNames: string[];
  serializable: boolean;
  transferable: boolean;
  secureContext: boolean;
  extendedAttributes: WebIdlExtendedAttributeModel[];
}

export interface WebIdlSymbolModel {
  ordinal: number;
  name: string;
  classifications: string[];
  specifications: string[];
  partialDeclarationCount: number;
  declarationCount: number;
  inheritance: string[];
  exposures: string[];
  globalNames: string[];
  serializable: boolean;
  transferable: boolean;
  secureContext: boolean;
  extendedAttributes: string[];
  extendedAttributeDetails: WebIdlExtendedAttributeModel[];
  includedMixins: string[];
  declarations: WebIdlDeclarationModel[];
  includeStatements: Array<{
    specification: string;
    mixin: string;
    extendedAttributes: WebIdlExtendedAttributeModel[];
  }>;
}

export type JsonValue =
  | null
  | boolean
  | number
  | string
  | JsonValue[]
  | JsonObject;

export interface JsonObject {
  [key: string]: JsonValue;
}

export interface TypeScriptCoverage {
  symbolCount: number;
  declarationCount: number;
  declarationKinds: Record<string, number>;
  memberCount: number;
  memberKinds: Record<string, number>;
  typeExpressionCount: number;
  typeExpressionKinds: Record<string, number>;
  mergedSymbolCount: number;
  eventMapCount: number;
  constructorObjectCount: number;
}

export interface WebIdlCoverage {
  specificationCount: number;
  definitionCount: number;
  canonicalSymbolCount: number;
  definitionKinds: Record<string, number>;
  includeStatementCount: number;
  memberCount: number;
  memberKinds: Record<string, number>;
  argumentCount: number;
}

export interface ProvenanceFile {
  name: string;
  sha256: string;
}

export interface DomModel {
  schemaVersion: number;
  generationProfile: {
    name: "Window";
    includedExposures: ["Window"];
    preservesAllExposureMetadata: true;
  };
  provenance: {
    generator: { name: string; version: string };
    typescript: {
      package: "typescript";
      version: string;
      license: "Apache-2.0";
      aggregateSha256: string;
      inputs: ProvenanceFile[];
    };
    webref: {
      package: "@webref/idl";
      version: string;
      license: "MIT";
      aggregateSha256: string;
      inputs: ProvenanceFile[];
    };
    webidl2: {
      package: "webidl2";
      version: string;
      license: "W3C";
    };
    supplementalTypeScript: SupplementalSourceProvenance[];
    overrides: { input: string; sha256: string; appliedCount: number };
  };
  symbols: SymbolModel[];
  webIdlSymbols: WebIdlSymbolModel[];
  coverage: CoverageModel;
}

export interface CoverageModel {
  typescript: TypeScriptCoverage;
  webIdl: WebIdlCoverage;
  reconciliation: {
    matched: number;
    matchedWebIdl: number;
    unmatchedTypeScript: string[];
    unmatchedWebIdl: string[];
    ambiguousWebIdl: string[];
    ambiguous: Array<{
      name: string;
      webIdlNames: string[];
      classifications: string[];
      reason: string;
    }>;
  };
  unsupported: Array<{ source: string; kind: string; location: string }>;
}

export interface InputSet {
  typescriptVersion: string;
  typescriptFiles: Array<{
    path: string;
    label: string;
    sha256: string;
    text?: string;
    supplemental?: boolean;
  }>;
  typescriptAggregateSha256: string;
  supplementalSources: SupplementalSourceProvenance[];
  webrefVersion: string;
  webIdlFiles: Array<{ name: string; text: string; sha256: string }>;
  webIdlAggregateSha256: string;
  webidl2Version: string;
  overridesPath: string;
  overridesSha256: string;
  overrideCount: number;
}
