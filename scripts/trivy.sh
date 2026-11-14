#!/usr/bin/env bash
# =============================================================================
# trivy.sh — run ONE pinned version of Trivy, the same way on a laptop and in CI.
#
#   bash scripts/trivy.sh <trivy arguments...>
#
#   bash scripts/trivy.sh fs --scanners secret --secret-config trivy-secret.yaml .
#   bash scripts/trivy.sh config --severity HIGH,CRITICAL --exit-code 1 .
#   bash scripts/trivy.sh image --severity HIGH,CRITICAL devopslab-api:local
#
# Why a container instead of installing Trivy:
#   - nothing to install on 30 classroom machines, only Docker (Session 6);
#   - the version is pinned here, once, so the laptop and the runner cannot
#     disagree about what "no Critical vulnerabilities" means (Session 4's rule:
#     same tool, same path, everywhere).
#
# Trivy's exit code is passed through unchanged. If Docker cannot even start
# the scanner, docker exits 125 — non-zero, so the step fails closed.
#
# Environment:
#   TRIVY_VERSION    image tag to use (default below)
#   TRIVY_CACHE_DIR  host folder for the vulnerability database (CI sets it);
#                    if unset, a named Docker volume is used instead.
# =============================================================================
set -euo pipefail

TRIVY_VERSION="${TRIVY_VERSION:-0.58.1}"

[ "$#" -ge 1 ] || { printf 'usage: trivy.sh <trivy arguments...>\n' >&2; exit 2; }

# Git Bash on Windows rewrites arguments that look like paths (/src, /root/...)
# before Docker sees them. Turn that off, and give Docker a Windows-style path
# for the repository (pwd -W exists only in Git Bash; elsewhere fall back).
export MSYS_NO_PATHCONV=1
host_pwd="$(pwd -W 2>/dev/null || pwd)"

if [ -n "${TRIVY_CACHE_DIR:-}" ]; then
    mkdir -p "$TRIVY_CACHE_DIR"
    cache_mount="$TRIVY_CACHE_DIR:/root/.cache/trivy"
else
    cache_mount="devopslab-trivy-cache:/root/.cache/trivy"
fi

exec docker run --rm \
    -v /var/run/docker.sock:/var/run/docker.sock \
    -v "$host_pwd":/src \
    -w /src \
    -v "$cache_mount" \
    -e TRIVY_NO_PROGRESS=true \
    "aquasec/trivy:${TRIVY_VERSION}" "$@"
