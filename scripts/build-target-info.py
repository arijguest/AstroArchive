#!/usr/bin/env python3
"""Project the reviewed SQLite release into a small, lazily read WPF data pack.

No Python/SQLite provider is required by the Windows application. Exact alias
ambiguity and citation/image metadata are preserved; prose drafts are omitted.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sqlite3
from target_image_credits import repair_image
import target_info_overrides
import target_info_essentials


def build(database):
    root = database.resolve().parent.parent
    con = sqlite3.connect(database.resolve().as_uri() + '?mode=ro', uri=True)
    con.row_factory = sqlite3.Row
    aliases = {}
    for row in con.execute('SELECT term,object_id FROM alias ORDER BY term,object_id'):
        aliases.setdefault(row['term'], []).append(row['object_id'])
    uncertain = [r[0] for r in con.execute("SELECT DISTINCT term FROM designation WHERE status='uncertain' ORDER BY term")]
    nonexistent = [r[0] for r in con.execute("SELECT DISTINCT term FROM designation WHERE status='nonexistent' ORDER BY term")]
    entries, images, buckets = {}, {}, {}
    image_assignments = 0
    # The licensed Horsehead field also depicts the surrounding IC 434 emission
    # nebula. Share its bytes while keeping the two catalogue identities distinct.
    shared_fields = {"IC 434": "B 33"}
    for row in con.execute('SELECT * FROM object ORDER BY id'):
        card = dict(row)
        ident = card['id']
        entry = 'cards/' + hashlib.sha256(ident.encode()).hexdigest()[:2] + '.json'
        entries[ident] = entry
        checked = card['review_status'] != 'draft'
        card = {key: card[key] for key in ('id', 'name', 'domain', 'type_label', 'constellation', 'parent_body', 'review_status', 'identity_status', 'content_tier')}
        card['summary'] = row['overview'] if checked else row['catalogue_summary']
        card['fact'] = row['fact'] if checked and row['content_tier'] == 'rich' else None
        card['aliases'] = [r[0] for r in con.execute('SELECT DISTINCT display_alias FROM alias WHERE object_id=? ORDER BY display_alias', (ident,))]
        card['measurements'] = [dict(r) for r in con.execute('SELECT * FROM measurement WHERE object_id=? ORDER BY field,id', (ident,))]
        card['citations'] = [dict(r) for r in con.execute('SELECT * FROM citation WHERE object_id=? ORDER BY id', (ident,))]
        card['source_urls'] = json.loads(row['source_urls']) if checked else []
        image = con.execute("SELECT * FROM image WHERE object_id=? AND status='approved_local' ORDER BY id LIMIT 1", (ident,)).fetchone()
        shared = ident in shared_fields and image is None
        if shared:
            image = con.execute("SELECT * FROM image WHERE object_id=? AND status='approved_local' ORDER BY id LIMIT 1", (shared_fields[ident],)).fetchone()
            if image is None:
                raise ValueError('Missing approved Horsehead/IC 434 field image')
        card['image'] = repair_image(dict(image)) if image else None
        if shared:
            card['image'].update(object_id=ident, id=image['id'] + ':ic434',
                                 representation='IC 434 emission nebula behind the dark Horsehead Nebula (Barnard 33).')
        if image:
            image_assignments += 1
            path = root / image['local_path']
            data = path.read_bytes()
            if hashlib.sha256(data).hexdigest() != image['sha256']:
                raise ValueError('Image hash mismatch: ' + str(path))
            images[image['local_path']] = data
        buckets.setdefault(entry, {})[ident] = card
    for entry, cards in sorted(buckets.items()):
        yield entry, json.dumps(cards, ensure_ascii=False, separators=(',', ':')).encode()
    manifest = dict(con.execute('SELECT key,value FROM metadata'))
    manifest.update(format_version=1, entries=entries, aliases=aliases,
                    uncertain=uncertain, nonexistent=nonexistent,
                    database_sha256=hashlib.sha256(database.read_bytes()).hexdigest(),
                    image_assignments=image_assignments, runtime_shared_fields=shared_fields)
    manifest['runtime_credit_revision'] = 1
    yield 'index.json', json.dumps(manifest, ensure_ascii=False, separators=(',', ':')).encode()
    for path, data in sorted(images.items()):
        yield path, data
    for name in ('LICENSES_AND_ATTRIBUTION.md', 'THUMBNAIL_COVERAGE.md'):
        yield name, (root / name).read_bytes()
    for path in sorted((root / 'source/upstream').rglob('*')):
        if path.is_file() and (path.suffix.lower() in ('.md', '.txt') or path.name in ('AUTHORS', 'reuse.toml')):
            yield 'notices/' + str(path.relative_to(root / 'source/upstream')), path.read_bytes()
    con.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('database', type=Path)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Application_Source/TargetInfo.zip')
    args = parser.parse_args()
    # Stable entry timestamps/order allow reproducible data-pack hashes. Reviewed image
    # and card corrections (scripts/target-info-overrides) are applied to the projection.
    entries = list(build(args.database))
    files = target_info_overrides.apply(dict(entries))
    target_info_essentials.write(target_info_essentials.apply(files), args.output)
    print(str(args.output) + ': ' + str(args.output.stat().st_size) + ' bytes')
