#!/usr/bin/env bash
# 讀取伺服器端單一 .env 檔案，依照 key 名稱分別套用成 collaboration-notes
# namespace 底下對應的 k8s Secret：
#   conotes-secrets        <- PayPal__*, Ai__*（API 用 envFrom 整包注入）
#   postgres-credentials   <- postgres-password, app-password, keycloak-password
#   keycloak-credentials   <- admin-password
#   cloudflared-credentials <- TUNNEL_TOKEN
# 這四個 Secret 完全不進 git、也不讓 ArgoCD 知道它們存在(見 infra/README.md
# 「密鑰管理」)。曾經試過在 infra/k8s 放空殼 manifest + ArgoCD ignoreDifferences
# 讓 ArgoCD 至少管理存在性，但 ignoreDifferences 不保證擋住 sync 時的實際覆蓋
# (連 RespectIgnoreDifferences=true 都遇到已知的 ArgoCD bug 沒擋住,
# github.com/argoproj/argo-cd/issues/8970),真的把這幾個 Secret 的密碼清空過一次
# 導致 Keycloak/API 掛掉——之後改回完全不進 git 這個更保守但可靠的做法。
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
