# Hybrid information and native 3D orbits in 3.2.9

Choose Hybrid or Offline during interactive setup or at an unrecorded first
launch. Pending and Offline block automatic internet requests. The per-user
`connectivity.json` handoff is atomic and merged into app preferences without
replacing unrelated settings. A running application watches external setup choices.
The existing `UseOnline` preference remains specific to plate solving.

Preferences > General > Online access exposes the mode, downloaded-information
usage, a 10–500 MiB cap (100 MiB by default), and clear action. Switching Offline
cancels requests and update downloads and suspends automatic release checks. Hybrid
honours the separately saved automatic-update preference. LAN telescope workflows
and local Stellarium stay available; browser/online-solver actions require an
explicit reconnection through Preferences.

## Information and rights

The deterministic ZIP keeps 13,528 identities and 66,171 alias-index entries,
including ambiguity and nonexistent-designation indexes. All Messier and 207
curated descriptions remain available. Essential photography is reduced to
640 pixels; shared source fields and image bytes are deduplicated. The shipped
pack measures about 13.54 MiB, compared with 48.22 MiB at the reviewed baseline.
`essentials.json` records selection, hashes, original credits and modifications.
Catalogue data and OpenNGC adaptations retain CC BY-SA 4.0 and acknowledgements.

Local/basic cards render first. Missing/stale enrichment uses canonical object
IDs, deduplicated bounded requests, cancellation and selection generations.
Reviewed prose, corrections, identities and measurements retain precedence.
Network failures leave local content visible. Enrichment restores scroll position,
expanded disclosures and the orbit camera.

JPL SBDB uses full precision, exact designations/SPK identities, fragment checking,
API-version validation, one request at a time and an identifying User-Agent.
Physical fields are deliberately limited to cited numeric diameter, albedo and
rotation period with their reported units/uncertainties; missing methods/epochs
are not inferred. Wikidata uses an exact P528 astronomical identifier and a unique
entity; MediaWiki verifies the article's `wikibase_item`, revision and site licence,
with English fallback. Commons uses reviewed file/subject associations, current
per-file licence checks and full credits. P18/search candidates are never selected
automatically. SIMBAD/VizieR/NED stay disabled pending dataset-specific rights.

Provider capability decisions and item attribution are distinct from the
first-party licence. JPL numerical facts have a documented factual-data reuse
basis; API fair-use terms are not a blanket CC0/redistribution grant. Wikipedia
adaptations retain CC BY-SA 4.0, revisions/history and modifications. Unknown
rights deny the capability. Full ESA/Hubble mandatory credits stay visible.

The per-user information cache is separate from installations and archives.
Revision/schema/policy keys, atomic blobs/pointers, LRU eviction and reference-counted
active-card protection bound disk use. Comet elements default to seven days,
introductions to thirty, and independent media records to ninety. Corruption falls
back to local content; clear invalidates late writes and cancels pending requests.
Misses/failures are briefly suppressed and Retry-After pauses the provider.

Download hosts are exact approved HTTPS names; redirects, ports and private DNS
destinations are checked separately from intentional LAN integrations. JSON depth,
lengths/counts, finite orbital values, units, downloaded bytes and raster dimensions
are bounded. Remote text is plain WPF text; remote raster decoding runs off the UI
thread. APIs cannot rename targets, write archive metadata or execute commands.
No owned manifests or viewer code are downloaded.

## Orbit model and interaction

The native WPF renderer projects physical 3D coordinates in the ecliptic frame and
draws in the sky globe's visual language, from the same palette. The backdrop is the
sky globe's constellation figures turned into that frame, so it rotates with the
camera; the reference plane is a faint disc with distance rings (round numbers of AU,
or kilometres at the Earth–Moon scale) and 30° spokes. The target orbit is solid above
the ecliptic and dashed below, fading with depth in a few batched strokes; ascending
(filled) and descending (open) nodes, perihelion/aphelion or perigee/apogee, a filled
direction arrow, a ringed marker and subdued tinted context orbits support inspection.
Labels are name pills placed by priority so the target and Sun win and the rest drop
out rather than overlap. Enlarged body markers are explicitly labelled and the scale
bar and axis triad stay put. There is one information scrollbar. Wheel zoom requires
focus/Ctrl; Shift-drag pans, drag/arrows rotate, +/− and pinch zoom, Home or a
double-click resets. Reset, Top view, Orbit plane, full fit, inner-system fit and
expansion are icon buttons over the view and chips below it; the camera eases to each
view in a fraction of a second, or jumps when Reduce motion or Windows' animation
setting is off. Orbit plane is a different frame from yaw and tilt, so it never
blends.

Ellipses close; parabolic/hyperbolic paths remain open with continuation arrows.
Bounded adaptive paths handle extreme extents. Stable Kepler/Barker equations
retain near-parabolic eccentricities and retrograde orientation. Geometry runs
off the UI thread; no API request runs per frame. The only timers are the camera
glide (the frame clock, only while it moves) and optional playback, which starts
paused, steps at four frames a second under Reduce motion, and stops when the pane
is hidden, the card changes or the user scrubs or sets a date.

Where dated positions are supported (planets and the solar-system overview) a
timeline spans 1972–2026, with Today, a UTC date, Orbit epoch and Play/Pause. Playback
runs about twelve seconds per revolution, by elapsed time rather than frame count.
The planets are drawn at the marker date from the same JPL Table 1 model, so a
comet or the Moon is seen among them; with nothing chosen yet, a dated body starts
at today.

Planets use the existing JPL Table 1 numerical approximation (1800–2050), with
Earth explicitly representing the Earth–Moon barycentre. The Sun presents a
solar-system overview. Fourteen complete audited small-body/Moon snapshots retain
source, solution, epoch, J2000 ecliptic frame, TDB standard and uncertainties. The
Moon is centred on Earth and preserves Horizons' Earth–Moon Keplerian GM. Other
satellites show unavailable geometry until complete reviewed data exist.

Snapshot markers use the source epoch; absence of a supported propagation range
disables dated controls. Supported planet markers use explicit approximate
UTC→TT→TDB conversion. UTC dated controls stop at 2026 because future leap seconds
are unknown. These are approximate orbits, not precision ephemerides/pointing.
Hyperbolic eccentricity alone does not establish interstellar origin.

## Packaging and validation

The release convention gives application 3.2.9 and package 3.2.9.1. One installer,
one checksum and a concise schema-1 feed replace future Offline-Setup output.
Historical releases stay intact. The reviewed updater already understands concise
assets; very old 1.2.0 launchers need a manual bridge installation.

Engine checks cover scientific reference fixtures, all conics, frames/time,
fragment identity, corrupt cache, rights, URL/raster rejection, offline gating,
deduplication, rate-limit suppression and active-card LRU. Installer checks cover
atomic choice migration and cancellation alongside existing integrity/rollback.
`hybrid.yml` runs Windows engines and seven-language light/dark narrow/wide WPF
checks, source-refresh camera/scroll retention, wheel focus and setup screenshots.

Before publication, review screenshots and manually exercise 100/125/150/200%
per-monitor DPI, text scaling/high contrast, touch, install/update/repair/rollback,
checked/unchecked opening, launch failure and actual telescope devices. Linux
cross-compilation and engine checks do not establish Windows visual behaviour.
