# Target information in the preview pane

Repository and Edited show target information when the current target has no
selected files. Selections retained in other target groups stay selected.
Select a target to see its catalogue overview, available offline thumbnail,
measured properties and sources. Deep-sky objects also have an interactive map
centred on their J2000 position. This is a celestial map, without an inferred
observing time or local horizon.

Right-click a target and choose **Show target info** to clear its page's file
selection, including files retained in other targets and selected session groups.
This reveals the Repository preview if it was hidden. Select a file to return to
its image preview. File selections on the other page are independent.

The pane wraps text and uses one vertical scrollbar. At narrow widths, the map
sits below the overview and facts; at 540 pixels or wider it sits alongside them.
Thumbnails retain their aspect ratio and use the available width, with a height
limit based on the pane. Compact creator and licence links sit immediately below
the image; long mandatory credits can be expanded there. Catalogue references
appear once in Sources and catalogue details. Constellations use their full names.
The map uses the same icon reset control as the image preview. The information view
has a minimum column width of 220 pixels and honours the programme's text scale,
theme and keyboard navigation. It does not reserve space in an image preview.

Ambiguous names show explicit object choices. Reading a candidate does not rename
files or change target identification. Unreviewed prose and facts are omitted;
catalogue summaries and cited measurements remain available. Targets without a
checked description or image have a useful text state rather than fabricated
content. Missing/corrupt information packs do not prevent file previews.

## Curated overviews

`Application_Source/TargetDescriptions.txt` (embedded in the application) gives all
110 Messier objects and about 60 popular NGC, IC, Sharpless and southern targets a
written overview and imaging notes, replacing generated catalogue sentences such as
"NGC 7000 is a h ii region in Cyg". Distances are rounded published values. Each
curated card links SIMBAD and, for Messier objects, NASA's Hubble Messier page.
A `Not:` line hides catalogue names the pack wrongly attaches (IC 434 is not the
Flame Nebula). The pane is divided into Overview, Imaging notes, At a glance,
Location in the sky and Sources, and the object ID appears once: in the heading
when the object has no other name, otherwise in the line beneath it.

## Offline data

`Application_Source/TargetInfo.zip` is a runtime projection of AstroArchive database
2.1.0, data version `2026.10.11.2`. It contains 13,524 cards and the 326 approved
thumbnail assignments (314 unique image files), exact alias/ambiguity indexes,
measurement citations, image credits, licence links and catalogue notices.
IC 434 shares the existing credited Horsehead field image with Barnard 33,
without merging their identities or storing another image. The complete pack is
about 48 MiB. Build and installer scripts distribute it
beside the executable as one managed file; Python and an SQLite provider are not
application dependencies.

The reader loads the alias index on its first background lookup and reads small
card buckets and image bytes as requested. A cache retains at most 12 cards with
thumbnails. Obsolete results cannot replace a more recent target or file preview.
Nothing downloads automatically. Source links open only when clicked.

Regenerate from a complete standalone data release, with its matching assets and
notices, using Python 3.10+:

```sh
python scripts/build-target-info.py /path/to/astroarchive-objectdb/data/astroarchive_objects.sqlite
```

The pack records the source database SHA-256 and version. Sorted entries and fixed
timestamps make a repeated export byte-for-byte reproducible. The database remains
the source of truth; the projection changes presentation, not scientific identities.

## Checks

On Windows, from the repository root:

```powershell
.\Application_Source\build.ps1
.\Application_Source\test.ps1 -TargetInfoOnly
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\target-info-ui --info-only --no-updates
```

The dedicated Windows workflow also runs these checks and saves screenshots.
UI coverage includes Repository/Edited, retained selections, restored raster
previews, cancelled target loads, southern sky markers, 240/570-pixel pane widths,
650-pixel window heights, both themes, 100/150% text and expanded sources/credits.
The broader target suite also includes the information engine checks. All seven
interface languages are checked with unchanged catalogue prose and attribution.

Local verification on 12 October 2026: compilation against .NET Framework 4.8
references and 90 focused target/metadata/information/language checks passed.
Windows checks cover all seven interface languages and installed-app rendering.
