#!/usr/bin/env bash
#
# q2 — one-click local development start for macOS.
#
#   ./start-dev.command              API + Nuxt against the persistent
#                                    Development database (nothing is deleted)
#   ./start-dev.command --manual     ManualTesting environment: the database is
#                                    rebuilt from scratch and freshly seeded
#   ./start-dev.command --help       all flags
#
# Double-clicking the file in Finder works too — that is why it is a .command
# and not a .sh.
#
# The script only prepares what is missing and then hands over to the existing
# root scripts (`bun run dev` / `bun run test:manual:start`); it deliberately
# does not re-implement anything they already do.

set -euo pipefail

# --- Where we are -----------------------------------------------------------
# Double-clicking starts the script with $HOME as the working directory, so the
# repository root is derived from the script's own location, never from $PWD.
REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$REPO_ROOT"

API_URL="http://localhost:5080"
APP_URL="http://localhost:3000"
API_PORT=5080
APP_PORT=3000

MODE="dev"          # dev | manual
OPEN_BROWSER=1
FORCE_PORTS=0

BOLD=$'\033[1m'; DIM=$'\033[2m'; RED=$'\033[31m'; GREEN=$'\033[32m'
YELLOW=$'\033[33m'; CYAN=$'\033[36m'; RESET=$'\033[0m'

heading() { printf '\n%s▶ %s%s\n' "$BOLD$CYAN" "$1" "$RESET"; }
info()    { printf '%s%s%s\n' "$DIM" "$1" "$RESET"; }
ok()      { printf '%s✔ %s%s\n' "$GREEN" "$1" "$RESET"; }
warn()    { printf '%s! %s%s\n' "$YELLOW" "$1" "$RESET"; }
fail()    { printf '%s✖ %s%s\n' "$RED" "$1" "$RESET" >&2; }

die() {
  fail "$1"
  shift
  for line in "$@"; do info "  $line"; done
  # Keep the window readable when the script was double-clicked in Finder.
  if [[ -t 0 ]]; then
    printf '\n'
    read -r -p "Press Return to close… " _ || true
  fi
  exit 1
}

usage() {
  cat <<'USAGE'
q2 local development start (macOS)

  ./start-dev.command [options]

Options
  -m, --manual       Start the ManualTesting environment instead of Development.
                     Its database (api/.data/q2-manual-testing.db) is DELETED and
                     rebuilt from the ManualTesting seed on every start. Your
                     Development database is never touched by either mode.
  -n, --no-open      Do not open http://localhost:3000 once Nuxt is up.
  -f, --force-ports  Stop whatever already listens on 3000/5080 without asking.
  -h, --help         Show this text.

What it does
  1. checks Bun, the .NET SDK and Node
  2. creates missing .env files from the committed .env.example templates
  3. runs `bun install` when the frontend dependencies are missing or stale
  4. restores the .NET tools and creates the Development database on first run
  5. frees ports 3000 / 5080 if you let it
  6. starts API + Nuxt and opens the browser

For a completely fresh checkout (including the Playwright browsers the E2E
suite needs) run `bun run setup` once — this script does not install those.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -m|--manual)      MODE="manual" ;;
    -n|--no-open)     OPEN_BROWSER=0 ;;
    -f|--force-ports) FORCE_PORTS=1 ;;
    -h|--help)        usage; exit 0 ;;
    *)                usage >&2; die "Unknown option: $1" ;;
  esac
  shift
done

printf '%s\n' "${BOLD}q2 — local development${RESET}"
info "  repository: $REPO_ROOT"
info "  mode:       $([[ $MODE == manual ]] && echo 'ManualTesting (database is rebuilt and seeded)' || echo 'Development (database is kept)')"

# --- 1. Prerequisites -------------------------------------------------------
heading "Checking prerequisites"

if [[ "$(uname -s)" != "Darwin" ]]; then
  warn "This script is written for macOS; on anything else use \`bun run dev\` directly."
fi

# `dotnet` is not on PATH in a GUI session unless the installer's entry was
# picked up, so look where the official .NET installer puts it.
if ! command -v dotnet >/dev/null 2>&1; then
  for candidate in /usr/local/share/dotnet /opt/homebrew/share/dotnet "$HOME/.dotnet"; do
    if [[ -x "$candidate/dotnet" ]]; then
      PATH="$candidate:$PATH"
      export PATH
      break
    fi
  done
