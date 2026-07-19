import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { loadPinnedInputs } from "./inputs.js";
import { buildDomModel } from "./model.js";
import {
  hashJsonLines,
  verifyJsonLines,
  writeJsonLines,
} from "./output.js";
import { stableJson, sha256 } from "./stable-json.js";
import { assertValid, loadArtifactValidators } from "./validation.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const repositoryRoot = path.resolve(toolRoot, "../..");
const outputDirectory = path.join(repositoryRoot, "data", "Blazor.DOM");
const typeScriptPath = path.join(outputDirectory, "typescript-symbols.jsonl");
const webIdlPath = path.join(outputDirectory, "webidl-symbols.jsonl");
const coveragePath = path.join(outputDirectory, "coverage.json");
const manifestPath = path.join(outputDirectory, "manifest.json");
const legacyModelPath = path.join(outputDirectory, "dom-model.json");

const command = process.argv[2];
if (command !== "generate" && command !== "verify") {
  throw new Error("Usage: node dist/src/cli.js <generate|verify>");
}

const validators = await loadArtifactValidators(toolRoot);
const overridesDocument: unknown = JSON.parse(
  await readFile(path.join(toolRoot, "overrides.json"), "utf8"),
);
assertValid("Overrides", validators.overrides, overridesDocument);
const inputs = await loadPinnedInputs(toolRoot);
const model = buildDomModel(inputs);
for (const [ordinal, symbol] of model.symbols.entries()) {
  assertValid(`TypeScript symbol ${ordinal}`, validators.typeScriptSymbol, symbol);
}
for (const [ordinal, symbol] of model.webIdlSymbols.entries()) {
  assertValid(`Web IDL symbol ${ordinal}`, validators.webIdlSymbol, symbol);
}
const coverageDocument = {
  $schema: "../../tools/Blazor.DOM.TypeScriptModel/schema/coverage.schema.json",
  schemaVersion: model.schemaVersion,
  ...model.coverage,
};
assertValid("Coverage", validators.coverage, coverageDocument);
const coverageText = stableJson(coverageDocument);

if (command === "generate") {
  await mkdir(outputDirectory, { recursive: true });
  const [typeScriptHash, webIdlHash] = await Promise.all([
    writeJsonLines(typeScriptPath, model.symbols),
    writeJsonLines(webIdlPath, model.webIdlSymbols),
  ]);
  await writeFile(coveragePath, coverageText, "utf8");
  const manifestDocument = manifest(
    model,
    typeScriptHash,
    webIdlHash,
    sha256(coverageText),
  );
  assertValid("Manifest", validators.manifest, manifestDocument);
  const manifestText = stableJson(manifestDocument);
  await writeFile(manifestPath, manifestText, "utf8");
  await rm(legacyModelPath, { force: true });
  console.log(
    `Generated ${model.symbols.length} TypeScript and ` +
    `${model.webIdlSymbols.length} Web IDL symbol records.`,
  );
} else {
  const typeScriptHash = hashJsonLines(model.symbols);
  const webIdlHash = hashJsonLines(model.webIdlSymbols);
  const manifestDocument = manifest(
    model,
    typeScriptHash,
    webIdlHash,
    sha256(coverageText),
  );
  assertValid("Manifest", validators.manifest, manifestDocument);
  const manifestText = stableJson(manifestDocument);
  const [, , expectedCoverage, expectedManifest] = await Promise.all([
    verifyJsonLines(typeScriptPath, model.symbols, validators.typeScriptSymbol),
    verifyJsonLines(webIdlPath, model.webIdlSymbols, validators.webIdlSymbol),
    readFile(coveragePath, "utf8"),
    readFile(manifestPath, "utf8"),
  ]);
  const actualCoverage: unknown = JSON.parse(expectedCoverage);
  const actualManifest: unknown = JSON.parse(expectedManifest);
  assertValid("Checked-in coverage", validators.coverage, actualCoverage);
  assertValid("Checked-in manifest", validators.manifest, actualManifest);
  const differences = [
    expectedCoverage === coverageText
      ? null
      : path.relative(repositoryRoot, coveragePath),
    expectedManifest === manifestText
      ? null
      : path.relative(repositoryRoot, manifestPath),
  ].filter((value): value is string => value !== null);
  if (differences.length > 0) {
    throw new Error(
      `Generated model is stale: ${differences.join(", ")}. Run npm run generate.`,
    );
  }
  console.log(
    `Verified TypeScript ${typeScriptHash} and Web IDL ${webIdlHash}.`,
  );
}

function manifest(
  model: ReturnType<typeof buildDomModel>,
  typeScriptHash: string,
  webIdlHash: string,
  coverageHash: string,
): unknown {
  return {
    $schema: "../../tools/Blazor.DOM.TypeScriptModel/schema/manifest.schema.json",
    schemaVersion: model.schemaVersion,
    generationProfile: model.generationProfile,
    files: {
      typescriptSymbols: {
        path: "typescript-symbols.jsonl",
        format: "jsonl",
        schema:
          "../../tools/Blazor.DOM.TypeScriptModel/schema/typescript-symbol.schema.json",
        records: model.symbols.length,
        sha256: typeScriptHash,
      },
      webIdlSymbols: {
        path: "webidl-symbols.jsonl",
        format: "jsonl",
        schema:
          "../../tools/Blazor.DOM.TypeScriptModel/schema/webidl-symbol.schema.json",
        records: model.webIdlSymbols.length,
        sha256: webIdlHash,
      },
      coverage: {
        path: "coverage.json",
        format: "json",
        schema:
          "../../tools/Blazor.DOM.TypeScriptModel/schema/coverage.schema.json",
        records: 1,
        sha256: coverageHash,
      },
    },
    counts: {
      typescriptSymbols: model.coverage.typescript.symbolCount,
      typescriptDeclarations: model.coverage.typescript.declarationCount,
      typescriptMembers: model.coverage.typescript.memberCount,
      webIdlSpecifications: model.coverage.webIdl.specificationCount,
      webIdlSymbols: model.coverage.webIdl.canonicalSymbolCount,
      webIdlMembers: model.coverage.webIdl.memberCount,
      webIdlArguments: model.coverage.webIdl.argumentCount,
      reconciledSymbols: model.coverage.reconciliation.matched,
      reconciledWebIdlSymbols: model.coverage.reconciliation.matchedWebIdl,
      unmatchedTypeScriptSymbols:
        model.coverage.reconciliation.unmatchedTypeScript.length,
      unmatchedWebIdlSymbols:
        model.coverage.reconciliation.unmatchedWebIdl.length,
      ambiguousSymbols: model.coverage.reconciliation.ambiguous.length,
      ambiguousWebIdlSymbols:
        model.coverage.reconciliation.ambiguousWebIdl.length,
      unsupportedShapes: model.coverage.unsupported.length,
    },
    provenance: model.provenance,
  };
}
