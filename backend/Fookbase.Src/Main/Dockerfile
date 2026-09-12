FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/Directory.Build.props backend/Directory.Build.props
COPY backend/Fookbase.Src/Main/Fookbase.Api.csproj backend/Fookbase.Src/Main/
RUN dotnet restore backend/Fookbase.Src/Main/Fookbase.Api.csproj

COPY backend/Fookbase.Src/Main/ backend/Fookbase.Src/Main/
RUN dotnet publish backend/Fookbase.Src/Main/Fookbase.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5000 \
    DOTNET_RUNNING_IN_CONTAINER=true

RUN apt-get update \
    && apt-get install --no-install-recommends -y ffmpeg \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 fookbase \
    && useradd --uid 10001 --gid fookbase --create-home --shell /usr/sbin/nologin fookbase \
    && mkdir -p /var/fookbase/data-protection-keys \
    && chown -R fookbase:fookbase /var/fookbase

COPY --from=build --chown=fookbase:fookbase /app/publish/ ./

USER fookbase
EXPOSE 5000

ENTRYPOINT ["dotnet", "Fookbase.Api.dll"]
