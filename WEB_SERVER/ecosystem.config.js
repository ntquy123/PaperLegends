module.exports = {
  apps: [
    {
      name: 'paper-legends-api',
      script: 'dist/server.js',
      cwd: __dirname,
      instances: 1,
      exec_mode: 'fork',
      env_file: '.env',
      env: {
        NODE_ENV: 'production',
        API_PORT: process.env.API_PORT || '5001',
        REDIS_URL: process.env.REDIS_URL,
        REDIS_HOST: process.env.REDIS_HOST || '127.0.0.1',
        REDIS_PORT: process.env.REDIS_PORT || '6379',
        DS_PORT_START: process.env.DS_PORT_START || '27200',
        DS_PORT_END: process.env.DS_PORT_END || '27299',
        DS_CONTAINER_PORT: process.env.DS_CONTAINER_PORT || '27015',
      },
    },
    // Paper Legends does not use market trading, so marketPrice and
    // marketBuyRequestMatcher workers are intentionally disabled.
    //not use temporarily
    // {
    //   name: 'paper-legends-monitor',
    //   script: 'monitor.js',
    //   cwd: __dirname,
    //   instances: 1,
    //   exec_mode: 'fork',
    //   env: {
    //     NODE_ENV: 'production',
    //   },
    // },
  ],
};
