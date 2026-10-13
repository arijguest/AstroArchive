#!/usr/bin/env python3
"""Merge the Wikidata distance companion into the target information pack.

The overlay (target-info-overrides/wikidata-distances.json.gz, see
build-distance-overlay.py) holds one referenced literature estimate per catalogue
identity. Rules, applied to the pack after the redshift distances of
target_info_distances.py:

* A Wikidata estimate replaces a redshift-derived distance on the same card; the two
  kinds are never shown together or averaged.
* A galaxy distance below 20,000 light-years is rejected as implausible (the nearest
  known galaxy is about 25,000 light-years away; such values are Mpc/pc unit slips).
* Where the estimate and the redshift distance differ by more than a factor of two,
  neither is shown until the conflict is reviewed. The ids are recorded in the pack index.
* Everything else, including the original value and unit, is kept in the tooltip text.

Run on an existing pack, or let build-target-info.py call it before the essentials
projection:

    python scripts/target_info_companions.py [Application_Source/TargetInfo.zip]
"""
import argparse
import gzip
import json
from pathlib import Path
import sys
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import target_info_essentials  # noqa: E402

HERE = Path(__file__).resolve().parent / 'target-info-overrides'
MIN_GALAXY_LY = 20000.0
MAX_DISAGREEMENT = 2.0
NOTICE_MARKER = '## Distance and comet companions'
NOTICE = '''
## Distance and comet companions

**Distances.** Galaxy and nebula distances come from Wikidata property P2583 (distance
from Earth), snapshot 13 October 2026, structured data released under CC0 1.0
(https://www.wikidata.org/wiki/Wikidata:Licensing). Each value links to its Wikidata
item and keeps the original quantity and unit. Only one referenced estimate for an
unambiguous catalogue identity is shown; competing estimates are not averaged. The
values are community-collected literature estimates, not a uniform survey. Catalogue
identities remain OpenNGC (CC BY-SA 4.0, Mattia Verga and contributors) and the adapted
crosswalk stays under CC BY-SA 4.0.

**Comets.** Orbital elements, uncertainties and meteor-parent associations come from the
NASA/JPL-Caltech Small-Body Database (API 1.0) and NASA/JPL Meteor Streams / NASA Science,
snapshot 13 October 2026. They are factual numerical data used with acknowledgement and no
implied endorsement; they are not asserted to be CC0. AstroArchive ships them in
CometCatalog.tsv.gz and MeteorShowers.json inside the application.
'''.lstrip('\n')


