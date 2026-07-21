import assert from "node:assert/strict";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { after, before, test } from "node:test";
import { fileURLToPath } from "node:url";
import { loadPinnedInputs } from "../src/inputs.js";
import { buildDomModel } from "../src/model.js";
import {
  hashJsonLines,
  serializeJsonLine,
  verifyJsonLines,
  writeJsonLines,
} from "../src/output.js";
import { InputSet, SymbolModel, WebIdlSymbolModel } from "../src/schema.js";
import { normalizeLf, stableJson, sha256 } from "../src/stable-json.js";
import {
  filterWebGpuWindowSource,
  generateWebIdlSupplement,
} from "../src/supplemental.js";
import { extractTypeScriptModel } from "../src/typescript-model.js";
import { extractWebIdlModel } from "../src/webidl-model.js";
import { assertValid, loadArtifactValidators } from "../src/validation.js";

const declarations = `
interface Event {}
interface CustomEvent<T> extends Event {
  detail: T;
}

/** Event names.
 * @deprecated Use ModernEventMap.
 */
interface FixtureEventMap {
  "ready": Event;
  "value-change": CustomEvent<string>;
}

interface HTMLElementTagNameMap {
  "fixture-element": string;
}

interface Generic<T extends string = "default"> {
  readonly value?: T;
  [key: string]: unknown;
  transform(value: string): number;
  transform(value: number): string;
}

interface Generic<T extends string = "default"> {
  callback: (value: T, ...rest: number[]) => Promise<T>;
}

interface Derived extends Generic<"derived"> {}
interface Collision {}

type Primitive = string | number | undefined;
type Combined = { left: string } & { right: number };
type Selection<T> = T[keyof T];
type Projection<T> = {
  readonly [K in keyof T as \`get\${Capitalize<string & K>}\`]?: T[K]
};
type Decision<T> =
  T extends (...args: infer A) => infer R
    ? readonly [args: A, result?: R]
    : Array<T>;
type Predicate = (value: unknown) => value is string;
type Constructor = abstract new <T>(value: T) => Generic<string>;
type Numeric = 1;
declare var Generic: {
  prototype: Generic<string>;
  new<T extends string = "default">(value?: T): Generic<T>;
};
declare var GenericAlias: typeof Generic;
declare var GenericFactory: typeof Generic;
declare var LegacyGeneric: typeof Generic;
declare var Collision: {
  prototype: Collision;
  new(): Collision;
};
declare const constantValue: number;
declare let mutableValue: number;

declare function createGeneric<T extends string = "default">(value?: T): Generic<T>;
declare function createGeneric(value: number): Generic<string>;
declare function alert(message?: string): void;
declare function fetch(): void;

declare namespace FixtureNamespace {
  function parse(value: string): void;
  function parse(value: number): void;
}
declare namespace Outer.Inner {
  interface Nested {}
}
declare namespace Variables {
  var z: number, a: string;
}
`;

const iterableDeclarations = `
interface Derived {
  values(): IterableIterator<string>;
}
`;

const idl = `
[Exposed=(Window,DedicatedWorker), SecureContext, Serializable,
 LegacyFactoryFunction=GenericFactory(optional DOMString value),
 LegacyWindowAlias=LegacyGeneric]
interface Generic {
  attribute DOMString value;
  undefined run(optional DOMString input);
};
[Exposed=Worker, Transferable]
interface Transfer {};
dictionary GenericOptions {
  required DOMString name;
  boolean enabled = false;
  unrestricted double limit = -Infinity;
};
callback CompletionCallback = Promise<DOMString> (optional long count = 1);
dictionary Collision {};
enum Mode { "one", "two" };
namespace Helpers {};
namespace FixtureNamespace {
  undefined parse(DOMString value);
  undefined parse(long value);
};
interface mixin Named {
  undefined fetch();
};
Generic includes Named;
[Exposed=Window] interface Scoped {
  attribute DOMString baseValue;
};
[Exposed=Worker, SecureContext] partial interface Scoped {
  attribute DOMString workerValue;
};
[Global=Window, Exposed=Window] interface WindowFixture {
  undefined alert(optional DOMString message);
};
WindowFixture includes Named;
[Reflect="for"] interface Reflected {};
`;

