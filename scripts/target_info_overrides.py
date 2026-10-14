#!/usr/bin/env python3
"""Apply reviewed image and card corrections to the target information pack.

The database projection (build-target-info.py) remains the source of the pack.
These overrides replace or add licensed thumbnails, correct a few card fields and
add pages that are not catalogue objects (the eclipse and Comets sections). They
are applied by build-target-info.py and can be applied to an existing pack:

    python scripts/target_info_overrides.py [Application_Source/TargetInfo.zip]

Entries keep the pack's fixed timestamps and order, so the result is reproducible.
"""
import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import target_info_distances  # noqa: E402

HERE = Path(__file__).resolve().parent / 'target-info-overrides'
COMMONS = 'https://upload.wikimedia.org/wikipedia/commons/'
THUMBS = 'https://thumb.wikimedia.org/wikipedia/commons/thumb/'


def normalize(name):
    # Mirrors TargetInfoCatalog.Normalize for plain names (not catalogue designations).
    return 'NAME:' + re.sub(r'\s+', ' ', name.strip().upper())


def bucket(ident):
    return 'cards/' + hashlib.sha256(ident.encode()).hexdigest()[:2] + '.json'


def jpeg_size(data):
    i = 2
    while i < len(data):
        if data[i] != 0xFF:
            raise ValueError('Not a JPEG marker')
        marker, length = data[i + 1], int.from_bytes(data[i + 2:i + 4], 'big')
        if marker in (0xC0, 0xC1, 0xC2):
            return int.from_bytes(data[i + 7:i + 9], 'big'), int.from_bytes(data[i + 5:i + 7], 'big')
        i += 2 + length
    raise ValueError('JPEG has no frame header')


def image_record(ident, spec, verified_on):
    data = (HERE / 'assets' / spec['file']).read_bytes()
    width, height = jpeg_size(data)
    path = spec['commons_path'].replace("'", '%27').replace('(', '%28').replace(')', '%29')
    name = path.rsplit('/', 1)[-1]
    digest = hashlib.sha256(data).hexdigest()
    record = {
        'id': 'commons:' + name + ':' + digest[:8],
        'object_id': ident,
        'status': 'approved_local',
        'file_page_url': spec['file_page_url'],
        'media_url': COMMONS + path,
        'thumbnail_url': THUMBS + path + '/960px-' + name,
        'local_path': 'assets/' + spec['file'],
    }
    for field in ('creator', 'attribution', 'rights_holder', 'licence', 'licence_url'):
        record[field] = spec[field]
    record.update(verified_on=verified_on,
                  modifications="Commons 960-pixel preview, displayed at about 600 pixels and re-encoded as JPEG; no further image processing.",
                  representation=spec['representation'], subject_review=spec['subject_review'],
                  sha256=digest, bytes=len(data), mime='image/jpeg', width=width, height=height)
    return record, data


def apply(files):
    """Apply the overrides to {entry name: bytes} and return the updated mapping."""
    overrides = json.loads((HERE / 'overrides.json').read_text(encoding='utf-8'))
    index = json.loads(files['index.json'])
    cards = {}
    for name, data in files.items():
        if name.startswith('cards/'):
            cards[name] = json.loads(data)

    def card(ident):
        return cards[index['entries'][ident]][ident]

    for spec in overrides['cards']:
        ident = spec['id']
        entry = bucket(ident)
        index['entries'][ident] = entry
        new = {'id': ident, 'name': spec['name'], 'domain': spec['domain'], 'type_label': spec['type_label'],
               'constellation': None, 'parent_body': '', 'review_status': 'source_checked',
               'identity_status': 'editorial_draft', 'content_tier': 'rich', 'summary': spec['summary'],
               'fact': spec['fact'], 'aliases': sorted(spec['aliases']), 'measurements': [], 'citations': [],
               'source_urls': spec['source_urls'], 'image': None}
        cards.setdefault(entry, {})[ident] = new
        for alias in spec['aliases']:
            ids = index['aliases'].setdefault(normalize(alias), [])
            if ident not in ids:
                ids.append(ident)
                ids.sort()
    for ident, fields in overrides.get('fields', {}).items():
        card(ident).update(fields)

    distances = target_info_distances.apply(cards)
    replaced = set()
    for ident, spec in overrides['images'].items():
        target = card(ident)
        if target.get('image'):
            replaced.add(target['image']['local_path'])
        record, data = image_record(ident, spec, overrides['verified_on'])
        target['image'] = record
        files[record['local_path']] = data
    used = {c['image']['local_path'] for bucket_cards in cards.values() for c in bucket_cards.values() if c.get('image')}
    for path in replaced - used:
        files.pop(path, None)
    index['image_assignments'] = sum(1 for bucket_cards in cards.values() for c in bucket_cards.values() if c.get('image'))
    index['runtime_overrides'] = {'revision': 2, 'distances': distances, 'verified_on': overrides['verified_on'], 'images': sorted(overrides['images']),
                                  'cards': [c['id'] for c in overrides['cards']]}

    for name, cards_in_bucket in cards.items():
        files[name] = json.dumps(dict(sorted(cards_in_bucket.items())), ensure_ascii=False, separators=(',', ':')).encode()
    files['index.json'] = json.dumps(index, ensure_ascii=False, separators=(',', ':')).encode()
    for name, replacements in overrides.get('notices', {}).items():
        if name in files:
            text = files[name].decode('utf-8')
            for old, new in replacements:
                text = text.replace(old, new)
            files[name] = text.encode('utf-8')
    return files


def order(name):
    # The exporter's layout: card buckets, index, image assets, then notices and documents.
    group = 0 if name.startswith('cards/') else 1 if name == 'index.json' else 2 if name.startswith('assets/') else 3
    return group, name if group < 3 else ''


def write(files, output, original_order=None):
    names = sorted(files, key=lambda n: (order(n), (original_order or {}).get(n, 0)))
    with tempfile.NamedTemporaryFile(dir=output.parent, suffix='.zip', delete=False) as temporary:
        staged = Path(temporary.name)
    try:
        with zipfile.ZipFile(staged, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for name in names:
                info = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
                archive.writestr(info, files[name], compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
        os.replace(staged, output)
    finally:
        staged.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('pack', type=Path, nargs='?', default=Path(__file__).resolve().parents[1] / 'Application_Source/TargetInfo.zip')
    args = parser.parse_args()
    with zipfile.ZipFile(args.pack) as archive:
        names = archive.namelist()
        index = json.loads(archive.read('index.json'))
        if index.get('runtime_overrides'):
            raise SystemExit(str(args.pack) + ' already has these overrides; rebuild it from the database first.')
        files = {name: archive.read(name) for name in names}
    write(apply(files), args.pack, {name: i for i, name in enumerate(names)})
    print(str(args.pack) + ': ' + str(args.pack.stat().st_size) + ' bytes')


if __name__ == '__main__':
    main()
