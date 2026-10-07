# Image and telescope compatibility

AstroArchive keeps one SHA-256 identity per original file. Existing SQLite records
remain readable: the additional metadata is optional JSON, without a destructive
schema migration. Re-detection is an explicit, reviewed action and preserves user
overrides. Import and ordinary file export retain the original bytes.

## Formats

| Format | Archive / original export | Pixel support | Stacking project |
| --- | --- | --- | --- |
| FITS, FITS.gz | Yes | Primary/IMAGE HDUs, 8/16/32/64-bit integers and 32/64-bit floats; selected HDUs and cube slices | Linear originals; explicitly convert selected containers/slices |
| Tile-compressed FITS (.fz) | Yes | Optional CFITSIO backend | Explicit decompression to derived FITS when backend is available |
| TIFF | Yes | Windows decoder; multiple pages, common mono/RGB integer and float layouts | Confirm linearity; explicit FITS conversion |
| PNG | Yes | Windows decoder; common 8/16-bit mono/RGB layouts | Confirm linearity; explicit FITS conversion |
| JPEG | Yes | Display preview | Original export only |
| XISF | Yes | Attached/base64 UInt8/16/32/64 and Float32/64 images; planar/interleaved; zlib, LZ4, byte shuffle; optional Zstandard | Confirm linearity; explicit FITS conversion |
| SER | Yes | 8/16-bit mono, RGGB/GRBG/GBRG/BGGR and RGB/BGR frames | Export the recording for planetary processing |
| AVI; CR2/CR3, NEF, ARW, DNG | Yes | No decoder bundled | Original export only |

Full numeric decoding for new formats and conversions is bounded to **32 million samples per image**, four channels,
256 TIFF/XISF images and the first 64 FITS HDUs. A sample is one channel value. Uncompressed FITS keeps its existing streamed, sampled preview/analysis and original stacking exports above this allocation limit.
Unlabelled extra FITS axes are slices; RGB axes need an explicit RGB label.
64-bit integer previews can lose precision, so conversion of those samples is
refused; an ordinary single-image FITS original can still be exported unchanged.
XISF base64 inline/embedded layouts also support numeric reading. Complex and subblock layouts are preserved as originals.
Alpha/indexed or other raster encodings outside the scientific layouts are
preview-only. Preview stretches never change science pixels. The existing Bayer colour and stretch renderer remains in use.

The standard installer includes the managed readers and Windows raster integration.
It **does not bundle CFITSIO or Zstandard**. Optional trusted x64 `cfitsio.dll` and
`libzstd.dll`, with their required dependencies, can be installed in
`%LOCALAPPDATA%\AstroArchive\codecs`, outside the versioned app folders. Settings
opens this folder and reports availability. Restart after provisioning. These
optional local components survive app updates; updates do not provision them. Missing codecs disable pixel operations,
while original-file import/export remains available.

## Instruments and metadata

Existing Seestar and DWARF structure/channel rules remain in use. Additional,
versioned recognition profiles accept **explicit header identities** for
Vaonis/Vespera/Stellina and Unistellar/eVscope/eQuinox/Odyssey. These profiles do not
claim coverage of every vendor mirror layout. Acquisition software signatures
identify N.I.N.A., ASIAIR, Ekos/KStars/INDI and SharpCap independently of the device.
Generic FITS/XISF camera and telescope names are retained, without inventing a
smart-telescope model. A known generic camera gets a Primary channel; ambiguous
DWARF channels still require review.

The physical telescope ID stays independent from telescope model, camera model
and camera serial/ID. Metadata also retains optical configuration, readout mode,
ROI, offset, linearity, registration and calibration steps. Provenance records
raw values, normalized values, units and evidence. Conflicting aliases appear in
the metadata report. Explicit milliseconds/Kelvin/Fahrenheit comments are normalized.
`CCDGAIN`/`EGAIN` or an explicitly labelled electrons/ADU value is separate from a
camera gain setting; it is never silently matched as that setting.

FITS capture times follow FITS UTC semantics unless TIMESYS says otherwise. Other
containers require an explicit UTC offset. Filename times retain an unknown
timezone until the user assigns a Windows timezone in Edit metadata. Ambiguous or
invalid daylight-saving times are rejected. Rotation excludes unknown timezones,
SER recordings and unspecialized cube timestamps.

Right-click **Re-detect metadata and review** to see before/after values before
applying them. Check individual fields to apply detected values; unchecked fields keep indexed values. Older saved settings without clear provenance are retained by default and flagged until explicitly resolved in Edit metadata. Recorded user changes retain priority. Choose **Choose HDU / page / frame** to save an image selection; the existing
preview pane and full preview window use that selection for display and analysis. Readers run on demand; filtering the library does not decode pixels.

Adjacent capture-name JSON/text files and recognized `session.json`, `capture.json`,
`metadata.json` and `acquisition.log` sidecars up to 16 MB are preserved with hashes.
These new sidecars are kept as evidence, without guessing their undocumented
schemas. Existing `shotsInfo.json` parsing and preservation remain supported.
Shared sidecars are retained when individual captures are deleted.

## Calibration and derived exports

Automatic matching explains **Accepted**, **Needs review** and **Rejected** decisions.
Known identity, camera channel, dimensions, binning, Bayer pattern and gain must
match. Serial/model, offset, readout and ROI are compared when supplied; one-sided
missing metadata requires review. Unresolved relevant alias conflicts prevent
automatic matching. Flats also require the same filter/night and compatible
optical configuration. Darks need the same exposure and temperatures within 3 C;
dark scaling is not assumed. Calibrated/registered lights receive no extra
calibration. Unknown calibration status remains opt-in.

Dark flats are matched to the **raw flats' exposure**, then exported in exposure-specific
`dark-flats` folders or `masters`. `FlatPreparation` in the manifest maps each set. The manifest distinguishes calibration for lights from calibration
for flats. Workflow notes explain the two stages and warn against subtracting
both a bias and a dark flat from the same flat. Existing raw/master preference
remains in place; stacking and master generation happen in external software.

**Convert supported images to FITS** is explicit in the stacking export dialog.
Derived files contain decoded physical values as Float64 FITS, with normalized
metadata and without applying BSCALE/BZERO twice. Multidimensional WCS is omitted
when extracting cube slices. Manifests record original SHA-256, image key/frame,
conversion description and output SHA-256. Originals remain untouched. Export
preflight rejects unsupported/processed inputs before creating a project;
interrupted exports retain `INCOMPLETE.txt`.

Generated fixtures cover these contracts. Windows release validation includes a separate WIC integration suite. Real telescope fixtures still need independent validation; see `Application_Source/Validation.txt`.
