#!/bin/zsh
set -e
cd "${0:a:h}"
exec ../../scripts/build-app.sh "$PWD" "SkadooshConverter"
