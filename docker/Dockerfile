# Famick Home Management - Self-Hosted Docker Image (Development)
# Build context should be the repository root
# Usage: docker build -f docker/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

ARG BUILD_VERSION=0.0.0

# Copy project files first (for layer caching)
COPY src/Famick.HomeManagement.Shared/Famick.HomeManagement.Shared.csproj src/Famick.HomeManagement.Shared/
COPY src/Famick.HomeManagement.Domain/Famick.HomeManagement.Domain.csproj src/Famick.HomeManagement.Domain/
COPY src/Famick.HomeManagement.Core/Famick.HomeManagement.Core.csproj src/Famick.HomeManagement.Core/
COPY src/Famick.HomeManagement.Infrastructure/Famick.HomeManagement.Infrastructure.csproj src/Famick.HomeManagement.Infrastructure/
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
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build \
    /p:Version=$BUILD_VERSION /p:InformationalVersion=$BUILD_VERSION

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish \
    /p:UseAppHost=false /p:Version=$BUILD_VERSION /p:InformationalVersion=$BUILD_VERSION

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy admin-cli script for easy CLI access
COPY docker/admin-cli /usr/local/bin/admin-cli
RUN chmod +x /usr/local/bin/admin-cli

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "Famick.HomeManagement.Web.dll"]
