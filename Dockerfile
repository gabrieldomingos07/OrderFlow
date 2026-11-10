# syntax=docker/dockerfile:1

# ---- Build -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only the files that affect it, so this layer is cached
# until a project file or a package version changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/OrderFlow.Domain/OrderFlow.Domain.csproj src/OrderFlow.Domain/
COPY src/OrderFlow.Application/OrderFlow.Application.csproj src/OrderFlow.Application/
COPY src/OrderFlow.Infrastructure/OrderFlow.Infrastructure.csproj src/OrderFlow.Infrastructure/
COPY src/OrderFlow.Api/OrderFlow.Api.csproj src/OrderFlow.Api/
RUN dotnet restore src/OrderFlow.Api/OrderFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/OrderFlow.Api/OrderFlow.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# ---- Runtime ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# The official images ship an unprivileged "app" user; never run as root.
USER $APP_UID

# Files stay owned by root (read-only for the app user), so a compromised process
# cannot rewrite its own binaries or configuration.
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Api.dll"]
