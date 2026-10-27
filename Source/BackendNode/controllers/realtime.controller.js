function createRealtimeController(realtime) {
  return async (req, res) => {
    try { await realtime.publish(req.body); res.sendStatus(204); }
    catch (error) { res.sendStatus(error instanceof TypeError ? 400 : 503); }
  };
}
module.exports = { createRealtimeController };
