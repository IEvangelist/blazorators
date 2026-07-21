import {
  DomModel,
  InputSet,
  MODEL_SCHEMA_VERSION,
  SemanticModel,
  SymbolModel,
  WebIdlBindingKind,
  WebIdlBindingModel,
  WebIdlDeclarationModel,
  WebIdlMemberModel,
  WebIdlSymbolModel,
} from "./schema.js";
import { compareOrdinal } from "./stable-json.js";
import { extractTypeScriptModel } from "./typescript-model.js";
import { extractWebIdlModel } from "./webidl-model.js";
import { classifyTransports } from "./transport.js";

export function buildDomModel(inputs: InputSet): DomModel {
  const typeScript = extractTypeScriptModel(
    inputs.typescriptFiles.map((file) => ({
      path: file.path,
      label: file.label,
      ...(file.text === undefined ? {} : { text: file.text }),
      ...(file.supplemental === undefined
        ? {}
        : { supplemental: file.supplemental }),
    })),
  );
  const webIdl = extractWebIdlModel(inputs.webIdlFiles);
  const webIdlCandidates = buildWebIdlCandidateMap(webIdl.symbols);
  const matchedWebIdlNames = new Set<string>();
  const ambiguousWebIdlNames = new Set<string>();
  const ambiguous: DomModel["coverage"]["reconciliation"]["ambiguous"] = [];
  const unmatchedTypeScript: string[] = [];
  let matched = 0;

  for (const symbol of typeScript.symbols) {
    const candidates = webIdlCandidates.get(symbol.name) ?? [];
    if (candidates.length === 0) {
      unmatchedTypeScript.push(symbol.name);
      continue;
    }
    const compatible = candidates.filter((candidate) =>
      candidate.symbol.classifications.length === 1 &&
      isCompatible(symbol, candidate)
    );
    if (
      candidates.length !== 1 ||
      compatible.length !== 1 ||
      compatible[0]?.symbol.classifications.length !== 1
    ) {
      symbol.semantic = semantic("ambiguous", compatible[0] ?? candidates[0]!);
      const candidateSymbols = candidates.map((candidate) => candidate.symbol);
      candidateSymbols.forEach((candidate) =>
        ambiguousWebIdlNames.add(candidate.name)
      );
      ambiguous.push({
        name: symbol.name,
        webIdlNames: unique(candidateSymbols.map((candidate) => candidate.name)),
        classifications: unique(candidateSymbols.flatMap((candidate) =>
          candidate.classifications
        )),
        reason: candidates.length > 1
          ? "multiple Web IDL candidates"
          : candidates[0]!.symbol.classifications.length > 1
          ? "multiple Web IDL classifications"
          : "TypeScript declaration shape is incompatible with the Web IDL classification",
      });
      continue;
    }
    symbol.semantic = semantic("matched", compatible[0]!);
    matchedWebIdlNames.add(compatible[0]!.symbol.name);
    matched++;
  }

  classifyTransports(typeScript.symbols);

  return {
    schemaVersion: MODEL_SCHEMA_VERSION,
    generationProfile: {
      name: "Window",
      includedExposures: ["Window"],
      preservesAllExposureMetadata: true,
    },
    provenance: {
      generator: {
        name: "@blazorators/dom-typescript-model",
        version: "0.1.0",
      },
      typescript: {
        package: "typescript",
        version: inputs.typescriptVersion,
        license: "Apache-2.0",
        aggregateSha256: inputs.typescriptAggregateSha256,
        inputs: inputs.typescriptFiles.map((file) => ({
          name: file.label,
          sha256: file.sha256,
        })).filter((_, index) => !inputs.typescriptFiles[index]!.supplemental),
      },
      webref: {
        package: "@webref/idl",
        version: inputs.webrefVersion,
        license: "MIT",
        aggregateSha256: inputs.webIdlAggregateSha256,
        inputs: inputs.webIdlFiles.map((file) => ({
          name: `${file.name}.idl`,
          sha256: file.sha256,
        })),
      },
      webidl2: {
        package: "webidl2",
        version: inputs.webidl2Version,
        license: "W3C",
      },
      supplementalTypeScript: inputs.supplementalSources,
      overrides: {
        input: inputs.overridesPath,
        sha256: inputs.overridesSha256,
        appliedCount: inputs.overrideCount,
      },
    },
    symbols: typeScript.symbols,
    webIdlSymbols: webIdl.symbols,
    coverage: {
      typescript: typeScript.coverage,
      webIdl: webIdl.coverage,
      reconciliation: {
        matched,
        matchedWebIdl: matchedWebIdlNames.size,
        unmatchedTypeScript,
        unmatchedWebIdl: webIdl.symbols
          .filter((symbol) =>
            !matchedWebIdlNames.has(symbol.name) &&
            !ambiguousWebIdlNames.has(symbol.name)
          )
          .map((symbol) => symbol.name),
        ambiguousWebIdl: webIdl.symbols
          .filter((symbol) =>
            !matchedWebIdlNames.has(symbol.name) &&
            ambiguousWebIdlNames.has(symbol.name)
          )
          .map((symbol) => symbol.name),
        ambiguous,
      },
      unsupported: [],
    },
  };
}

