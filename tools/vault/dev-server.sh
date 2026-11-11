#!/usr/bin/env bash
# Local HashiCorp Vault for development (in-memory dev mode: data is lost when it stops).
#   brew tap hashicorp/tap && brew install hashicorp/tap/vault
#   tools/vault/dev-server.sh        # terminal 1, keep running
#   tools/vault/setup-dev.sh         # terminal 2, after every Vault (re)start
# Never use dev mode outside your machine: it is unsealed, unencrypted and has a fixed root token.
set -euo pipefail

exec vault server -dev \
  -dev-root-token-id="${VAULT_DEV_ROOT_TOKEN:-dev-root}" \
  -dev-listen-address="${VAULT_DEV_LISTEN:-127.0.0.1:8200}"
