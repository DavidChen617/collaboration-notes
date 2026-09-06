#!/usr/bin/env bash
# 讀取伺服器端單一 .env 檔案，依照 key 名稱分別套用成 collaboration-notes
# namespace 底下對應的 k8s Secret：
#   conotes-secrets        <- PayPal__*, Ai__*（API 用 envFrom 整包注入）
#   postgres-credentials   <- postgres-password, app-password, keycloak-password
#   keycloak-credentials   <- admin-password
#   cloudflared-credentials <- TUNNEL_TOKEN
# 這三個 Secret 都刻意不 commit 進 git 真實內容（只有空殼在 infra/k8s 裡讓 ArgoCD
# 管理存在性，見 infra/argocd/application.yaml 的 ignoreDifferences），避免
# selfHeal 把伺服器端套用的真實密鑰值改回去(見 infra/README.md「密鑰管理」)。
# 用法：./infra/scripts/apply-secrets.sh [.env 檔案路徑，預設 /etc/conotes/secrets.env]
set -euo pipefail

ENV_FILE="${1:-/etc/conotes/secrets.env}"
NAMESPACE="collaboration-notes"

if [ ! -f "${ENV_FILE}" ]; then
  echo "找不到 .env 檔案：${ENV_FILE}" >&2
  exit 1
fi

CONOTES_ARGS=()
POSTGRES_ARGS=()
KEYCLOAK_ARGS=()
CLOUDFLARED_ARGS=()

while IFS='=' read -r KEY VALUE; do
  # 略過空行、註解行
  [[ -z "${KEY}" || "${KEY}" == \#* ]] && continue

  case "${KEY}" in
    postgres-password | app-password | keycloak-password)
      POSTGRES_ARGS+=("--from-literal=${KEY}=${VALUE}")
      ;;
    admin-password)
      KEYCLOAK_ARGS+=("--from-literal=${KEY}=${VALUE}")
      ;;
    TUNNEL_TOKEN)
      CLOUDFLARED_ARGS+=("--from-literal=${KEY}=${VALUE}")
      ;;
    *)
      CONOTES_ARGS+=("--from-literal=${KEY}=${VALUE}")
      ;;
  esac
done < "${ENV_FILE}"

apply_secret() {
  local SECRET_NAME="$1"
  shift
  local ARGS=("$@")

  if [ "${#ARGS[@]}" -eq 0 ]; then
    echo "跳過 Secret ${SECRET_NAME}：${ENV_FILE} 裡沒有對應的 key" >&2
    return
  fi

  kubectl create secret generic "${SECRET_NAME}" \
    --namespace "${NAMESPACE}" \
    "${ARGS[@]}" \
    --dry-run=client -o yaml \
    | kubectl apply -f -

  echo "已套用 Secret ${SECRET_NAME} (namespace: ${NAMESPACE})"
}

apply_secret conotes-secrets "${CONOTES_ARGS[@]}"
apply_secret postgres-credentials "${POSTGRES_ARGS[@]}"
apply_secret keycloak-credentials "${KEYCLOAK_ARGS[@]}"
apply_secret cloudflared-credentials "${CLOUDFLARED_ARGS[@]}"