let directory: string;
let declarationPath: string;
let iterableDeclarationPath: string;

before(async () => {
  directory = await mkdtemp(path.join(tmpdir(), "blazor-dom-model-"));
  declarationPath = path.join(directory, "fixture.d.ts");
  iterableDeclarationPath = path.join(directory, "fixture.iterable.d.ts");
  await writeFile(declarationPath, declarations, "utf8");
  await writeFile(iterableDeclarationPath, iterableDeclarations, "utf8");
});

after(async () => {
  await rm(directory, { recursive: true, force: true });
});

test("captures representative TypeScript declarations and type expressions", () => {
  const result = extractTypeScriptModel(typeScriptInputs());
  const generic = symbol(result.symbols, "Generic");
  const derived = symbol(result.symbols, "Derived");
  const eventMap = symbol(result.symbols, "FixtureEventMap");
  const tagNameMap = symbol(result.symbols, "HTMLElementTagNameMap");
  const createGeneric = symbol(result.symbols, "createGeneric");
  const fixtureNamespace = symbol(result.symbols, "FixtureNamespace");

  assert.equal(generic.isDeclarationMerged, true);
  assert.deepEqual(
    generic.declarations.map((declaration) => declaration.kind),
    ["interface", "interface", "globalVariable"],
  );
  assert.equal(
    generic.declarations.find((declaration) => declaration.kind === "globalVariable")
      ?.constructorObject,
    true,
  );
  assert.equal(
    generic.declarations[0]?.members.filter((member) => member.kind === "method").length,
    2,
  );
  assert.deepEqual(eventMap.declarations[0]?.eventMap, {
    isEventMap: true,
    keys: ["ready", "value-change"],
  });
  assert.equal(eventMap.declarations[0]?.documentation.deprecated, true);
  assert.equal(tagNameMap.declarations[0]?.eventMap.isEventMap, false);
  assert.equal(createGeneric.declarations.length, 2);
  assert.equal(derived.declarations.length, 2);
  assert.equal(
    derived.declarations[1]?.location.source,
    "fixture.iterable.d.ts",
  );
  assert.equal(
    symbol(result.symbols, "GenericAlias").declarations[0]?.constructorObject,
    true,
  );
  assert.equal(
    symbol(result.symbols, "constantValue").declarations[0]?.variableKind,
    "const",
  );
  assert.equal(
    symbol(result.symbols, "mutableValue").declarations[0]?.variableKind,
    "let",
  );
  assert.equal(
    generic.declarations.find((declaration) => declaration.kind === "globalVariable")
      ?.variableKind,
    "var",
  );
  assert.equal(
    symbol(result.symbols, "Constructor").declarations[0]?.type?.abstract,
    true,
  );
  assert.equal(
    symbol(result.symbols, "Numeric").declarations[0]?.type?.literalKind,
    "NumericLiteral",
  );
  assert.ok(
    result.symbols.every((item) => !serializeJsonLine(item).includes("FirstLiteralToken")),
  );
  assert.deepEqual(
    fixtureNamespace.declarations[0]?.namespaceMembers,
    ["FixtureNamespace.parse"],
  );
  assert.deepEqual(
    symbol(result.symbols, "Outer").declarations[0]?.namespaceMembers,
    ["Outer.Inner"],
  );
  assert.deepEqual(
    symbol(result.symbols, "Outer.Inner").declarations[0]?.namespaceMembers,
    ["Outer.Inner.Nested"],
  );
  assert.ok(result.symbols.some((item) => item.name === "Outer.Inner.Nested"));
  assert.deepEqual(
    symbol(result.symbols, "Variables").declarations[0]?.namespaceMembers,
    ["Variables.a", "Variables.z"],
  );
  assert.ok(result.symbols.some((item) => item.name === "Variables.a"));
  assert.ok(result.symbols.some((item) => item.name === "Variables.z"));
  for (
    const kind of [
      "ArrayType",
      "ConditionalType",
      "FunctionType",
      "IndexedAccessType",
      "InferType",
      "IntersectionType",
      "MappedType",
      "NamedTupleMember",
      "TemplateLiteralType",
      "TupleType",
      "TypeOperator",
      "UnionType",
    ]
  ) {
    assert.ok((result.coverage.typeExpressionKinds[kind] ?? 0) > 0, `Missing ${kind}`);
  }
});

