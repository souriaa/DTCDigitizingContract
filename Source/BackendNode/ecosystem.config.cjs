module.exports = {
  apps: [{
    name: 'econtract-node', cwd: __dirname, script: 'index.js', instances: 1,
    exec_mode: 'fork', autorestart: true, max_memory_restart: '256M',
    kill_timeout: 10000, env: { NODE_ENV: 'production' }
  }]
};
