const { test } = require('node:test');
const assert = require('node:assert/strict');
const { once } = require('node:events');
const { randomBytes, randomUUID } = require('node:crypto');
const { io } = require('socket.io-client');
const { createBackend } = require('../index');
const { sign } = require('../middlewares/service-auth');
const { loadConfig } = require('../config/realtime');
const { createDotnetService } = require('../services/dotnet.service');

const origin = 'http://localhost:3000';
const secret = 'test-only-realtime-service-secret-32-characters';
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

test('HMAC protocol agrees with the independent .NET vector', () => {
  assert.equal(sign('key', 'POST', '/internal/realtime/consume', '{}', '1000000000000',
    '11111111-1111-1111-1111-111111111111')['x-realtime-signature'],
    'fae7f842ba610e1b2719c643c5c350a015f98b06683bcdd73619781d68c8d7cf');
});

async function fixture(t, overrides = {}) {
  const tickets = new Map(), leases = new Map();
  const authority = {
    async consume(ticket, clientOrigin) {
      const scope = tickets.get(ticket);
      tickets.delete(ticket);
      if (!scope || !config.origins.includes(clientOrigin)) throw new Error('Invalid');
      leases.set(scope.leaseId, scope);
      return scope;
    },
    async validate(lease) { if (!leases.has(lease)) throw new Error('Revoked'); return leases.get(lease); },
    async release(lease) { leases.delete(lease); return { released: true }; }
  };
  const config = { ...loadConfig({ REALTIME_SERVICE_SECRET: secret, ALLOWED_ORIGINS: origin, PORT: '0' }), revalidateMs: 50, ...overrides };
  const backend = createBackend(config, authority);
  backend.server.listen(0, '127.0.0.1');
  await once(backend.server, 'listening');
  const url = `http://127.0.0.1:${backend.server.address().port}`;
  t.after(async () => { await new Promise(resolve => backend.io.close(resolve)); });
  function client(room, actorKey = room, extra = {}) {
    const ticket = randomBytes(32).toString('base64url');
    const scope = { leaseId: randomUUID(), room, actorKey, expiresAtUtc: new Date(Date.now() + 60_000).toISOString() };
    tickets.set(ticket, scope);
    const socket = io(url, {
      path: config.socketPath, transports: ['websocket'], reconnection: false,
      extraHeaders: { Origin: origin }, auth: { ticket }, autoConnect: false, forceNew: true, ...extra
    });
    t.after(() => socket.disconnect());
    return { socket, ticket, scope };
  }
  async function connect(client) { const connected = once(client.socket, 'connect'); client.socket.connect(); await connected; }
  async function publish(event, headers, body = JSON.stringify(event)) {
    const path = '/internal/realtime/publish';
    return fetch(`${url}${path}`, {
      method: 'POST', headers: {
        'content-type': 'application/json',
        ...(headers || sign(secret, 'POST', path, body))
      }, body
    });
  }
  return { backend, authority, config, url, tickets, leases, client, connect, publish };
}

test('configuration rejects missing secrets, wildcard origins, public API and production HTTP', () => {
  assert.throws(() => loadConfig({}));
  for (const extra of [{ ALLOWED_ORIGINS: '*' }, { DOTNET_BASE_URL: 'http://other-server' },
  { NODE_ENV: 'production' }, { HOST: '0.0.0.0' }])
    assert.throws(() => loadConfig({ REALTIME_SERVICE_SECRET: secret, ALLOWED_ORIGINS: origin, ...extra }));
});

test('scoped invalidations isolate tenant, contract and version; duplicate publish is idempotent', async t => {
  const f = await fixture(t);
  const room = 'discussion:tenant:1:contract:20:version:30';
  const clients = [f.client(room, 'employee:1:1'), f.client(room, 'customer:1:hash'),
  f.client('discussion:tenant:2:contract:20:version:30'), f.client('discussion:tenant:1:contract:21:version:30'),
  f.client('discussion:tenant:1:contract:20:version:31')];
  await Promise.all(clients.map(f.connect));
  const counts = clients.map(() => 0);
  clients.forEach((c, index) => c.socket.on('DiscussionChanged', (...args) => { assert.deepEqual(args, []); counts[index]++; }));
  const event = { eventId: randomUUID(), eventName: 'DiscussionChanged', room };
  const received = once(clients[0].socket, 'DiscussionChanged');
  assert.equal((await f.publish(event)).status, 204);
  await received;
  assert.equal((await f.publish(event)).status, 204);
  await delay(60);
  assert.deepEqual(counts, [1, 1, 0, 0, 0]);
});

test('notification targets only recipient, including equal employee IDs in different tenants', async t => {
  const f = await fixture(t);
  const a = f.client('tenant:1:employee:7'), b = f.client('tenant:2:employee:7'), c = f.client('tenant:1:employee:8');
  await Promise.all([a, b, c].map(f.connect));
  let foreign = 0;
  b.socket.on('NotificationsChanged', () => foreign++); c.socket.on('NotificationsChanged', () => foreign++);
  const received = once(a.socket, 'NotificationsChanged');
  assert.equal((await f.publish({ eventId: randomUUID(), eventName: 'NotificationsChanged', room: a.scope.room })).status, 204);
  await received; await delay(60); assert.equal(foreign, 0);
});