fi

# Same story for Bun, which installs into ~/.bun/bin.
if ! command -v bun >/dev/null 2>&1 && [[ -x "$HOME/.bun/bin/bun" ]]; then
  PATH="$HOME/.bun/bin:$PATH"
  export PATH
fi

command -v bun >/dev/null 2>&1 || die "Bun is not installed (1.3.9 or newer is expected)." \
  "Install it with:  curl -fsSL https://bun.sh/install | bash" \
  "or:               brew install oven-sh/bun/bun"

command -v dotnet >/dev/null 2>&1 || die "The .NET SDK is not installed (10.0.x is expected)." \
  "Download it from: https://dotnet.microsoft.com/download/dotnet/10.0" \
  "or:               brew install --cask dotnet-sdk"

BUN_VERSION="$(bun --version)"
if [[ "$(printf '%s\n1.3.0\n' "$BUN_VERSION" | sort -V | head -n 1)" != "1.3.0" ]]; then
  die "Bun $BUN_VERSION is too old — 1.3.0 or newer is required (package.json pins 1.3.9)." \
      "Update it with:  bun upgrade"
fi
ok "bun $BUN_VERSION"

if ! dotnet --list-sdks | grep -q '^10\.'; then
  die "No .NET 10 SDK found. Installed SDKs:" "$(dotnet --list-sdks | tr '\n' ' ')" \
      "Get 10.0.x from https://dotnet.microsoft.com/download/dotnet/10.0"
fi
ok ".NET SDK $(dotnet --list-sdks | grep '^10\.' | tail -n 1 | cut -d' ' -f1)"

# Node is not needed to serve the app, but vue-tsc, Vitest's coverage provider
# and Playwright are `#!/usr/bin/env node` scripts that fail *quietly* without
# it — see README.md section 3. Worth a warning on a machine meant for testing.
if command -v node >/dev/null 2>&1; then
  ok "node $(node --version)"
else
  warn "Node is not installed. The dev server runs fine without it, but typecheck,"
  warn "coverage and the E2E suite degrade silently. Install Node 24 LTS: brew install node"
fi

# --- 2. Configuration -------------------------------------------------------
heading "Checking configuration"

created_env=0
for dir in "." "api" "app"; do
  example="$dir/.env.example"
  target="$dir/.env"
  if [[ -f "$example" && ! -f "$target" ]]; then
    cp "$example" "$target"
    ok "created ${target#./} from .env.example"
    created_env=1
  fi
done
[[ $created_env -eq 0 ]] && ok ".env files present"

# --- 3. Dependencies --------------------------------------------------------
# `bun install` is fast when nothing changed, but not free, and it rewrites the
# lockfile's timestamp — so the comparison is against a stamp this script owns
# rather than against node_modules' own mtime, which npm-style tools do not
# update reliably.
INSTALL_STAMP="node_modules/.q2-start-dev-installed"
needs_install=0
if [[ ! -d node_modules || ! -d app/node_modules || ! -f "$INSTALL_STAMP" ]]; then
  needs_install=1
elif [[ bun.lock -nt "$INSTALL_STAMP" ]] \
  || [[ package.json -nt "$INSTALL_STAMP" ]] \
  || [[ app/package.json -nt "$INSTALL_STAMP" ]]; then
  needs_install=1
fi

if [[ $needs_install -eq 1 ]]; then
  heading "Installing frontend dependencies"
  bun install || die "bun install failed." "Reproduce with:  bun install"
  touch "$INSTALL_STAMP"
else
  ok "frontend dependencies up to date"
fi

# Restores dotnet-ef as pinned in .config/dotnet-tools.json. Idempotent and
# quick; only `db:add-migration` actually needs it, but a missing tool manifest
# is a confusing failure much later.
if [[ -f .config/dotnet-tools.json ]]; then
  dotnet tool restore --verbosity quiet >/dev/null 2>&1 \
    || warn "dotnet tool restore failed — only \`bun run db:add-migration\` needs it."
fi

