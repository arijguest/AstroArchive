#!/usr/bin/env python3
"""Reduce the AstroArchive distance companion to the single referenced estimates the pack shows.

Input is the unpacked AstroArchive_Distances_1.0.0 folder (Wikidata P2583, snapshot
13 October 2026, CC0). Output is scripts/target-info-overrides/wikidata-distances.json.gz,
which target_info_companions.py merges into the target information pack.

Only estimates the companion marks `single_referenced_estimate` are kept: one referenced,
non-deprecated statement for an unambiguous catalogue identity. Objects with competing
estimates, unreferenced statements or identity questions are left out rather than averaged.
Values are rounded to six significant figures; conversion adds no scientific precision.
"""
import argparse
import gzip
import json
import math
from pathlib import Path

SNAPSHOT = '2026-10-13'


def significant(value, digits=6):
    scale = 10 ** (digits - 1 - int(math.floor(math.log10(value))))
    return round(value * scale) / scale


def build(folder):
    records = json.loads((folder / 'runtime/distances.json').read_text(encoding='utf-8'))['records']
    out = {}
    for ident, record in sorted(records.items()):
        if record['selection']['selection_status'] != 'single_referenced_estimate':
            continue
        estimate = next(e for e in record['estimates'] if e['selected'])
        if not estimate['value_ly'] or estimate['value_ly'] <= 0:
            continue
        # The statement GUID follows the item identifier: ...wiki/Q2469#Q2469$<guid>.
        guid = estimate['statement_url'].split('$', 1)[1]
        low, high = estimate['lower_ly'], estimate['upper_ly']
        out[ident] = [estimate['id'].split(':', 1)[1], significant(estimate['value_ly']),
                      significant(low) if low else None, significant(high) if high else None,
                      estimate['source_amount'], estimate['source_unit'], estimate['wikidata_id'], guid]
    return {'snapshot': SNAPSHOT, 'property': 'P2583', 'base_data_version': json.loads((folder / 'COVERAGE.json').read_text())['base_data_version'],
            'columns': ['estimate', 'value_ly', 'lower_ly', 'upper_ly', 'source_amount', 'source_unit', 'wikidata_item', 'statement'],
            'records': out}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('companion', type=Path, help='unpacked AstroArchive_Distances_1.0.0 folder')
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parent / 'target-info-overrides/wikidata-distances.json.gz')
    args = parser.parse_args()
    data = json.dumps(build(args.companion), separators=(',', ':'), sort_keys=True).encode()
    with open(args.output, 'wb') as handle:
        with gzip.GzipFile(filename='', mode='wb', fileobj=handle, compresslevel=9, mtime=0) as archive:
            archive.write(data)
    print('%s: %d estimates, %d bytes' % (args.output, len(json.loads(data)['records']), args.output.stat().st_size))


if __name__ == '__main__':
    main()
