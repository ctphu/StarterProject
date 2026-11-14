#!/usr/bin/env bash
# =============================================================================
# dependency-gate.sh — Software Composition Analysis that really fails.
#
#   bash scripts/dependency-gate.sh [DevOpsLab.sln | path/to/project.csproj]
#
# `dotnet list package --vulnerable` has two traps, and this script exists
# because of both:
#
#   1. It exits 0 even when it finds a vulnerable package. In a pipeline that
#      is a false gate: red text in the log, green tick on the pull request.
#   2. Without --include-transitive it looks only at the packages you named.
#      Most real vulnerabilities live in the packages THEY pulled in.
#
# This script always includes transitive packages, reads the report, and sets
# the exit code itself. It also fails CLOSED: if it cannot confirm that every
# project was checked (no restore, no network, an unexpected message), it
# fails rather than reporting a green it did not earn.
#
# Exit 0  every project reports "no vulnerable packages"
# Exit 1  at least one vulnerable package, or the check could not be completed
# Needs   `dotnet restore` to have run first (CI does it in the step before).
# =============================================================================
set -euo pipefail

TARGET="${1:-DevOpsLab.sln}"

# The CLI is not localised into every language, but force English so the
# phrases matched below are the ones the SDK actually prints.
export DOTNET_CLI_UI_LANGUAGE=en

fail() { printf '\nFAIL  %s\n' "$1" >&2; exit 1; }

[ -e "$TARGET" ] || fail "not found: $TARGET (run from the repository root)"

# --- How many projects must report? ------------------------------------------
case "$TARGET" in
    *.sln)
        expected="$(dotnet sln "$TARGET" list | grep -c '\.csproj$' || true)"
        ;;
    *.csproj)
        expected=1
        ;;
    *)
        fail "expected a .sln or .csproj, got: $TARGET"
        ;;
esac
[ "$expected" -gt 0 ] || fail "no projects found in $TARGET"

# --- Run the scan. Capture the report even if the command itself fails. -----
status=0
report="$(dotnet list "$TARGET" package --vulnerable --include-transitive 2>&1)" || status=$?
printf '%s\n' "$report"

if [ "$status" -ne 0 ]; then
    fail "dotnet list package exited with $status. Did 'dotnet restore' run first?"
fi

clean="$(printf '%s\n' "$report" | grep -c 'has no vulnerable packages' || true)"
vulnerable="$(printf '%s\n' "$report" | grep -c 'has the following vulnerable packages' || true)"

printf '\n== Dependency gate ==\n'
printf '  projects expected : %s\n' "$expected"
printf '  clean             : %s\n' "$clean"
printf '  vulnerable        : %s\n' "$vulnerable"

if [ "$vulnerable" -gt 0 ]; then
    fail "$vulnerable project(s) depend on a package with a known vulnerability.
      Read the table above: the 'Transitive Package' rows are the ones you did
      not choose. Fix by upgrading the package that pulls it in, or by pinning
      a patched version directly in the project that needs it."
fi

if [ "$clean" -ne "$expected" ]; then
    fail "only $clean of $expected project(s) confirmed clean. The vulnerability
      data may not have been reachable, or restore did not run. A check that
      could not be completed is not a pass."
fi

printf '\nPASS  no known-vulnerable packages, direct or transitive.\n'
