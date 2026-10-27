const { Router } = require('express');
const { createServiceAuth } = require('../middlewares/service-auth');
const { createRealtimeController } = require('../controllers/realtime.controller');

function createRoutes(config, realtime) {
  const router = Router();
  router.get('/health/live', (_req, res) => res.json({ status: 'live' }));
  router.post('/internal/realtime/publish', createServiceAuth(config.secret), createRealtimeController(realtime));
  return router;
}
module.exports = createRoutes;
