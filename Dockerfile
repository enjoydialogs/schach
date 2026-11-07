# Schach web: ein Image (`schach-web`), drei Stufen (SCHACH-SPEC 13.2, nach Kronrat). Eingefroren ab Phase 0.
#
# Das SPA baut auf Node, die API veroeffentlicht auf dem .NET-SDK, die Laufzeit ist das schlanke
# aspnet-Image. Der Vite-Bau wandert VOR `dotnet publish` nach wwwroot: nur so kennt das
# Static-Web-Assets-Manifest die Dateien.
#
# Debian-Basis, NICHT Alpine: Program.cs setzt de-DE als Kultur, das braucht ICU.

# ---------------------------------------------------------------- 1. SPA
# Docker Official Image aus ECR Public: kein anonymes Docker-Hub-Pull-Limit auf CI-Runnern.
ARG NODE_REGISTRY=public.ecr.aws/docker/library
FROM ${NODE_REGISTRY}/node:22-bookworm-slim AS spa
# Der Footer-Link „Quellcode“ zeigt auf diesen Commit (SCHACH-SPEC 13.6). Leer: Link aufs Repo.
ARG COMMIT_SHA=
ENV VITE_COMMIT_SHA=${COMMIT_SHA}
WORKDIR /web
COPY src/Schach.Web/package.json src/Schach.Web/package-lock.json ./
RUN npm ci
COPY src/Schach.Web/ ./
RUN npm run build

# ---------------------------------------------------------------- 2. API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG INFORMATIONAL_VERSION=
WORKDIR /src
# Build-Dateien zuerst: global.json pinnt das SDK, Directory.Packages.props die Paketversionen (CPM),
# .editorconfig die Analyzer-Schwere (Warnungen sind Fehler).
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props .editorconfig ./
COPY src/Schach.Domain/ src/Schach.Domain/
COPY src/Schach.Application/ src/Schach.Application/
COPY src/Schach.Infrastructure/ src/Schach.Infrastructure/
COPY src/Schach.Api/ src/Schach.Api/
# prompts/ (S2c) sind Content-Items des API-Projekts. Das Verzeichnis existiert ab Phase 0
# (prompts/.gitkeep), damit diese Zeile nie an einem fehlenden Pfad scheitert.
COPY prompts/ prompts/
COPY --from=spa /web/dist/ src/Schach.Api/wwwroot/
RUN if [ -n "$INFORMATIONAL_VERSION" ]; then set -- "-p:InformationalVersion=$INFORMATIONAL_VERSION"; fi; \
    dotnet publish src/Schach.Api/Schach.Api.csproj -c Release -o /app --nologo "$@"

# ---------------------------------------------------------------- 3. Laufzeit
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Schach.Api.dll"]
