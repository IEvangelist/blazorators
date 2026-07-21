import { readFile } from "node:fs/promises";
import path from "node:path";
import { createRequire } from "node:module";
import { listAll } from "@webref/idl";
import { InputSet } from "./schema.js";
import { compareOrdinal, normalizeLf, sha256 } from "./stable-json.js";
import {
  filterWebGpuWindowSource,
  generateWebIdlSupplement,
  SupplementalBuildResult,
  WebIdlSupplementalSource,
  webIdlWindowConstructors,
  webIdlWindowNamespaces,
} from "./supplemental.js";

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
  const supplemental = await loadSupplementalInputs(
    toolRoot,
    webref.version,
    webIdlFiles,
  );
  const allTypeScriptFiles = [
    ...typescriptFiles,
    ...supplemental.inputs,
  ];
  const overridesFile = path.join(toolRoot, "overrides.json");
  const overridesText = normalizeLf(await readFile(overridesFile, "utf8"));
  const overrideCount = validateOverrides(overridesText);

  return {
    typescriptVersion: typescript.version,
    typescriptFiles: allTypeScriptFiles,
    typescriptAggregateSha256: sha256(
      typescriptFiles.map((file) => `${file.label}\0${file.sha256}\n`).join(""),
    ),
    supplementalSources: supplemental.provenance,
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

const webIdlSupplementalSources: ReadonlyArray<
  Omit<WebIdlSupplementalSource, "text" | "sha256">
> = [
  {
    family: "File System Access",
    specification: "file-system-access",
    sourceUrl: "https://fs.spec.whatwg.org/",
  },
  {
    family: "Presentation API",
    specification: "presentation-api",
    sourceUrl: "https://www.w3.org/TR/presentation-api/",
  },
  {
    family: "Web Serial",
    specification: "serial",
    sourceUrl: "https://wicg.github.io/serial/",
  },
  {
    family: "Web Bluetooth",
    specification: "web-bluetooth",
    sourceUrl: "https://webbluetoothcg.github.io/web-bluetooth/",
  },
  {
    family: "Web Bluetooth",
    specification: "web-bluetooth-scanning",
    sourceUrl: "https://webbluetoothcg.github.io/web-bluetooth/scanning.html",
  },
  {
    family: "WebHID",
    specification: "webhid",
    sourceUrl: "https://wicg.github.io/webhid/",
  },
  {
    family: "WebUSB",
    specification: "webusb",
    sourceUrl: "https://wicg.github.io/webusb/",
  },
];

async function loadSupplementalInputs(
  toolRoot: string,
  webrefVersion: string,
  webIdlFiles: InputSet["webIdlFiles"],
): Promise<SupplementalBuildResult> {
  const filesByName = new Map(webIdlFiles.map((file) => [file.name, file]));
  const generated = webIdlSupplementalSources.map((source) => {
    const file = filesByName.get(source.specification);
    if (file === undefined) {
      throw new Error(
        `Pinned @webref/idl is missing '${source.specification}.idl'.`,
      );
    }
    return generateWebIdlSupplement(
      { ...source, text: file.text, sha256: file.sha256 },
      webrefVersion,
      path.join(toolRoot, ".virtual", `${source.specification}.d.ts`),
      sha256,
    );
  });

  const webgpuDirectory = path.join(
    toolRoot,
    "node_modules",
    "@webgpu",
    "types",
  );
  const webgpu = await packageInfoFromDirectory(webgpuDirectory, "@webgpu/types");
  if (webgpu.license !== "BSD-3-Clause") {
    throw new Error(
      `Unexpected @webgpu/types license '${webgpu.license ?? "(missing)"}'.`,
    );
  }
  const webgpuPath = path.join(webgpuDirectory, "dist", "index.d.ts");
  const webgpuSource = normalizeLf(await readFile(webgpuPath, "utf8"));
  const webgpuIdl = filesByName.get("webgpu");
  if (webgpuIdl === undefined) {
    throw new Error("Pinned @webref/idl is missing 'webgpu.idl'.");
  }
  const webgpuText = filterWebGpuWindowSource(
    webgpuSource,
    webIdlWindowNamespaces(webgpuIdl.text),
    webIdlWindowConstructors(webgpuIdl.text),
  );
  const webgpuSourceHash = sha256(webgpuSource);
  const webgpuOutputHash = sha256(webgpuText);
  const webgpuLabel = "supplemental/@webgpu/types/dist/index.window.d.ts";

  return {
    inputs: [
      ...generated.map((item) => item.input),
      {
        path: path.join(toolRoot, ".virtual", "webgpu.window.d.ts"),
        label: webgpuLabel,
        text: webgpuText,
        sha256: webgpuOutputHash,
        supplemental: true,
      },
    ],
    provenance: [
      ...generated.map((item) => item.provenance),
      {
        family: "WebGPU",
        sourceKind: "package-declaration",
        package: "@webgpu/types",
        version: webgpu.version,
        license: webgpu.license,
        sourceUrl: "https://github.com/gpuweb/types",
        inputs: [{
          name: "@webgpu/types/dist/index.d.ts",
          sha256: webgpuSourceHash,
        }],
        output: { name: webgpuLabel, sha256: webgpuOutputHash },
        generationMethod:
          "Exact package declaration filtered to Window exposure; non-WebIDL new(): never signatures are removed, Web IDL namespaces are normalized from interface/value pairs, and finite Required/Omit mapped heritage is flattened without widening.",
      },
    ],
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

async function packageInfoFromDirectory(
  directory: string,
  packageName: string,
): Promise<{ version: string; license: string }> {
  const text = await readFile(path.join(directory, "package.json"), "utf8");
  const value: unknown = JSON.parse(text);
  if (
    typeof value !== "object" ||
    value === null ||
    !("name" in value) ||
    value.name !== packageName ||
    !("version" in value) ||
    typeof value.version !== "string" ||
    !("license" in value) ||
    typeof value.license !== "string"
  ) {
    throw new Error(`Malformed package metadata for '${packageName}'.`);
  }
  return { version: value.version, license: value.license };
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
