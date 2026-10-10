#!/usr/bin/env bash
# CI-only loopback drive: no physical device or user archive is touched.
set -euo pipefail
cd "$(dirname "$0")/.."
drive_test_root="$(mktemp -d)"
drive_test_mount="$drive_test_root/mount"
mkdir "$drive_test_mount"
drive_test_mounted=false
cleanup_drive_test() {
  if [[ "$drive_test_mounted" == true ]]; then sudo umount "$drive_test_mount"; fi
  rm -rf "$drive_test_root"
}
trap cleanup_drive_test EXIT
truncate -s 128M "$drive_test_root/drive.img"
mkfs.exfat "$drive_test_root/drive.img"
sudo mount -t exfat -o "loop,uid=$(id -u),gid=$(id -g),umask=077" "$drive_test_root/drive.img" "$drive_test_mount"
drive_test_mounted=true
dotnet run --project CrossPlatform/EngineTests -- "$drive_test_mount/fixture" --interop-create
sudo umount "$drive_test_mount"
drive_test_mounted=false
sudo mount -t exfat -o "loop,uid=$(id -u),gid=$(id -g),umask=077" "$drive_test_root/drive.img" "$drive_test_mount"
drive_test_mounted=true
dotnet run --no-build --project CrossPlatform/EngineTests -- "$drive_test_mount/fixture" --interop-update
dotnet run --no-build --project CrossPlatform/EngineTests -- "$drive_test_mount/fixture" --interop-verify
sudo umount "$drive_test_mount"
drive_test_mounted=false
sudo mount -t exfat -o "loop,ro,uid=$(id -u),gid=$(id -g)" "$drive_test_root/drive.img" "$drive_test_mount"
drive_test_mounted=true
if dotnet run --no-build --project CrossPlatform/EngineTests -- "$drive_test_mount/fixture" --interop-update; then
  echo 'FAIL: read-only drive was opened for writing' >&2
  exit 1
fi
echo 'PASS: exFAT import/export, unmount/remount, index freshness and read-only mount rejection'
