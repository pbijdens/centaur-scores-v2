FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY centaur-scores-api-v2/CentaurScores.Api/CentaurScores.Api.csproj CentaurScores.Api/
RUN dotnet restore CentaurScores.Api/CentaurScores.Api.csproj

COPY centaur-scores-api-v2/CentaurScores.Api/ CentaurScores.Api/
RUN dotnet publish CentaurScores.Api/CentaurScores.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl logrotate \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
COPY centaur-scores-docker-v2/images/api/logrotate.conf /etc/logrotate.d/centaur-scores-api
COPY centaur-scores-docker-v2/images/api/entrypoint.sh /usr/local/bin/centaur-scores-entrypoint
RUN chmod +x /usr/local/bin/centaur-scores-entrypoint \
    && chmod 0644 /etc/logrotate.d/centaur-scores-api \
    && mkdir -p /var/log/centaur-scores

EXPOSE 8080
ENTRYPOINT ["centaur-scores-entrypoint"]
