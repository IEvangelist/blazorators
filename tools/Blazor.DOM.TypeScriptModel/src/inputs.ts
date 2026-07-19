import { readFile } from "node:fs/promises";
import path from "node:path";
import { createRequire } from "node:module";
import { listAll } from "@webref/idl";
import { InputSet } from "./schema.js";
import { compareOrdinal, normalizeLf, sha256 } from "./stable-json.js";

const require = createRequire(import.meta.url);

export async function loadPinnedInputs(toolRoot: string): Promise<InputSet> {
  const typescript = await packageInfo("typescript");
  const webref = await packageInfo("@webref/idl");
  const webidl2 = await packageInfo("webidl2");
  const typescriptLibraryDirectory = path.dirname(require.resolve("typescript"));
  const typescriptFiles = await Promise.all(
    ["lib.dom.d.ts", "lib.dom.iterable.d.ts", "lib.dom.asynciterable.d.ts"].map(
      async (name) => {
        const filePath = path.join(typescriptLibraryDirectory, name);
        const text = await readFile(filePath, "utf8");
        return {
          path: filePath,
          label: `typescript/lib/${name}`,
          sha256: sha256(text),
        };
      },
    ),
  );
  const idlFiles = await listAll();
  const webIdlFiles = await Promise.all(
    Object.entries(idlFiles)
      .sort(([left], [right]) => compareOrdinal(left, right))
      .map(async ([name, file]) => {
        const text = await file.text();
        return { name, text, sha256: sha256(text) };
      }),
  );
  const overridesFile = path.join(toolRoot, "overrides.json");
  const overridesText = normalizeLf(await readFile(overridesFile, "utf8"));
  const overrideCount = validateOverrides(overridesText);

  return {
    typescriptVersion: typescript.version,
    typescriptFiles,
    typescriptAggregateSha256: sha256(
      typescriptFiles.map((file) => `${file.label}\0${file.sha256}\n`).join(""),
    ),
    webrefVersion: webref.version,
    webIdlFiles,
    webIdlAggregateSha256: sha256(
      webIdlFiles.map((file) => `${file.name}\0${file.sha256}\n`).join(""),
    ),
    webidl2Version: webidl2.version,
    overridesPath: "tools/Blazor.DOM.TypeScriptModel/overrides.json",
    overridesSha256: sha256(overridesText),
    overrideCount,
  };
}

async function packageInfo(packageName: string): Promise<{ version: string }> {
  let directory = path.dirname(require.resolve(packageName));
  while (true) {
    const packagePath = path.join(directory, "package.json");
    try {
      const text = await readFile(packagePath, "utf8");
      const value: unknown = JSON.parse(text);
      if (
        typeof value === "object" &&
        value !== null &&
        "name" in value &&
        value.name === packageName &&
        "version" in value &&
        typeof value.version === "string"
      ) {
        return { version: value.version };
      }
    } catch (error: unknown) {
      if (!isFileNotFound(error)) {
        throw error;
      }
    }

    const parent = path.dirname(directory);
    if (parent === directory) {
      throw new Error(`Could not locate package.json for '${packageName}'.`);
    }
    directory = parent;
  }
}

function validateOverrides(text: string): number {
  const value: unknown = JSON.parse(text);
  if (
    typeof value !== "object" ||
    value === null ||
    !("schemaVersion" in value) ||
    value.schemaVersion !== 1 ||
    !("overrides" in value) ||
    !Array.isArray(value.overrides)
  ) {
    throw new Error("overrides.json does not conform to override schema version 1.");
  }

  for (const [index, override] of value.overrides.entries()) {
    if (
      typeof override !== "object" ||
      override === null ||
      !("rationale" in override) ||
      typeof override.rationale !== "string" ||
      override.rationale.trim().length === 0
    ) {
      throw new Error(`Override ${index} must have a non-empty rationale.`);
    }
  }
  if (value.overrides.length > 0) {
    throw new Error(
      "Override entries exist but no override handlers are implemented; " +
      "add a typed handler before adding the override.",
    );
  }
  return value.overrides.length;
}

function isFileNotFound(error: unknown): boolean {
  return typeof error === "object" &&
    error !== null &&
    "code" in error &&
    error.code === "ENOENT";
}
