// Run: node --test scripts/contract-discussion-realtime.test.mjs
// Execute the actual hook with controlled React/SignalR/timers; no backend or browser required.
import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const source = ts.transpileModule(
  fs.readFileSync(path.join(__dirname, "../hooks/use-contract-discussion-realtime.ts"), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } },
).outputText;

const flush = async () => { for (let i = 0; i < 12; i++) await Promise.resolve(); };
const deferred = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
};

function mount(onRefresh, firstStart = Promise.resolve()) {
  const effects = [], timers = new Map(), listeners = new Map();
  let now = 0, timerId = 0;
  const connection = {
    state: "Disconnected", starts: 0, stops: 0,
    start() {
      this.starts++;
      this.state = "Connecting";
      return (this.starts === 1 ? firstStart : Promise.resolve()).then(
        () => { this.state = "Connected"; },
        (error) => { this.state = "Disconnected"; throw error; },
      );
    },
    async stop() { this.stops++; this.state = "Disconnected"; },
    on(_name, handler) { this.changed = handler; },
    onreconnected(handler) { this.reconnected = handler; },
    onclose(handler) { this.closed = handler; },
  };
  class Builder {
    withUrl(url, options) { connection.url = url; connection.options = options; return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return connection; }
  }
  const surface = {
    visibilityState: "visible",
    addEventListener(name, handler) { listeners.set(name, handler); },
    removeEventListener(name) { listeners.delete(name); },
  };
  const moduleExports = {};
  vm.runInNewContext(source, {
    exports: moduleExports,
    require(name) {
      if (name === "react") return { useRef: (value) => ({ current: value }), useEffect: (effect) => effects.push(effect) };
      if (name === "@microsoft/signalr") return {
        HubConnectionBuilder: Builder, HubConnectionState: { Disconnected: "Disconnected" }, LogLevel: { None: 6 },
      };
      throw new Error(`Unexpected import ${name}`);
    },
    process: { env: { NEXT_PUBLIC_API_URL: "https://app.test/" } },
    AbortController, window: surface, document: surface,
    setTimeout(callback, delay) { const id = ++timerId; timers.set(id, { at: now + delay, callback }); return id; },
    clearTimeout(id) { timers.delete(id); },
  });
  moduleExports.useContractDiscussionRealtime("/api/contracts/1/versions/2/discussion-hub", onRefresh);
  const cleanups = effects.map((effect) => effect());
  return {
    connection, listeners,
    dispose() { cleanups.forEach((cleanup) => cleanup?.()); },
    async advance(milliseconds) {
      now += milliseconds;
      for (const [id, timer] of [...timers]) {
        if (timer.at <= now && timers.has(id)) { timers.delete(id); timer.callback(); }
      }
      await flush();
    },
  };
}

test("burst invalidations coalesce; events during a pending REST refresh are not lost", async () => {
  const firstRefresh = deferred();
  let calls = 0;
  const app = mount(async () => { if (++calls === 1) await firstRefresh.promise; });
  await flush();
  app.connection.changed(); app.connection.changed();
  await app.advance(150);
  assert.equal(calls, 1);
  app.connection.changed(); app.connection.changed();
  await app.advance(150);
  assert.equal(calls, 1);
  firstRefresh.resolve();
  await flush();
  assert.equal(calls, 2);
  assert.equal(app.connection.url, "https://app.test/api/contracts/1/versions/2/discussion-hub");
  assert.equal(app.connection.options.withCredentials, true);
  app.dispose();
});

test("unmount during negotiation does not stop it prematurely; completion cleans up without fetching", async () => {
  const negotiation = deferred();
  let calls = 0;
  const app = mount(async () => { calls++; }, negotiation.promise);
  app.dispose();
  assert.equal(app.connection.stops, 0);
  negotiation.resolve();
  await flush();
  await app.advance(60000);
  assert.equal(app.connection.stops, 1);
  assert.equal(calls, 0);
  assert.equal(app.listeners.size, 0);
});

test("failed start retries and rechecks access; reconnect/focus resync and disposal aborts refresh", async () => {
  const negotiation = deferred();
  let calls = 0, signal;
  const app = mount(async (requestSignal) => { calls++; signal = requestSignal; }, negotiation.promise);
  negotiation.reject(new Error("Network unavailable"));
  await flush();
  await app.advance(150);
  assert.equal(calls, 1);
  await app.advance(2000);
  await app.advance(150);
  assert.equal(app.connection.starts, 2);
  assert.equal(calls, 2);
  app.connection.reconnected();
  app.listeners.get("focus")();
  await app.advance(150);
  assert.equal(calls, 3);
  app.dispose();
  assert.equal(signal.aborted, true);
});
