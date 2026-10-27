const { createHmac, randomUUID, timingSafeEqual } = require('node:crypto');

// The exact bytes, method and path are signed. A different body/path cannot reuse a signature.
function sign(secret, method, path, body, timestamp = Date.now().toString(), nonce = randomUUID()) {
  const signature = createHmac('sha256', secret)
    .update(`${timestamp}\n${nonce}\n${method}\n${path}\n${body}`).digest('hex');
  return { 'x-realtime-timestamp': timestamp, 'x-realtime-nonce': nonce, 'x-realtime-signature': signature };
}

function createServiceAuth(secret) {
  const seen = new Map();
  return (req, res, next) => {
    const timestamp = req.get('x-realtime-timestamp');
    const nonce = req.get('x-realtime-nonce') || '';
    const signature = req.get('x-realtime-signature') || '';
    const now = Date.now();
    for (const [key, expiry] of seen) if (expiry <= now) seen.delete(key);
    if (!/^\d{13}$/.test(timestamp || '') || Math.abs(now - Number(timestamp)) > 30_000
      || !/^[a-f0-9-]{36}$/i.test(nonce) || !/^[a-f0-9]{64}$/i.test(signature)
      || seen.has(nonce) || seen.size >= 10_000) return res.sendStatus(401);
    const expected = sign(secret, req.method, req.originalUrl, req.rawBody || '', timestamp, nonce)['x-realtime-signature'];
    if (!timingSafeEqual(Buffer.from(signature, 'hex'), Buffer.from(expected, 'hex'))) return res.sendStatus(401);
    seen.set(nonce, now + 60_000);
    next();
  };
}
module.exports = { sign, createServiceAuth };
