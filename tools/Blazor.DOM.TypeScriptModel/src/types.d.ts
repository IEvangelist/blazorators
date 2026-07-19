declare module "@webref/idl" {
  export interface IdlFile {
    filename: string;
    shortname: string;
    path: string;
    text(): Promise<string>;
    parse(): Promise<unknown[]>;
  }

  export function listAll(options?: { folder?: string }): Promise<Record<string, IdlFile>>;
  export function parseAll(options?: { folder?: string }): Promise<Record<string, unknown[]>>;
}

declare module "webidl2" {
  export function parse(text: string): unknown[];
  export function validate(ast: unknown[]): unknown[];
}
