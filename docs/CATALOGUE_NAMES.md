# Catalogue names and identities

`Application_Source/CatalogNames.cs` supplements the embedded OpenNGC common
names without replacing its sky positions or image metadata. Names already
handled by OpenNGC, such as Flaming Star (IC405), Cocoon (IC5146), Toby Jug
(IC2220) and Coddington's Nebula (IC2574), remain available.

## Shared resolution

The bundled catalogue, `CatalogNames.Entries`, explicit whole-region name owners
and saved `Settings.TargetNames` rules feed one identity resolver. Repository,
Import, Edited, search and target choices use that resolver. Targets show the
preferred catalogue ID before the common name, such as **M31 - Andromeda Galaxy**,
with alternate IDs below. Comet designations and recognised NEAT/LINEAR/NEOWISE
names share Solar system; Meteors sit above Other targets.

Target rows omit a trailing parenthesized catalogue ID when it repeats the ID
already shown before the common name: **IC434 - Flame Nebula**. Qualified names
remain available to the identity resolver.

Right-click a target on Repository or Edited to edit its common name and aliases.
Rules persist in `settings.json` and publish as a validated immutable snapshot.
Saving rebuilds search indexes and the Edited fuzzy matcher and regroups existing
entries immediately. Rules cannot steal another object's name, reassign catalogue
numbers or claim an unknown catalogue ID. Original image files are unchanged.

Name matching normalizes accents, punctuation and full catalogue prefixes.
Explicit conflicting IDs, including unknown IDs, prevent an automatic assignment.
Numeric IDs are never fuzzy-matched; Edited alone tolerates small spelling errors
in names. Filename matching recognizes the longest common-name phrase and avoids
mistaking a letter inside it plus a frame counter for a catalogue ID (for example,
`Coma B_001.fit`).

The regression audit checks every supplementary name and alias through exact
lookup, filenames and search, and checks that every displayed catalogue name
resolves back to its own object. Add a new established name once in
`CatalogNames.Entries`, cite its source here and run the audit. Resolve collisions
with an explicit whole-region owner or a qualified name rather than the order in
which entries load.

## Expanded Caldwell and galaxy names

[NASA's Caldwell 23 page](https://science.nasa.gov/mission/hubble/science/explore-the-night-sky/hubble-caldwell-catalog/caldwell-23/)
confirms C23 and NGC891 refer to the same galaxy. The established **Silver Sliver
Galaxy** and **Outer Limits Galaxy** names are documented in the
[NGC891 entry](https://en.wikipedia.org/wiki/NGC_891). Its bundled UGC1831 and
PGC9031 IDs retain the same identity.

The [Caldwell catalogue common-name table](https://en.wikipedia.org/wiki/Caldwell_catalogue)
supplies Bow-Tie Nebula, String of Pearls Galaxy, Polarissima Cluster, Skull
Nebula, Sculptor Pinwheel Galaxy, Owl / E.T. Cluster, Hubble's Variable Nebula,
Tau Canis Majoris Cluster, Eight-Burst / Southern Ring Nebula, Ghost of Jupiter,
Coma B, S Normae Cluster, Great Peacock Globular, Blinking Planetary Nebula,
Superman Galaxy and Blue Snowball Nebula, plus the Greek spelling of h and χ
Persei. Additional documented galaxy and cluster names:

| Object | Name | Source |
| --- | --- | --- |
| NGC2419 | Intergalactic Wanderer | [NGC2419](https://en.wikipedia.org/wiki/NGC_2419) |
| NGC4244 | Silver Needle Galaxy | [NGC4244](https://en.wikipedia.org/wiki/NGC_4244) |
| NGC5907 | Splinter Galaxy | [NGC5907](https://en.wikipedia.org/wiki/NGC_5907) |
| NGC7814 | Little Sombrero Galaxy | [NGC7814](https://en.wikipedia.org/wiki/NGC_7814) |

## Supplementary IC names

| Object | Added or expanded display name | Source |
| --- | --- | --- |
| IC63 | Ghost of Cassiopeia | [ESA/Hubble](https://esahubble.org/news/heic1818/) |
| IC410 | Tadpole Nebula | [NASA APOD](https://apod.nasa.gov/apod/ap240202.html) |
| IC418 | Spirograph Nebula | [ESA/Hubble](https://esahubble.org/images/opo0028a/) |
| IC2118 / NGC1909 | Witch Head Nebula | [SIMBAD identity](https://simbad.cds.unistra.fr/simbad/sim-id?Ident=NGC1909) |
| IC2391 | Omicron Velorum Cluster | OpenNGC `omi Vel Cluster`, expanded for display |
| IC2602 | Southern Pleiades | [Cambridge observing guide](https://www.cambridge.org/turnleft/pages/southern_skies/in_carina_the_southern_pleiades_ic_2602) |
| IC2944 | Running Chicken Nebula | [ESO](https://www.eso.org/public/images/eso1135a/) |
| IC3568 | Lemon Slice Nebula | [Astronomy magazine](https://www.astronomy.com/science/lemon-slice-nebula/) |
| IC4406 | Retina Nebula | [ESA/Hubble](https://esahubble.org/images/opo0214a/) |
| IC4592 | Blue Horsehead Nebula | [NASA APOD](https://apod.nasa.gov/apod/ap210705.html) |
| IC4604 | Rho Ophiuchi Nebula | OpenNGC `rho Oph Nebula`, expanded for display |
| IC4665 | Summer Beehive Cluster | [Sky & Telescope](https://skyandtelescope.org/astronomy-news/explore-caroline-herschels-celestial-showpieces/) |
| IC4756 | Graff's Cluster | [SEDS](https://spider.seds.org/spider/Misc/i4756.html) |

The specific name **Ghost of Cassiopeia** maps to IC63; IC59 retains its own
identity. The unspecific **Ghost Nebula** is not introduced as a unique alias.
IC2118 uses the existing NGC1909 position and canonical object rather than
adding a second sky entry.

Southern Pleiades, Blue Horsehead and Summer Beehive contain shorter names of
different objects. Filename recognition prefers the full phrase within that
span. An independent shorter name elsewhere, or an explicit conflicting
catalogue ID, still makes the filename ambiguous.

AstroArchive uses NGC2237 as the whole Rosette Nebula target. NGC2244 capture
labels (the associated cluster) are assigned to that target for archive grouping.
The general Rosette Nebula name also uses NGC2237; the bundled NGC2238 component
retains its specific catalogue ID and position.

The same explicit ownership rule maps Eagle Nebula to M16, Flame Nebula to
NGC2024 and Eastern Veil / Network Nebula to NGC6992. Component IDs remain
distinct and their displayed names include the ID when necessary. Antennae
Galaxies is not assigned to either component without an ID. The unspecific
Lobster Nebula name also remains ambiguous; NGC6357 uses War and Peace Nebula
and the qualified alias Lobster Nebula (NGC6357).
