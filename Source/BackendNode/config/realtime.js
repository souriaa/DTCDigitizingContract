function loadConfig(env = process.env) {
  const secret = env.REALTIME_SERVICE_SECRET || '';
  const origins = (env.ALLOWED_ORIGINS || '').split(',').map(x => x.trim()).filter(Boolean);
  const api = new URL(env.DOTNET_BASE_URL || 'http://127.0.0.1:5005');
  const port = Number(env.PORT || 4000);
  if (secret.length < 32) throw new Error('REALTIME_SERVICE_SECRET must contain at least 32 characters');
  if (!origins.length || origins.some(origin => {
    try { return new URL(origin).origin !== origin || (env.NODE_ENV === 'production' && !origin.startsWith('https://')); }
    catch { return true; }
  })) throw new Error('ALLOWED_ORIGINS must contain exact origins (HTTPS in production)');
  // This service deliberately runs on the same host as the current single-instance API.
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(api.hostname)
    || api.username || api.password || api.search || api.hash || api.pathname !== '/')
    throw new Error('DOTNET_BASE_URL must be a loopback origin');
  if (!Number.isInteger(port) || port < 0 || port > 65535) throw new Error('Invalid PORT');
  const host = env.HOST || '127.0.0.1';
  if (!['127.0.0.1', '::1'].includes(host)) throw new Error('HOST must be loopback; expose only through Nginx');
  return {
    secret, origins, dotnetBaseUrl: api.origin, host, port,
    socketPath: '/realtime/socket.io', revalidateMs: 30_000, maxConnections: 2000,
    maxPerIp: 30, maxPerActor: 8
  };
}
module.exports = { loadConfig };
