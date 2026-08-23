#!/bin/sh
set -eu

log_file=/var/log/centaur-scores/api.log
mkdir -p "$(dirname "$log_file")"
touch "$log_file"

(
    while true; do
        logrotate --state /tmp/logrotate.status /etc/logrotate.d/centaur-scores-api
        sleep 3600
    done
) &

exec dotnet CentaurScores.Api.dll >>"$log_file" 2>&1