# --- 4. Database ------------------------------------------------------------
# Development keeps its file and the API applies pending migrations on startup
# (README.md section 6). Only a first run has to create the file, and doing it
# here means a database error is readable instead of interleaved with Nuxt's
# output. ManualTesting rebuilds itself on startup and needs nothing here.
if [[ $MODE == "dev" && ! -f api/.data/q2-development.db ]]; then
  heading "Creating the Development database (first run)"
  bun run db:migrate || die "Creating the database failed." \
    "Reproduce with:  bun run db:migrate"
  ok "api/.data/q2-development.db created"
fi

# --- 5. Ports ---------------------------------------------------------------
port_pid() { lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | head -n 1; }

free_port() {
  local port="$1" name="$2" pid
  pid="$(port_pid "$port")" || true
  [[ -z "$pid" ]] && return 0

  local what
  what="$(ps -p "$pid" -o comm= 2>/dev/null || echo 'unknown process')"
  warn "Port $port ($name) is already in use by PID $pid — $what"

  if [[ $FORCE_PORTS -eq 0 ]]; then
    if [[ ! -t 0 ]]; then
      die "Port $port is occupied and the script cannot ask what to do." \
          "Stop that process, or start again with --force-ports."
    fi
    local answer
    read -r -p "  Stop it? [y/N] " answer || answer=""
    [[ "$answer" =~ ^[YyJj]$ ]] || die "Port $port stays occupied — nothing was started." \
      "Stop the process yourself, or use --force-ports."
  fi

  kill "$pid" 2>/dev/null || true
  for _ in $(seq 1 10); do
    [[ -z "$(port_pid "$port")" ]] && break
    sleep 0.5
  done
  if [[ -n "$(port_pid "$port")" ]]; then
    kill -9 "$pid" 2>/dev/null || true
    sleep 1
  fi
  [[ -z "$(port_pid "$port")" ]] || die "Could not free port $port (PID $pid)."
  ok "port $port freed"
}

free_port "$API_PORT" "API"
free_port "$APP_PORT" "Nuxt"

# --- 6. Start ---------------------------------------------------------------
if [[ $OPEN_BROWSER -eq 1 ]]; then
  # Waits for Nuxt to answer, then opens it. Runs detached so it survives the
  # `exec` below; it gives up quietly after two minutes.
  (
    for _ in $(seq 1 120); do
      if curl -sfo /dev/null --max-time 2 "$APP_URL"; then
        open "$APP_URL" >/dev/null 2>&1 || true
        exit 0
      fi
      sleep 1
    done
  ) &
fi

# The one-tap fill-in on the sign-in screen only exists when both variables are
# set (README.md section 10), and `bun run dev` takes them from app/.env — so
# say which of the two situations this start is in instead of promising a
# button that may not be there. ManualTesting sets them itself.
demo_ready=0
if [[ $MODE == "manual" ]]; then
  demo_ready=1
elif [[ -f app/.env ]] \
  && grep -Eq '^[[:space:]]*(export[[:space:]]+)?NUXT_PUBLIC_DEMO_EMAIL=.+' app/.env \
  && grep -Eq '^[[:space:]]*(export[[:space:]]+)?NUXT_PUBLIC_DEMO_PASSWORD=.+' app/.env; then
  demo_ready=1
fi

heading "Starting"
info "  frontend: $APP_URL"
info "  backend:  $API_URL   (OpenAPI: $API_URL/openapi/v1.json)"
if [[ $demo_ready -eq 1 ]]; then
  info "  sign-in:  mara.k@kudos.example / kudos-demo-2026 — the screen fills it in with one tap"
else
  info "  sign-in:  mara.k@kudos.example / kudos-demo-2026 (every seeded person, same password)"
  info "            set NUXT_PUBLIC_DEMO_EMAIL and NUXT_PUBLIC_DEMO_PASSWORD in app/.env"
  info "            to get the one-tap fill-in — see app/.env.example"
fi
if [[ $MODE == "manual" ]]; then
  info "  database: api/.data/q2-manual-testing.db, deleted and re-seeded on this start"
else
  info "  database: api/.data/q2-development.db, kept as it is"
fi
info "  q2 is a phone app: look at it at 390 × 844, not in a wide window."
info ""
info "  Stop everything with Ctrl-C."

if [[ $MODE == "manual" ]]; then
  exec bun run test:manual:start
else
  exec bun run dev
fi
