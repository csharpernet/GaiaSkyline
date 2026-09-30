# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage: .NET 10 SDK + Node.js 20 (Node is needed for the Tailwind build
# step, which runs automatically via the TailwindBuild MSBuild target).
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Restore first (better layer caching) then copy the rest.
COPY GaiaSkyline.sln global.json Directory.Build.props .editorconfig ./
COPY src/ ./src/
COPY tests/ ./tests/
RUN dotnet restore GaiaSkyline.sln

RUN dotnet publish src/GaiaSkyline.Web/GaiaSkyline.Web.csproj \
    -c Release -o /app/publish --no-restore

# ---------------------------------------------------------------------------
# Runtime stage: ASP.NET Core 10 runtime only (no SDK, no Node).
# The base image already runs as a non-root user and listens on 8080.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "GaiaSkyline.Web.dll"]
