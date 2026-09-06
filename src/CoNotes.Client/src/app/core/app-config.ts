// 開發時期的預設值, 對應這個專案文件所描述的本機 stack
// (見 infra/README.md)。目前專案還沒有 environments/ 設定;
// 正式部署前, production 的值(https://api.<domain>, https://auth.<domain>/realms/conotes)
// 應該要取代這裡的內容。
export const API_BASE_URL = 'http://localhost:5094';

export const KEYCLOAK_CONFIG = {
  // Host port 用 8081, 不是 Keycloak 預設的 8080 - 見 infra/README.md
  // (8080 被本機 SigNoz UI 佔用了)。
  url: 'http://localhost:8081',
  realm: 'conotes',
  clientId: 'conotes-spa',
};
