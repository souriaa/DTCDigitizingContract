require('dotenv').config({ path: require('node:path').join(__dirname, '.env'), quiet: true });
const express = require('express');
const http = require('node:http');
const { loadConfig } = require('./config/realtime');
const { createDotnetService } = require('./services/dotnet.service');
const initializeChatSocket = require('./sockets/chat.socket');
const createRoutes = require('./routes');

// Same entry-point flow as the reference backend; future Node modules can add their own routes/services.
function createBackend(config = loadConfig(), authority = createDotnetService(config)) {
  const app = express();
  app.disable('x-powered-by');
  const server = http.createServer(app);
  const realtime = initializeChatSocket(server, config, authority);
  app.use(express.json({ limit: '16kb', verify: (req, _res, buffer) => { req.rawBody = buffer.toString('utf8'); } }));
  app.use(createRoutes(config, realtime));
  app.use((_error, _req, res, _next) => res.sendStatus(400));
  server.requestTimeout = 10_000;
  server.headersTimeout = 10_000;
  return { app, server, ...realtime };
}

if (require.main === module) {
  const config = loadConfig();
  const backend = createBackend(config);
  backend.server.listen(config.port, config.host, () => console.log(`eContract Node listening on ${config.host}:${config.port}`));
  let closing = false;
  const stop = () => {
    if (closing) return;
    closing = true;
    const deadline = setTimeout(() => process.exit(1), 10_000);
    deadline.unref();
    backend.io.close(() => { clearTimeout(deadline); });
  };
  process.on('SIGTERM', stop);
  process.on('SIGINT', stop);
}
module.exports = { createBackend };
