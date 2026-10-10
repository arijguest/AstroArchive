#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
release_version=3.1.4.1-preview.1
# Debian's tilde makes preview packages sort below the matching stable version.
release_package_version=3.1.4.1~preview.1
release_output="${1:-$PWD/linux-artifacts}"
mkdir -p "$release_output"
release_output="$(realpath "$release_output")"
release_stage="$(mktemp -d)"
trap 'rm -rf "$release_stage"' EXIT
dotnet publish CrossPlatform/Desktop/AstroArchive.Desktop.csproj -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=false -p:PublishTrimmed=false -p:RestoreLockedMode=true -o "$release_stage/app"
cp LICENSE "$release_stage/app/LICENSE" 2>/dev/null || cp LICENSE.md "$release_stage/app/LICENSE"
cp CrossPlatform/README.md "$release_stage/app/README.md"
cp CrossPlatform/FEATURE-PARITY.md "$release_stage/app/FEATURE-PARITY.md"
python3 scripts/linux-notices.py "$release_stage/app"
"$release_stage/app/AstroArchive" --version
"$release_stage/app/AstroArchive" --self-test "$release_stage/package-test" > "$release_output/package-test.log"
release_name="AstroArchive-${release_version}-linux-x64"
mkdir -p "$release_stage/portable/$release_name"
cp -a "$release_stage/app/." "$release_stage/portable/$release_name/"
chmod -R a+rX "$release_stage/portable"
tar -C "$release_stage/portable" -czf "$release_output/$release_name.tar.gz" "$release_name"
mkdir -p "$release_stage/deb/DEBIAN" "$release_stage/deb/usr/lib/astroarchive" \
  "$release_stage/deb/usr/bin" "$release_stage/deb/usr/share/applications" "$release_stage/deb/usr/share/icons/hicolor/256x256/apps"
cp -a "$release_stage/app/." "$release_stage/deb/usr/lib/astroarchive/"
ln -s ../lib/astroarchive/AstroArchive "$release_stage/deb/usr/bin/astroarchive"
cp Application_Source/Assets/AstroArchive_Logo.png "$release_stage/deb/usr/share/icons/hicolor/256x256/apps/astroarchive.png"
cat > "$release_stage/deb/usr/share/applications/astroarchive.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=AstroArchive
Comment=Preserve and organize astrophotography captures
Exec=astroarchive
Icon=astroarchive
Terminal=false
Categories=Education;Science;Astronomy;
StartupWMClass=AstroArchive
EOF
cat > "$release_stage/deb/DEBIAN/control" <<EOF
Package: astroarchive
Version: $release_package_version
Section: science
Priority: optional
Architecture: amd64
Maintainer: AstroArchive maintainers
Homepage: https://github.com/arijguest/AstroArchive
Depends: libc6 (>= 2.35), libgcc-s1, libstdc++6, libicu70 | libicu72 | libicu74 | libicu76 | libicu78, libssl3 | libssl3t64, libx11-6, libice6, libsm6, libfontconfig1
Recommends: libzstd1, libcfitsio10 | libcfitsio9, libsecret-tools, xdg-utils, ffmpeg
Description: Astrophotography archive manager for Linux (preview)
 Verified capture imports, exports, Edited working copies and archive analytics.
 Includes its .NET runtime. Windows and Linux share the existing archive format.
EOF
chmod -R a+rX "$release_stage/deb"
dpkg-deb --root-owner-group --build "$release_stage/deb" "$release_output/astroarchive_${release_version}_amd64.deb"
test "$(dpkg-deb -f "$release_output/astroarchive_${release_version}_amd64.deb" Version)" = "$release_package_version"
dpkg --compare-versions "$release_package_version" gt 3.1.4-preview.2
dpkg --compare-versions "$release_package_version" lt 3.1.4.1
python3 - "$release_output" <<'PY'
import hashlib, pathlib, sys
folder = pathlib.Path(sys.argv[1])
files = sorted([*folder.glob('*.deb'), *folder.glob('*.tar.gz')])
def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b''):
            value.update(chunk)
    return value.hexdigest()
(folder/'SHA256SUMS').write_text(''.join(digest(p)+'  '+p.name+'\n' for p in files))
PY
printf 'Linux packages written to %s\n' "$release_output"
