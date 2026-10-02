// Run from Source: node scripts/recovery-client-smoke.cjs
// No browser, SMTP or real API calls; executes the actual Axios public clients.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { createRequire } = require('node:module');
const { pathToFileURL } = require('node:url');

async function check(app) {
  const root = path.resolve(__dirname, '..', app);
  const appRequire = createRequire(path.join(root, 'package.json'));
  const ts = appRequire('typescript');
  const calls = [];
  const originalFetch = global.fetch;
  global.fetch = async (input, init) => {
    const request = input instanceof Request ? input : new Request(input, init);
    calls.push({ request, body: await request.clone().json() });
    return new Response(JSON.stringify({ message: 'QA accepted' }), { status: 200, headers: { 'Content-Type': 'application/json' } });
  };
  try {
    const source = fs.readFileSync(path.join(root, 'services/password-recovery-api.ts'), 'utf8');
    const javascript = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
    const module = { exports: {} };
    vm.runInNewContext(javascript, { exports: module.exports, module, require: appRequire,
      process: { env: { NEXT_PUBLIC_API_URL: 'http://recovery.qa.invalid' } } });
    const api = module.exports.passwordRecoveryApi;
    const link = { requestId: '00000000-0000-0000-0000-000000000001', token: 'Q'.repeat(43), ...(app === 'Frontend' ? { tenantCode: 'tenant-qa' } : {}) };
    await api.request('qa-account', app === 'Frontend' ? 'tenant-qa' : undefined);
    await api.validate(link);
    await api.reset(link, 'QAPassword123!', 'QAPassword123!');
    assert.equal(calls.length, 3);
    for (const { request } of calls) {
      assert.equal(request.method, 'POST');
      assert.equal(request.credentials, 'omit');
      assert.equal(request.headers.has('Cookie'), false);
      assert.equal(request.headers.has('X-Tenant-Code'), false);
      assert.equal(request.headers.has('Authorization'), false);
      assert.equal(new URL(request.url).search, '');
    }
    assert.equal(calls[1].body.token, link.token);
    assert.equal(calls[2].body.newPassword, 'QAPassword123!');
    assert.equal(module.exports.getRecoveryErrorMessage(new Error('private-secret'), 'Lỗi an toàn'), 'Lỗi an toàn');
    const config = (await import(pathToFileURL(path.join(root, 'next.config.mjs')).href)).default;
    const headers = await config.headers();
    const analytics = fs.readFileSync(path.join(root, 'components/safe-analytics.tsx'), 'utf8');
    for (const route of ['/forgot-password', '/reset-password', '/verify-recovery-email']) {
      const definition = headers.find(x => x.source === route);
      assert.ok(definition);
      assert.ok(definition.headers.some(x => x.key === 'Cache-Control' && x.value === 'no-store'));
      assert.ok(definition.headers.some(x => x.key === 'Referrer-Policy' && x.value === 'no-referrer'));
      assert.ok(analytics.includes(`"${route}"`));
    }
    console.log(`${app}: recovery client credentials/header/body/redaction and route headers PASS`);
  } finally { global.fetch = originalFetch; }
}
(async () => { await check('Frontend'); await check('SystemAdmin'); })().catch(() => {
  // Assertion objects may contain mock request data; do not dump them like production errors.
  console.error('Recovery smoke FAILED; review assertions (no payload/secret dump).');
  process.exitCode = 1;
});