test("classifies Web IDL and preserves exposure metadata", () => {
  const result = extractWebIdlModel([{ name: "fixture", text: idl }]);
  const generic = webIdlSymbol(result.symbols, "Generic");
  const options = webIdlSymbol(result.symbols, "GenericOptions");
  const callback = webIdlSymbol(result.symbols, "CompletionCallback");
  const mode = webIdlSymbol(result.symbols, "Mode");
  const transfer = webIdlSymbol(result.symbols, "Transfer");
  const scoped = webIdlSymbol(result.symbols, "Scoped");
  const reflected = webIdlSymbol(result.symbols, "Reflected");

  assert.deepEqual(generic.classifications, ["interface"]);
  assert.deepEqual(generic.exposures, ["DedicatedWorker", "Window"]);
  assert.equal(generic.secureContext, true);
  assert.equal(generic.serializable, true);
  assert.deepEqual(generic.includedMixins, ["Named"]);
  const genericMembers = generic.declarations[0]?.members;
  assert.ok(genericMembers);
  assert.equal(genericMembers[0]?.kind, "attribute");
  assert.equal(genericMembers[1]?.kind, "operation");
  const factoryAttribute = generic.extendedAttributeDetails.find(
    (attribute) => attribute.name === "LegacyFactoryFunction",
  );
  assert.ok(factoryAttribute);
  assert.equal(factoryAttribute.arguments[0]?.name, "value");
  assert.equal(factoryAttribute.arguments[0]?.type.name, "DOMString");
  assert.equal(options.declarations[0]?.members[0]?.required, true);
  assert.equal(options.declarations[0]?.members[1]?.default?.value, false);
  assert.equal(options.declarations[0]?.members[2]?.default?.kind, "-Infinity");
  assert.equal(callback.declarations[0]?.type?.generic, "Promise");
  assert.equal(callback.declarations[0]?.type?.typeArguments[0]?.name, "DOMString");
  assert.equal(callback.declarations[0]?.arguments[0]?.default?.value, "1");
  assert.deepEqual(mode.declarations[0]?.enumValues, ["one", "two"]);
  assert.equal(transfer.transferable, true);
  assert.deepEqual(scoped.exposures, ["Window"]);
  assert.equal(scoped.secureContext, false);
  assert.deepEqual(scoped.declarations[1]?.exposures, ["Worker"]);
  assert.equal(scoped.declarations[1]?.secureContext, true);
  assert.deepEqual(
    reflected.extendedAttributeDetails[0]?.values,
    ["for"],
  );
  assert.deepEqual(
    result.symbols.map((item) => item.name),
    [
      "Collision",
      "CompletionCallback",
      "FixtureNamespace",
      "Generic",
      "GenericOptions",
      "Helpers",
      "Mode",
      "Named",
      "Reflected",
      "Scoped",
      "Transfer",
      "WindowFixture",
    ],
  );
  assert.equal(result.coverage.includeStatementCount, 2);
  assert.equal(result.coverage.argumentCount, 6);
});

