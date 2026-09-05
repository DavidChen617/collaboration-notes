#!/bin/bash
set -e

# Mirrors deploy/k8s/postgres/configmap-init.yaml: one shared Postgres instance,
# two databases with their own owning roles (see design.md decision #2 in
# openspec/changes/setup-infra-and-auth/).
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
  CREATE ROLE conotes_app WITH LOGIN PASSWORD '$APP_PASSWORD';
  CREATE DATABASE app OWNER conotes_app;

  CREATE ROLE conotes_keycloak WITH LOGIN PASSWORD '$KEYCLOAK_PASSWORD';
  CREATE DATABASE keycloak OWNER conotes_keycloak;
EOSQL
