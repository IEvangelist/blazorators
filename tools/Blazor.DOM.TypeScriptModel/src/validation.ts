import { readFile } from "node:fs/promises";
import path from "node:path";
import { Ajv2020 } from "ajv/dist/2020.js";
import type { AnySchemaObject, ErrorObject, ValidateFunction } from "ajv";

export interface ArtifactValidators {
  readonly typeScriptSymbol: ValidateFunction;
  readonly webIdlSymbol: ValidateFunction;
  readonly coverage: ValidateFunction;
  readonly manifest: ValidateFunction;
  readonly overrides: ValidateFunction;
}

export async function loadArtifactValidators(
  toolRoot: string,
): Promise<ArtifactValidators> {
  const schemaDirectory = path.join(toolRoot, "schema");
  const ajv = new Ajv2020({
    allErrors: true,
    strict: true,
  });

  const schemaFiles = [
    "typescript-symbol.schema.json",
    "webidl-symbol.schema.json",
    "coverage.schema.json",
    "manifest.schema.json",
    "overrides.schema.json",
  ] as const;
  const validators = new Map<string, ValidateFunction>();

  for (const fileName of schemaFiles) {
    const schemaPath = path.join(schemaDirectory, fileName);
    const schema: unknown = JSON.parse(await readFile(schemaPath, "utf8"));
    if (!isSchemaObject(schema)) {
      throw new Error(`${schemaPath} must contain a JSON schema object.`);
    }
    validators.set(fileName, ajv.compile(schema));
  }

  return {
    typeScriptSymbol: required(validators, "typescript-symbol.schema.json"),
    webIdlSymbol: required(validators, "webidl-symbol.schema.json"),
    coverage: required(validators, "coverage.schema.json"),
    manifest: required(validators, "manifest.schema.json"),
    overrides: required(validators, "overrides.schema.json"),
  };
}

export function assertValid(
  label: string,
  validator: ValidateFunction,
  value: unknown,
): void {
  if (!validator(value)) {
    throw new Error(
      `${label} does not conform to its JSON schema: ${formatErrors(validator.errors)}`,
    );
  }
}

function required(
  validators: ReadonlyMap<string, ValidateFunction>,
  name: string,
): ValidateFunction {
  const validator = validators.get(name);
  if (validator === undefined) {
    throw new Error(`Missing JSON schema validator '${name}'.`);
  }
  return validator;
}

function formatErrors(errors: ErrorObject[] | null | undefined): string {
  if (errors === null || errors === undefined || errors.length === 0) {
    return "unknown validation error";
  }

  return errors
    .map((error) =>
      `${error.instancePath.length === 0 ? "/" : error.instancePath} ${error.message ?? "is invalid"}`
    )
    .join("; ");
}

function isSchemaObject(value: unknown): value is AnySchemaObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
