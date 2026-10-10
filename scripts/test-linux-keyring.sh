#!/usr/bin/env bash
set -euo pipefail
: "${1:?Pass the installed executable}"
: "${2:?Pass a new test directory}"
printf '%s\n' 'AstroArchive-synthetic-CI-keyring-password' | gnome-keyring-daemon --unlock --components=secrets >/dev/null
"$1" --keyring-test "$2"
