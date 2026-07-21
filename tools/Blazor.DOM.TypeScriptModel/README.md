# Blazor DOM TypeScript model

This tool builds the checked-in semantic input model for exhaustive DOM interop generation. It runs offline in this repository; it is not loaded by the existing `netstandard2.0` Roslyn generator or by consumer builds.

## Commands

From `tools/Blazor.DOM.TypeScriptModel` with Node.js 24 or later:

```powershell
npm ci
npm test
npm run generate
npm run verify
```

`generate` writes one compact, ordinal-sorted record per line to `data/Blazor.DOM/typescript-symbols.jsonl` and `webidl-symbols.jsonl`, plus `coverage.json` and `manifest.json`. This keeps upstream changes at symbol-level diffs. Coverage partitions Web IDL symbols into matched, ambiguous, and unmatched sets, while retaining the binding-level TypeScript reconciliation records. Both generation and verification validate every nested record against the checked-in JSON Schema contracts. `verify` also compares the JSONL streams record-by-record and fails if any checked-in file differs. Output uses ordinal string ordering, stable source ordinals, LF line endings, no timestamps, and SHA-256 provenance. Text inputs are normalized to LF before hashing so provenance is independent of Git checkout settings.

TypeScript declarations are checked as one program containing the pinned `lib.dom.d.ts`, `lib.dom.iterable.d.ts`, and `lib.dom.asynciterable.d.ts` inputs, their standard-library closure, and the deterministic supplemental closure below. TypeScript remains the authoritative API shape. Every declaration records its source ordinal and supplemental status; symbols are marked supplemental only when all of their declarations come from supplemental inputs. Declaration merging is performed by the TypeScript checker rather than by name-based text deduplication.

Web Bluetooth, WebUSB, WebHID, Web Serial, and Presentation API declarations are generated at run time from the exact pinned WebRef IDL. The converter accepts only reviewed IDL definitions, members, generics, numeric/string primitives, nullability, iterables/maplikes, constructors, mixins/includes, callbacks, dictionaries, enums, and namespaces; any unsupported semantic form fails generation. Worker-only navigator augmentations are omitted from the Window declaration program while their exposure metadata remains in the complete Web IDL IR. `@webgpu/types` supplies the spec-editor-maintained WebGPU shape. Its WorkerNavigator-only augmentation and non-WebIDL `new(): never` signatures are removed, Web IDL namespaces are normalized from equivalent interface/value declaration pairs, and finite `Required<Omit<T, K>>` helper heritage is expanded without widening. Prototype/static interface objects remain available as statics contracts; only interfaces with actual Web IDL constructors become factories.

Web IDL is independently normalized to typed emitter inputs: classifications, inheritance/includes, declaration- and member-scoped exposure and extended attributes, dictionaries and required/default fields, enums, callbacks, operations, attributes, arguments, and IDL type expressions. Raw WebIDL2 AST blobs are not retained.

Every TypeScript type expression also carries transport metadata for generated code: `json-value`, `js-reference`, `js-stream`, `binary`, `transferable`, or `unsupported`, plus nullability, structured-clone, and byte-stream convenience flags. Named Web IDL interfaces remain live references even when marked `[Serializable]`; the attribute records structured-clone support and never grants JSON compatibility. Web IDL dictionaries are JSON only when every nested member is a reviewed JSON transport. For example, `BlobCallback` is proven as a nullable `Blob` JS reference, while `Blob.arrayBuffer()`, `Blob.bytes()`, and explicitly ArrayBuffer-backed `ArrayBufferView`/`BufferSource` values are eligible for bounded stream consumption. Omitted/default `ArrayBufferLike` backing stores and explicit `SharedArrayBuffer` shapes remain binary but are not marked streamable because the official JS stream-reference API rejects shared buffers. `any`, `unknown`, `object`, non-string record keys, ambiguous Web IDL matches, and incompatible unions remain unsupported with a precise reason instead of falling back to JSON.

To update the inputs, change only exact versions in `package.json`, run `npm install` to refresh `package-lock.json`, then run the commands above. Review the manifest counts and the explicit unmatched/ambiguous reconciliation lists as a baseline change. Unsupported TypeScript or Web IDL AST forms fail generation. Do not add an override until its typed handler exists; every override must include a rationale and should cite a specification or upstream issue.

## Inputs and licenses

| Input | Pinned version | Purpose | License |
|---|---:|---|---|
| [`typescript`](https://www.npmjs.com/package/typescript) | 5.9.3 | Official compiler API, type checker, and the DOM/iterable declaration closure | Apache-2.0 |
| [`@webref/idl`](https://www.npmjs.com/package/@webref/idl) | 3.81.3 | Web-platform IDL corpus | MIT |
| [`@webgpu/types`](https://www.npmjs.com/package/@webgpu/types) | 0.1.71 | WebGPU declarations maintained by GPU for the Web editors | BSD-3-Clause |
| [`webidl2`](https://www.npmjs.com/package/webidl2) | 24.5.0 | Official Web IDL parser used by WebRef | W3C |
| [`ajv`](https://www.npmjs.com/package/ajv) | 8.17.1 | Draft 2020-12 validation for generated and checked-in IR | MIT |

### Supplemental standards

| Family | Exact declaration source | Standard source |
|---|---|---|
| Web Bluetooth | `@webref/idl` 3.81.3: `web-bluetooth.idl`, `web-bluetooth-scanning.idl` | [Web Bluetooth](https://webbluetoothcg.github.io/web-bluetooth/) |
| WebUSB | `@webref/idl` 3.81.3: `webusb.idl` | [WebUSB](https://wicg.github.io/webusb/) |
| WebHID | `@webref/idl` 3.81.3: `webhid.idl` | [WebHID](https://wicg.github.io/webhid/) |
| Web Serial | `@webref/idl` 3.81.3: `serial.idl` | [Web Serial](https://wicg.github.io/serial/) |
| Presentation API | `@webref/idl` 3.81.3: `presentation-api.idl` | [Presentation API](https://www.w3.org/TR/presentation-api/) |
| WebGPU | `@webgpu/types` 0.1.71: `dist/index.d.ts` | [GPU for the Web types](https://github.com/gpuweb/types) |

The manifest records every supplemental input and generated-output SHA-256, source URL, package/version/license, and generation method. The package lock records resolved npm artifacts. Apache-2.0, MIT, W3C, and BSD-3-Clause inputs permit this deterministic build and redistribution; the installed packages retain their license files and generated provenance remains attached to the checked-in IR. No third-party declaration file is copied into this repository. The existing embedded `src/Blazor.SourceGenerators/Data/lib.dom.d.ts` remains a separate legacy generator input and is not rewritten by this tool.
