import assert from "node:assert/strict";
import test from "node:test";

interface DotNetStub {
  createJSObjectReference(value: unknown): unknown;
  createJSStreamReference(value: unknown): unknown;
  disposeJSObjectReference(reference: unknown): void;
}

interface DotNetCallback {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<boolean>;
}

interface RuntimeModule {
  addDotNetEventListener(
    target: EventTargetStub,
    type: string,
    callback: { dispose(): void },
    callbackMethod: string,
    registrationCallback: DotNetCallback,
    registrationMethod: string,
  ): Promise<void>;
  addDotNetReferenceEventListener(
    target: EventTargetStub,
    type: string,
    callback: DotNetCallback,
    callbackMethod: string,
    registrationCallback: DotNetCallback,
    registrationMethod: string,
  ): Promise<void>;
  createDotNetStreamReference(
    value: Blob | ArrayBuffer | ArrayBufferView,
    callback: DotNetCallback,
    callbackMethod: string,
  ): Promise<void>;
  getIndexDotNetObjectReference(
    target: Record<number, unknown>,
    index: number,
    callback: DotNetCallback,
    callbackMethod: string,
  ): Promise<void>;
  getPropertyDotNetObjectReference(
    target: Record<string, unknown>,
    name: string,
    callback: DotNetCallback,
    callbackMethod: string,
  ): Promise<void>;
  invokeMethodDotNetObjectReference(
    target: Record<string, (...args: unknown[]) => unknown>,
    name: string,
    args: unknown[],
    callback: DotNetCallback,
    callbackMethod: string,
  ): Promise<void>;
}

class EventTargetStub {
  readonly listeners = new Map<string, Set<(value: unknown) => void>>();
  addCount = 0;
  removeCount = 0;

  addEventListener(type: string, listener: (value: unknown) => void): void {
    this.addCount++;
    const listeners = this.listeners.get(type) ?? new Set();
    listeners.add(listener);
    this.listeners.set(type, listeners);
  }

  removeEventListener(type: string, listener: (value: unknown) => void): void {
    this.removeCount++;
    this.listeners.get(type)?.delete(listener);
  }
}

const runtime = await import(
  new URL(
    "../../../../src/Blazor.DOM/wwwroot/blazorators.dom.js",
    import.meta.url,
  ).href
) as RuntimeModule;

const globalWithDotNet = globalThis as typeof globalThis & {
  DotNet?: DotNetStub;
};

async function withDotNet(
  dotNet: DotNetStub,
  action: () => Promise<void>,
): Promise<void> {
  const previous = globalWithDotNet.DotNet;
  globalWithDotNet.DotNet = dotNet;
  try {
    await action();
  } finally {
    if (previous === undefined) {
      delete globalWithDotNet.DotNet;
    } else {
      globalWithDotNet.DotNet = previous;
    }
  }
}

test("empty Blob, ArrayBuffer, and typed array avoid JS stream references", async () => {
  let streamReferenceCreations = 0;
  const deliveries: unknown[][] = [];
  await withDotNet({
    createJSObjectReference: (value) => value,
    createJSStreamReference: (value) => {
      streamReferenceCreations++;
      return value;
    },
    disposeJSObjectReference: () => undefined,
  }, async () => {
    const callback: DotNetCallback = {
      invokeMethodAsync: async (method, ...args) => {
        assert.equal(method, "ReceiveStream");
        deliveries.push(args);
        return true;
      },
    };
    for (const value of [
      new Blob([]),
      new ArrayBuffer(0),
      new Uint8Array(0),
    ]) {
      await runtime.createDotNetStreamReference(
        value,
        callback,
        "ReceiveStream",
      );
    }
  });

  assert.equal(streamReferenceCreations, 0);
  assert.deepEqual(deliveries, [
    [null, 0, true],
    [null, 0, true],
    [null, 0, true],
  ]);
});

test("failed stream delivery disposes the provisional JS reference", async () => {
  const streamReference = { id: "stream" };
  const disposed: unknown[] = [];
  await withDotNet({
    createJSObjectReference: (value) => value,
    createJSStreamReference: () => streamReference,
    disposeJSObjectReference: (reference) => disposed.push(reference),
  }, async () => {
    await assert.rejects(
      runtime.createDotNetStreamReference(
        new Uint8Array([1]),
        { invokeMethodAsync: async () => false },
        "ReceiveStream",
      ),
      /rejected stream delivery/,
    );
  });

  assert.deepEqual(disposed, [streamReference]);
});

