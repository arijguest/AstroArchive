# Image quality scoring

AstroArchive can score how sharp each light frame is compared with similar frames,
sort subs within a session by that score, and filter the library by quality rank.
Scores only suggest which frames are weaker. They never set the rejected state
and never delete or move a file.

## What is scored

| Frame | Scored? | How |
| --- | --- | --- |
| Deep-sky light frames (FITS or XISF, linear data) | Yes | Star shape and noise |
| Planetary, lunar and solar stills | Yes | Disc sharpness (separate method), plus an exposure fit column |
| Stacks (FITS or XISF, linear data) | Yes | Same methods, compared only with other stacks of the same target, telescope, camera and filter |
| Calibration frames, recordings | No | Shown as `-` with a reason in the tooltip |
| JPEG, GIF, BMP and other non-linear data, meteor captures, images under 96 px or beyond the decoder limit | No | Same |

Exposure fit (Under, Good, Over) is only computed for planetary, lunar and solar
stills. It is a separate column and is not part of the quality score.

## Measurements

Measurements are taken once per file content and stored in the repository database
(`quality` table, keyed by the content hash and the measurement version). Renamed or
moved files reuse them; edited files are measured again. Scores are recomputed from
the stored measurements every time the library loads and are never saved with the
capture record. The table travels with the repository's portable index, so scores
survive program updates and moving the repository. When an update improves the
measurement, existing scores stay in place and are refreshed the next time Analyse
image quality runs. Original image files are never rewritten to hold a score: that
would change their content hash and checksums.

*Stars.* Noise comes from 32 px tiles. Stars are found on a binned plane (4x for
large frames) using a tile background and a 5 sigma threshold; stars at 10 sigma or
more are counted, and the 100 best isolated, unsaturated stars are fitted at full
resolution. Each fit gives a half-flux diameter, a Gaussian-windowed second-moment
FWHM and eccentricity. One-shot-colour frames use a 2x2 mean so the Bayer pattern does
not add noise.

*Discs.* An Otsu threshold finds the limb, the 10 to 90 percent edge width is
measured along the boundary normals, and the ratio of fine to coarse gradient energy
(with background noise energy removed) gives a detail ratio. Saturation or black
clipping beyond 2 percent of the disc caps the score at 40.

A file that cannot be measured because of its content (an unsupported layout, damaged
data, or too large for the memory available even when measured on its own) is set aside:
it is stored with that status and skipped on later runs until Re-measure image quality
is used. A file that cannot be opened is tried again on the next run. The run report
lists both, file by file, with the reason.

## Scoring

Each measurement is compared with the others in its cohort (same group and session by
default; same target when "Compare deep-sky frames across the whole target" is on;
disc frames always compare within the session and target; stacks compare only with
other stacks of the same target, telescope, camera and filter). Robust z-scores (median
and MAD with floors) pass through a logistic map to 0 to 100 and are combined with a
weighted geometric mean.

| Frame | Weights |
| --- | --- |
| Deep-sky | FWHM 0.35, SNR 0.25, star count 0.20, eccentricity 0.20 |
| Disc | limb edge width 0.40, detail ratio 0.30, SNR 0.30 |

Cohorts under 8 frames use fixed reference values instead of relative ranking, so a
uniformly bad night is still flagged. Flags (Clouded, Soft, Trailed, Bright sky,
Clipped, Black clipped, Under or Over exposed) are absolute and explained in the tooltip.

Exposure fit compares the brightest pixels of the disc with the sensor ceiling (255, 1023,
4095, 16383 or 65535), inferred from the brightest frame in the same comparison group. If a
whole session is uniformly under-exposed it can therefore read as Good; clipping is always
measured directly and still flags Over.

## Where it appears

* QUALITY column (sortable) and an optional EXPOSURE FIT column in the repository table.
* Filters: Quality rank (Top 10 percent, Top 25 percent, Top half, Bottom half, Weakest 25 percent,
  Has quality flags, Not scored), Exposure fit, and a Quality score range.
* Repository > Review and analysis > Analyse image quality / Re-measure image quality.
* Settings > General > Image quality: measure after each import, and the deep-sky comparison scope.

Saved table layouts gain the QUALITY column once; hide it again from the Columns menu and it
stays hidden. The EXPOSURE FIT column is available from the same menu.

## Tests

`QualityEngineTests.cs` measures synthetic star fields and discs with known width, noise, blur and
clipping; `QualityTests.cs` covers eligibility, scoring in the library, filters, ordering, stored
measurements and the column layout. Run them alone with `AstroArchiveTests.exe <folder> --quality-only`.

