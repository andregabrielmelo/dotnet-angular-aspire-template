const apiEnviromentVariableName = 'services__web__https__0';
const apiTarget = process.env[apiEnviromentVariableName];

if (apiTarget === undefined) {
  throw new Error(
    `proxy.conf.js: ${apiEnviromentVariableName} is not set. This is injected by the Aspire AppHost via WithReference(api) on the "angular" resource. Check the Aspire dashboard > angular resource > Environment Variables if this keeps happening,the variable name may have changed.`,
  );
}

module.exports = {
  '/api': {
    target: apiTarget,
    secure: process.env['NODE_ENV'] !== 'development',
    logLevel: 'debug',
    changeOrigin: true,
    pathRewrite: {
      '^/api': '', // Remove the /api prefix
    },
  },
};
