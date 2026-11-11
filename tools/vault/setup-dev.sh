#!/usr/bin/env bash
# Configures a local Vault for Patterns.ai and wires the API to it. Idempotent; rerun after every
# dev-server restart (dev mode is in-memory, so the AppRole SecretId changes each time).
#
# What it does:
#   1. Ensures a KV v2 engine at secret/.
#   2. Creates policy "patternsai-read": read-only on secret/data/patternsai/*.
#   3. Creates AppRole "patternsai" bound to that policy and issues a SecretId.
#   4. Stores Vault settings for the API in .NET user-secrets (outside the repo).
#   5. Seeds a sample secret per tenant: secret/patternsai/<tenantId>/demo/saucedemo
#      (public SauceDemo credentials) so a variable "demo/saucedemo#password" works immediately.
set -euo pipefail

export VAULT_ADDR="${VAULT_ADDR:-http://127.0.0.1:8200}"
export VAULT_TOKEN="${VAULT_TOKEN:-dev-root}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
API_PROJECT="$ROOT/src/ATIP.Api"
DB_URL="${DB_URL:-postgresql://atip:atip@localhost:5432/atip}"

vault status >/dev/null || { echo "Vault is not reachable at $VAULT_ADDR (start tools/vault/dev-server.sh)"; exit 1; }

vault secrets list -format=json | grep -q '"secret/"' || vault secrets enable -path=secret kv-v2

vault policy write patternsai-read - <<'HCL'
path "secret/data/patternsai/*" {
  capabilities = ["read"]
}
path "secret/metadata/patternsai/*" {
  capabilities = ["read", "list"]
}
HCL

vault auth list -format=json | grep -q '"approle/"' || vault auth enable approle
vault write auth/approle/role/patternsai \
  token_policies=patternsai-read token_ttl=1h token_max_ttl=4h secret_id_ttl=0 >/dev/null

ROLE_ID="$(vault read -field=role_id auth/approle/role/patternsai/role-id)"
SECRET_ID="$(vault write -f -field=secret_id auth/approle/role/patternsai/secret-id)"

grep -q "<UserSecretsId>" "$API_PROJECT/ATIP.Api.csproj" || dotnet user-secrets init --project "$API_PROJECT" >/dev/null
us() { dotnet user-secrets set "$1" "$2" --project "$API_PROJECT" >/dev/null; }
us "Vault:Enabled" "true"
us "Vault:Address" "$VAULT_ADDR"
us "Vault:AuthMethod" "approle"
us "Vault:RoleId" "$ROLE_ID"
us "Vault:SecretId" "$SECRET_ID"

if command -v psql >/dev/null 2>&1; then
  for tenant in $(psql -t -A "$DB_URL" -c 'select "Id" from tenants' 2>/dev/null); do
    vault kv put "secret/patternsai/$tenant/demo/saucedemo" username=standard_user password=secret_sauce >/dev/null
    echo "Seeded secret/patternsai/$tenant/demo/saucedemo"
  done
fi

echo
echo "Vault ready at $VAULT_ADDR. AppRole credentials stored in user-secrets for ATIP.Api."
echo "Restart the API, then add an environment variable of type 'Vault' with value demo/saucedemo#password."
echo "Add your own: vault kv put secret/patternsai/<tenantId>/<app>/<account> username=... password=..."
