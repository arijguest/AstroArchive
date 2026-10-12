"""Join GeoNames timezone IDs to the existing city catalogue; map IDs with CLDR.

python scripts/build-city-timezones.py SOURCE_DIRECTORY
SOURCE_DIRECTORY contains cities500.zip, windowsZones.xml and timezone.xml.
The place catalogue, names, coordinates and ordering are left unchanged.
"""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile


def build(source, catalogue, output):
    windows = {}
    for zone in ET.parse(source / 'windowsZones.xml').iter('mapZone'):
        for ident in zone.attrib['type'].split():
            windows[ident] = zone.attrib['other']
    for zone in ET.parse(source / 'timezone.xml').iter('type'):
        aliases = zone.attrib.get('alias', '').split()
        match = next((windows[a] for a in aliases if a in windows), None)
        if match:
            for alias in aliases:
                windows.setdefault(alias, match)
    zones = {}
    with zipfile.ZipFile(source / 'cities500.zip') as archive:
        for line in io.TextIOWrapper(archive.open('cities500.txt'), encoding='utf-8'):
            fields = line.rstrip('\r\n').split('\t')
            zones[fields[0]] = fields[17]
    rows, missing = [], []
    with gzip.open(catalogue, 'rt', encoding='utf-8') as cities:
        next(cities)
        for line in cities:
            identity = line.split('\t', 1)[0]
            ident = zones.get(identity)
            if ident and ident in windows:
                rows.append(identity + '\t' + ident + '\t' + windows[ident])
            else:
                missing.append((identity, ident))
    # Fail rather than ship city choices whose timezone silently cannot resolve.
    if missing:
        raise ValueError('Missing timezone mappings: ' + repr(missing[:30]))
    with output.open('wb') as stream:
        with gzip.GzipFile(filename='', fileobj=stream, mode='wb', mtime=0) as zipped:
            zipped.write(('Id\tIana\tWindows\n' + '\n'.join(rows) + '\n').encode())
    hashes = {name: hashlib.sha256((source / name).read_bytes()).hexdigest()
              for name in ('cities500.zip', 'windowsZones.xml', 'timezone.xml')}
    report = dict(cities=len(rows), bytes=output.stat().st_size, source_hashes=hashes,
                  output_sha256=hashlib.sha256(output.read_bytes()).hexdigest())
    print(json.dumps(report, indent=2))
    output.with_suffix('.json').write_text(json.dumps(report, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    root = Path(__file__).resolve().parents[1]
    parser.add_argument('--catalogue', type=Path, default=root / 'Application_Source/cities.tsv.gz')
    parser.add_argument('--output', type=Path, default=root / 'Application_Source/city-timezones.tsv.gz')
    args = parser.parse_args()
    build(args.source, args.catalogue, args.output)