test("property, method, and index references use acknowledged callback delivery", async () => {
  const value = { id: "value" };
  const created: unknown[] = [];
  const received: unknown[] = [];
  await withDotNet({
    createJSObjectReference: (candidate) => {
      const reference = { candidate };
      created.push(reference);
      return reference;
    },
    createJSStreamReference: (candidate) => candidate,
    disposeJSObjectReference: () => undefined,
  }, async () => {
    const callback: DotNetCallback = {
      invokeMethodAsync: async (_, reference) => {
        received.push(reference);
        return true;
      },
    };
    await runtime.getPropertyDotNetObjectReference(
      { value },
      "value",
      callback,
      "ReceiveReference",
    );
    await runtime.invokeMethodDotNetObjectReference(
      { value: () => value },
      "value",
      [],
      callback,
      "ReceiveReference",
    );
    await runtime.getIndexDotNetObjectReference(
      { 0: value },
      0,
      callback,
      "ReceiveReference",
    );
  });

  assert.equal(created.length, 3);
  assert.deepEqual(received, created);
});

test("null references are delivered without allocating a JS reference", async () => {
  let created = 0;
  const received: unknown[] = [];
  await withDotNet({
    createJSObjectReference: () => {
      created++;
      return {};
    },
    createJSStreamReference: (candidate) => candidate,
    disposeJSObjectReference: () => undefined,
  }, async () => {
    const callback: DotNetCallback = {
      invokeMethodAsync: async (_, reference) => {
        received.push(reference);
        return true;
      },
    };
    await runtime.getPropertyDotNetObjectReference(
      { value: null },
      "value",
      callback,
      "ReceiveReference",
    );
    await runtime.invokeMethodDotNetObjectReference(
      { value: () => null },
      "value",
      [],
      callback,
      "ReceiveReference",
    );
    await runtime.getIndexDotNetObjectReference(
      { 0: null },
      0,
      callback,
      "ReceiveReference",
    );
  });

  assert.equal(created, 0);
  assert.deepEqual(received, [null, null, null]);
});

test("rejected object-reference delivery rolls back its JS reference", async () => {
  const reference = { id: "reference" };
  const disposed: unknown[] = [];
  await withDotNet({
    createJSObjectReference: () => reference,
    createJSStreamReference: (candidate) => candidate,
    disposeJSObjectReference: (candidate) => disposed.push(candidate),
  }, async () => {
    await assert.rejects(
      runtime.getPropertyDotNetObjectReference(
        { value: {} },
        "value",
        { invokeMethodAsync: async () => false },
        "ReceiveReference",
      ),
      /rejected object reference delivery/,
    );
  });

  assert.deepEqual(disposed, [reference]);
});

test("rejected listener registrations roll back IDs and object references", async () => {
  const disposed: unknown[] = [];
  const registrationReference = { id: "registration" };
  await withDotNet({
    createJSObjectReference: () => registrationReference,
    createJSStreamReference: (candidate) => candidate,
    disposeJSObjectReference: (candidate) => disposed.push(candidate),
  }, async () => {
    const jsonTarget = new EventTargetStub();
    let callbackDisposals = 0;
    await assert.rejects(
      runtime.addDotNetEventListener(
        jsonTarget,
        "click",
        { dispose: () => callbackDisposals++ },
        "HandleEvent",
        { invokeMethodAsync: async () => false },
        "ReceiveRegistration",
      ),
      /rejected event registration delivery/,
    );
    assert.equal(jsonTarget.addCount, 1);
    assert.equal(jsonTarget.removeCount, 1);
    assert.equal(callbackDisposals, 1);

    const typedTarget = new EventTargetStub();
    await assert.rejects(
      runtime.addDotNetReferenceEventListener(
        typedTarget,
        "change",
        { invokeMethodAsync: async () => true },
        "HandleReference",
        {
          invokeMethodAsync: async () => {
            throw new Error("registration delivery cancelled");
          },
        },
        "ReceiveRegistration",
      ),
      /registration delivery cancelled/,
    );
    assert.equal(typedTarget.addCount, 1);
    assert.equal(typedTarget.removeCount, 1);
  });

  assert.deepEqual(disposed, [registrationReference]);
});
