"""Require the Windows trust report and bind it to the uploaded installer bytes."""
import hashlib
import json
import os
from pathlib import Path
import re
import sys


def verify(directory, publisher, allow_unsigned=False):
    feed = json.loads((directory / 'update.json').read_text(encoding='utf-8'))
    signed = feed.get('authenticode_signed')
    if signed not in (True, False) or not isinstance(signed, bool):
        raise ValueError('Missing Windows signing status in update feed.')
    package = feed['package_version']
    version = feed['application_version']
    if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+', package) or package.rsplit('.', 1)[0] != version:
        raise ValueError('Invalid release version.')
    installer = f'AstroArchive{package}.exe'
    alias = f'AstroArchive-{version}-Windows-x64-Offline-Setup.exe'
    records = None
    if not allow_unsigned or signed or (directory / 'signatures.json').exists():
        if not signed or not publisher or not (directory / 'signatures.json').is_file():
            raise ValueError('Unsigned release blocked. Configure Microsoft Artifact Signing as described in docs/RELEASING.md.')
        report = json.loads((directory / 'signatures.json').read_text(encoding='utf-8'))
        records = report['files']
        required = {installer, 'AstroArchive.exe', 'Start.exe'}
        if report['schema'] != 1 or len(records) != 3 or {entry['file'] for entry in records} != required:
            raise ValueError('Incomplete Windows signature report.')
        for entry in records:
            if (entry['status'] != 'Valid' or entry['publisher'] != publisher or
                    not entry['thumbprint'] or not entry['timestamp_publisher']):
                raise ValueError(f"Unverified signature for {entry['file']}.")
    primary = (directory / installer).read_bytes()
    digest = hashlib.sha256(primary).hexdigest()
    record = next(entry for entry in records if entry['file'] == installer) if records else None
    if (record and digest != record['sha256']) or digest != feed['sha256'] or len(primary) != feed['size']:
        raise ValueError('Installer changed after Windows signature verification.')
    if (directory / alias).read_bytes() != primary:
        raise ValueError('Compatibility installer differs from the verified installer.')
    if {path.name for path in directory.glob('*.exe')} != {installer, alias}:
        raise ValueError('Unexpected executable in release artifacts.')
    for name in (installer, alias):
        expected = f'{digest}  {name}'
        if (directory / f'{name}.sha256').read_text(encoding='ascii').strip() != expected:
            raise ValueError(f'Checksum does not match the signed file: {name}.')


if __name__ == '__main__':
    try:
        verify(Path(sys.argv[1]), os.environ.get('SIGNING_PUBLISHER', ''), allow_unsigned=sys.argv[2:] == ['--allow-unsigned'])
    except (ValueError, KeyError, OSError, IndexError, TypeError) as error:
        sys.exit(str(error))
    print('PASS release signing policy, installer integrity, alias and update feed agree')
