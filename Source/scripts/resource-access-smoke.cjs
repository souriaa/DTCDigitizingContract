// Run from Source: node scripts/resource-access-smoke.cjs
// Exercises actual classification and shared UI; no browser, API or database calls.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { createRequire } = require('node:module');

const root = path.resolve(__dirname, '..', 'Frontend');
const appRequire = createRequire(path.join(root, 'package.json'));
const ts = appRequire('typescript');
const React = appRequire('react');
const { renderToStaticMarkup } = appRequire('react-dom/server');
const axios = appRequire('axios');
let allowed = false;
const cache = new Map();

function load(relativePath) {
  const filename = path.join(root, relativePath);
  if (cache.has(filename)) return cache.get(filename).exports;
  const module = { exports: {} };
  cache.set(filename, module);
  const javascript = ts.transpileModule(fs.readFileSync(filename, 'utf8'), {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
      esModuleInterop: true,
    },
  }).outputText;
  function resolve(id) {
    if (id === 'next/navigation') return { useRouter: () => ({ back() {} }) };
    if (id === 'next/link') {
      return { __esModule: true, default: (props) => React.createElement('a', props) };
    }
    if (id === '@/hooks/use-permission') return { usePermission: () => ({ can: () => allowed }) };
    if (id === '@/hooks/use-auth-store') {
      return { useAuthStore: (selector) => selector({ user: { fullName: 'Nhân viên thử nghiệm', roleName: 'Nhân viên' } }) };
    }
    if (id.startsWith('@/')) {
      const relative = id.slice(2);
      const extension = fs.existsSync(path.join(root, relative + '.tsx')) ? '.tsx' : '.ts';
      return load(relative + extension);
    }
    return appRequire(id);
  }
  vm.runInNewContext(javascript, { module, exports: module.exports, require: resolve, setTimeout }, { filename });
  return module.exports;
}

const { getResourceAccessErrorKind } = load('lib/api-error.ts');
const { PermissionGuard, ResourceAccessErrorState } = load('components/auth/permission-guard.tsx');
const { RBAC_PERMISSIONS } = load('lib/rbac.ts');
function apiError(status, code) {
  return new axios.AxiosError('Request failed', undefined, undefined, undefined, {
    status, data: { code }, headers: {}, statusText: '', config: {},
  });
}

for (const [error, expected] of [
  [apiError(403), 'forbidden'],
  [apiError(404), 'not-found'],
  [apiError(500), 'error'],
  [apiError(400, 'PermissionDenied'), 'forbidden'],
  [apiError(400, 'ResourceNotFound'), 'not-found'],
  [apiError(401), 'error'],
  [new axios.AxiosError('Network Error'), 'error'],
  [new Error('Unexpected'), 'error'],
  [null, 'error'],
]) assert.equal(getResourceAccessErrorKind(error), expected);

function render(component, props) {
  return renderToStaticMarkup(React.createElement(component, props));
}
const permission = RBAC_PERMISSIONS.templateManage;
const guardProps = { permission, children: 'Nội dung được bảo vệ' };
assert.match(render(PermissionGuard, guardProps), /403/);
assert.doesNotMatch(render(PermissionGuard, guardProps), /Nội dung được bảo vệ/);
assert.match(render(PermissionGuard, { ...guardProps, fallback: 'Thông báo tùy chỉnh' }), /Thông báo tùy chỉnh/);
for (const variant of ['inline', 'card', 'full-page']) {
  const html = render(PermissionGuard, { ...guardProps, variant });
  assert.match(html, /template\.manage/);
  assert.match(html, /Sao chép mã quyền/);
}
allowed = true;
assert.equal(render(PermissionGuard, guardProps), 'Nội dung được bảo vệ');
// API resource denial must remain visible even when the account has module-level RBAC.
assert.match(render(ResourceAccessErrorState, { kind: 'forbidden' }), /403/);
assert.doesNotMatch(render(ResourceAccessErrorState, { kind: 'forbidden' }), /Quyền yêu cầu/);
const missing = render(ResourceAccessErrorState, { kind: 'not-found', backHref: '/contracts', backLabel: 'Quay lại danh sách' });
assert.match(missing, /404/);
assert.match(missing, /href="\/contracts"/);
assert.doesNotMatch(missing, /sao chép mã quyền|Quyền yêu cầu|Thử lại/);
const failure = render(ResourceAccessErrorState, { kind: 'error', onRetry() {} });
assert.match(failure, /Thử lại/);
assert.doesNotMatch(failure, /403|404|Quyền yêu cầu/);
const embedded = render(ResourceAccessErrorState, { variant: 'card', showBackButton: false, showHomeButton: false });
assert.doesNotMatch(embedded, /Quay lại|Về trang tổng quan/);
console.log('Resource access: classification, RBAC, resource denial, 404, retry and variants PASS');
