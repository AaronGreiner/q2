#!/usr/bin/env bash
#
# q2 — release deployment. Runs ON THE SERVER, invoked over SSH by
# .github/workflows/release.yml.
#
# Contract with the workflow: everything needed is already in
# /var/www/q2/incoming — two tarballs, the two rendered environment files and
# this script. The workflow uploads, this script decides.
#
# Why a script on the server rather than a list of SSH commands in the
# workflow: the interesting part of a deployment is what happens when a step
# fails halfway through. That logic wants to be in one readable file that can
# be run by hand during an incident, not spread across YAML steps that abort
# wherever they happen to be.
#
# Guarantees:
#   * the previous release is kept and restored automatically if the new one
#     does not become healthy;
#   * migrations run after the swap and before the API is started, because
#     Staging and Production refuse to migrate on startup by design;
#   * secrets are never echoed — the environment files are moved, never printed.
#
# Usage: deploy.sh <release-id>          e.g. deploy.sh q2@1.2.3

set -euo pipefail

readonly ROOT=/var/www/q2
readonly INCOMING="$ROOT/incoming"
readonly SHARED="$ROOT/shared"
readonly PREVIOUS="$ROOT/previous"
readonly FAILED="$ROOT/failed"
readonly UNPACKED="$INCOMING/unpacked"

readonly API_URL=http://127.0.0.1:5080
readonly APP_URL=http://127.0.0.1:3002

readonly RELEASE="${1:?Usage: deploy.sh <release-id>}"

