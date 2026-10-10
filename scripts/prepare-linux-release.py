#!/usr/bin/env python3
"""Assemble release assets only from the successful Ubuntu 22.04 CI artifact."""
import hashlib
import os
import pathlib
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile

source = pathlib.Path('linux-artifacts')
output = pathlib.Path('linux-release')
subprocess.run(['sha256sum', '-c', 'SHA256SUMS'], cwd=source, check=True)
engine_log = (source / 'engine.log').read_text()
assert not re.search(r'^FAIL ', engine_log, re.M), 'Engine checks contain a failure'
engine = re.search(r'(\d+) tests passed; (\d+) platform/optional-codec tests skipped',
                   engine_log)
assert engine and int(engine[1]) > 0, 'Missing successful engine evidence'
trx = ET.parse(source / 'test-results/linux-desktop.trx')
counters = trx.find('.//{*}Counters')
assert counters is not None and int(counters.attrib['executed']) > 0
assert counters.attrib['passed'] == counters.attrib['executed'], 'Desktop checks did not all pass'
drive = (source / 'exfat-drive.log').read_text()
assert 'PASS: exFAT import/export, unmount/remount, index freshness and read-only mount rejection' in drive
assert 'Read-only file system' in drive, 'Missing genuine filesystem rejection'
assert 'PASS: native Linux FTP and SMB list/download, sidecars, verified retry and unchanged telescope originals' in (source / 'network-test.log').read_text()
assert 'PASS: actual Linux Secret Service persistence, private settings and credential removal' in (source / 'keyring-test.log').read_text()
assert 'PASS: independently decoded Linux MP4/GIF ratios, themes, timing, colors, full resolutions and six-chart stories' in (source / 'media-test.log').read_text()
assert 'PARITY BUTTONS PASS:' in (source / 'ui-test.log').read_text()
assert 'PASS native telescope rows render capture filenames' in (source / 'ui-test.log').read_text()
assert 'PASS native recovery rows render the paused import title' in (source / 'ui-test.log').read_text()
assert 'PASS native import preferences survive settings reload' in (source / 'ui-test.log').read_text()
assert re.search(r'^Pages:\s+6$', (source / 'pdf-test.log').read_text(), re.M)
parity = sorted((source / 'ui').glob('parity-*.png'))
assert len(parity) == 9, 'Missing native workflow screenshots'
screenshots = sorted((source / 'ui').glob('page-*.png'))
assert len(screenshots) == 24, 'Missing native normal, compact, large-text or light-theme screenshots'

output.mkdir(exist_ok=True)
for name in ['AstroArchive-3.1.4.1-preview.1-linux-x64.tar.gz', 'astroarchive_3.1.4.1-preview.1_amd64.deb']:
    shutil.copy2(source / name, output / name)
package_version = subprocess.check_output(['dpkg-deb', '-f', str(output / 'astroarchive_3.1.4.1-preview.1_amd64.deb'), 'Version'], text=True).strip()
assert package_version == '3.1.4.1~preview.1', 'Incorrect Debian prerelease metadata'
subprocess.run(['dpkg', '--compare-versions', package_version, 'gt', '3.1.4-preview.2'], check=True)
subprocess.run(['dpkg', '--compare-versions', package_version, 'lt', '3.1.4.1'], check=True)
shutil.copy2('CrossPlatform/README.md', output / 'README.md')
shutil.copy2('CrossPlatform/FEATURE-PARITY.md', output / 'FEATURE-PARITY.md')
ci_url = 'https://github.com/' + os.environ['GITHUB_REPOSITORY'] + '/actions/runs/' + os.environ['GITHUB_RUN_ID']
report = pathlib.Path('CrossPlatform/VALIDATION.md').read_text()
for key, value in {'SOURCE_SHA': os.environ['GITHUB_SHA'], 'CI_URL': ci_url,
                   'ENGINE_TESTS': engine[1], 'ENGINE_SKIPS': engine[2],
                   'DESKTOP_TESTS': counters.attrib['passed']}.items():
    report = report.replace('@' + key + '@', value)
assert not re.search(r'@[A-Z_]+@', report), 'Unresolved evidence placeholder'
(output / 'VALIDATION.md').write_text(report)
pages = ['repository', 'import', 'edited', 'analytics', 'settings', 'guide']
with zipfile.ZipFile(output / 'AstroArchive-Linux-page-screenshots.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for screenshot in parity:
        archive.write(screenshot, screenshot.name)
    for screenshot in screenshots:
        match = re.fullmatch(r'page-(\d+)(-compact|-large-text|-light)?\.png', screenshot.name)
        assert match and 0 <= int(match[1]) < 6
        name = pages[int(match[1])] + (match[2] or '-normal') + '.png'
        archive.write(screenshot, name)
with zipfile.ZipFile(output / 'Linux-validation-logs.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in ['engine.log', 'package-test.log', 'exfat-drive.log', 'network-test.log', 'keyring-test.log', 'ui-test.log', 'pdf-test.log', 'media-test.log', 'test-results/linux-desktop.trx']:
        archive.write(source / name, name)

def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()

files = sorted(p for p in output.iterdir() if p.is_file() and p.name != 'SHA256SUMS')
(output / 'SHA256SUMS').write_text(''.join(digest(p) + '  ' + p.name + '\n' for p in files))
print('Prepared verified packages, 33 native screenshots and CI evidence at', output)
