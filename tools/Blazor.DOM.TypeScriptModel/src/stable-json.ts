import { createHash } from "node:crypto";

export function compareOrdinal(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0;
}

export function stableJson(value: unknown): string {
  return `${JSON.stringify(value, null, 2)}\n`;
}

export function normalizeLf(value: string): string {
  return value.replace(/\r\n?/g, "\n");
}

export function sha256(value: string | Buffer): string {
  return createHash("sha256").update(value).digest("hex");
}

export function increment(counts: Record<string, number>, key: string): void {
  const current = Object.prototype.hasOwnProperty.call(counts, key)
    ? counts[key] ?? 0
    : 0;
  counts[key] = current + 1;
}