log()  { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
info() { printf '    %s\n' "$*"; }

# Set once the rollback is armed, and read by `fail`. `exit` does not fire an
# ERR trap, so a `fail` after the swap would otherwise end the deployment
# leaving the new, broken release in place and the previous one stranded in
# previous/ — which is the one outcome step 2 exists to prevent. Every `fail`
# before the trap is armed still just exits, because nothing has been touched.
rollback_armed=false

fail() {
  printf '\n\033[31mERROR: %s\033[0m\n' "$*" >&2
  $rollback_armed && rollback
  exit 1
}

# Two deployments at once would interleave the swap and leave a mixed tree.
exec 9>/var/lock/q2-deploy.lock
flock --nonblock 9 || fail "Another deployment is already running."

# ---------------------------------------------------------------------------
# 1. Validate what was uploaded, before anything is touched.
# ---------------------------------------------------------------------------
log "Validating upload for $RELEASE"

for f in "$INCOMING/api.tar.gz" "$INCOMING/app.tar.gz"; do
  [ -s "$f" ] || fail "Missing or empty: $f"
done

rm -rf "$UNPACKED"
mkdir -p "$UNPACKED/api" "$UNPACKED/app"

# --warning=no-unknown-keyword: a tarball written by BSD tar (a macOS machine
# running this by hand) carries xattr headers GNU tar does not know, and the
# resulting flood of warnings buries the errors that matter.
tar --warning=no-unknown-keyword -xzf "$INCOMING/api.tar.gz" -C "$UNPACKED/api"
tar --warning=no-unknown-keyword -xzf "$INCOMING/app.tar.gz" -C "$UNPACKED/app"

# A tarball that unpacks but has no entrypoint would pass every step until the
# service fails to start, by which point the old release is already gone.
[ -f "$UNPACKED/api/Q2.Api.dll" ] \
  || fail "api.tar.gz does not contain Q2.Api.dll"
[ -f "$UNPACKED/app/.output/server/index.mjs" ] \
  || fail "app.tar.gz does not contain .output/server/index.mjs"

info "api: $(du -sh "$UNPACKED/api" | cut -f1), app: $(du -sh "$UNPACKED/app" | cut -f1)"

# ---------------------------------------------------------------------------
# 2. Snapshot everything a rollback has to put back, and arm the rollback.
#
#    The configuration is included, not just the two application directories:
#    a rollback that restored the old binaries but left the new release.env in
#    place would leave the service reporting a release to Sentry that was never
#    successfully deployed. Every later error would be filed under a version
#    that does not exist.
# ---------------------------------------------------------------------------
log "Snapshotting the current release"

mkdir -p "$SHARED"
rm -rf "$PREVIOUS"
mkdir -p "$PREVIOUS"
cp -a "$SHARED" "$PREVIOUS/shared"
info "configuration snapshotted"

rolled_back=false

rollback() {
  $rolled_back && return
  rolled_back=true

  log "Rolling back to the previous release"
  systemctl stop q2-app.service q2-api.service 2>/dev/null || true

  # The release that failed is kept rather than deleted: without it the only
  # evidence of why a deployment failed is gone by the time anyone looks.
  rm -rf "$FAILED"
  mkdir -p "$FAILED"

  for name in api app; do
    if [ -d "$PREVIOUS/$name" ]; then
      if [ -d "$ROOT/$name" ]; then
        mv "$ROOT/$name" "$FAILED/$name"
      fi
      mv "$PREVIOUS/$name" "$ROOT/$name"
      info "$name restored"
    fi
  done

  if [ -d "$PREVIOUS/shared" ]; then
    rm -rf "$SHARED"
    mv "$PREVIOUS/shared" "$SHARED"
    info "configuration restored"
  fi

  systemctl start q2-api.service q2-app.service 2>/dev/null || true
  info "Services restarted from the previous release."
  info "The failed release is in $FAILED."

  # Explicit, rather than relying on the exit status bash happens to carry out
  # of an ERR trap. A rollback reported to the workflow as success would be the
  # worst outcome this script can produce: the site would be serving the old
  # release while the release notes claim the new one shipped.
  exit 1
}

trap 'rollback' ERR
rollback_armed=true

# ---------------------------------------------------------------------------
# 3. Configuration. The workflow renders both files from GitHub secrets, so a
#    changed secret reaches the server on the next deploy and nowhere else.
#    Absent files mean "keep what is already installed", which is what makes a
#    manual re-run of this script safe.
# ---------------------------------------------------------------------------
log "Installing configuration"

for name in api app; do
  if [ -f "$INCOMING/$name.env" ]; then
    install -m 600 "$INCOMING/$name.env" "$SHARED/$name.env"
    info "$name.env updated"
  else
    [ -f "$SHARED/$name.env" ] || fail "No $name.env uploaded and none installed."
    info "$name.env kept"
  fi
done

# The one thing that genuinely differs per release. Both services read it;
# each ignores the variables meant for the other.
cat > "$SHARED/release.env" <<EOF
# Written by deploy.sh — do not edit, the next deployment overwrites it.
Q2_RELEASE=$RELEASE
Sentry__Release=$RELEASE
NUXT_PUBLIC_SENTRY_RELEASE=$RELEASE
GIT_COMMIT_SHA=${GIT_COMMIT_SHA:-}
CI_RUN_ID=${CI_RUN_ID:-}
EOF
chmod 600 "$SHARED/release.env"
info "release.env written for $RELEASE"

# ---------------------------------------------------------------------------
# 4. Swap.
# ---------------------------------------------------------------------------
log "Stopping services"
systemctl stop q2-app.service q2-api.service 2>/dev/null || true

log "Swapping in $RELEASE"

for name in api app; do
  # An explicit `if` rather than `[ -d … ] && mv …`: under `set -e` with the
  # rollback trap armed, the short form would abort the deployment on the very
  # first release, when there is nothing to move aside yet.
  if [ -d "$ROOT/$name" ]; then
    mv "$ROOT/$name" "$PREVIOUS/$name"
  fi
  mv "$UNPACKED/$name" "$ROOT/$name"
done
info "New release in place, previous kept in $PREVIOUS"

# ---------------------------------------------------------------------------
# 5. Migrations — an explicit deployment step. Staging and Production have
#    MigrateOnStartup=false on purpose (docs/next-steps.md section 11).
# ---------------------------------------------------------------------------
log "Applying database migrations"

# Subshell: the exported configuration must not leak into the rest of the run.
(
  set -a
  # shellcheck disable=SC1090,SC1091
  . "$SHARED/api.env"
  . "$SHARED/release.env"
  set +a
  cd "$ROOT/api"
  /usr/bin/dotnet "$ROOT/api/Q2.Api.dll" db migrate
)
info "Migrations applied"

# ---------------------------------------------------------------------------
# 6. Start and prove it actually works. A deployment that reports success
#    without a request having been served is not a deployment.
#
#    "Up" is not "working", and the difference is not academic: v0.0.3 started
#    cleanly, reported healthy and served a page — and every screen in it was
#    an error. /health only opens the database, and an empty database opens
#    perfectly well; the frontend's error state is a well-formed HTML document,
#    so a check for '<!DOCTYPE html>' passes on an app that renders nothing but
#    failures. Both proofs below assert what that release actually broke.
# ---------------------------------------------------------------------------

# The guarded endpoint the proof below uses. Any of them would do — this one is
# the start screen's first request, so a failure here is a failure the first
# visitor sees.
readonly GUARDED_ROUTE=/api/profile

wait_for() {
  local name=$1 url=$2 expect=$3 attempts=${4:-30}

  for ((i = 1; i <= attempts; i++)); do
    # -L: an anonymous request to the frontend's "/" is answered with a 302 to
    # the sign-in screen, and a redirect carries no markup to match against.
    if body=$(curl -fsSL --max-time 5 "$url" 2>/dev/null) && [[ $body == *"$expect"* ]]; then
      info "$name healthy after ${i}s"
      return 0
    fi
    sleep 1
  done

  printf '\n--- last 40 log lines: %s ---\n' "$name" >&2
  journalctl -u "$name" -n 40 --no-pager >&2 || true
  return 1
}

# No credentials, no seed, no state: an anonymous request to a guarded route
# must be refused with 401. A 5xx means the API cannot work out who is asking,
# which is how a release can be "healthy" and useless at the same time.
#
# No -f: it would turn the 401 this wants to see into a curl failure.
prove_api_refuses_anonymously() {
  local status
  status=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 5 "$API_URL$GUARDED_ROUTE" 2>/dev/null) \
    || status=000

  if [ "$status" = 401 ]; then
    info "$GUARDED_ROUTE refuses an anonymous request with 401"
    return 0
  fi

  printf '\n%s answered %s; expected 401.\n' "$GUARDED_ROUTE" "$status" >&2
  printf -- '--- last 40 log lines: q2-api.service ---\n' >&2
  journalctl -u q2-api.service -n 40 --no-pager >&2 || true
  return 1
}

