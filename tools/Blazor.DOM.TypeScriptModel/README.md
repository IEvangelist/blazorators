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

TypeScript declarations are checked as one program containing the pinned `lib.dom.d.ts`, `lib.dom.iterable.d.ts`, and `lib.dom.asynciterable.d.ts` inputs plus their standard-library closure. They remain the authoritative API shape. Web IDL is normalized to typed emitter inputs: classifications, inheritance/includes, declaration- and member-scoped exposure and extended attributes, dictionaries and required/default fields, enums, callbacks, operations, attributes, arguments, and IDL type expressions. Raw WebIDL2 AST blobs are not retained.

Every TypeScript type expression also carries transport metadata for generated code: `json-value`, `js-reference`, `js-stream`, `binary`, `transferable`, or `unsupported`, plus nullability, structured-clone, and byte-stream convenience flags. Named Web IDL interfaces remain live references even when marked `[Serializable]`; the attribute records structured-clone support and never grants JSON compatibility. Web IDL dictionaries are JSON only when every nested member is a reviewed JSON transport. For example, `BlobCallback` is proven as a nullable `Blob` JS reference, while `Blob.arrayBuffer()`, `Blob.bytes()`, and explicitly ArrayBuffer-backed `ArrayBufferView`/`BufferSource` values are eligible for bounded stream consumption. Omitted/default `ArrayBufferLike` backing stores and explicit `SharedArrayBuffer` shapes remain binary but are not marked streamable because the official JS stream-reference API rejects shared buffers. `any`, `unknown`, `object`, non-string record keys, ambiguous Web IDL matches, and incompatible unions remain unsupported with a precise reason instead of falling back to JSON.

To update the inputs, change only exact versions in `package.json`, run `npm install` to refresh `package-lock.json`, then run the commands above. Review the manifest counts and the explicit unmatched/ambiguous reconciliation lists as a baseline change. Unsupported TypeScript or Web IDL AST forms fail generation. Do not add an override until its typed handler exists; every override must include a rationale and should cite a specification or upstream issue.

## Inputs and licenses

| Input | Pinned version | Purpose | License |
|---|---:|---|---|
| [`typescript`](https://www.npmjs.com/package/typescript) | 5.9.3 | Official compiler API, type checker, and the DOM/iterable declaration closure | Apache-2.0 |
| [`@webref/idl`](https://www.npmjs.com/package/@webref/idl) | 3.81.3 | Web-platform IDL corpus | MIT |
| [`webidl2`](https://www.npmjs.com/package/webidl2) | 24.5.0 | Official Web IDL parser used by WebRef | W3C |
| [`ajv`](https://www.npmjs.com/package/ajv) | 8.17.1 | Draft 2020-12 validation for generated and checked-in IR | MIT |

The model records the package versions, every WebRef IDL file hash, each TypeScript DOM input hash plus its aggregate hash, the override hash, and package license identifiers. The package lock records the resolved npm artifacts. The existing embedded `src/Blazor.SourceGenerators/Data/lib.dom.d.ts` remains a separate legacy generator input and is not rewritten by this tool.