test('browser on the API reverse proxy can connect when its exact origin is configured', async t => {
  const proxyOrigin = 'http://localhost:5005';
  const f = await fixture(t, { origins: [origin, proxyOrigin] });
  const client = f.client('tenant:1:employee:7', undefined, { extraHeaders: { Origin: proxyOrigin } });
  await f.connect(client);
  const received = once(client.socket, 'NotificationsChanged');
  assert.equal((await f.publish({ eventId: randomUUID(), eventName: 'NotificationsChanged', room: client.scope.room })).status, 204);
  await received;
});

test('missing/unapproved Origin and reused ticket cannot connect', async t => {
  const f = await fixture(t);
  const a = f.client('tenant:1:employee:1');
  await f.connect(a);
  const replay = f.client('tenant:1:employee:1', undefined, { auth: { ticket: a.ticket } });
  const denied = once(replay.socket, 'connect_error'); replay.socket.connect(); await denied;
  for (const badOrigin of ['', 'http://evil.test']) {
    const bad = f.client('tenant:1:employee:1', undefined, { extraHeaders: { Origin: badOrigin } });
    const error = once(bad.socket, 'connect_error'); bad.socket.connect(); await error;
  }
});

test('before-delivery validation closes revoked lease and never emits event', async t => {
  const f = await fixture(t, { revalidateMs: 60_000 });
  const c = f.client('tenant:1:employee:2'); await f.connect(c);
  let received = 0; c.socket.on('NotificationsChanged', () => received++);
  f.leases.delete(c.scope.leaseId);
  const disconnected = once(c.socket, 'disconnect');
  assert.equal((await f.publish({ eventId: randomUUID(), eventName: 'NotificationsChanged', room: c.scope.room })).status, 204);
  await disconnected; assert.equal(received, 0);
});

test('periodic validation and expiry close idle subscriptions', async t => {
  const f = await fixture(t);
  const c = f.client('tenant:1:employee:2'); await f.connect(c);
  f.leases.delete(c.scope.leaseId); await once(c.socket, 'disconnect');
  const expired = f.client('tenant:1:employee:3');
  expired.scope.expiresAtUtc = new Date(Date.now() - 1000).toISOString();
  const error = once(expired.socket, 'connect_error'); expired.socket.connect(); await error;
});

test('client cannot join another room or write a chat message through socket', async t => {
  const f = await fixture(t);
  const c = f.client('tenant:1:employee:2'); await f.connect(c);
  const disconnected = once(c.socket, 'disconnect');
  c.socket.emit('send_message', { room: 'tenant:2:employee:2', text: 'forged' });
  await disconnected;
});

test('publish rejects signature replay, tampered bytes, stale timestamp, and invalid event/room', async t => {
  const f = await fixture(t);
  const event = { eventId: randomUUID(), eventName: 'NotificationsChanged', room: 'tenant:1:employee:1' };
  const headers = sign(secret, 'POST', '/internal/realtime/publish', JSON.stringify(event));
  assert.equal((await f.publish(event, headers)).status, 204);
  assert.equal((await f.publish(event, headers)).status, 401);
  assert.equal((await f.publish(event, headers, JSON.stringify({ ...event, room: 'tenant:2:employee:1' }))).status, 401);
  const stale = sign(secret, 'POST', '/internal/realtime/publish', JSON.stringify(event), (Date.now() - 60_000).toString());
  assert.equal((await f.publish(event, stale)).status, 401);
  assert.equal((await f.publish({ ...event, room: 'discussion:tenant:1:contract:1:version:1' })).status, 400);
  assert.equal((await f.publish({ ...event, eventName: 'send_message' })).status, 400);
});

test('actor/IP limits reject additional connection without leaking the consumed lease', async t => {
  const f = await fixture(t, { maxPerActor: 1 });
  const a = f.client('tenant:1:employee:1', 'employee:1:1'); await f.connect(a);
  const b = f.client('discussion:tenant:1:contract:2:version:3', 'employee:1:1');
  const error = once(b.socket, 'connect_error'); b.socket.connect(); await error;
  await delay(20); assert.equal(f.leases.has(b.scope.leaseId), false);
});

test('authority HTTP client signs each exact request and does not send browser credentials', async t => {
  const original = global.fetch;
  const calls = [];
  global.fetch = async (url, options) => {
    calls.push({ url, options });
    const path = new URL(url).pathname;
    const headers = options.headers;
    const expected = sign(secret, 'POST', path, options.body, headers['x-realtime-timestamp'], headers['x-realtime-nonce']);
    assert.equal(headers['x-realtime-signature'], expected['x-realtime-signature']);
    assert.equal(headers.cookie, undefined);
    assert.equal(headers['x-forwarded-proto'], 'https');
    return { ok: true, json: async () => ({ ok: true }) };
  };
  t.after(() => { global.fetch = original; });
  const api = createDotnetService({ secret, dotnetBaseUrl: 'http://127.0.0.1:5005' });
  await api.consume('ticket', origin); await api.validate('lease'); await api.release('lease');
  assert.deepEqual(calls.map(call => new URL(call.url).pathname),
    ['/internal/realtime/consume', '/internal/realtime/validate', '/internal/realtime/release']);
});
