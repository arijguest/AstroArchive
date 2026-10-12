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