interface WebIdlCandidate {
  bindingKind: WebIdlBindingKind;
  symbol: WebIdlSymbolModel;
  memberName: string | null;
  memberKinds: string[];
  extendedAttributes: string[];
  bindings: WebIdlBindingModel[];
}

function buildWebIdlCandidateMap(
  symbols: WebIdlSymbolModel[],
): Map<string, WebIdlCandidate[]> {
  const candidates = new Map<string, WebIdlCandidate[]>();
  const symbolsByName = new Map(symbols.map((symbol) => [symbol.name, symbol]));

  for (const symbol of symbols) {
    addCandidate(
      candidates,
      symbol.name,
      candidate(
        "definition",
        symbol,
        null,
        [],
        symbol.extendedAttributes,
        symbol.declarations.map((declaration) =>
          declarationBinding("definition", symbol, declaration)
        ),
      ),
    );

    for (const declaration of symbol.declarations) {
      for (const attribute of declaration.extendedAttributes) {
        if (attribute.name === "LegacyNamespace") {
          const binding = declarationBinding(
            "legacyNamespace",
            symbol,
            declaration,
          );
          for (const namespace of attribute.values) {
            addCandidate(
              candidates,
              `${namespace}.${symbol.name}`,
              candidate(
                "legacyNamespace",
                symbol,
                null,
                [],
                symbol.extendedAttributes,
                [binding],
              ),
            );
          }
        } else if (attribute.name === "LegacyWindowAlias") {
          const binding = declarationBinding(
            "legacyWindowAlias",
            symbol,
            declaration,
          );
          for (const alias of attribute.values) {
            addCandidate(
              candidates,
              alias,
              candidate(
                "legacyWindowAlias",
                symbol,
                null,
                [],
                symbol.extendedAttributes,
                [binding],
              ),
            );
          }
        } else if (attribute.name === "LegacyFactoryFunction") {
          const binding = declarationBinding(
            "legacyFactoryFunction",
            symbol,
            declaration,
          );
          for (const factory of attribute.values) {
            addCandidate(
              candidates,
              factory,
              candidate(
                "legacyFactoryFunction",
                symbol,
                null,
                [],
                symbol.extendedAttributes,
                [binding],
              ),
            );
          }
        }
      }

      if (symbol.classifications.includes("namespace")) {
        for (const member of declaration.members) {
          if (member.name !== null) {
            addCandidate(
              candidates,
              `${symbol.name}.${member.name}`,
              memberCandidate(
                "namespaceMember",
                symbol,
                declaration,
                member,
                symbol,
              ),
            );
          }
        }
      }
    }
  }

  const globalCandidates = new Map<string, WebIdlCandidate[]>();
  for (const owner of symbols.filter((symbol) => symbol.globalNames.length > 0)) {
    addGlobalMembers(
      globalCandidates,
      symbolsByName,
      owner,
      owner,
      new Set<string>(),
    );
  }
  for (const [name, values] of globalCandidates) {
    for (const value of values) {
      if (
        value.bindings.some((binding) =>
          binding.exposures.includes("Window") ||
          binding.exposures.includes("*") ||
          binding.globalNames.includes("Window")
        )
      ) {
        addCandidate(candidates, name, value);
      }
    }
  }

  return candidates;
}

