# syntax=docker/dockerfile:1
# Single-container image: supervisord runs both the API and YARP Gateway.
# Vue SPA static assets are baked into the Gateway's wwwroot at build time.

ARG APP_VERSION=0.5.0
ARG DOTNET_VERSION=10.0
ARG NODE_VERSION=22
ARG VITE_API_BASE_URL=

# ─── Stage 1: Build Vue frontend ──────────────────────────────────────────────
FROM node:${NODE_VERSION}-alpine AS frontend-build
WORKDIR /app/frontend
COPY src/frontend/package*.json ./
RUN npm ci
COPY src/frontend/ .
ARG APP_VERSION
ARG VITE_API_BASE_URL
RUN VITE_APP_VERSION=${APP_VERSION} VITE_API_BASE_URL=${VITE_API_BASE_URL} npm run build
# Output: /app/frontend/dist

# ─── Stage 2: Restore .NET dependencies ───────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS dotnet-restore
WORKDIR /src
COPY NuGet.Config ./
COPY src/Garrard.Mcp.Explorer.Core/Garrard.Mcp.Explorer.Core.csproj                           Garrard.Mcp.Explorer.Core/
COPY src/Garrard.Mcp.MessageContentProtection/Garrard.Mcp.MessageContentProtection.csproj     Garrard.Mcp.MessageContentProtection/
COPY src/Garrard.Mcp.Explorer.Infrastructure/Garrard.Mcp.Explorer.Infrastructure.csproj       Garrard.Mcp.Explorer.Infrastructure/
COPY src/Garrard.Mcp.Explorer.Api/Garrard.Mcp.Explorer.Api.csproj                             Garrard.Mcp.Explorer.Api/
COPY src/Garrard.Mcp.Explorer.Gateway/Garrard.Mcp.Explorer.Gateway.csproj                     Garrard.Mcp.Explorer.Gateway/
RUN dotnet restore Garrard.Mcp.Explorer.Api/Garrard.Mcp.Explorer.Api.csproj
RUN dotnet restore Garrard.Mcp.Explorer.Gateway/Garrard.Mcp.Explorer.Gateway.csproj

# ─── Stage 3: Build and publish API ───────────────────────────────────────────
FROM dotnet-restore AS api-build
COPY src/ ./
RUN dotnet publish Garrard.Mcp.Explorer.Api/Garrard.Mcp.Explorer.Api.csproj \
    -c Release -o /app/api /p:UseAppHost=false

# ─── Stage 4: Build and publish Gateway (with Vue assets in wwwroot) ──────────
FROM dotnet-restore AS gateway-build
COPY src/ ./
# Embed the built Vue SPA into the Gateway's wwwroot
COPY --from=frontend-build /app/frontend/dist ./Garrard.Mcp.Explorer.Gateway/wwwroot/
RUN dotnet publish Garrard.Mcp.Explorer.Gateway/Garrard.Mcp.Explorer.Gateway.csproj \
    -c Release -o /app/gateway /p:UseAppHost=false

# ─── Stage 5: Runtime image ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

ARG APP_VERSION
ENV AppMetadata__Version=${APP_VERSION}

# Install supervisor to manage both processes
RUN apt-get update && apt-get install -y --no-install-recommends supervisor \
    && rm -rf /var/lib/apt/lists/*

COPY --from=api-build     /app/api     ./api
COPY --from=gateway-build /app/gateway ./gateway
COPY docker/supervisord.conf /etc/supervisor/conf.d/mcp-explorer.conf

# Gateway is the public-facing port
EXPOSE 8080

ENTRYPOINT ["supervisord", "-n", "-c", "/etc/supervisor/conf.d/mcp-explorer.conf"]