test("strict supplemental conversion preserves IDL constructs and fails closed", () => {
  const supplementalIdl = `
[Exposed=Window, SecureContext]
interface SupplementalFixture : EventTarget {
  constructor(DOMString name, optional sequence<unsigned long> values = []);
  Promise<DOMString> run(optional BufferSource input);
  attribute EventHandler onchange;
  readonly maplike<DOMString, long>;
};
dictionary SupplementalOptions {
  required DOMString name;
  boolean enabled = false;
  record<DOMString, sequence<DOMString>> labels;
};
enum SupplementalMode { "one", "two" };
callback SupplementalCallback = Promise<DOMString> (SupplementalOptions options);
namespace SupplementalConstants {
  readonly attribute unsigned long VALUE;
};
`;
  const generated = generateWebIdlSupplement(
    {
      family: "Fixture",
      specification: "fixture",
      sourceUrl: "https://example.test/fixture/",
      text: supplementalIdl,
      sha256: sha256(supplementalIdl),
    },
    "fixture",
    path.join(directory, "fixture.supplemental.d.ts"),
    sha256,
  );
  assert.match(generated.input.text, /interface SupplementalFixture extends EventTarget/);
  assert.match(generated.input.text, /new\(name: string, values\?: Array<number>\)/);
  assert.match(generated.input.text, /run\(input\?: BufferSource\): Promise<string>/);
  assert.match(generated.input.text, /labels\?: Record<string, Array<string>>/);
  assert.match(generated.input.text, /\[Symbol\.iterator\]\(\): MapIterator/);
  assert.match(generated.input.text, /type SupplementalCallback = \(options: SupplementalOptions\) => Promise<string>/);
  assert.match(generated.input.text, /declare namespace SupplementalConstants/);
  assert.equal(generated.provenance.sourceKind, "webref-idl-generated");
  assert.equal(generated.provenance.output.sha256, sha256(generated.input.text));

  const unsupported = `
[Exposed=Window] interface UnsupportedFixture {
  readonly setlike<DOMString>;
};
`;
  assert.throws(
    () =>
      generateWebIdlSupplement(
        {
          family: "Unsupported",
          specification: "unsupported",
          sourceUrl: "https://example.test/unsupported/",
          text: unsupported,
          sha256: sha256(unsupported),
        },
        "fixture",
        path.join(directory, "unsupported.d.ts"),
        sha256,
      ),
    /Unsupported Web IDL member 'setlike'/,
  );
});