function addGlobalMembers(
  candidates: Map<string, WebIdlCandidate[]>,
  symbolsByName: ReadonlyMap<string, WebIdlSymbolModel>,
  owner: WebIdlSymbolModel,
  current: WebIdlSymbolModel,
  visited: Set<string>,
): void {
  if (visited.has(current.name)) {
    return;
  }
  visited.add(current.name);

  for (const declaration of current.declarations) {
    for (const member of declaration.members) {
      if (member.name !== null) {
        addCandidate(
          candidates,
          member.name,
          memberCandidate(
            "globalMember",
            current,
            declaration,
            member,
            owner,
          ),
        );
      }
    }
  }

  for (const relatedName of [...current.inheritance, ...current.includedMixins]) {
    const related = symbolsByName.get(relatedName);
    if (related === undefined) {
      throw new Error(
        `Unresolved global Web IDL relationship '${current.name}' -> '${relatedName}'.`,
      );
    }
    addGlobalMembers(candidates, symbolsByName, owner, related, visited);
  }
}

function memberCandidate(
  kind: "namespaceMember" | "globalMember",
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
  member: WebIdlMemberModel,
  owner: WebIdlSymbolModel,
): WebIdlCandidate {
  return candidate(
    kind,
    symbol,
    member.name,
    [member.kind],
    unique([
      ...declaration.extendedAttributes.map((attribute) => attribute.name),
      ...member.extendedAttributes.map((attribute) => attribute.name),
    ]),
    [memberBinding(kind, symbol, declaration, member, owner)],
  );
}

function candidate(
  bindingKind: WebIdlBindingKind,
  symbol: WebIdlSymbolModel,
  memberName: string | null,
  memberKinds: string[],
  extendedAttributes: string[],
  bindings: WebIdlBindingModel[],
): WebIdlCandidate {
  return {
    bindingKind,
    symbol,
    memberName,
    memberKinds: unique(memberKinds),
    extendedAttributes: unique(extendedAttributes),
    bindings,
  };
}

function addCandidate(
  candidates: Map<string, WebIdlCandidate[]>,
  name: string,
  value: WebIdlCandidate,
): void {
  const existing = candidates.get(name) ?? [];
  const match = existing.find((item) =>
    item.bindingKind === value.bindingKind &&
    item.symbol.name === value.symbol.name &&
    item.memberName === value.memberName
  );
  if (match === undefined) {
    existing.push(value);
    existing.sort((left, right) =>
      compareOrdinal(candidateKey(left), candidateKey(right))
    );
    candidates.set(name, existing);
    return;
  }

  match.memberKinds = unique([...match.memberKinds, ...value.memberKinds]);
  match.extendedAttributes = unique([
    ...match.extendedAttributes,
    ...value.extendedAttributes,
  ]);
  const bindingKeys = new Set(match.bindings.map(bindingKey));
  for (const binding of value.bindings) {
    const key = bindingKey(binding);
    if (!bindingKeys.has(key)) {
      match.bindings.push(binding);
      bindingKeys.add(key);
    }
  }
  match.bindings.sort((left, right) =>
    compareOrdinal(bindingKey(left), bindingKey(right))
  );
}

function candidateKey(value: WebIdlCandidate): string {
  return `${value.bindingKind}\0${value.symbol.name}\0${value.memberName ?? ""}`;
}

function declarationBinding(
  kind: WebIdlBindingKind,
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
): WebIdlBindingModel {
  const windowBinding = kind === "legacyWindowAlias" ||
    kind === "legacyFactoryFunction";
  return {
    kind,
    webIdlName: symbol.name,
    webIdlMemberName: null,
    specification: declaration.specification,
    declarationOrdinal: declaration.ordinal,
    memberOrdinal: null,
    exposures: windowBinding
      ? ["Window"]
      : declaration.exposures.length > 0
      ? declaration.exposures
      : symbol.exposures,
    globalNames: windowBinding
      ? ["Window"]
      : declaration.globalNames.length > 0
      ? declaration.globalNames
      : symbol.globalNames,
    secureContext: symbol.secureContext || declaration.secureContext,
  };
}

function memberBinding(
  kind: "namespaceMember" | "globalMember",
  symbol: WebIdlSymbolModel,
  declaration: WebIdlDeclarationModel,
  member: WebIdlMemberModel,
  owner: WebIdlSymbolModel,
): WebIdlBindingModel {
  const exposures = member.exposures.length > 0
    ? member.exposures
    : declaration.exposures.length > 0
    ? declaration.exposures
    : symbol.exposures.length > 0
    ? symbol.exposures
    : owner.exposures;
  const globalNames = declaration.globalNames.length > 0
    ? declaration.globalNames
    : owner.globalNames;
  return {
    kind,
    webIdlName: symbol.name,
    webIdlMemberName: member.name,
    specification: declaration.specification,
    declarationOrdinal: declaration.ordinal,
    memberOrdinal: member.ordinal,
    exposures,
    globalNames,
    secureContext: owner.secureContext ||
      symbol.secureContext ||
      declaration.secureContext ||
      member.secureContext,
  };
}

