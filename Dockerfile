# syntax=docker/dockerfile:1
# Meridian: one image serving the Angular SPA and the ASP.NET Core API on a single origin (ADR-0004).
#
#   docker build --build-arg SOURCE_REVISION=$(git rev-parse HEAD) -t meridian .
#
# Stages: web (Node builds the SPA) → api (SDK publishes the API) → runtime (chiseled, non-root, no shell/SDK).

# ---- 1. Build the Angular SPA ------------------------------------------------
FROM --platform=$BUILDPLATFORM node:24-alpine AS web
WORKDIR /src/meridian-web
# Dependencies first so this layer is cached until package*.json changes.
COPY src/meridian-web/package.json src/meridian-web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/meridian-web/ ./
RUN npm run build

# ---- 2. Publish the API --------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS api
ARG TARGETARCH
# Git commit stamped into AssemblyInformationalVersion (served by GET /api/v1/version).
ARG SOURCE_REVISION=unknown
WORKDIR /src
COPY .editorconfig global.json Directory.Build.props Directory.Packages.props ./
# Project files only, so `dotnet restore` is cached until dependencies change.
COPY src/Meridian.Domain/Meridian.Domain.csproj src/Meridian.Domain/
COPY src/Meridian.Application/Meridian.Application.csproj src/Meridian.Application/
COPY src/Meridian.Infrastructure/Meridian.Infrastructure.csproj src/Meridian.Infrastructure/
COPY src/Meridian.Api/Meridian.Api.csproj src/Meridian.Api/
RUN dotnet restore src/Meridian.Api/Meridian.Api.csproj -a $TARGETARCH
COPY src/Meridian.Domain/ src/Meridian.Domain/
COPY src/Meridian.Application/ src/Meridian.Application/
COPY src/Meridian.Infrastructure/ src/Meridian.Infrastructure/
COPY src/Meridian.Api/ src/Meridian.Api/
COPY content/ content/
# No .git in the build context, so Source Link can't read the commit; pass it explicitly instead.
RUN dotnet publish src/Meridian.Api/Meridian.Api.csproj -c Release -a $TARGETARCH --no-restore -o /app \
      -p:SourceRevisionId=$SOURCE_REVISION -p:EnableSourceControlManagerQueries=false -p:UseAppHost=false
COPY --from=web /src/meridian-web/dist/meridian-web/browser /app/wwwroot

# ---- Migrations (separate target: `docker compose` runs it before the app) -----------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS migrations-build
ARG TARGETARCH
WORKDIR /src
COPY .editorconfig dotnet-tools.json global.json Directory.Build.props Directory.Packages.props ./
COPY src/Meridian.Domain/ src/Meridian.Domain/
COPY src/Meridian.Application/ src/Meridian.Application/
COPY src/Meridian.Infrastructure/ src/Meridian.Infrastructure/
COPY content/ content/
RUN RID="linux-$([ "$TARGETARCH" = "amd64" ] && echo x64 || echo "$TARGETARCH")" \
 && dotnet tool restore \
 && dotnet restore src/Meridian.Infrastructure/Meridian.Infrastructure.csproj -r "$RID" \
 && dotnet ef migrations bundle --project src/Meridian.Infrastructure --startup-project src/Meridian.Infrastructure \
      --configuration Release --self-contained --target-runtime "$RID" --output /out/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS migrations
COPY --from=migrations-build /out/efbundle /efbundle
ENTRYPOINT ["/efbundle"]

# ---- 3. Runtime (default target) ---------------------------------------------
# Chiseled: no shell, no package manager, runs as the non-root "app" user, listens on 8080.
# Globalization is invariant (no ICU); currency/date formatting happens in the SPA.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=api /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "Meridian.Api.dll"]
