#!/bin/sh
set -eu

case "${PUBLIC_APP_VDIR:-}" in
    /*/) ;;
    *) echo "PUBLIC_APP_VDIR must start and end with '/': ${PUBLIC_APP_VDIR:-<empty>}" >&2; exit 1 ;;
esac

if [ "$PUBLIC_APP_VDIR" = "/" ]; then
    echo "PUBLIC_APP_VDIR must not be '/'" >&2
    exit 1
fi

target="/usr/share/nginx/html${PUBLIC_APP_VDIR}"
mkdir -p "$target"
cp -R /opt/centaur-scores/scoring/. "$target"
