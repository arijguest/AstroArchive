# Processing in external software

Select Repository files and choose **Export → Open in app…**. The same option is
available on the file context menu and in Edited, and opens the Export window
with **Open in app** chosen. One destination chooser covers all eight applications. Unsupported choices are disabled and explain their format
requirements on hover. Applications are installed separately.

Repository images receive verified working copies in Edited before being opened.
Already Edited images open from their existing working paths. Save processed
outputs alongside the working copy, then refresh Edited. Acquisition subframes and
recordings are copied to an export folder outside the repository. Choose subframes
separately from finished stacks; combining a stack with its constituent subs would
count data twice. Rejected/failed inputs remain available through **Export → Files**.

## Supported handoffs on Windows

| Destination | Images / inputs offered | Handoff and remaining action |
| --- | --- | --- |
| PixInsight | FITS, TIFF, XISF, PNG, JPEG images; FITS/TIFF/XISF/RAW subframe folders | A generated PJSR script opens and shows image windows. For a folder, starts PixInsight; choose the exported inputs in WBPP. |
| Siril | One FITS/TIFF/PNG/JPEG/RAW image; folders of these, SER or AVI | Image is a positional file argument. Folder uses `--directory`; convert/load the sequence in Siril. |
| DeepSkyStacker | FITS, TIFF or camera RAW subframes | Loads a generated DSS text file list with light/dark/flat/dark-flat/bias roles. Each incompatible capture group gets its own list. A group chooser appears when there is more than one. Register/stack in DSS. |
| GIMP | FITS, TIFF, PNG, JPEG, BMP, GIF working images | Passes the absolute filenames to the GUI executable. FITS requires the installed FITS plug-in. No automatic RAW loader is assumed. |
| Photoshop | TIFF, PNG, JPEG, BMP, GIF working images | Uses Adobe's Windows COM `Open` interface. Windows automation must be registered for the configured installation. Convert FITS/XISF separately; no silent conversion is performed. |
| AutoStakkert! 4 | SER, AVI, TIFF/PNG/BMP subframe sequences | Starts AS!4 and offers the input folder. Load inputs in AS!4. AVI must use an accepted codec (normally uncompressed); image precision is subject to the receiving build. No undocumented command-line imports are used. |
| AstroWizard | One uncompressed FITS, TIFF or XISF image | Passes one absolute working-image filename as the first argument. |
| Stacking Wizard | FITS or camera RAW subframe folders | Starts the app and offers the input folder. Choose the folder in Stacking Wizard. RAW/compressed FITS need a recent build. No verified public folder-import argument is assumed. |

Format lists are deliberately conservative and are not complete lists of every
receiving application's abilities. A file extension cannot establish codec,
precision, Bayer or RAW-camera support. Export preserves original bytes. Use
**Export → Stacking folder → More options** for explicit, supported scientific
FITS conversion. Gzip/tile-compressed FITS are excluded from direct AstroWizard and
GIMP image handoffs; create an uncompressed copy first.

## Defaults and application locations

**Settings → Export** includes a default export folder and two expandable sections:

- **Default applications:** choose a destination for FITS, TIFF, XISF, PNG, JPEG, BMP, GIF, SER,
  AVI and camera RAW, plus an optional subframe-folder preference. Aliases such as
  `.fit`/`.fits` and `.tif`/`.tiff` share a preference. Defaults preselect the chooser;
  exporting still requires confirmation. Incompatible defaults are ignored. Mixed
  selections need a common compatible default, otherwise the chooser asks.
- **Application locations:** select an app and browse to its GUI executable, find it
  automatically, or clear the override to use automatic detection. Cancellation
  discards pending edits. An unavailable old path does not block unrelated changes.

Automatic detection checks Windows App Paths and uninstall install locations in
both registry views, PATH, Program Files, Local AppData Programs, and common
Desktop/Downloads application folders. Searches are bounded; renamed, deeply nested
or relocated portable folders may require Browse. A configured path takes priority.
If it is missing, or detection cannot find an app, Open in app asks for the GUI
executable before any copies are created. Earlier Siril/StackingWizard preferences
are retained. Installers and known headless executable variants are refused.

## Verified exports and failures

Matching calibrations are optional and use AstroArchive's existing identity,
geometry and acquisition checks. Unknown calibration state requires explicit
inclusion. Already calibrated/registered subs receive no extra calibrations. DSS
lists use the actual verified export paths, including collision suffixes, and work
without optional metadata. Calibration bytes are retained; users review stacking
settings in the receiving application.

Files are copied and verified before launch. Cancellation prevents subsequent
launches. Export collisions preserve existing files. A launch failure retains the
completed working copies/folder and offers the location and app settings. A started
process is not confirmation that the receiver has imported its documents; decoding
and import errors remain visible in that app.

