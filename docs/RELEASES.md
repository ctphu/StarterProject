# Releases

Every release is an image in GHCR, identified by its **digest**. Environments are
promoted by digest, never by tag.

| Version | Digest | Commit | Date | Run | Released by |
|---|---|---|---|---|---|
| 0.2.0 | `sha256:d892b5dfe4b6910cf0640f13d7c2a8526b50171cbad62e728fdae09dc9457e30` | `810559c` | 2026-11-21 | https://github.com/khinthuzaraung131/StarterProject/actions/runs/44866421731 | khinthuzaraung131 |

## Retention policy

- Release images (`0.x.y`) are kept indefinitely: a released digest must stay
  pullable so it can be rolled back to or audited.
- `sha-<commit>` images that never became a release are kept until the team deletes
  them by hand. There is no automatic cleanup job: a job that deletes images could
  delete the digest an environment is running.
- Policy owner: khinthuzaraung131. Last reviewed: 2026-11-21.