def encoded(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()


def significant(value, digits=2):
    import math
    scale = 10 ** (digits - 1 - int(math.floor(math.log10(value))))
    return round(value * scale) / scale


def light_years(value):
    if value >= 1e9:
        return '%s billion light-years' % format(significant(value / 1e9), 'g')
    if value >= 1e6:
        return '%s million light-years' % format(significant(value / 1e6), 'g')
    return '{:,} light-years'.format(int(significant(value)))


def qualifier(amount, unit, lower, upper):
    text = ('Literature estimate recorded by Wikidata (distance from Earth, P2583). Original value: %s %s.' % (amount, unit))
    if lower and upper:
        text += ' Source range: %s to %s.' % (light_years(lower), light_years(upper))
    elif lower or upper:
        text += ' Source bound: %s.' % light_years(lower or upper)
    return text + (' The source does not state the distance method or definition. This is not a redshift calculation.')


def load_overlay():
    with gzip.open(HERE / 'wikidata-distances.json.gz', 'rb') as handle:
        return json.loads(handle.read())


def apply(files):
    """Merge the overlay into {entry name: bytes} and return the updated mapping."""
    index = json.loads(files['index.json'])
    if index.get('runtime_companions'):
        raise ValueError('The Wikidata distance companion is already merged.')
    overlay = load_overlay()
    compact = index.get('essentials_revision') == 1
    cards = {name: json.loads(data) for name, data in files.items() if name.startswith('cards/')}
    stats = {'wikidata': 0, 'replaced_redshift': 0, 'implausible': [], 'conflicts': []}
    for ident, row in sorted(overlay['records'].items()):
        entry = index['entries'].get(ident)
        if entry is None:
            continue
        card = cards[entry][ident]
        estimate, value, lower, upper, amount, unit, item, statement = row
        if 'galaxy' in (card.get('type_label') or '').lower() and value < MIN_GALAXY_LY:
            stats['implausible'].append(ident)
            continue
        measurements = card.get('measurements') or []
        existing = [m for m in measurements if m['field'] == 'Distance']
        redshift = next((m for m in existing if m.get('band') == 'redshift'), None)
        if existing and redshift is None:
            continue  # A reviewed non-redshift distance keeps precedence.
        if redshift is not None:
            measurements = [m for m in measurements if m is not redshift]
            ratio = max(value, redshift['value']) / min(value, redshift['value'])
            if ratio > MAX_DISAGREEMENT:
                card['measurements'] = measurements
                stats['conflicts'].append(ident)
                continue
            stats['replaced_redshift'] += 1
        common = {'catalogue_version': overlay['snapshot'], 'dataset_id': 'wikidata', 'publisher': 'Wikidata contributors',
                  'retrieved_on': overlay['snapshot'], 'source_url': 'https://www.wikidata.org/wiki/' + item + '#P2583'}
        # Each citation links its own Wikidata item, so it keeps its source inline instead of
        # adding thousands of shared index references (the essentials projection leaves
        # dataset_id "wikidata" citations alone for the same reason).
        citation = {'id': 'distance:' + estimate + ':citation', 'object_id': ident, 'applies_to': 'measurement:distance:' + estimate,
                    'bibcode': None, 'doi': None, 'review_status': 'source_record_checked', 'source_row': statement,
                    'source_title': 'Wikidata P2583 · ' + item}
        citation.update(common)
        uncertainty = json.dumps({k: v for k, v in (('lower_ly', lower), ('upper_ly', upper)) if v}, separators=(',', ':')) if lower or upper else None
        measurements.append({'id': 'distance:' + estimate, 'object_id': ident, 'field': 'Distance', 'value': value, 'unit': 'ly',
                             'epoch': None, 'band': 'wikidata', 'uncertainty': uncertainty,
                             'qualifier': qualifier(amount, unit, lower, upper), 'citation_id': citation['id']})
        card['measurements'] = measurements
        card['citations'] = [c for c in card.get('citations', []) if c['id'] != citation['id']] + [citation]
        stats['wikidata'] += 1
    for name, bucket in cards.items():
        files[name] = encoded(bucket)
    stats['implausible'].sort()
    stats['conflicts'].sort()
    index['runtime_companions'] = {'revision': 1, 'distances': {'source': 'Wikidata P2583', 'snapshot': overlay['snapshot'], **stats}}
    files['index.json'] = encoded(index)
    for notice in ('LICENSES_AND_ATTRIBUTION.md',):
        text = files[notice].decode('utf-8')
        if NOTICE_MARKER not in text:
            files[notice] = (text.rstrip('\n') + '\n\n' + NOTICE).encode('utf-8')
    if compact:
        files = target_info_essentials.seal(files)
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('pack', type=Path, nargs='?', default=Path(__file__).resolve().parents[1] / 'Application_Source/TargetInfo.zip')
    args = parser.parse_args()
    with zipfile.ZipFile(args.pack) as archive:
        files = {name: archive.read(name) for name in archive.namelist()}
    try:
        files = apply(files)
    except ValueError as error:
        raise SystemExit(str(args.pack) + ': ' + str(error))
    target_info_essentials.write(files, args.pack)
    stats = json.loads(files['index.json'])['runtime_companions']['distances']
    print('%s: %d bytes; %d Wikidata distances (%d replaced redshift), %d implausible, %d conflicts' % (
        args.pack, args.pack.stat().st_size, stats['wikidata'], stats['replaced_redshift'], len(stats['implausible']), len(stats['conflicts'])))


if __name__ == '__main__':
    main()