## The Export window

**Export** opens one window for every export. Its header shows how many files are
selected (or in view) and their targets. The left list chooses **Files**,
**Stacking folder**, **Open in app**, **AstroBin CSV** or **Catalogue CSV**; each
shows only its own settings. Folder exports share one destination at the bottom,
with an optional new folder. The primary button names the action. Right-click
**Export** items and the Export menu open the same window with the relevant option
chosen; the Export button and Ctrl+E reopen the option used last. Manual working
copies are on the file context menu. Optional metadata and advanced stacking
options expand under More options. Export completion keeps Open folder and Close.

In **Edited**, right-click **Export → Files…** to copy selected working images.
Selection can span targets. Copies keep their format, are verified, and receive
suffixes for filename collisions. **Delete files…** removes only selected Edited
copies after confirmation; source originals and archived captures remain.

## Open an exported stack in Siril

The Files and Stacking folder options offer **Open with… after export**
for a single eligible FITS stack. Choose **Siril** to open the verified exported
copy, including any filename collision suffix, in its actual working folder.
Subs-only exports and multiple stacks disable this choice. Conversion stays opt-in.
Set the default export folder and Siril location in **Settings → Export**; select
Siril under **Application locations**. Cancellation or an export failure prevents
launch. A launch failure keeps the completed export and shows its folder.

## AstroBin acquisition CSV

**AstroBin CSV** builds the table AstroBin imports under an image's acquisition
details, using its documented
[CSV columns](https://welcome.astrobin.com/importing-acquisitions-from-csv).
Preview it, copy it to the clipboard or save it without exporting images.
**Stacking folder** adds `astrobin_acquisition.csv` and
`astrobin_acquisition_notes.txt` by default (per target when several are exported);
clear **Add AstroBin acquisition CSV** to omit them.

- Values come from the archived originals' metadata, so details lost in processed
  files are kept. Rows combine matching subs by date, equipment, filter, exposure,
  binning, gain and ISO; sensorCooling is the rounded mean sensor temperature.
- Calibration frames are never integration. Darks, flats, flat darks and bias count
  the matching archive frames (or those copied by the stacking folder), each once.
  A master counts the frames it records combining; otherwise the count stays blank.
- Duplicate exposures (same file, or same camera at the same recorded instant) and
  rejected files are excluded. A stack is skipped when subs from its session or date
  are included; overlapping live-stack snapshots count once, using the largest.
  A stack's duration is its recorded total divided by its recorded sub count.
- Each target gets its own CSV, as each needs its own AstroBin image. Planetary
  recordings use the video columns with their recorded frame counts.
- Missing values are left blank and listed under **Check before uploading**: no
  date, exposure, sub count, local start time, Bortle, SQM, seeing or filter ID is
  estimated. AstroBin needs its numeric filter ID: paste the filter's AstroBin page
  address or number under **AstroBin filter IDs**; IDs are remembered.
- Files use UTF-8 without a byte-order mark, a header line and no spaces around
  commas. Columns without values are omitted; number and duration are always present.

## Integration references

Contracts checked against current documentation/source on 8 October 2026. Automated
tests exercise compatibility, defaults, serialization, launch argument construction,
verified exports and DSS lists; real receiver installations still need manual checks.

- [PixInsight development releases](https://pixinsight.net/dev/index.php?page=1) and
  [AutoIntegrate developer's command-line/PJSR integration](https://ruuth.online/AutoIntegrateSetup.html).
- [Siril GUI command-line manual](https://siril.org/docs/man/).
- [DSS command-line documentation](https://raw.githubusercontent.com/deepskystacker/DSS/master/Help/english/commandline.htm)
  and [DSS GUI argument handling](https://github.com/deepskystacker/DSS/blob/6.2.2/DeepSkyStacker/DeepSkyStacker.cpp).
- [GIMP command-line manual](https://www.gimp.org/man/gimp.html) and
  [GIMP FITS loader](https://gitlab.gnome.org/GNOME/gimp/-/blob/master/plug-ins/file-fits/fits.c).
- [Adobe Photoshop scripting interfaces](https://helpx.adobe.com/photoshop/using/scripting.html).
- [AutoStakkert downloads](https://www.autostakkert.com/wp/download/) and
  [change notes](https://www.autostakkert.com/wp/change-notes/).
- [AstroWizard change log](https://astrowizard.lukomatico.com/changelog.html) and
  [external handoff developer's launcher](https://github.com/mjm1138/m110/blob/main/m110/launch.py).
- [Stacking Wizard release information](https://astrowizard.lukomatico.com/stackingwizard.html).
