# syntax=docker/dockerfile:1
# Fugue API image plus an EF Core migrations bundle (target "migrate"). Build context: repo root.
# Build the web console from web/Dockerfile. Targets: "api" (runtime host), "migrate" (one-shot bundle).

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
# Restore layer: only the files that decide package resolution, so source edits do not bust it.
COPY global.json .editorconfig Directory.Build.props Directory.Packages.props Fugue.sln ./
COPY .config/dotnet-tools.json .config/
COPY src/Fugue.Domain/Fugue.Domain.csproj src/Fugue.Domain/
COPY src/Fugue.Application/Fugue.Application.csproj src/Fugue.Application/
COPY src/Fugue.Infrastructure/Fugue.Infrastructure.csproj src/Fugue.Infrastructure/
COPY src/Fugue.Api/Fugue.Api.csproj src/Fugue.Api/
RUN dotnet restore src/Fugue.Api/Fugue.Api.csproj && dotnet tool restore
COPY src/ src/

FROM restore AS publish
RUN dotnet publish src/Fugue.Api/Fugue.Api.csproj -c Release --no-restore -o /out/api /p:UseAppHost=false

FROM restore AS bundle
ARG TARGETARCH
RUN case "$TARGETARCH" in arm64) rid=linux-arm64 ;; amd64) rid=linux-x64 ;; *) echo "unsupported arch $TARGETARCH" >&2; exit 1 ;; esac \
 && dotnet ef migrations bundle --project src/Fugue.Infrastructure \
      --configuration Release --self-contained -r "$rid" -o /out/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS migrate
WORKDIR /app
COPY --from=bundle /out/efbundle ./efbundle
USER $APP_UID
# Connection string is passed by compose (--connection); the bundle exits 0 when the schema is current.
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
COPY --from=publish /out/api .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fugue.Api.dll"]
