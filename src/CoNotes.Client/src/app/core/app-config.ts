// Dev-time defaults, matching the local stack this project's docs describe
// (see infra/README.md). No environments/ setup exists yet in this project;
// production values (https://api.<domain>, https://auth.<domain>/realms/conotes)
// should replace these before any real deployment.
export const API_BASE_URL = 'http://localhost:5094';

export const KEYCLOAK_CONFIG = {
  // Host port 8081, not Keycloak's default 8080 - see infra/README.md
  // (8080 is taken by the local SigNoz UI).
  url: 'http://localhost:8081',
  realm: 'conotes',
  clientId: 'conotes-spa',
};