test("WebGPU filtering keeps Window shapes and normalizes semantic namespaces", () => {
  const source = `
interface Config {
  retained?: string;
  omitted?: number;
}
interface ConfigOut extends Required<Omit<Config, "omitted">> {
  own?: boolean;
}
interface Navigator extends NavigatorGPU {}
interface WorkerNavigator extends NavigatorGPU {}
interface GPUFlags {
  readonly READ: number;
}
declare var GPUFlags: GPUFlags;
interface StaticOnly {}
declare var StaticOnly: {
  prototype: StaticOnly;
  new(): never;
};
`;
  const filtered = filterWebGpuWindowSource(source, new Set(["GPUFlags"]));
  assert.doesNotMatch(filtered, /WorkerNavigator/);
  assert.match(filtered, /interface Navigator extends NavigatorGPU/);
  assert.match(filtered, /interface ConfigOut \{\s+retained: string;/);
  assert.match(filtered, /own\?: boolean/);
  assert.doesNotMatch(
    /interface ConfigOut \{(?<body>[\s\S]*?)\}/.exec(filtered)?.groups?.body ?? "",
    /omitted/,
  );
  assert.match(filtered, /declare namespace GPUFlags \{\s+const READ: number;/);
  assert.doesNotMatch(filtered, /declare var GPUFlags/);
  assert.match(filtered, /declare var StaticOnly/);
  assert.doesNotMatch(filtered, /new\(\): never/);
});

test("pinned supplemental closure is Window-scoped and provenance-complete", async () => {
  const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
  const inputs = await loadPinnedInputs(toolRoot);
  const model = buildDomModel(inputs);
  const expected = [
    "Bluetooth",
    "USB",
    "HID",
    "Serial",
    "Presentation",
    "GPU",
  ];
  for (const name of expected) {
    const item = symbol(model.symbols, name);
    assert.equal(item.supplemental, true, name);
    assert.equal(item.semantic.exposedOnWindow, true, name);
  }
  for (const name of ["Bluetooth", "USB", "GPUAdapter"]) {
    const declaration = symbol(model.symbols, name).declarations.find((item) =>
      item.kind === "globalVariable" && item.supplemental
    );
    assert.ok(declaration, `${name} interface object`);
    assert.equal(declaration.constructorObject, false, name);
  }
  const presentationRequestObject = symbol(model.symbols, "PresentationRequest")
    .declarations.find((item) =>
      item.kind === "globalVariable" && item.supplemental
    );
  assert.ok(presentationRequestObject);
  assert.equal(presentationRequestObject.constructorObject, true);
  const navigator = symbol(model.symbols, "Navigator");
  assert.equal(navigator.supplemental, false);
  assert.ok(navigator.declarations.some((item) => item.supplemental));
  const window = symbol(model.symbols, "Window");
  assert.equal(window.supplemental, false);
  assert.deepEqual(
    window.declarations
      .filter((item) => item.supplemental)
      .flatMap((item) => item.members)
      .map((item) => item.name?.text)
      .filter((name) => name?.startsWith("show"))
      .sort(),
    ["showDirectoryPicker", "showOpenFilePicker", "showSaveFilePicker"],
  );
  assert.equal(
    model.symbols.some((item) => item.name === "WorkerNavigator"),
    false,
  );
  assert.equal(inputs.supplementalSources.length, 8);
  assert.deepEqual(
    inputs.supplementalSources.map((item) => item.family),
    [
      "File System Access",
      "Presentation API",
      "Web Serial",
      "Web Bluetooth",
      "Web Bluetooth",
      "WebHID",
      "WebUSB",
      "WebGPU",
    ],
  );
  assert.ok(
    model.symbols
      .flatMap((item) => item.declarations)
      .every((item) =>
        item.location.sourceOrdinal >= 0 &&
        item.supplemental === item.location.supplemental
      ),
  );
});

test("regeneration is byte-identical and reconciliation is explicit", async () => {
  const inputs: InputSet = {
    typescriptVersion: "fixture",
    typescriptFiles: [
      {
        path: declarationPath,
        label: "fixture.d.ts",
        sha256: sha256(declarations),
      },
      {
        path: iterableDeclarationPath,
        label: "fixture.iterable.d.ts",
        sha256: sha256(iterableDeclarations),
      },
    ],
    typescriptAggregateSha256: sha256(
      `fixture.d.ts\0${sha256(declarations)}\n` +
      `fixture.iterable.d.ts\0${sha256(iterableDeclarations)}\n`,
    ),
    supplementalSources: [],
    webrefVersion: "fixture",
    webIdlFiles: [{ name: "fixture", text: idl, sha256: sha256(idl) }],
    webIdlAggregateSha256: sha256(`fixture\0${sha256(idl)}\n`),
    webidl2Version: "fixture",
    overridesPath: "overrides.json",
    overridesSha256: sha256("{}"),
    overrideCount: 0,
  };

  const model = buildDomModel(inputs);
  const regenerated = buildDomModel(inputs);
  assert.deepEqual(
    model.symbols.map(serializeJsonLine),
    regenerated.symbols.map(serializeJsonLine),
  );
  assert.deepEqual(
    model.webIdlSymbols.map(serializeJsonLine),
    regenerated.webIdlSymbols.map(serializeJsonLine),
  );
  assert.equal(stableJson(model.coverage), stableJson(regenerated.coverage));
  assert.ok(model.symbols.every((item) => !serializeJsonLine(item).includes("\n")));
  const streamedPath = path.join(directory, "fixture-symbols.jsonl");
  const expectedHash = hashJsonLines(model.symbols);
  assert.equal(await writeJsonLines(streamedPath, model.symbols), expectedHash);
  assert.equal(await verifyJsonLines(streamedPath, model.symbols), expectedHash);
  assert.equal(
    (await readFile(streamedPath, "utf8")).split("\n").filter(Boolean).length,
    model.symbols.length,
  );

  assert.equal(model.coverage.reconciliation.matched, 7);
  assert.equal(model.coverage.reconciliation.matchedWebIdl, 4);
  assert.ok(model.coverage.reconciliation.unmatchedTypeScript.includes("Primitive"));
  assert.ok(model.coverage.reconciliation.unmatchedWebIdl.includes("Transfer"));
  assert.deepEqual(model.coverage.reconciliation.ambiguousWebIdl, ["Collision"]);
  assert.equal(
    model.coverage.reconciliation.matchedWebIdl +
      model.coverage.reconciliation.unmatchedWebIdl.length +
      model.coverage.reconciliation.ambiguousWebIdl.length,
    model.webIdlSymbols.length,
  );
  assert.equal(model.coverage.reconciliation.ambiguous.length, 1);
  assert.equal(model.coverage.reconciliation.ambiguous[0]?.name, "Collision");
  assert.equal(symbol(model.symbols, "Collision").semantic.status, "ambiguous");
  assert.equal(symbol(model.symbols, "Generic").semantic.exposedOnWorker, true);
  assert.equal(
    symbol(model.symbols, "GenericFactory").semantic.bindingKind,
    "legacyFactoryFunction",
  );
  assert.equal(
    symbol(model.symbols, "LegacyGeneric").semantic.bindingKind,
    "legacyWindowAlias",
  );
  assert.deepEqual(
    symbol(model.symbols, "LegacyGeneric").semantic.exposures,
    ["Window"],
  );
  assert.deepEqual(
    symbol(model.symbols, "LegacyGeneric").semantic.globalNames,
    ["Window"],
  );
  assert.equal(
    symbol(model.symbols, "LegacyGeneric").semantic.exposedOnWorker,
    false,
  );
  assert.equal(
    symbol(model.symbols, "FixtureNamespace.parse").semantic.bindingKind,
    "namespaceMember",
  );
  assert.equal(
    symbol(model.symbols, "FixtureNamespace.parse").semantic.bindings.length,
    2,
  );
  assert.equal(symbol(model.symbols, "alert").semantic.bindingKind, "globalMember");
  assert.equal(symbol(model.symbols, "fetch").semantic.webIdlName, "Named");
});

test("classifies generated values without treating structured clone as JSON", async () => {
  const transportDeclarations = `
interface Blob {}
interface BlobCallback {
  (blob: Blob | null): void;
}
interface ReadableStream<T> {}
interface MessagePort {}
interface Options {
  label: string;
}
interface UnsafeOptions {
  payload: any;
}
interface AmbiguousOptions {
  label: string;
}
declare var AmbiguousOptions: {
  prototype: AmbiguousOptions;
  new(): AmbiguousOptions;
};
type Mode = "one" | "two";
type FixtureBufferSource = ArrayBufferView<ArrayBuffer> | ArrayBuffer;
declare function transportFixture(
  primitive: string,
  options: Options,
  mode: Mode,
  blob: Blob,
  arrayBuffer: ArrayBuffer,
  bytes: Uint8Array<ArrayBuffer>,
  view: ArrayBufferView<ArrayBuffer>,
  sharedBytes: Uint8Array<SharedArrayBuffer>,
  likeBytes: Uint8Array<ArrayBufferLike>,
  defaultBytes: Uint8Array,
  sharedView: ArrayBufferView<ArrayBufferLike>,
  defaultView: ArrayBufferView,
  shared: SharedArrayBuffer,
  source: FixtureBufferSource,
  stream: ReadableStream<Uint8Array<ArrayBuffer>>,
  port: MessagePort,
  dynamic: unknown,
  unsafeOptions: UnsafeOptions,
  ambiguousOptions: AmbiguousOptions,
  bigintValue: bigint,
  invalidRecord: Record<number, string>
): void;
`;
  const transportIdl = `
[Serializable] interface Blob {};
callback BlobCallback = undefined (Blob? blob);
dictionary Options { DOMString label; };
dictionary UnsafeOptions { any payload; };
dictionary AmbiguousOptions { DOMString label; };
enum Mode { "one", "two" };
[Transferable] interface ReadableStream {};
[Transferable] interface MessagePort {};
`;
  const transportPath = path.join(directory, "transport.d.ts");
  await writeFile(transportPath, transportDeclarations, "utf8");
  const model = buildDomModel({
    typescriptVersion: "fixture",
    typescriptFiles: [{
      path: transportPath,
      label: "transport.d.ts",
      sha256: sha256(transportDeclarations),
    }],
    typescriptAggregateSha256: sha256(
      `transport.d.ts\0${sha256(transportDeclarations)}\n`,
    ),
    supplementalSources: [],
    webrefVersion: "fixture",
    webIdlFiles: [{
      name: "transport",
      text: transportIdl,
      sha256: sha256(transportIdl),
    }],
    webIdlAggregateSha256: sha256(
      `transport\0${sha256(transportIdl)}\n`,
    ),
    webidl2Version: "fixture",
    overridesPath: "overrides.json",
    overridesSha256: sha256("{}"),
    overrideCount: 0,
  });

  const callback = symbol(model.symbols, "BlobCallback");
  const callbackParameter =
    callback.declarations[0]?.members[0]?.parameters[0]?.type;
  assert.ok(callbackParameter);
  assert.equal(callbackParameter.kind, "union");
  assert.equal(callbackParameter.transport.kind, "js-reference");
  assert.equal(callbackParameter.transport.nullable, true);
  assert.equal(callbackParameter.transport.streamable, true);
  assert.equal(callbackParameter.transport.structuredClone, true);
  assert.equal(callback.semantic.serializable, false);
  assert.equal(symbol(model.symbols, "Blob").semantic.serializable, true);
  assert.equal(
    webIdlSymbol(model.webIdlSymbols, "BlobCallback")
      .declarations[0]?.arguments[0]?.type.nullable,
    true,
  );

  const parameters = symbol(model.symbols, "transportFixture")
    .declarations[0]?.parameters;
  assert.ok(parameters);
  assert.deepEqual(
    parameters.map((parameter) => parameter.type?.transport.kind),
    [
      "json-value",
      "json-value",
      "json-value",
      "js-reference",
      "binary",
      "binary",
      "binary",
      "binary",
      "binary",
      "binary",
      "binary",
      "binary",
      "binary",
      "binary",
      "transferable",
      "transferable",
      "unsupported",
      "unsupported",
      "unsupported",
      "unsupported",
      "unsupported",
    ],
  );
  const parameter = (name: string) => {
    const result = parameters.find((item) => item.name === name);
    assert.ok(result, `Missing parameter '${name}'.`);
    return result;
  };
  for (const name of ["arrayBuffer", "bytes", "view", "source"]) {
    assert.equal(parameter(name).type?.transport.streamable, true, name);
  }
  for (const name of [
    "sharedBytes",
    "likeBytes",
    "defaultBytes",
    "sharedView",
    "defaultView",
    "shared",
  ]) {
    assert.equal(parameter(name).type?.transport.streamable, false, name);
  }
  assert.match(
    parameter("dynamic").type?.transport.reason ?? "",
    /explicit runtime transport escape hatch/,
  );
  assert.match(
    parameter("unsafeOptions").type?.transport.reason ?? "",
    /dictionary 'UnsafeOptions'.*explicit runtime transport escape hatch/,
  );
  assert.equal(
    symbol(model.symbols, "AmbiguousOptions").semantic.status,
    "ambiguous",
  );
  assert.match(
    parameter("ambiguousOptions").type?.transport.reason ?? "",
    /ambiguous Web IDL semantics/,
  );
  assert.match(
    parameter("bigintValue").type?.transport.reason ?? "",
    /not JSON-compatible/,
  );
  assert.match(
    parameter("invalidRecord").type?.transport.reason ?? "",
    /reviewed string key type/,
  );
});

test("schemas validate nested IR and reject malformed records", async () => {
  const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
  const validators = await loadArtifactValidators(toolRoot);
  const typescript = extractTypeScriptModel(typeScriptInputs());
  const webIdl = extractWebIdlModel([{ name: "fixture", text: idl }]);

  assertValid(
    "valid TypeScript symbol",
    validators.typeScriptSymbol,
    typescript.symbols[0],
  );
  assertValid(
    "valid Web IDL symbol",
    validators.webIdlSymbol,
    webIdl.symbols[0],
  );

  const repositoryRoot = path.resolve(toolRoot, "../..");
  const coverage: unknown = JSON.parse(
    await readFile(path.join(repositoryRoot, "data", "Blazor.DOM", "coverage.json"), "utf8"),
  );
  const manifest: unknown = JSON.parse(
    await readFile(path.join(repositoryRoot, "data", "Blazor.DOM", "manifest.json"), "utf8"),
  );
  const overrides: unknown = JSON.parse(
    await readFile(path.join(toolRoot, "overrides.json"), "utf8"),
  );
  assertValid("valid coverage", validators.coverage, coverage);
  assertValid("valid manifest", validators.manifest, manifest);
  assertValid("valid overrides", validators.overrides, overrides);

  const malformedTypeScript = {
    ...typescript.symbols[0],
    declarations: [42],
    semantic: {
      ...typescript.symbols[0]?.semantic,
      status: 42,
    },
  };
  assert.throws(
    () =>
      assertValid(
        "malformed TypeScript symbol",
        validators.typeScriptSymbol,
        malformedTypeScript,
      ),
    /does not conform/,
  );

  const malformedTypeScriptSet = {
    ...typescript.symbols[0],
    semantic: {
      ...typescript.symbols[0]?.semantic,
      exposures: ["Window", "Window", ""],
    },
  };
  assert.throws(
    () =>
      assertValid(
        "malformed TypeScript set",
        validators.typeScriptSymbol,
        malformedTypeScriptSet,
      ),
    /does not conform/,
  );

  const malformedMatchedTypeScript = {
    ...typescript.symbols[0],
    semantic: {
      ...typescript.symbols[0]?.semantic,
      status: "matched",
      webIdlName: null,
      bindingKind: null,
      bindings: [],
    },
  };
  assert.throws(
    () =>
      assertValid(
        "malformed matched TypeScript symbol",
        validators.typeScriptSymbol,
        malformedMatchedTypeScript,
      ),
    /does not conform/,
  );

  const malformedWebIdl = {
    ...webIdl.symbols[0],
    declarations: ["not a declaration"],
  };
  assert.throws(
    () =>
      assertValid(
        "malformed Web IDL symbol",
        validators.webIdlSymbol,
        malformedWebIdl,
      ),
    /does not conform/,
  );

  const malformedWebIdlSet = {
    ...webIdl.symbols[0],
    extendedAttributes: ["Exposed", "Exposed", ""],
  };
  assert.throws(
    () =>
      assertValid(
        "malformed Web IDL set",
        validators.webIdlSymbol,
        malformedWebIdlSet,
      ),
    /does not conform/,
  );

  assert.ok(typeof coverage === "object" && coverage !== null);
  assert.ok("typescript" in coverage);
  assert.ok(typeof coverage.typescript === "object" && coverage.typescript !== null);
  const malformedCoverage = {
    ...coverage,
    typescript: {
      ...coverage.typescript,
      symbolCount: "1859",
    },
  };
  assert.throws(
    () => assertValid("malformed coverage", validators.coverage, malformedCoverage),
    /does not conform/,
  );

  assert.ok(typeof manifest === "object" && manifest !== null);
  assert.ok("files" in manifest);
  assert.ok(typeof manifest.files === "object" && manifest.files !== null);
  const malformedManifest = {
    ...manifest,
    files: {
      ...manifest.files,
      coverage: {
        path: "coverage.json",
        format: "json",
        schema: "coverage.schema.json",
        records: 1,
        sha256: "not-a-sha256",
      },
    },
  };
  assert.throws(
    () => assertValid("malformed manifest", validators.manifest, malformedManifest),
    /does not conform/,
  );
});

test("canonical input text is independent of checkout line endings", () => {
  const lf = '{\n  "schemaVersion": 1\n}\n';
  assert.equal(normalizeLf(lf.replace(/\n/g, "\r\n")), lf);
  assert.equal(sha256(normalizeLf(lf.replace(/\n/g, "\r\n"))), sha256(lf));
});

function symbol(symbols: SymbolModel[], name: string): SymbolModel {
  const value = symbols.find((item) => item.name === name);
  assert.ok(value, `Missing TypeScript symbol ${name}`);
  return value;
}

function webIdlSymbol(
  symbols: WebIdlSymbolModel[],
  name: string,
): WebIdlSymbolModel {
  const value = symbols.find((item) => item.name === name);
  assert.ok(value, `Missing Web IDL symbol ${name}`);
  return value;
}

function typeScriptInputs(): Array<{ path: string; label: string }> {
  return [
    { path: declarationPath, label: "fixture.d.ts" },
    { path: iterableDeclarationPath, label: "fixture.iterable.d.ts" },
  ];
}
