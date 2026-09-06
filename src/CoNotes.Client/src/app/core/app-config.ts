// 開發時期的預設值, 對應這個專案文件所描述的本機 stack(見 infra/README.md)。
// production 建置會用 angular.json 的 fileReplacements 換成 app-config.prod.ts。
export const API_BASE_URL = 'http://localhost:5094';

export const KEYCLOAK_CONFIG = {
  // Host port 用 8081, 不是 Keycloak 預設的 8080 - 見 infra/README.md
  // (8080 被本機 SigNoz UI 佔用了)。
  url: 'http://localhost:8081',
  realm: 'conotes',
  clientId: 'conotes-spa',
};
