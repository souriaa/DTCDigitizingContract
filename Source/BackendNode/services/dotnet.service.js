const { sign } = require('../middlewares/service-auth');

function createDotnetService(config) {
  async function post(path, payload) {
    const body = JSON.stringify(payload);
    const response = await fetch(`${config.dotnetBaseUrl}${path}`, {
      method: 'POST', redirect: 'error', signal: AbortSignal.timeout(5000),
      // Same-host internal HTTP sits behind the deployment's HTTPS termination.
      // .NET trusts forwarded headers only from loopback; keep HTTPS redirection out of the control hop.
      headers: {
        'content-type': 'application/json', 'x-forwarded-proto': 'https',
        ...sign(config.secret, 'POST', path, body)
      }, body
    });
    if (!response.ok) throw new Error('Realtime authorization unavailable');
    return response.json();
  }
  return {
    consume: (ticket, origin) => post('/internal/realtime/consume', { ticket, origin }),
    validate: leaseId => post('/internal/realtime/validate', { leaseId }),
    release: leaseId => post('/internal/realtime/release', { leaseId })
  };
}
module.exports = { createDotnetService };
