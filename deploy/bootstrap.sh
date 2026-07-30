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
# 5. DNS, checked before Caddy is touched.
#
#    Caddy asks for a certificate the moment it loads a site block, not on the
#    first request. If the A record does not exist yet, Let's Encrypt answers
#    NXDOMAIN, Caddy backs off for hours, and until something makes it retry
#    every TLS handshake for this domain fails with an internal error — while
#    both services sit there perfectly healthy on loopback. That looks like a
#    broken release and is not one.
#
#    So this is checked here rather than mentioned at the end. Everything above
#    is local and worth having regardless, which is why the check sits at this
#    point and not at the top: preparing the host before DNS exists is fine,
#    asking a CA to validate it is not.
# ---------------------------------------------------------------------------
log "Checking DNS for $DOMAIN"

# Public DNS is what the CA resolves, so ask a public resolver when dig is
# available; getent would also be satisfied by an /etc/hosts entry the CA
# cannot see.
#
# The first A record is picked with a loop rather than `| head -1`. A reader
# that exits early closes the pipe under its writer, and pipefail would then
# turn a lookup that actually succeeded into an aborted script. Both outputs
# here are one small write, so it would not bite in practice — but this script
# only exists to stop a plumbing detail from masquerading as a broken host.
resolve_a() {
  local out line
  if command -v dig >/dev/null; then
    out=$(dig +short A "$1" @1.1.1.1 2>/dev/null) || out=
  else
    out=$(getent ahostsv4 "$1" 2>/dev/null) || out=
  fi

  # An explicit `if`, for the reason deploy.sh gives at its own: a trailing
  # `[[ … ]] && …` that does not match is a failing last command in the loop
  # body, which set -e would take as an error.
  while IFS=' ' read -r line _; do
    if [[ $line =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ ]]; then
      printf '%s\n' "$line"
      return 0
    fi
  done <<<"$out"

  return 0
}

RESOLVED="$(resolve_a "$DOMAIN")"

if [ -z "$RESOLVED" ]; then
  fail "$DOMAIN does not resolve. Create the A record pointing at this host,
       wait for it to propagate, then run this script again — it is idempotent,
       and everything above is already done. Adding the Caddy site now would
       burn the certificate request and leave the domain unreachable over TLS
       until Caddy is reloaded again."
fi

LOCAL_IPS="$(ip -4 -o addr show scope global 2>/dev/null | awk '{split($4,a,"/"); print a[1]}')"

# Exact whole-line membership, done in bash: `| grep -qxF` would be a reader
# that exits on the first match, and if that ever raced the writer, pipefail
# would report "not this host" for an address that is in fact local.
if [[ $'\n'$LOCAL_IPS$'\n' == *$'\n'$RESOLVED$'\n'* ]]; then
  info "$DOMAIN -> $RESOLVED (this host)"
else
  info "WARNING: $DOMAIN -> $RESOLVED, which is not an address of this host."
  info "         Legitimate behind NAT or a proxy; otherwise the certificate"
  info "         request below will fail. The check at the end will say which."
fi

# ---------------------------------------------------------------------------
# 6. Caddy.
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

# ---------------------------------------------------------------------------
# 7. Prove the certificate exists.
#
#    The reload above is what triggers issuance, so this is the moment it can
#    be observed. Without this the script reports success and the failure
#    surfaces much later, as a curl exit 35 in the release smoke test, with the
#    explanation already scrolled out of the Caddy journal.
# ---------------------------------------------------------------------------
log "Waiting for the TLS certificate"

# Deliberately not -f: no backend is running yet at bootstrap time, so a 502 is
# the expected answer and any HTTP status means the handshake succeeded. That
# handshake is the whole assertion.
for ((i = 1; i <= 60; i++)); do
  if curl -sS -o /dev/null --max-time 5 "https://$DOMAIN/" 2>/dev/null; then
    CERT_OK=1
    info "Certificate obtained and served after ${i}s"
    break
  fi
  sleep 1
done

if [ -z "${CERT_OK:-}" ]; then
  printf '\n--- caddy, recent certificate log lines ---\n' >&2
  journalctl -u caddy --no-pager -n 300 2>/dev/null \
    | grep -iE 'acme|tls\.obtain|certificate' | tail -20 >&2 || true
  fail "Caddy did not obtain a certificate for $DOMAIN — see the lines above.
       The site block is installed and the services are set up; nothing needs
       undoing. Fix what those lines report, then 'systemctl reload caddy' to
       make Caddy retry. It will not retry promptly on its own."
fi

log "Server prepared"
info "$DOMAIN resolves, and Caddy is serving a certificate for it"
info "Deploy with: .github/workflows/release.yml (tag v<version>)"
