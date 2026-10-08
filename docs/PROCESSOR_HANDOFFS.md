# Processing in external software

## Send a stack to Siril

1. Select one non-rejected, uncompressed FITS stack in Repository.
2. Right-click **More actions → Edit a copy in Siril**.
3. Set the Siril GUI executable (`siril.exe`) in **Settings → Export** if prompted.

AstroArchive copies and verifies the stack in Edited, then opens that working copy
in a new Siril process. Save outputs alongside it and refresh Edited to find them.
The archived original remains unchanged. Gzip FITS, multiple files and `siril-cli.exe`
are not supported by this action.

## AstroWizard and other editors

Select repository images and right-click **Create Edited copies**.
Confirm the working copies, then load the copies from the opened folder in your editor.
Save outputs there so they appear in Edited.

From Edited, use **Open in editor** for Siril or the Windows default application,
or right-click **Open image folder** to load files manually. For acquisition subs, use a
stacking export. Enable **Add Metadata** to include workflow notes and a manifest.
By default, exports contain image files only; **Create new folder** is also optional.

## Open an exported stack in Siril

Choose **Export files** or **Stacking folder**, then select **Siril** under
**Open with… after export**. Set `siril.exe` and the default export folder in
**Settings → Export**. Siril opens one eligible FITS stack only after the export
completes and its copies are verified, using the actual output filename and folder.
A stacking export can explicitly convert a supported linear stack to FITS first.
Cancelled or failed exports never launch Siril; launch failures retain the export.

For subs or multiple stacks, export a stacking folder and load its inputs in your
preferred application. Matching calibrations are an option in that same popup.
**More options** holds session separation and optional metadata. The completion
popup opens the finished folder without an additional application menu.

Editors are installed separately:

- [Siril](https://siril.org/)
- [AstroWizard](https://github.com/lukomaticoYT/astrowizard-releases)
