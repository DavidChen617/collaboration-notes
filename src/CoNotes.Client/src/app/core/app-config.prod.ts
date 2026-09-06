// production 建置版本, 透過 angular.json 的 fileReplacements 換掉 app-config.ts。
export const API_BASE_URL = 'https://api.davish.net';

export const KEYCLOAK_CONFIG = {
  url: 'https://auth.davish.net',
  realm: 'conotes',
  clientId: 'conotes-spa',
};
