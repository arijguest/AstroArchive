# Processing in external software

## Send a stack to Siril

1. Select one non-rejected, uncompressed FITS stack in Repository.
2. Right-click **Export → Send stack to Siril**.
3. Locate the Siril GUI executable (`siril.exe`).

AstroArchive copies and verifies the stack in Edited, then opens that working copy
in a new Siril process. Save outputs alongside it and refresh Edited to find them.
The archived original remains unchanged. Gzip FITS, multiple files and `siril-cli.exe`
are not supported by this action.

## AstroWizard and other editors

Select repository images and choose **Export → Create Edited working copies**.
Confirm the working copies, then load the copies from the opened folder in your editor.
Save outputs there so they appear in Edited.

From Edited, use **Open in editor** for Siril or the Windows default application,
or **Open image folder** to load files manually. For acquisition subs, use a
stacking export. Enable **Add Metadata** to include workflow notes and a manifest.
By default, exports contain image files only; **Create new folder** is also optional.

Editors are installed separately:

- [Siril](https://siril.org/)
- [AstroWizard](https://github.com/lukomaticoYT/astrowizard-releases)
