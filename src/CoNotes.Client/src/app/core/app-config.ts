// Dev-time defaults, matching the local stack this project's docs describe
// (see deploy/README.md). No environments/ setup exists yet in this project;
// production values (https://api.<domain>, https://auth.<domain>/realms/conotes)
// should replace these before any real deployment.
export const API_BASE_URL = 'http://localhost:5088';

export const KEYCLOAK_CONFIG = {
  url: 'http://localhost:8080',
  realm: 'conotes',
  clientId: 'conotes-spa',
};
