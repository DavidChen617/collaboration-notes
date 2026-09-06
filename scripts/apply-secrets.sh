#!/usr/bin/env bash
# 讀取伺服器端的 .env 檔案，套用成 collaboration-notes namespace 底下的
# conotes-secrets 這個 k8s Secret；API 的 Deployment 透過 envFrom 把這個 Secret
# 的每個 key 直接當環境變數注入(見 infra/k8s/api/deployment.yaml)。
# 用法：./scripts/apply-secrets.sh [.env 檔案路徑，預設 /etc/conotes/secrets.env]
set -euo pipefail

ENV_FILE="${1:-/etc/conotes/secrets.env}"
NAMESPACE="collaboration-notes"
SECRET_NAME="conotes-secrets"

if [ ! -f "$ENV_FILE" ]; then
  echo "找不到 .env 檔案：$ENV_FILE" >&2
  exit 1
fi

kubectl create secret generic "$SECRET_NAME" \
  --namespace "$NAMESPACE" \
  --from-env-file="$ENV_FILE" \
  --dry-run=client -o yaml \
  | kubectl apply -f -

echo "已套用 Secret ${SECRET_NAME} (namespace: ${NAMESPACE})，內容來自 ${ENV_FILE}"
