"""Bundle package licence material and an exact dependency inventory."""
import json
import pathlib
import shutil
import sys
import xml.etree.ElementTree as ET

destination = pathlib.Path(sys.argv[1]) / 'licenses'
destination.mkdir()
assets = json.loads(pathlib.Path('CrossPlatform/Desktop/obj/project.assets.json').read_text())
cache = pathlib.Path(next(iter(assets['packageFolders'])))
lines = ['AstroArchive Linux third-party dependencies', '', 'Required Notice: Copyright 2026 Ari J. Guest (https://arijguest.com)', '']
for key, library in sorted(assets['libraries'].items()):
    if library['type'] != 'package':
        continue
    folder = cache / library['path']
    spec = ET.parse(next(folder.glob('*.nuspec'))).getroot()
    license_node = next((n for n in spec.iter() if n.tag.split('}')[-1] == 'license'), None)
    license_text = license_node.text if license_node is not None else 'See package notices'
    lines.append(f'{key} — {license_text}')
    target = destination / key.replace('/', '-')
    target.mkdir()
    shutil.copy(next(folder.glob('*.nuspec')), target)
    for item in folder.rglob('*'):
        if item.is_file() and any(word in item.name.lower() for word in ['license', 'licence', 'notice', 'copying']):
            if item.suffix in {'.dll', '.xml'}:
                continue
            output = target / item.relative_to(folder)
            output.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy(item, output)
runtime = cache / 'microsoft.netcore.app.runtime.linux-x64' / '10.0.12'
for filename in ['LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT']:
    shutil.copy(runtime / filename, destination / ('dotnet-runtime-' + filename))
remote = pathlib.Path('Application_Source/Remote')
for item in remote.rglob('*'):
    if item.is_file() and any(word in item.name.lower() for word in ['license', 'licence', 'notice']):
        shutil.copy(item, destination / item.name)
(destination.parent/'THIRD-PARTY-NOTICES.txt').write_text('\n'.join(lines)+'\n')
