# Image and metadata support

Import and ordinary export preserve original files. Preview support and
scientific export eligibility are shown separately.

## Formats

| Format | Preview | Processing export |
| --- | --- | --- |
| FITS / gzip FITS | Supported image HDUs, RGB/Bayer data and selected cube slices | Eligible linear originals; explicit conversion for selected slices/containers |
| Tile-compressed FITS (`.fz`) | Optional CFITSIO codec | Explicit decompression to derived FITS |
| TIFF / PNG | Windows decoder; supported mono/RGB integer and float layouts, including TIFF pages | Confirm linearity and convert eligible images to FITS |
| XISF | Supported integer/float, planar/interleaved, attached/base64 and zlib/LZ4/shuffled layouts; optional Zstandard | Confirm linearity and convert eligible images to FITS |
| JPEG / GIF | Still or animated display | Original-file export |
| SER | 8/16-bit mono, common Bayer and RGB/BGR recordings; playback and frame selection | Original recording for planetary processing |
| AVI / MP4 / MOV / M4V / WMV / MKV | Codecs installed in Windows | Original-file export |
| CR2 / CR3 / NEF / ARW / DNG | External preview needs a compatible Windows image codec | Original-file export |

Windows image formats outside this import list may still open as external previews.
Unsupported complex/subblock XISF layouts and non-scientific raster layouts remain
original-file exports. RGB FITS axes need explicit RGB metadata; other extra axes
are treated as slices. Conversion of 64-bit integers is refused to avoid precision loss.

Numeric decoding/conversion is limited to 32 million channel samples per image,
four channels, 256 TIFF/XISF images and the first 64 FITS HDUs. Uncompressed FITS
can use sampled previews and original exports above the numeric allocation limit.
Inline still previews use sampled buffers, normally capped at 1,400 pixels on
the longest edge. Large still/SER popups decode native pixels separately, within
decoder limits. Full-size popup buffers do not replace the sampled sidebar cache.
Source files are unchanged.
GIF preview supports up to 4,096 frames and a 32-million-pixel canvas.

## Optional codecs

CFITSIO and Zstandard are not included in the installer. **Settings → General → Image compatibility**
shows availability and opens `%LOCALAPPDATA%\AstroArchive\codecs`.
Install compatible x64 `cfitsio.dll` or `libzstd.dll` and their dependencies there,
then restart AstroArchive. These local codecs survive updates. Missing codecs
leave original-file import/export available. Windows video and RAW codecs are
installed separately through their providers.

## Acquisition metadata

Seestar/DWARF folders and explicit instrument headers support classification.
Vaonis/Unistellar identities and N.I.N.A., ASIAIR, Ekos/KStars/INDI and SharpCap
software signatures are recognised where supplied. Check uncertain device or
camera labels; a model name does not identify a physical telescope.

Right-click **Edit metadata** to correct labels, or **Re-detect metadata and review**
to compare detected values before applying selected fields. Recorded user edits
retain priority. **Choose HDU / page / frame** selects an image for supported
pixel operations. Related capture/session sidecars are preserved as evidence.

Times with known UTC information can support analysis. Filename times retain an
unknown timezone until assigned in Edit metadata. Unknown exposure, gain, coordinates
and processing state remain explicit; conflicting values need review.

For Edited images, explicit filename products such as `120x60s` establish sub count
and total integration. `30s40` means sub exposure/gain, and DWARF's `stacked-16`
is bit depth. When a DWARF name omits the sub exposure, the session's `shotsInfo.json`
supplies it (`exp`, seconds per sub); its `shotsStacked` must equal total ÷ sub, or the
file is treated as another stack and no count is guessed. Unspecified `EXPTIME` is not assumed to be per-sub or total.
Seestar's `Stacked_36_NGC 7000_5.0s_LP_20260906-...` explicitly supplies 36
subs at 5 seconds each. Repository exposure is therefore 180 seconds, and Edited
metadata retains both the count and per-sub duration. Explicit header counts/totals
and user corrections take precedence. Saved Seestar stack records and their import
history are repaired when the repository opens, using cached metadata only.
GIFs inherit missing acquisition details only from a uniquely matching nearby still
image. See [the user guide](../Application_Source/Quick_Start.txt) for matching names.

Repository stacks show confirmed combined-frame counts, such as **Stack (1445)**;
the filter remains **Stack**. Condensed subs show per-sub duration separately
from total integration. Mixed or missing durations remain explicit.

## Calibration and conversion

Calibration candidates are **Accepted**, **Partial match**, **Needs review** or **Rejected**.
Any value recorded on both the light and the calibration must agree: telescope, make,
camera channel, dimensions, Bayer pattern, binning, gain, offset, camera ID/model,
readout, ROI and (for flats) filter and optical configuration. Telescope, binning and
gain must be known; darks also need a known exposure and a temperature within 3°C.
Descriptive values missing on only one file (common in masters from Siril, PixInsight
or DSS) make a **Partial match**, used only when no fully accepted calibration exists;
the export lists each one as a warning. Lights of an identified target other than the
Milky Way count as the telephoto camera.

Each export group gets one set per calibration type: darks with the closest temperature,
then the nearest night; flats and biases from the nearest night. Raw frames from one
night form a set and win a tie with a master. Flats come from the same night; Seestar and
DWARF (fixed optics), or rigs recording the same optical configuration, may use the
nearest night within 30 days. XISF and 16/32-bit TIFF calibration masters are treated as
linear unless marked otherwise. Conflicting evidence requires review. Processed lights
receive no extra calibration; unknown calibration state is opt-in.

Dark flats match raw-flat exposure in separate sets;
do not subtract both a bias and a dark flat from the same flat.

**Convert supported images to FITS** is an explicit stacking-export option for
eligible linear images. Confirm linearity in Edit metadata. Conversion records
physical values without applying a preview stretch. **Add Metadata** optionally
records source, image selection and output checksums in a manifest, alongside
session metadata and readme/workflow notes. It is off by default, as is
**Create new folder**. Stacks copy directly to the destination; subs retain
compatible input folders. Originals remain intact. Cancellation retains verified
copies and removes unfinished temporary files; wait for completion before processing a full set.
