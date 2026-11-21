# Environments

One image, promoted by digest. Everything below comes from the environment,
never from the image.

| Setting | Development (Compose) | Staging (Session 9) | Production (Session 10) | Source |
|---|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Development | Staging | Production | env var |
| `ConnectionStrings__Default` | from `.env` | GitHub Environment secret `staging` | GitHub Environment secret `production` | secret |
| `ASPNETCORE_HTTP_PORTS` | 8080 | 8080 | 8080 | env var (image default) |
| Image | built locally | `ghcr.io/khinthuzaraung131/starterproject@sha256:d892b5dfe4b6910cf0640f13d7c2a8526b50171cbad62e728fdae09dc9457e30` | **the same digest** | registry |

Hits from the search and what we decided:
- `appsettings.json` — `Server=localhost,1433;...Encrypt=False`: local default with no
  password. Overridden everywhere else by `ConnectionStrings__Default`, which carries
  the real server and password from a secret.
- `launchSettings.json` — `http://localhost:5080`: used only by `dotnet run` on a
  developer machine. Not part of the image; the container listens on 8080.
- `Program.cs` — `if (!app.Environment.IsProduction())`: migrations and seed data run
  automatically in Development and Staging, never in Production. The behaviour is chosen
  by `ASPNETCORE_ENVIRONMENT`, not by anything baked into the image, so the same
  image behaves correctly in each environment.
- Remaining matches for `prod` are the word `Product` (class and table names) and the
  EF Core `ProductVersion` annotation. Not environment settings.