import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { open } from "node:fs/promises";
import { createInterface } from "node:readline";
import type { ValidateFunction } from "ajv";
import { assertValid } from "./validation.js";

export function serializeJsonLine(value: unknown): string {
  return JSON.stringify(value);
}

export function hashJsonLines(records: readonly unknown[]): string {
  const hash = createHash("sha256");
  for (const record of records) {
    hash.update(serializeJsonLine(record));
    hash.update("\n");
  }
  return hash.digest("hex");
}

export async function writeJsonLines(
  fileName: string,
  records: readonly unknown[],
): Promise<string> {
  const file = await open(fileName, "w");
  const hash = createHash("sha256");
  try {
    for (const record of records) {
      const line = Buffer.from(`${serializeJsonLine(record)}\n`, "utf8");
      hash.update(line);
      let offset = 0;
      while (offset < line.length) {
        const result = await file.write(line, offset, line.length - offset, null);
        if (result.bytesWritten === 0) {
          throw new Error(`Could not complete write to ${fileName}.`);
        }
        offset += result.bytesWritten;
      }
    }
  } finally {
    await file.close();
  }
  return hash.digest("hex");
}

export async function verifyJsonLines(
  fileName: string,
  records: readonly unknown[],
  validator?: ValidateFunction,
): Promise<string> {
  const input = createReadStream(fileName, { encoding: "utf8" });
  const actualHash = createHash("sha256");
  input.on("data", (chunk) => actualHash.update(chunk));
  const lines = createInterface({
    input,
    crlfDelay: Infinity,
  })[Symbol.asyncIterator]();
  const expectedHash = createHash("sha256");

  for (const [ordinal, record] of records.entries()) {
    const actual = await lines.next();
    const expected = serializeJsonLine(record);
    if (actual.done === true) {
      throw new Error(
        `${fileName} ended before expected record ${ordinal}.`,
      );
    }
    if (validator !== undefined) {
      let parsed: unknown;
      try {
        parsed = JSON.parse(actual.value);
      } catch (error: unknown) {
        throw new Error(`${fileName} contains invalid JSON at record ${ordinal}.`, {
          cause: error,
        });
      }
      assertValid(`${fileName} record ${ordinal}`, validator, parsed);
    }
    if (actual.value !== expected) {
      throw new Error(`${fileName} differs at record ${ordinal}.`);
    }
    expectedHash.update(expected);
    expectedHash.update("\n");
  }

  const extra = await lines.next();
  if (extra.done !== true) {
    throw new Error(`${fileName} contains unexpected records.`);
  }
  const actualDigest = actualHash.digest("hex");
  if (actualDigest !== expectedHash.digest("hex")) {
    throw new Error(`${fileName} has non-canonical line endings or termination.`);
  }
  return actualDigest;
}
