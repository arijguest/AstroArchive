"""Validate shipped identity, scientific provenance and essentials integrity."""
import hashlib
import json
from pathlib import Path
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
pack = ROOT / 'Application_Source/TargetInfo.zip'
assert pack.stat().st_size <= 15 * 1048576, 'Essentials exceed 15 MiB'
with zipfile.ZipFile(pack) as archive:
    names = archive.namelist()
    assert len(names) == len(set(names))
    assert all(not n.startswith('/') and '..' not in n and '\\' not in n and ':' not in n for n in names)
    manifest = json.loads(archive.read('essentials.json'))
    index = json.loads(archive.read('index.json'))
    assert manifest['identity_count'] == len(index['entries']) == 13528
    assert manifest['alias_count'] == len(index['aliases']) == 66171
    assert index['uncertain'] and index['nonexistent']
    for name, digest in manifest['entries'].items():
        assert hashlib.sha256(archive.read(name)).hexdigest() == digest, name
    cards = {key: value for n in names if n.startswith('cards/') for key, value in json.loads(archive.read(n)).items()}
    assert set(cards) == set(index['entries'])
    for n in range(1, 111):
        assert 'M' + str(n) in cards
    for card in cards.values():
        sources = {c['id'] for c in card.get('citations', [])}
        for citation in card.get('citations', []):
            if citation.get('dataset_id') == 'wikidata':
                assert citation['source_url'].startswith('https://www.wikidata.org/wiki/Q') and citation['publisher'] and citation['retrieved_on']
            else:
                assert citation['reference_id'] in index['references']
        for measurement in card.get('measurements', []):
            assert measurement['citation_id'] in sources, card['id']
        image = card.get('image')
        if image:
            assert image['licence'] and image['creator'] and image['attribution'] and image['rights_evidence']
            assert image['width'] <= 640 and image['height'] <= 640
            assert hashlib.sha256(archive.read(image['local_path'])).hexdigest() == image['sha256']
    for ident in ['COMETS', 'SOLAR ECLIPSE', 'LUNAR ECLIPSE']:
        assert any(c['id'] == ident or c['name'].upper() == ident for c in cards.values()), ident
    assert any(n.endswith('LICENSES_AND_ATTRIBUTION.md') for n in names)
    assert 'notices/CC-BY-SA-4.0.txt' in names and 'OpenNGC' in archive.read('LICENSES_AND_ATTRIBUTION.md').decode()
    assert hashlib.sha256((ROOT / 'Application_Source/OrbitEssentials.json').read_bytes()).hexdigest() == manifest['orbital_resource_sha256']
providers = json.loads((ROOT / 'Application_Source/InformationProviders.json').read_text())
for provider in providers:
    if provider['provider'] in ('SIMBAD', 'VizieR', 'NED'):
        assert not any(provider[key] for key in ('allowed_display', 'allowed_local_cache', 'allowed_bundle', 'allowed_redistribution'))
orbits = json.loads((ROOT / 'Application_Source/OrbitEssentials.json').read_text())
for ident, orbit in orbits.items():
    assert orbit['object_id'] == ident and orbit['time_standard'] == 'TDB'
    assert orbit['coordinate_frame'] == 'J2000' and orbit['reference_plane'] == 'ecliptic'
    assert all(key in orbit for key in ('epoch', 'e', 'q', 'i', 'ascending_node', 'argument_of_perihelion', 'perihelion_time', 'source', 'rights'))
    assert orbit['rights']['licence_id'] == 'numerical-facts'
    assert 'valid_from' not in orbit and 'valid_to' not in orbit
print(f"PASS {len(cards)} identities, {len(index['aliases'])} aliases, {len(manifest['images'])} credited essential thumbnails, {len(orbits)} complete orbits; {pack.stat().st_size / 1048576:.2f} MiB")
