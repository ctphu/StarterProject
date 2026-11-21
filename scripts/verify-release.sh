#!/usr/bin/env bash
# =============================================================================
# verify-release.sh — prove where a published image came from.
#
#   bash scripts/verify-release.sh <image>:<tag> <owner>/<repo>
#   bash scripts/verify-release.sh ghcr.io/team-3/devopslab:0.2.0 team-3/devopslab
#
# Three questions, each answered by a check that can fail:
#   1. Which exact image is this tag pointing at right now?   -> the digest
#   2. Was that digest built by OUR workflow, from OUR repo?  -> SLSA provenance
#   3. Is there a signed SBOM for that same digest?           -> SBOM attestation
#
# A tag is a label anyone with push rights can move. The digest is the content.
# The attestation is a signed statement, made by GitHub Actions at build time,
# binding that content to a repository, a commit and a workflow.
#
# Exit 0 only when all three hold. Exit 1 otherwise. Needs: docker, gh >= 2.49
# (logged in: gh auth login).
# =============================================================================
set -euo pipefail

REF="${1:?usage: verify-release.sh <image>:<tag> <owner>/<repo>}"
REPO="${2:?usage: verify-release.sh <image>:<tag> <owner>/<repo>}"

fail() { printf '\nFAIL  %s\n' "$1" >&2; exit 1; }
ok()   { printf 'OK    %s\n' "$1"; }

command -v gh >/dev/null 2>&1 || fail "gh (GitHub CLI) is not installed — use the Attestations page in the browser instead"

case "$REF" in
    *@sha256:*) fail "pass a TAG, not a digest — the point is to see which digest the tag resolves to" ;;
    *:*)        ;;
    *)          fail "no tag in '$REF' (expected <image>:<tag>)" ;;
esac
IMAGE="${REF%:*}"

# --- 1. Resolve the tag to a digest -------------------------------------------
printf '== 1. Which image does %s point at?\n' "$REF"
docker pull --quiet "$REF" >/dev/null || fail "cannot pull $REF (private package? wrong name? not published yet?)"
repo_digest="$(docker image inspect --format '{{range .RepoDigests}}{{println .}}{{end}}' "$REF" \
               | grep -F "$IMAGE@" | head -n 1 || true)"
[ -n "$repo_digest" ] || fail "no registry digest recorded for $REF"
DIGEST="${repo_digest#*@}"
ok "digest $DIGEST"

# --- 2. Build provenance ------------------------------------------------------
printf '\n== 2. Was it built by %s?\n' "$REPO"
gh attestation verify "oci://$IMAGE@$DIGEST" --repo "$REPO" >/dev/null \
    || fail "no valid SLSA provenance from $REPO for $DIGEST — do NOT deploy this image"
ok "SLSA build provenance verified (signed by a GitHub Actions workflow in $REPO)"

# --- 3. SBOM attestation ------------------------------------------------------
printf '\n== 3. Is there a signed SBOM for this exact image?\n'
gh attestation verify "oci://$IMAGE@$DIGEST" --repo "$REPO" \
    --predicate-type https://cyclonedx.org/bom >/dev/null \
    || fail "no CycloneDX SBOM attestation for $DIGEST"
ok "CycloneDX SBOM attestation verified"

printf '\nPASS  %s\n' "$REF"
printf '      Promote by digest, never by tag:\n'
printf '      %s@%s\n' "$IMAGE" "$DIGEST"
