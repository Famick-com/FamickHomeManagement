# Famick Home Management - Self-Hosted Docker Image (Production)
# Build context: repository root (publish-dockerhub.sh sets this up).
# To build manually: docker build -f self-hosted/docker-compose/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first (for layer caching)
COPY src/Famick.HomeManagement.Shared/Famick.HomeManagement.Shared.csproj src/Famick.HomeManagement.Shared/
COPY src/Famick.HomeManagement.Domain/Famick.HomeManagement.Domain.csproj src/Famick.HomeManagement.Domain/
COPY src/Famick.HomeManagement.Core/Famick.HomeManagement.Core.csproj src/Famick.HomeManagement.Core/
COPY src/Famick.HomeManagement.Infrastructure/Famick.HomeManagement.Infrastructure.csproj src/Famick.HomeManagement.Infrastructure/
COPY src/Famick.HomeManagement.Jobs/Famick.HomeManagement.Jobs.csproj src/Famick.HomeManagement.Jobs/
COPY src/Famick.HomeManagement.UI/Famick.HomeManagement.UI.csproj src/Famick.HomeManagement.UI/
COPY src/Famick.HomeManagement.Web.Shared/Famick.HomeManagement.Web.Shared.csproj src/Famick.HomeManagement.Web.Shared/
COPY src/Famick.HomeManagement.Web/Famick.HomeManagement.Web.csproj src/Famick.HomeManagement.Web/
COPY src/Famick.HomeManagement.Web.Client/Famick.HomeManagement.Web.Client.csproj src/Famick.HomeManagement.Web.Client/

# Restore dependencies
RUN dotnet restore src/Famick.HomeManagement.Web/Famick.HomeManagement.Web.csproj

# Copy all source files
COPY src/ src/

# Build the application
WORKDIR /src/src/Famick.HomeManagement.Web
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Install Kerberos libraries for Npgsql (eliminates warning) and supercronic for the
# scheduler sidecar. Same image runs both modes: ENTRYPOINT defaults to the web app;
# docker-compose's scheduler service overrides the command to run supercronic instead.
#
# arm64 is required for Raspberry Pi 4/5 — the dominant Home Assistant OS
# hardware — and for ARM Macs running docker-compose locally.
#
# Do NOT give TARGETARCH a default value. buildx sets it automatically per
# --platform, but a default (e.g. =amd64) SHADOWS that per-platform value, so
# the arm64 build would still see amd64 and download supercronic-linux-amd64
# into the arm64 image (=> "cannot execute binary file: Exec format error" on
# the Pi). For a plain `docker build` without buildx, TARGETARCH is empty; we
# fall back to the build host's arch via `dpkg --print-architecture`.
ARG SUPERCRONIC_VERSION=0.2.29
ARG SUPERCRONIC_SHA1_AMD64=cd48d45c4b10f3f0bfdd3a57d054cd05ac96812b
ARG SUPERCRONIC_SHA1_ARM64=512f6736450c56555e01b363144c3c9d23abed4c
ARG TARGETARCH
RUN apt-get update && apt-get install -y --no-install-recommends \
    libkrb5-3 \
    curl \
    && rm -rf /var/lib/apt/lists/* \
    && SC_ARCH="${TARGETARCH:-$(dpkg --print-architecture)}" \
    && case "${SC_ARCH}" in \
        amd64) SUPERCRONIC_SHA1="${SUPERCRONIC_SHA1_AMD64}" ;; \
        arm64) SUPERCRONIC_SHA1="${SUPERCRONIC_SHA1_ARM64}" ;; \
        *) echo "Unsupported arch '${SC_ARCH}' — add a supercronic SHA1 above"; exit 1 ;; \
    esac \
    && curl -fsSLO "https://github.com/aptible/supercronic/releases/download/v${SUPERCRONIC_VERSION}/supercronic-linux-${SC_ARCH}" \
    && echo "${SUPERCRONIC_SHA1}  supercronic-linux-${SC_ARCH}" | sha1sum -c - \
    && chmod +x "supercronic-linux-${SC_ARCH}" \
    && mv "supercronic-linux-${SC_ARCH}" /usr/local/bin/supercronic

COPY --from=publish /app/publish .
COPY self-hosted/docker-compose/scheduler-crontab /app/scheduler-crontab

# Ship the HA-add-on bootstrap script alongside the app so the wrapper repo's
# cont-init layer doesn't need its own copy. Safe for non-add-on deployments
# (just an unused 2 KB shell script on /app/scripts/).
COPY --chmod=755 self-hosted/home-assistant-plugin/bootstrap.sh /app/scripts/bootstrap.sh

# Health check (web mode only — scheduler service overrides command and disables this)
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -fsS http://localhost:80/health || exit 1

# CMD (not ENTRYPOINT) so compose's `command:` can override cleanly for the
# scheduler sidecar (`command: ["supercronic", "/app/scheduler-crontab"]`).
# ENTRYPOINT here would prepend `dotnet Famick.HomeManagement.Web.dll` to
# whatever command is set, silently running web mode with the override as
# ignored CLI args.
CMD ["dotnet", "Famick.HomeManagement.Web.dll"]