function bindingKey(value: WebIdlBindingModel): string {
  return [
    value.kind,
    value.webIdlName,
    value.webIdlMemberName ?? "",
    value.specification,
    value.declarationOrdinal,
    value.memberOrdinal ?? "",
    value.exposures.join(","),
    value.globalNames.join(","),
    value.secureContext,
  ].join("\0");
}

function isCompatible(symbol: SymbolModel, candidate: WebIdlCandidate): boolean {
  const declarationKinds = new Set(
    symbol.declarations.map((declaration) => declaration.kind),
  );
  if (
    candidate.bindingKind === "namespaceMember" ||
    candidate.bindingKind === "globalMember"
  ) {
    return candidate.memberKinds.every((kind) => kind === "operation")
      ? declarationKinds.has("globalFunction")
      : candidate.memberKinds.every((kind) =>
          kind === "attribute" || kind === "const"
        ) && declarationKinds.has("globalVariable");
  }
  if (candidate.bindingKind === "legacyWindowAlias") {
    return symbol.declarations.some((declaration) =>
      declaration.kind === "globalVariable" && declaration.constructorObject
    );
  }
  if (candidate.bindingKind === "legacyFactoryFunction") {
    return declarationKinds.has("globalFunction") ||
      symbol.declarations.some((declaration) =>
        declaration.kind === "globalVariable" && declaration.constructorObject
      );
  }

  const classification = candidate.symbol.classifications[0] ?? "";
  switch (classification) {
    case "interface":
    case "mixin":
      return declarationKinds.has("interface") ||
        (
          declarationKinds.has("typeAlias") &&
          candidate.symbol.declarations.some((declaration) =>
            declaration.members.length === 1 &&
            declaration.members.every((member) => member.kind === "setlike")
          )
        );
    case "dictionary":
      return (
        declarationKinds.has("interface") ||
        declarationKinds.has("typeAlias")
      ) &&
        !symbol.declarations.some((declaration) => declaration.constructorObject);
    case "callback":
      return declarationKinds.has("typeAlias") ||
        symbol.declarations.some((declaration) =>
        declaration.members.some((member) =>
          member.kind === "callSignature" ||
          member.kind === "constructSignature"
        )
      );
    case "callbackInterface":
      return declarationKinds.has("interface") ||
        declarationKinds.has("typeAlias");
    case "enum":
    case "typedef":
      return declarationKinds.has("typeAlias");
    case "namespace":
      return declarationKinds.has("namespace") ||
        declarationKinds.has("globalVariable");
    default:
      return false;
  }
}

function semantic(
  status: "matched" | "ambiguous",
  candidate: WebIdlCandidate,
): SemanticModel {
  const webIdl = candidate.symbol;
  const isMember = candidate.bindingKind === "namespaceMember" ||
    candidate.bindingKind === "globalMember";
  const usesBindingScope = isMember ||
    candidate.bindingKind === "legacyWindowAlias" ||
    candidate.bindingKind === "legacyFactoryFunction";
  const exposures = usesBindingScope
    ? unique(candidate.bindings.flatMap((binding) => binding.exposures))
    : webIdl.exposures;
  const globalNames = usesBindingScope
    ? unique(candidate.bindings.flatMap((binding) => binding.globalNames))
    : webIdl.globalNames;
  return {
    status,
    webIdlName: webIdl.name,
    bindingKind: candidate.bindingKind,
    webIdlMemberName: candidate.memberName,
    classifications: webIdl.classifications,
    specifications: unique(
      candidate.bindings.map((binding) => binding.specification),
    ),
    exposures,
    exposedOnWindow: exposures.includes("Window") ||
      exposures.includes("*"),
    exposedOnWorker: exposures.some((exposure) =>
      exposure === "Worker" ||
      exposure.endsWith("Worker") ||
      exposure === "*"
    ),
    globalNames,
    serializable: isMember ? false : webIdl.serializable,
    transferable: isMember ? false : webIdl.transferable,
    secureContext: isMember
      ? candidate.bindings.length > 0 &&
        candidate.bindings.every((binding) => binding.secureContext)
      : webIdl.secureContext,
    extendedAttributes: isMember
      ? candidate.extendedAttributes
      : webIdl.extendedAttributes,
    bindings: candidate.bindings,
  };
}

function unique(values: readonly string[]): string[] {
  return [...new Set(values)].sort(compareOrdinal);
}