# The frontend served *a* document; this asks whether that document is a screen
# or an apology. `data-testid="error-state"` is AppErrorState, the component
# every failed screen renders, and the E2E suite already depends on that id.
prove_app_rendered_a_screen() {
  local page
  page=$(curl -fsSL --max-time 10 "$APP_URL/" 2>/dev/null) || return 1

  if [[ $page != *'data-testid="error-state"'* ]]; then
    info "the frontend rendered a screen, not an error state"
    return 0
  fi

  printf '\nThe frontend rendered its error state — the app is up but broken.\n' >&2
  printf -- '--- last 40 log lines: q2-app.service ---\n' >&2
  journalctl -u q2-app.service -n 40 --no-pager >&2 || true
  return 1
}

log "Starting q2-api"
systemctl start q2-api.service
wait_for q2-api.service "$API_URL/health" '"healthy"' || fail "The API did not become healthy."
prove_api_refuses_anonymously || fail "The API is running but cannot answer a request."

log "Starting q2-app"
systemctl start q2-app.service
wait_for q2-app.service "$APP_URL/" '<!DOCTYPE html>' || fail "The frontend did not become healthy."
prove_app_rendered_a_screen || fail "The frontend is running but every screen is an error."

trap - ERR

# ---------------------------------------------------------------------------
# 7. Report. The release the services actually report, not the one we intended.
# ---------------------------------------------------------------------------
log "Deployed $RELEASE"
systemctl is-active q2-api.service q2-app.service | tr '\n' ' '; echo
info "Rollback: $PREVIOUS still holds the previous release."

rm -f "$INCOMING"/*.tar.gz "$INCOMING"/api.env "$INCOMING"/app.env
rm -rf "$UNPACKED"
