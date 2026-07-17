#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_ROOT="$(mktemp -d /tmp/mini-admin-installer-test.XXXXXX)"
FIXTURE_REPOSITORY="${TEST_ROOT}/repository"
INSTALL_DIR="${TEST_ROOT}/install"
FAKE_BIN="${TEST_ROOT}/fake-bin"

cleanup() {
  if [[ "$TEST_ROOT" == /tmp/mini-admin-installer-test.* && -d "$TEST_ROOT" ]]; then
    rm -rf -- "$TEST_ROOT"
  fi
}

trap cleanup EXIT

mkdir -p \
  "$FIXTURE_REPOSITORY/scripts" \
  "$FIXTURE_REPOSITORY/src/MiniAdmin.Api" \
  "$INSTALL_DIR/src/MiniAdmin.Api/Generated" \
  "$FAKE_BIN"

cp "$ROOT_DIR/deploy.sh" "$FIXTURE_REPOSITORY/deploy.sh"
cp "$ROOT_DIR/docker-compose.yml" "$FIXTURE_REPOSITORY/docker-compose.yml"
cp "$ROOT_DIR/Dockerfile.api" "$FIXTURE_REPOSITORY/Dockerfile.api"
cp "$ROOT_DIR/scripts/deploy-mini-admin.sh" "$FIXTURE_REPOSITORY/scripts/deploy-mini-admin.sh"
cp "$ROOT_DIR/scripts/acceptance-mini-admin.sh" "$FIXTURE_REPOSITORY/scripts/acceptance-mini-admin.sh"

git -C "$FIXTURE_REPOSITORY" init --initial-branch=test-main --quiet
git -C "$FIXTURE_REPOSITORY" config user.name "MiniAdmin CI"
git -C "$FIXTURE_REPOSITORY" config user.email "ci@miniadmin.invalid"
git -C "$FIXTURE_REPOSITORY" add .
git -C "$FIXTURE_REPOSITORY" commit --quiet -m "fixture"

printf 'PRESERVED_CONFIG=yes\n' > "$INSTALL_DIR/.env"
printf 'obsolete source\n' > "$INSTALL_DIR/src/MiniAdmin.Api/Generated/SampleOrderEndpoints.cs"
git -C "$INSTALL_DIR" init --quiet

cat > "$FAKE_BIN/docker" <<'EOF'
#!/bin/sh
exit 0
EOF

cat > "$FAKE_BIN/bash" <<'EOF'
#!/bin/sh
case "$1" in
  deploy.sh)
    test -f "$PWD/.env"
    exit
    ;;
  scripts/acceptance-mini-admin.sh)
    exit 0
    ;;
  *)
    exec /usr/bin/bash "$@"
    ;;
esac
EOF
chmod +x "$FAKE_BIN/docker" "$FAKE_BIN/bash"

PATH="$FAKE_BIN:$PATH" /usr/bin/bash "$ROOT_DIR/mini-admin-server-install.sh" \
  --repair \
  --repo "$FIXTURE_REPOSITORY" \
  --branch test-main \
  --dir "$INSTALL_DIR"

grep -qx 'PRESERVED_CONFIG=yes' "$INSTALL_DIR/.env"
test ! -e "$INSTALL_DIR/src/MiniAdmin.Api/Generated/SampleOrderEndpoints.cs"
test -f "$INSTALL_DIR/scripts/acceptance-mini-admin.sh"

SOURCE_BACKUP_DIR="$(find "$TEST_ROOT" \
  -maxdepth 1 \
  -type d \
  -name 'install.source-backup-*' \
  -print -quit)"
test -n "$SOURCE_BACKUP_DIR"
test -f "$SOURCE_BACKUP_DIR/src/MiniAdmin.Api/Generated/SampleOrderEndpoints.cs"

printf 'MiniAdmin installer repair smoke test passed.\n'
