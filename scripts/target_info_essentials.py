#!/usr/bin/env python3
"""Deterministic hybrid essentials projection; Pillow is a build-time tool only.

Retain all identities, aliases, ambiguities, measurements, corrections and notices.
Keep the approved image coverage, reducing photographic thumbnails to 640 px.
The complete audited orbital numbers remain a separate embedded resource.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def encoded(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()


def apply(files):
    index = json.loads(files['index.json'])
    if index.get('essentials_revision') == 1:
        return files
    cards = {name: json.loads(data) for name, data in files.items() if name.startswith('cards/')}
    resized, originals, images = {}, set(), []
    references = {}
    curated = set(re.findall(r'^## (.+)$', (ROOT / 'Application_Source/TargetDescriptions.txt').read_text(), re.M))
    required = curated | {'NGC 6744', 'IC 2944', 'LDN 1622', 'NGC 55', 'NGC 6726', 'IC 10', 'NGC 6752', 'NGC 3201', 'NGC 6397', 'NGC 6101', 'NGC 346', 'NGC 3324', 'NGC 247', 'NGC 362'}
    for bucket in cards.values():
        for card in bucket.values():
            for citation in card.get('citations', []):
                common = {key: citation.pop(key) for key in ('source_url', 'publisher', 'retrieved_on', 'dataset_id', 'catalogue_version') if key in citation}
                key = hashlib.sha256(encoded(common)).hexdigest()[:24]
                if key in references and references[key] != common:
                    raise ValueError('Conflicting shared source: ' + key)
                references[key] = common
                citation['reference_id'] = key
            image = card.get('image')
            if not image:
                continue
            path = image['local_path']
            data = files[path]
            if hashlib.sha256(data).hexdigest() != image['sha256']:
                raise ValueError('Approved thumbnail hash mismatch: ' + path)
            originals.add(path)
            if card['domain'] == 'deep_sky' and card['id'] not in required and not re.fullmatch(r'M\d+', card['id']):
                card['image'] = None
                continue
            if path not in resized:
                with Image.open(io.BytesIO(data)) as source:
                    source.load()
                    source.thumbnail((640, 640), Image.Resampling.LANCZOS)
                    target = io.BytesIO()
                    # The approved assets are astronomical photographs. Composite
                    # transparent pixels against a black sky; document the change.
                    rgb = source.convert('RGBA')
                    background = Image.new('RGBA', rgb.size, (0, 0, 0, 255))
                    background.alpha_composite(rgb)
                    background.convert('RGB').save(target, format='JPEG', quality=70, optimize=True, progressive=False)
                    extension, mime = '.jpg', 'image/jpeg'
                    blob = target.getvalue()
                    digest = hashlib.sha256(blob).hexdigest()
                    resized[path] = ('assets/essential-' + digest[:24] + extension, blob, source.size, mime, digest)
            dest, blob, size, mime, digest = resized[path]
            old_digest = image['sha256']
            image.update(local_path=dest, sha256=digest, bytes=len(blob), width=size[0], height=size[1], mime=mime)
            image['modifications'] = (image.get('modifications') or '') + ' AstroArchive essentials: resized to at most 640 pixels, transparent pixels composited against black, and re-encoded as JPEG.'
            image['rights_evidence'] = image.get('subject_review') or ('Existing approved_local reviewed file mapping; upstream SHA-256 ' + old_digest)
            images.append({'object_id': card['id'], 'path': dest, 'sha256': digest,
                           'source_url': image['file_page_url'], 'licence': image['licence'],
                           'creator': image['creator'], 'full_attribution': image['attribution'],
                           'modifications': image['modifications'], 'rights_evidence': image['rights_evidence']})
    for path in originals:
        files.pop(path)
    for dest, data, _, _, _ in resized.values():
        files[dest] = data
    for name, bucket in cards.items():
        files[name] = encoded(bucket)
    index['references'] = references
    index['essentials_revision'] = 1
    index['thumbnail_max_pixels'] = 640
    files['index.json'] = encoded(index)
    prefix = (f'# Hybrid essentials projection\n\nThis build ships {len(images)} credited essential image assignments. '
              'The following upstream inventory describes the full reviewed source set; additional reviewed mappings are retained in '
              'InformationMedia.json for on-demand enrichment. Essentials hashes, exact credits, licences and resizing/black-matte '
              'modifications are recorded in essentials.json. Catalogue identities and acknowledgements remain intact.\n\n')
    for notice in ('LICENSES_AND_ATTRIBUTION.md', 'THUMBNAIL_COVERAGE.md'):
        files[notice] = prefix.encode() + files[notice]
    ids = sorted(index['entries'])
    curated = sorted(re.findall(r'^## (.+)$', (ROOT / 'Application_Source/TargetDescriptions.txt').read_text(), re.M))
    manifest = {'schema': 1, 'version': '3.2.9.1', 'policy': 1, 'identity_count': len(ids),
                'alias_count': len(index['aliases']), 'selection': 'All compact identities and all Messier/curated/southern essentials; other reviewed thumbnails enrich on demand. 640 px thumbnails; shared citations.',
                'messier': ['M' + str(n) for n in range(1, 111)], 'curated_cards': curated,
                'orbital_resource_sha256': hashlib.sha256((ROOT / 'Application_Source/OrbitEssentials.json').read_bytes()).hexdigest(),
                'images': sorted(images, key=lambda x: x['object_id']),
                'entries': {name: hashlib.sha256(data).hexdigest() for name, data in sorted(files.items())}}
    files['essentials.json'] = encoded(manifest)
    return files


def write(files, output):
    temp = output.with_suffix('.zip.tmp')
    try:
        with zipfile.ZipFile(temp, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for name, data in sorted(files.items()):
                if name.startswith('/') or '..' in name or '\\' in name or ':' in name:
                    raise ValueError('Unsafe entry: ' + name)
                entry = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
                entry.compress_type = zipfile.ZIP_DEFLATED
                entry.external_attr = 0o644 << 16
                archive.writestr(entry, data, compresslevel=9)
        if temp.stat().st_size > 15 * 1024 * 1024:
            raise ValueError('Essentials exceed the 15 MiB compressed budget.')
        temp.replace(output)
    finally:
        temp.unlink(missing_ok=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('pack', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    with zipfile.ZipFile(args.pack) as archive:
        files = {name: archive.read(name) for name in archive.namelist()}
    output = args.output or args.pack
    write(apply(files), output)
    print(f'{output}: {output.stat().st_size} bytes ({output.stat().st_size / 1048576:.2f} MiB)')
