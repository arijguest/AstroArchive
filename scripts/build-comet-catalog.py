#!/usr/bin/env python3
"""Project the AstroArchive comet companion into two small embedded resources.

Input is the unpacked AstroArchive_Comets_1.0.0 folder (NASA/JPL Small-Body Database
snapshot of 13 October 2026 plus meteor-parent facts). Output:

  Application_Source/CometCatalog.tsv.gz  one row per comet: identity, full-precision
                                          osculating elements, 1-sigma values, MOIDs
                                          and observation span
  Application_Source/MeteorShowers.json   named showers and their listed or suspected
                                          comet parents, one record per shower/comet pair

Only numerical facts, designations and association facts are kept; no prose or images.
Output is byte-for-byte reproducible from the same input.
"""
import argparse
import csv
import gzip
import io
import json
from pathlib import Path

COLUMNS = ['id', 'spkid', 'full_name', 'name', 'orbit_class', 'kind', 'orbit_id', 'epoch', 'e', 'q', 'i', 'node', 'peri', 'ma', 'tp',
           'period_years', 'a', 'aphelion', 'moid_earth', 'moid_jupiter', 'observations', 'arc_days', 'first_obs', 'last_obs',
           'sigma_e', 'sigma_q', 'sigma_i', 'sigma_node', 'sigma_peri', 'sigma_tp']
SIGMA_FIELDS = {'Eccentricity': 'sigma_e', 'Perihelion': 'sigma_q', 'Inclination': 'sigma_i', 'AscendingNode': 'sigma_node',
                'ArgumentOfPerihelion': 'sigma_peri', 'TimeOfPerihelion': 'sigma_tp'}
SOURCE_COLUMNS = {'epoch': 'epoch_jd_tdb', 'e': 'eccentricity', 'q': 'perihelion_au', 'i': 'inclination_deg', 'node': 'node_deg',
                  'peri': 'argument_perihelion_deg', 'ma': 'mean_anomaly_deg', 'tp': 'perihelion_jd_tdb', 'period_years': 'period_years',
                  'a': 'semimajor_axis_au', 'aphelion': 'aphelion_au', 'moid_earth': 'earth_moid_au', 'moid_jupiter': 'jupiter_moid_au',
                  'observations': 'observations_used', 'arc_days': 'data_arc_days', 'first_obs': 'first_observation_utc',
                  'last_obs': 'last_observation_utc', 'kind': 'orbit_kind', 'orbit_class': 'orbit_class', 'orbit_id': 'orbit_id',
                  'full_name': 'full_name', 'name': 'name', 'spkid': 'spkid'}
STATUS_ORDER = {'listed': 0, 'source_listed': 0, 'suspected': 1, 'candidate': 2}


def read(folder, name):
    with open(folder / name, newline='', encoding='utf-8') as handle:
        return list(csv.DictReader(handle))


def sigma(text):
    return (text or '').split(' ')[0]


def comets(folder):
    sigmas = {}
    for row in read(folder, 'measurement.csv'):
        column = SIGMA_FIELDS.get(row['field'])
        if column and row['uncertainty']:
            sigmas.setdefault(row['object_id'], {})[column] = sigma(row['uncertainty'])
    out = io.StringIO(newline='')
    writer = csv.writer(out, delimiter='\t', lineterminator='\n', quoting=csv.QUOTE_NONE, escapechar='\\')
    writer.writerow(COLUMNS)
    rows = sorted(read(folder, 'comet.csv'), key=lambda r: r['id'])
    for row in rows:
        values = {'id': row['id']}
        for column, source in SOURCE_COLUMNS.items():
            values[column] = row[source]
        values.update(sigmas.get(row['id'], {}))
        writer.writerow([values.get(column, '') for column in COLUMNS])
    return out.getvalue().encode('utf-8'), len(rows)


def showers(folder):
    names = {r['id']: r for r in read(folder, 'meteor_shower.csv')}
    pairs = {}
    for row in read(folder, 'meteor_association.csv'):
        if not row['object_id']:
            continue
        pair = pairs.setdefault((row['shower_id'], row['object_id']), {'comet': row['object_id'], 'listed_as': row['source_parent_name'], 'statuses': set(), 'sources': set()})
        pair['statuses'].add(row['status'])
        pair['sources'].add(row['source_url'])
    parents = {}
    for (shower, comet), pair in sorted(pairs.items()):
        status = sorted(pair['statuses'], key=lambda s: STATUS_ORDER.get(s, 9))[0]
        status = 'listed' if status == 'source_listed' else status
        parents.setdefault(shower, []).append({'comet': comet, 'listed_as': pair['listed_as'], 'status': status, 'sources': sorted(pair['sources'])})
    records = []
    for ident, row in sorted(names.items()):
        records.append({'code': row['code'], 'name': row['name'], 'peak': row['peak_approximate'], 'parents': parents.get(ident, [])})
    return {'format_version': 1, 'snapshot': '2026-10-13',
            'note': 'Peak wording is approximate and is not a year-specific forecast. No listed parent means not recorded here, not that none exists.',
            'sources': ['https://ssd.jpl.nasa.gov/sb/meteor_streams.html', 'https://science.nasa.gov/solar-system/meteors-meteorites/'],
            'showers': records}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('companion', type=Path, help='unpacked AstroArchive_Comets_1.0.0 folder')
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Application_Source')
    args = parser.parse_args()
    table, count = comets(args.companion)
    with open(args.output / 'CometCatalog.tsv.gz', 'wb') as handle:
        with gzip.GzipFile(filename='', mode='wb', fileobj=handle, compresslevel=9, mtime=0) as archive:
            archive.write(table)
    document = json.dumps(showers(args.companion), ensure_ascii=False, indent=1, sort_keys=True) + '\n'
    (args.output / 'MeteorShowers.json').write_text(document, encoding='utf-8', newline='\n')
    print('%d comets, %d bytes raw' % (count, len(table)))


if __name__ == '__main__':
    main()
