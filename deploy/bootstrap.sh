#!/usr/bin/env bash
#
# q2 — one-time server preparation. Idempotent: running it again changes
# nothing that is already correct.
#
# This is not part of a deployment. It creates the things a deployment assumes
# already exist — directories, systemd units, the Caddy site, the runtime
# configuration — so that deploy.sh only ever has to move files and restart
# services.
#
# The host also runs other projects (tram, tcg_home, Caddy). Nothing here
# touches them: the Caddy block is appended only if absent, and no shared file
# is rewritten.
#
# Usage, from a checkout on the server or over SSH:
#   Q2_API_DSN=... Q2_APP_DSN=... bash bootstrap.sh
#
# The DSNs are optional. Without them the services still start and simply do
# not report to Sentry; the first deployment installs the real values from the
# repository secrets.

set -euo pipefail

readonly ROOT=/var/www/q2
readonly SHARED="$ROOT/shared"
readonly DOMAIN=q2.aarongreiner.dev
readonly CADDYFILE=/etc/caddy/Caddyfile
readonly UNIT_DIR=/etc/systemd/system
readonly HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

log()  { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
info() { printf '    %s\n' "$*"; }
fail() { printf '\n\033[31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

[ "$(id -u)" -eq 0 ] || fail "Run as root."

# ---------------------------------------------------------------------------
# 1. Runtime. The API is published framework-dependent, so the host supplies
#    the ASP.NET Core runtime and apt keeps it patched — same arrangement as
#    Node for the two Nuxt services already on this box.
# ---------------------------------------------------------------------------
log "Checking runtimes"

if ! dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App 10\.'; then
  info "Installing aspnetcore-runtime-10.0"
  DEBIAN_FRONTEND=noninteractive apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq aspnetcore-runtime-10.0
fi
info "dotnet: $(dotnet --list-runtimes | grep AspNetCore | head -1)"

command -v node >/dev/null || fail "node is not installed."
info "node:   $(node --version)"

# ---------------------------------------------------------------------------
# 2. Layout.
#
#    shared/ and .data/ sit outside api/ and app/ on purpose: those two are
#    replaced wholesale by every deployment, and configuration and the database
#    must survive that.
# ---------------------------------------------------------------------------
log "Creating $ROOT"

mkdir -p "$ROOT"/{api,app,.data,shared,incoming,previous}
chmod 700 "$SHARED"
info "api/ app/ .data/ shared/ incoming/ previous/"

# ---------------------------------------------------------------------------
# 3. Runtime configuration.
#
#    Written only when absent — a deployment is the authority on these files,
#    and re-running bootstrap must never overwrite a value that came from a
#    repository secret.
# ---------------------------------------------------------------------------
log "Runtime configuration"

if [ ! -f "$SHARED/api.env" ]; then
  cat > "$SHARED/api.env" <<EOF
# Backend configuration. Replaced by every deployment from GitHub secrets.
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://127.0.0.1:5080

# A full SQLite connection string, not a bare file name — DatabaseLocation
# parses it with SqliteConnectionStringBuilder. The relative Data Source is
# resolved against Q2_DATA_DIR, never the working directory.
#
# Quoted because of the space: systemd strips the quotes, and deploy.sh sources
# this file with bash, which would otherwise read "Source=..." as a command.
Q2_DATA_DIR=$ROOT/.data
ConnectionStrings__Database="Data Source=q2-staging.db"

Sentry__Dsn=${Q2_API_DSN:-}
Sentry__Debug=false
# Errors are never sampled away, and this host exists to be analysed, so
# traces are kept in full too — appsettings.Staging.json defaults to 0.5.
Sentry__SampleRate=1.0
Sentry__TracesSampleRate=1.0
EOF
  chmod 600 "$SHARED/api.env"
  info "api.env created"
else
  info "api.env exists — kept"
fi

if [ ! -f "$SHARED/app.env" ]; then
  cat > "$SHARED/app.env" <<EOF
# Frontend configuration. Replaced by every deployment from GitHub secrets.
# The API is reached through the same origin, so the browser needs no CORS and
# the value works unchanged for SSR and for the client.
NUXT_PUBLIC_API_BASE_URL=https://$DOMAIN
NUXT_PUBLIC_APP_ENV=staging

# /diagnostics calls /api/diagnostics/*, which does not exist in Staging by
# design (DiagnosticsEndpoints skips Protected environments). Enabling the page
# here would show a permanently broken panel.
NUXT_PUBLIC_DIAGNOSTICS_ENABLED=false

NUXT_PUBLIC_SENTRY_DSN=${Q2_APP_DSN:-}
NUXT_PUBLIC_SENTRY_ENVIRONMENT=staging
NUXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE=1
EOF
  chmod 600 "$SHARED/app.env"
  info "app.env created"
else
  info "app.env exists — kept"
fi

# ---------------------------------------------------------------------------
# 4. systemd units.
# ---------------------------------------------------------------------------
log "Installing systemd units"

install -m 644 "$HERE/server/q2-api.service" "$UNIT_DIR/q2-api.service"
install -m 644 "$HERE/server/q2-app.service" "$UNIT_DIR/q2-app.service"
systemctl daemon-reload
systemctl enable q2-api.service q2-app.service >/dev/null
info "q2-api.service and q2-app.service installed and enabled"

# ---------------------------------------------------------------------------
# 5. Caddy.
#
#    Appended to the existing Caddyfile in the same style as the tram and
#    tcg_home blocks, and only when the domain is not already configured.
# ---------------------------------------------------------------------------
log "Configuring Caddy"

if grep -q "^$DOMAIN" "$CADDYFILE"; then
  info "$DOMAIN already present — unchanged"
else
  cp "$CADDYFILE" "$CADDYFILE.bak-$(date +%Y%m%d%H%M%S)"

  cat >> "$CADDYFILE" <<EOF

# q2 — frontend and API share one origin, so the browser makes same-origin
# requests and the API needs no CORS configuration.
$DOMAIN {
	encode zstd gzip

	handle /api/* {
		reverse_proxy 127.0.0.1:5080
	}
	handle /health {
		reverse_proxy 127.0.0.1:5080
	}
	handle /openapi/* {
		reverse_proxy 127.0.0.1:5080
	}
	handle {
		reverse_proxy 127.0.0.1:3002
	}
}
EOF
  info "$DOMAIN block appended (backup taken)"
fi

caddy validate --config "$CADDYFILE" --adapter caddyfile >/dev/null 2>&1 \
  || fail "The Caddyfile is invalid — not reloading. Restore from the .bak file."

systemctl reload caddy
info "Caddy validated and reloaded"

log "Server prepared"
info "Deploy with: .github/workflows/release.yml (tag v<version>)"
info "DNS still required: A record $DOMAIN -> this host"
