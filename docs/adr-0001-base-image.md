# ADR-0001 — Runtime base image

## Status
Accepted — Session 6

## Context
- image-report.sh output:

| Image | Size | Layers | USER | Exposed |
|---|---:|---:|---|---|
| `devopslab-api:local` (chiseled-extra) | 234.5 MB | 7 | app | 8080/tcp |
| `devopslab-api:alpine` | 195.7 MB | 9 | app | 8080/tcp |
| `mcr.microsoft.com/dotnet/sdk:8.0` | 1185.1 MB | 11 | root | - |

- Build context size (Step 4.2): 58.42 kB
- Rebuild after a one-line source change (Step 4.4):
  correct layer order 18.2 s · wrong layer order 39.4 s

## Decision
We use mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-extra as the runtime
image. Plain noble-chiseled was tried first, but it runs in globalization-invariant
mode and the API crashed at startup with CultureNotFoundException ("en-us is an
invalid culture identifier") when SqlClient opened its first connection. The -extra
variant ships ICU, and we set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false. It keeps
the properties we wanted: no shell, no package manager, non-root user `app`.
Alpine measured smaller (195.7 MB vs 234.5 MB), but needs icu-libs managed by us,
uses musl instead of glibc, and ships a shell and curl, which enlarge the attack surface.

## Consequences
- What we gain: non-root by default, no shell or package manager to exploit, ICU included.
- What we give up: `docker exec` (no shell), `curl`, `id`, and a container-level
  HEALTHCHECK, since all need a shell or extra binaries. The image is also larger than
  plain chiseled and larger than Alpine.
- How we diagnose a running container without a shell: `docker logs`, `docker inspect`,
  `docker image history`, `docker cp`, `docker top`.
- How we check health: readiness is watched from outside the container by calling
  /healthz/ready (Compose has no healthcheck on the api service by design).
- appsettings.json is baked into the image and contains a connection string with a
  password (Server=localhost). Real credentials come from environment variables in
  Compose, and Session 7's scanner will flag the baked-in value.
- Readiness took 14.8 s to answer 503 with the database down (default SQL connection
  timeout 15 s), longer than the experiment script's timeout, so we set `Connect Timeout=3`.
- Readiness recovery time after a database restart (Step 8b): 20 s