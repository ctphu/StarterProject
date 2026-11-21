#!/usr/bin/env bash
# =============================================================================
# compare-builds.sh — why we build once.
#
#   bash scripts/compare-builds.sh [build-context]
#
# Builds the SAME commit twice, from scratch (--no-cache), and compares the two
# image IDs. Same source, same Dockerfile, same machine, one minute apart.
#
# This is a REPORT, not a gate: it exits 0 whatever the comparison shows.
# Exit 2 only if a build itself fails.
#
# Expect DIFFERENT IDs. Timestamps of the files `dotnet publish` writes end up
# in the layers, and a layer's digest is the hash of its bytes. Two builds of
# one commit are two different artifacts — so the image tested in Staging and
# an image rebuilt "from the same commit" for Production are not the same thing.
# =============================================================================
set -euo pipefail

CTX="${1:-.}"
commit="$(git rev-parse --short HEAD 2>/dev/null || echo 'not a git repository')"

if ! git diff --quiet HEAD 2>/dev/null; then
    printf 'NOTE  the working tree has uncommitted changes — the comparison still holds\n'
fi

printf 'Commit %s — building twice, no cache. This takes a few minutes.\n\n' "$commit"

for n in 1 2; do
    printf '  build %s ... ' "$n"
    docker build --no-cache --quiet -t "devopslab-api:compare-$n" "$CTX" >/dev/null \
        || { printf 'FAILED\n' >&2; exit 2; }
    printf 'done\n'
done

id1="$(docker image inspect --format '{{.Id}}' devopslab-api:compare-1)"
id2="$(docker image inspect --format '{{.Id}}' devopslab-api:compare-2)"

printf '\n| Build | Image ID |\n|---|---|\n'
# The backticks are Markdown, not command substitution.
# shellcheck disable=SC2016
printf '| 1 | `%s` |\n' "$id1"
# shellcheck disable=SC2016
printf '| 2 | `%s` |\n' "$id2"

if [ "$id1" = "$id2" ]; then
    printf '\nIDENTICAL — this build is reproducible on this machine. Rare; say so in class.\n'
else
    printf '\nDIFFERENT — one commit, two artifacts.\n'
    printf 'Which layers differ:\n'
    diff <(docker image inspect --format '{{range .RootFS.Layers}}{{println .}}{{end}}' devopslab-api:compare-1) \
         <(docker image inspect --format '{{range .RootFS.Layers}}{{println .}}{{end}}' devopslab-api:compare-2) \
        | grep '^[<>]' | sed 's/^/  /' || true
fi

docker image rm devopslab-api:compare-1 devopslab-api:compare-2 >/dev/null 2>&1 || true

printf '\nBuild once. Promote the digest that was tested.\n'
