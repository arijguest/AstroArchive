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
the image; full ESA/Hubble mandatory credits stay visible, with supplementary processing details expandable. Catalogue references
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
Flame Nebula). The pane is divided into Overview, Imaging notes, Parameters,
Location in the sky and Sources, and the object ID appears once: in the heading
when the object has no other name, otherwise in the line beneath it.

## Meteors

The Meteors target (and any alias such as "Meteor shower" or "Shooting star") has its own page with a licensed Perseid photograph (ESO/S. Guisard, CC BY 4.0). Below the overview it shows:

- **Meteor showers through the year**: a ridgeline chart of the 19 annual showers in the International Meteor Organization's working list (`MeteorShowers.cs`), ordered by peak date. Each bar spans the shower's usual dates and its height eases up to the peak; the tallest point scales with the hourly rate. The location comes from the observing town in Settings (`CaptureSite.For(null, settings)`): the latitude sets how high each radiant climbs, so the rate is the shower's zenithal rate times the sine of that height, and a dashed line replaces a shower whose radiant never clears 10 degrees. With no town set the chart shows the raw zenithal rates. A line marks today, a dot marks the peak and a ring means the Moon is more than 60% lit at that year's peak. Hovering a row gives the dates, rate, radiant height and Moon.
- **What colours can mean**: sodium, iron, magnesium, calcium, the air and oxygen trains, and how speed and camera white balance shift them.

In Capture details, a frame whose target is Meteor and whose capture time falls inside a shower's usual dates gains a **Meteor shower** row (for example "Perseids (peak 12 Aug)"), naming up to three showers and ranking by activity and, when a site is known, by what that latitude can see. Showers are matched by date only; the meteor's direction is not compared with a radiant.

## Comets

Every comet target has a page. The information pack holds only name stubs for 21
well-known comets, so `CometInfo.cs` supplies the rest. A target such as
"C/2023 A3 (Tsuchinshan-ATLAS)" or "12P/Pons-Brooks" is looked up by its designation,
and a comet the pack lacks gets a page built from that designation. Overviews for
about 30 well-known comets sit in `TargetDescriptions.txt` under their designation;
any other comet's overview explains what its designation records (type and
discovery half-month). Parameters show the orbit type, perihelion distance, aphelion (periodic comets), period
and eccentricity from the NASA/JPL Small-Body Database, with the perihelion date for
one-off and long-period passages. Imaging notes describe comet-aligned stacking.
Comets have no fixed catalogue sky position. Complete solutions have a native 3D orbit, and they cite JPL
instead of SIMBAD. Individual comet images require independently reviewed file mappings; a comet card shows one as soon
as the pack carries an approved image for it.

## Comets, eclipse pages and Stellarium in 3.2.8

Every comet target sits under one **Comets** entry in Repository and Edited. Selecting
it shows all comet files, each comet's files kept together in the current sort order
(`TargetNavigation.Section` and `GroupByTarget`). The Comets information page has a
general overview, the Hale-Bopp image, imaging notes and a **Your comets** list with
file counts; choosing a comet opens its usual page with an **All comets** button back.
Comets has no rename or target-card actions, like the eclipse sections.

Solar Eclipse and Lunar Eclipse sections open their own pages (overview, fun fact,
imaging notes and an image), with the original capture note beneath the heading. An
older pack without these pages falls back to the Sun or Moon page.

Imaging notes are a full section, not a disclosure. Surface gravity reads, for
example, "2.53 g (24.79 m/s²)" with 1 g = 9.80665 m/s². Pages with one target have
**Open in Stellarium**: deep-sky objects open at their J2000 position framed to about
three times their size; the Sun, Moon and planets are selected by name and
tracked at Stellarium's current time. The eclipse pages and comet pages have no
button. As in the
preview header, a running Stellarium receives the view through Remote Control, an
installed one is started with a script, and otherwise Stellarium Web opens.

### Pack overrides

`scripts/target-info-overrides/overrides.json` and its `assets/` hold reviewed
changes applied on top of the database projection by `scripts/target_info_overrides.py`
(called by `build-target-info.py`, or run directly on an existing pack once). In 3.2.8
they replace the Sun's granulation close-up with a full-disk SDO/HMI white-light image
(public domain), replace the Elephant's Trunk (IC 1396A) image with Astropizza's 2025
Hubble-palette image (CC BY-SA 4.0), add ESA/Hubble's NGC 7640 image (CC BY 4.0) and
change its type to barred spiral galaxy, and add Solar Eclipse (Michael S Adler,
CC BY-SA 4.0), Lunar Eclipse (Alfredo Garcia, Jr, CC BY-SA 2.0) and Comets
(E. Kolmhofer, H. Raab, Johannes-Kepler-Observatory, CC BY-SA 3.0) pages. Each file
page and licence was checked on 13 October 2026; the thumbnails are Commons 960-pixel
previews shown at about 600 pixels and re-encoded as JPEG. Credits use the same
fields and display as the other thumbnails. The pack now has 330 image assignments
over 318 files, and `index.json` records `runtime_overrides`.

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

## Credit audit and imaging notes in 3.2.8

All 326 thumbnail assignments were audited. Commons featured-picture notices and
institutional copyright-template instructions had been flattened into creator or
source/processing attribution text; talk-page controls also appeared in two
creators. The Iris Nebula now credits **Andreigusan** without its Persian Wikipedia
featured-picture notice. M46's creator is **Boris Štromar**, with the existing
permission text retained in the attribution. The Helix creator is the file's stated
credit, **NASA, ESA, and C.R. O'Dell (Vanderbilt University)**; its full collaborator
acknowledgements remain available. Genuine processing credits and acknowledgements
remain intact. Whitespace was normalized in 88 assignment records. Thumbnail bytes,
licences, file/licence links, identities and scientific values are unchanged.

`scripts/target_image_credits.py` applies the same repairs during future database
exports. The pack retains the original SQLite source hash/data version and records
`runtime_credit_revision: 1`, distinguishing the repaired projection from the
upstream database. `python scripts/test-target-image-credits.py` audits every
assignment, checks image hashes and exercises export repairs.

Comet notes now suggest starting with 10–30 second sub-exposures, or 5–10 seconds
for fast motion/long focal lengths, followed by inspection for trailing. They also
give the one-pixel drift limit from image scale and apparent angular motion. Notes
are shown only when guidance exists; empty entries have no disclosure. Engine and
Windows UI checks cover repaired Iris credits and empty imaging-note entries.

Deep-sky pages show an approximate **Distance** in light-years when the catalogue
redshift is large enough to outweigh the galaxy's own motion (recession velocity of
at least 1,000 km/s). `scripts/target_info_distances.py` calculates it for a flat
universe (H0 = 70 km/s/Mpc, matter density 0.3) from the OpenNGC redshift already in
the pack, so no other catalogue is involved. The value carries a "≈" and its tooltip
gives the redshift and assumptions. Nearby galaxies, nebulae and star clusters show
no distance, because the pack holds no licensed measured distance for them.

Below the image, the short image description now comes first and the Credits
disclosure follows, with its arrow centred on the label.

Verified on Windows on 13 October 2026: the credit audit, focused engine and
installer checks, and information-pane rendering in all seven languages passed
in [run 38337070529](https://github.com/arijguest/AstroArchive/actions/runs/38337070529).
