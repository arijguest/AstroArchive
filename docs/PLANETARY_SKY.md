# Planetary positions in the capture sky map

The map previously resolved the target only from measured frame coordinates or
the fixed deep-sky catalogue. A recording of the Moon or Jupiter with a valid
capture time but no RA/Dec consequently had no target position. The separate
Moon phase overlay did not fix the target marker or the camera's initial view.

`CaptureSky.Resolve` now falls back to an offline ephemeris for the Sun, Moon,
Mercury, Venus, Mars, Jupiter, Saturn, Uranus and Neptune. It uses the recorded
capture time, with the capture location or saved observing location when available.
Measured frame coordinates and plate solutions retain priority. The calculation
does not depend on file format or video duration. Stacks use their recorded
timestamp, without treating integration time as a track through the sky.

The Moon uses the existing Meeus model and the same observer-corrected direction
as its phase overlay. The Sun uses the existing solar model. The seven planets
use [JPL's approximate Keplerian model, Table 1](https://ssd.jpl.nasa.gov/planets/approx_pos.html),
including planetary light-time and observer parallax. Its range is 1800–2050 and
its positions are explicitly approximate. These positions serve the rounded sky
globe; they are not telescope-pointing or plate-solving coordinates. No new
runtime dependency, download or catalogue package is required.

The marker, camera, elevation and Moon separation share a direction in equatorial
coordinates of the capture date. Calculated coordinates are not precessed twice;
J2000 catalogue/frame coordinates continue to be precessed once. Unsupported
bodies (including Pluto, other moons and comets) require measured coordinates;
the app does not substitute a different body.

## Capture times and file formats

AVI and other recordings pass their retained frame metadata to the same map
resolver as still images. AVI container duration is a length, not a capture time.
Unzoned filename times use the saved observing town/city timezone when a capture
timezone is absent. Explicit UTC, clock offsets, stored header clocks and capture
timezones have priority. The city fallback applies historical daylight-saving rules
at the capture date and leaves saved frame metadata unchanged. No additional
label is shown. Date-only, invalid or ambiguous daylight-saving times remain
unresolved; an explicit capture timezone/offset can still be set in metadata. The app
does not substitute file creation/modification time, the current clock or a session
folder timestamp. A supplied UTC timestamp or explicit offset works regardless of
format. Explicit `TIMESYS=UTC` is now honoured in non-FITS headers, too; previously
an unzoned `DATE-OBS` with that declared standard could lose its UTC interpretation.
Unknown or unsupported time standards remain unresolved. Older indexed non-FITS
records can recover a declared UTC clock from their retained image headers, without
reopening the file or changing saved metadata. User clock edits take precedence.

Tests cover AVI, MP4, MOV, M4V, WMV, MKV, SER, FITS, XISF, TIFF, PNG, JPEG and DNG
frame/header clocks. Physical AVI and SER fixtures go through the scanner, timezone
editing and/or retained metadata. SER uses its UTC capture header. This change
does not introduce parsing of arbitrary acquisition logs or treating generic video
creation metadata as a capture time.

Capture details shows “Not out (below the horizon)” when the Moon was below the
local horizon at acquisition. Its phase glyph, illumination and separation facts
are then omitted. Without an observing site, phase information remains available
but the app does not assume the Moon was visible.

## Independent reference checks

The planet tests compare against [JPL Horizons](https://ssd.jpl.nasa.gov/horizons/)
API results retrieved on 12 October 2026. Requests used `CENTER='500@399'`,
`EPHEM_TYPE='OBSERVER'`, `QUANTITIES='1,2'`, `ANG_FORMAT='DEG'`, `CSV_FORMAT='YES'`,
and one-minute steps from 00:00 to 00:01 UTC. Quantity 1 gives astrometric ICRF
coordinates; quantity 2 gives apparent coordinates of date.

| Body | Horizons command | 10 Oct 2024, RA / Dec (ICRF degrees) | 12 Oct 2026, RA / Dec (ICRF degrees) |
| --- | --- | --- | --- |
| Mercury | 199 | 201.85129 / −8.76835 | 219.95550 / −18.56809 |
| Venus | 299 | 227.55234 / −18.49208 | 211.64240 / −20.86597 |
| Mars | 499 | 110.75012 / 22.82596 | 130.33367 / 19.53798 |
| Jupiter | 599 | 80.24644 / 22.41411 | 143.69073 / 15.05857 |
| Saturn | 699 | 345.59690 / −8.51915 | 10.56246 / 1.59499 |
| Uranus | 799 | 54.07170 / 19.06763 | 63.00007 / 20.96271 |
| Neptune | 899 | 358.37190 / −2.15090 | 2.56646 / −0.43535 |

The checks allow 0.25° for Jupiter/Saturn and 0.06° for the other planets. These
are regression thresholds at the sampled dates, not an accuracy guarantee over
the complete model range. A separate Moon check uses Horizons apparent coordinates
210.67964°, −17.43228° on 12 October 2026. Existing Meeus worked-example, phase,
observer-parallax, southern/polar horizon and unknown-clock tests also run.

On Windows:

```powershell
.\Application_Source\test.ps1 -SkyOnly
.\Application_Source\build.ps1
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\planetary-sky --sky-only --no-updates
```

The dedicated Windows workflow saves marker/phase screenshots in both themes.
Local Linux verification can compile the WPF code against .NET Framework 4.8
references and run the engine checks, but cannot execute WPF rendering.

The offline city timezone index covers every existing city choice and adds about
740 KiB. It maps GeoNames IDs to IANA zones and Unicode CLDR Windows IDs so the
same rules resolve on .NET Framework/Windows and on the Linux test runtime.
It avoids country-wide or longitude-only timezone guesses. Build it with
`scripts/build-city-timezones.py`; its source hashes and licence notices accompany
the resource. The original city catalogue and translations are unchanged.

Verified locally on 12 October 2026: all 33 focused sky/video checks passed under
Mono, including independent JPL references, AVI/SER scanning, saved header clock
recovery, observing-city timezone fallback, summer/winter DST, fractional offsets,
explicit-timezone priority and absent/invalid clocks. The application compiled
against .NET Framework 4.8 references. The city timezone index covers all 236,003
existing city choices and rebuilds byte-for-byte. Windows visual checks are
provided in the workflow and remain pending in this Linux workspace.

## Eclipses

`Eclipses.cs` derives eclipse geometry from the same bundled Meeus Moon and Sun
models; nothing is downloaded and no eclipse table is shipped.

- **Solar:** the Moon and Sun are placed as seen from the capture or saved observing
  location (lunar parallax reaches about a degree, so a site is required). Apparent
  radii come from the topocentric distances. Obscuration is the overlapping area of
  the two discs as a fraction of the Sun's disc; magnitude is the fraction of the
  Sun's diameter covered, or the ratio of diameters once the eclipse is total or
  annular, matching NASA's convention.
- **Lunar:** the Moon's geocentric distance from the antisolar point is compared with
  Earth's umbral and penumbral radii using Danjon's enlargement
  (1.01 × lunar parallax ± solar semi-diameter + solar parallax). Coverage is the
  fraction of the Moon's disc inside the umbra; umbral and penumbral magnitudes are
  reported. No site is needed.

Checks against NASA's Five Millennium Canon greatest-eclipse values (2017, 2022,
2023 annular and 2024 total solar; 2022, 2023 partial/penumbral and 2025 lunar)
agree within 0.005 magnitude for solar and 0.01 for lunar eclipses. A capture's
eclipse is only as good as its clock: a timezone error moves the Moon by about
half a degree per hour.

Sun and Moon captures taken during an eclipse list under **Solar Eclipse** or
**Lunar Eclipse** in the target list. This is a display grouping only; the stored
target, folders and file names are unchanged. The sky map adds a close-up of
both discs to scale; clicking it, the Moon, the Sun or the target opens Capture
details.

Capture details open with an eclipse card: a cartoon of the discs at the capture
time (true sizes and separation, fixed direction), the coverage at capture, and a
curve of coverage across the whole event with the capture marked. Below it are the
maximum and its time, when the eclipse began and ended, its duration, and the
partial phase and totality (or annularity) where they occur. `Eclipses.Event`
finds these from the same models: it steps out from the capture time to the outer
contacts, bisects them to a tenth of a second, finds greatest eclipse by golden-
section search on the disc separation and bisects inner contacts from it. Solar
contacts are local to the site (first to last contact); lunar contacts are the
penumbral ones, with umbral and total phases listed separately. Times use the
capture's timezone, then the observing town's, then UTC. Against NASA's 2025 March
14 and 2023 October 28 lunar contacts the results agree within 90 seconds (the
models run on UT without a ΔT correction, so lunar times are about a minute late);
2024 April 8 totality at greatest eclipse comes out at 4 min 30 s against 4 min 28 s.

Clicking a constellation's lines names it with a pill over its label point. The
name stays while the map is rotated and is dismissed by clicking the
constellation, the name or empty sky, pressing Escape, or selecting another
capture. Constellation names are the IAU Latin names; tables may add translations.

## Daytime maps and Moon stacks

Captures of the Sun, or taken while the Sun was up, use a daytime map with the Sun
drawn beneath the Moon. Lunar, solar and planetary stacks often record a blank
(0°, 0°) mount pointing; that is ignored in favour of the calculated position.
A solar or lunar capture mode names the body when the target is unknown. Stacks
without a usable clock use the median capture time of their subs in the same
session or night. A body calculated below the horizon keeps its map and points to
the capture timezone instead of disappearing.
