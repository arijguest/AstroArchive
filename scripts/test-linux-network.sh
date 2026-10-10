#!/usr/bin/env bash
set -euo pipefail
: "${1:?Pass a new scratch directory}"
validation_root=$(realpath -m "$1")
if [[ -e "$validation_root" ]]; then echo 'Choose a new network test directory.' >&2; exit 1; fi
mkdir -p "$validation_root/ftp/storage" "$validation_root/smb"
network_python=${ASTROARCHIVE_NETWORK_PYTHON:-python3}
dotnet_command=${ASTROARCHIVE_DOTNET:-dotnet}
server_processes=()
cleanup() { if ((${#server_processes[@]})); then kill "${server_processes[@]}" 2>/dev/null || true; wait "${server_processes[@]}" 2>/dev/null || true; fi; }
trap cleanup EXIT
"$network_python" Application_Source/Remote/ftp-test-server.py "$validation_root/ftp/storage" 24221 "$validation_root/ftp.ready" > "$validation_root/ftp-server.log" 2>&1 &
server_processes+=("$!")
"$network_python" Application_Source/Remote/smb-test-server.py "$validation_root/smb" --port 24445 --ready "$validation_root/smb.ready" > "$validation_root/smb-server-console.log" 2>&1 &
server_processes+=("$!")
for attempt in $(seq 1 100); do
    if [[ -f "$validation_root/ftp.ready" && -f "$validation_root/smb.ready" ]]; then break; fi
    for pid in "${server_processes[@]}"; do kill -0 "$pid"; done
    sleep 0.1
done
test -f "$validation_root/ftp.ready"
test -f "$validation_root/smb.ready"
"$dotnet_command" run --project CrossPlatform/EngineTests -- "$validation_root/ftp" --remote-ftp 24221
"$dotnet_command" run --project CrossPlatform/EngineTests -- "$validation_root/smb" --remote-smb 24445
printf '%s\n' 'PASS: native Linux FTP and SMB list/download, sidecars, verified retry and unchanged telescope originals'
