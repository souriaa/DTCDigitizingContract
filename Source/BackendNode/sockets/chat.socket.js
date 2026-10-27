// Adapted from E:\test_nodejs\backend\sockets\chat.socket.js.
// Keep initializeChatSocket -> io.use -> connection/disconnect familiar to the reference.
const { Server } = require('socket.io');
const { isIP } = require('node:net');

function clientIp(request) {
  const peer = request.socket.remoteAddress;
  const forwarded = request.headers['x-forwarded-for'];
  // Node listens on loopback. Nginx must OVERWRITE this header with $remote_addr.
  return ['127.0.0.1', '::1', '::ffff:127.0.0.1'].includes(peer)
    && typeof forwarded === 'string' && isIP(forwarded) ? forwarded : peer;
}

function validScope(scope) {
  return scope && typeof scope.leaseId === 'string' && scope.leaseId.length <= 128
    && typeof scope.room === 'string' && /^(tenant:\d+:employee:\d+|discussion:tenant:\d+:contract:\d+:version:\d+)$/.test(scope.room)
    && typeof scope.actorKey === 'string' && scope.actorKey.length <= 200
    && Number.isFinite(Date.parse(scope.expiresAtUtc)) && Date.parse(scope.expiresAtUtc) > Date.now();
}

function initializeChatSocket(server, config, authority) {
  const pendingByIp = new Map();
  const attempts = new Map();
  const io = new Server(server, {
    path: config.socketPath, transports: ['websocket'], serveClient: false,
    connectTimeout: 8000, maxHttpBufferSize: 4096, perMessageDeflate: false,
    pingInterval: 25_000, pingTimeout: 20_000,
    allowRequest: (req, done) => {
      const now = Date.now();
      for (const [key, value] of attempts) if (value.until <= now) attempts.delete(key);
      const ip = clientIp(req);
      const rate = attempts.get(ip) || { count: 0, until: now + 60_000 };
      const allowed = config.origins.includes(req.headers.origin) && io.engine.clientsCount < config.maxConnections
        && attempts.size < 10_000 && rate.count < 60;
      if (allowed) { rate.count++; attempts.set(ip, rate); }
      done(null, allowed);
    },
    cors: { origin: config.origins, credentials: true }
  });
  const activeFor = (predicate) => [...io.sockets.sockets.values()].filter(predicate).length;
  io.use(async (socket, next) => {
    const ip = clientIp(socket.request);
    const pending = pendingByIp.get(ip) || 0;
    const ticket = socket.handshake.auth?.ticket;
    if (typeof ticket !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(ticket)
      || io.engine.clientsCount > config.maxConnections
      || pending + activeFor(s => clientIp(s.request) === ip) >= config.maxPerIp)
      return next(new Error('Không thể mở kết nối realtime.'));
    pendingByIp.set(ip, pending + 1);
    let scope;
    try {
      scope = await authority.consume(ticket, socket.handshake.headers.origin);
      if (!validScope(scope) || activeFor(s => s.data.scope.actorKey === scope.actorKey) >= config.maxPerActor
        || socket.conn.readyState !== 'open') throw new Error('Invalid subscription');
      socket.data.scope = scope;
      next();
    } catch {
      if (scope?.leaseId) void authority.release(scope.leaseId).catch(() => { });
      next(new Error('Phiên realtime không hợp lệ hoặc đã hết hạn.'));
    } finally {
      const count = (pendingByIp.get(ip) || 1) - 1;
      if (count) pendingByIp.set(ip, count); else pendingByIp.delete(ip);
    }
  });
  io.on('connection', socket => {
    const scope = socket.data.scope;
    socket.join(scope.room); // Room is ONLY obtained from the .NET authority.
    const expiryTimer = setTimeout(() => socket.disconnect(true), Math.max(1, Date.parse(scope.expiresAtUtc) - Date.now()));
    let validating = false;
    const interval = setInterval(async () => {
      if (validating) return;
      validating = true;
      try { await check(socket); } finally { validating = false; }
    }, config.revalidateMs);
    // This is an invalidation channel. Chat creation/history still uses the existing REST APIs.
    socket.onAny(() => socket.disconnect(true));
    socket.on('disconnect', () => {
      clearTimeout(expiryTimer); clearInterval(interval);
      void authority.release(scope.leaseId).catch(() => { });
    });
  });
  async function check(socket) {
    try {
      const previous = socket.data.scope;
      const current = await authority.validate(previous.leaseId);
      if (!validScope(current) || current.leaseId !== previous.leaseId || current.room !== previous.room
        || current.actorKey !== previous.actorKey || Date.parse(previous.expiresAtUtc) <= Date.now()) throw new Error('Revoked');
      return socket.connected;
    } catch {
      socket.disconnect(true);
      return false;
    }
  }
  const delivered = new Map();
  const inFlight = new Map();
  async function deliver(event) {
    const sockets = [...(io.sockets.adapter.rooms.get(event.room) || [])]
      .map(id => io.sockets.sockets.get(id)).filter(Boolean);
    // Recheck before every delivery, even between periodic checks. Never emit via global io.emit.
    for (let start = 0; start < sockets.length; start += 8) {
      await Promise.all(sockets.slice(start, start + 8).map(async socket => {
        if (await check(socket)) {
          if (!socket.conn.transport.writable) { socket.disconnect(true); return; }
          socket.emit(event.eventName); // No contract data or message content in the event.
        }
      }));
    }
    delivered.set(event.eventId, Date.now() + 300_000);
  }
  async function publish(event) {
    const notification = event.eventName === 'NotificationsChanged' && /^tenant:\d+:employee:\d+$/.test(event.room);
    const discussion = event.eventName === 'DiscussionChanged' && /^discussion:tenant:\d+:contract:\d+:version:\d+$/.test(event.room);
    if ((!notification && !discussion) || typeof event.eventId !== 'string'
      || event.eventId.length > 128 || !event.eventId.length) throw new TypeError('Invalid event');
    for (const [key, expiry] of delivered) if (expiry <= Date.now()) delivered.delete(key);
    if (delivered.has(event.eventId)) return;
    if (inFlight.has(event.eventId)) return inFlight.get(event.eventId);
    if (delivered.size >= 20_000 || inFlight.size >= 32) throw new Error('Realtime capacity reached');
    const task = deliver(event).finally(() => inFlight.delete(event.eventId));
    inFlight.set(event.eventId, task);
    return task;
  }
  return { io, publish };
}
module.exports = initializeChatSocket;
