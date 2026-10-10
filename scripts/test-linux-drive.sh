#!/usr/bin/env bash
# CI-only loopback drive: no physical device or user archive is touched.
set -euo pipefail
cd "$(dirname "$0")/.."
drive_test_root="$(mktemp -d)"
drive_test_mount="$drive_test_root/mount"
mkdir "$drive_test_mount"
drive_test_mounted=false
drive_test_backend=kernel
drive_test_device=""
mount_drive_test() {
  local drive_test_mode="$1"
  local drive_test_options="$drive_test_mode,uid=$(id -u),gid=$(id -g),umask=077"
  if [[ "$drive_test_backend" == kernel ]]; then
    if sudo mount -t exfat -o "$drive_test_options" "$drive_test_device" "$drive_test_mount"; then
      drive_test_mounted=true
      return
    fi
    # Hosted runners may omit the exFAT kernel module. FUSE mounts the same
    # genuine exFAT image; it does not emulate repository operations.
    drive_test_backend=fuse
  fi
  sudo mount.exfat-fuse -o "$drive_test_options,allow_other" "$drive_test_device" "$drive_test_mount"
  drive_test_mounted=true
}
cleanup_drive_test() {
  if [[ "$drive_test_mounted" == true ]]; then sudo umount "$drive_test_mount"; fi
  if [[ -n "$drive_test_device" ]]; then sudo losetup --detach "$drive_test_device"; fi
  rm -rf "$drive_test_root"
}
trap cleanup_drive_test EXIT
truncate -s 128M "$drive_test_root/drive.img"
mkfs.exfat "$drive_test_root/drive.img"
# FUSE's fuseblk mount requires a block device, even for an image fixture.
drive_test_device="$(sudo losetup --find --show "$drive_test_root/drive.img")"
mount_drive_test rw
echo "exFAT test mount backend: $drive_test_backend"
dotnet run --project CrossPlatform/EngineTests -- "$drive_test_mount/fixture" --interop-create
sudo umount "$drive_test_mount"
drive_test_mounted=false
mount_drive_test rw
dotnet run --no-build --project CrossPlatform/EngineTests -- "$drive_test_mount/fixture" --interop-update
dotnet run --no-build --project CrossPlatform/EngineTests -- "$drive_test_mount/fixture" --interop-verify
sudo umount "$drive_test_mount"
drive_test_mounted=false
mount_drive_test ro
if dotnet run --no-build --project CrossPlatform/EngineTests -- "$drive_test_mount/fixture" --interop-verify > "$drive_test_root/read-only.log" 2>&1; then
  echo 'FAIL: read-only drive was opened for writing' >&2
  exit 1
fi
cat "$drive_test_root/read-only.log"
if ! python3 - "$drive_test_root/read-only.log" <<'PY'
import pathlib, sys
sys.exit(0 if 'Read-only file system' in pathlib.Path(sys.argv[1]).read_text() else 1)
PY
then
  echo 'FAIL: read-only probe failed for an unexpected reason' >&2
  exit 1
fi
echo 'PASS: exFAT import/export, unmount/remount, index freshness and read-only mount rejection'
