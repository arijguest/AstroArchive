"""Regenerate the compact, offline constellation figures from pinned D3-Celestial data."""
import json
from pathlib import Path
from urllib.request import urlopen

REVISION = '7e720a3de062059d4c5400a379146a601d9010e0'
BASE = f'https://raw.githubusercontent.com/ofrohn/d3-celestial/{REVISION}/'
root = Path(__file__).resolve().parents[1]

def fetch(name):
    with urlopen(BASE + name, timeout=30) as response:
        return response.read().decode('utf-8')

names = {f['id']: f for f in json.loads(fetch('data/constellations.json'))['features']}
lines = json.loads(fetch('data/constellations.lines.json'))['features']
rows = ['# D3-Celestial constellation figures (J2000 degrees); see Sky_Catalogue_Notice.md',
        '# ID|name|label RA,Dec|line paths (RA,Dec pairs separated by spaces; paths by /)']
for feature in lines:
    identifier = feature['id']
    label = names[identifier]
    def coordinate(pair):
        ra, dec = pair
        assert -90 <= dec <= 90
        return f'{ra % 360:.4f},{dec:.4f}'
    paths = ' / '.join(' '.join(coordinate(point) for point in path)
                       for path in feature['geometry']['coordinates'])
    rows.append('|'.join([identifier, label['properties']['name'],
                          coordinate(label['geometry']['coordinates']), paths]))
(root / 'Application_Source/sky-constellations.txt').write_text('\n'.join(rows) + '\n', encoding='utf-8')
notice = ('# Constellation figures\n\n'
          'The offline sky globe uses the J2000 constellation line figures and label positions\n'
          'from [D3-Celestial by Olaf Frohn](https://github.com/ofrohn/d3-celestial),\n'
          f'revision `{REVISION}`. Sources: `data/constellations.lines.json` and\n'
          '`data/constellations.json`. Coordinates are rounded to four decimal places,\n'
          'right ascension is normalized to 0–360 degrees, and unrelated translations\n'
          'are omitted. These figures are illustrations, not IAU constellation boundaries.\n\n'
          'D3-Celestial is distributed under the following BSD 3-clause licence:\n\n' + fetch('LICENSE') + '\n')
(root / 'Application_Source/Sky_Catalogue_Notice.md').write_text(notice, encoding='utf-8')
print(f'Wrote {len(lines)} constellation figures, including the two Serpens regions.')
