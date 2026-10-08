# Mosaic collections

Use **Repository → Mosaic collections → Browse mosaics** to organise panel inputs
and completed stitched images. A collection can span targets, sessions, filters
and cameras. Linking an image to a collection does not create another archive copy.

## Create and review a collection

1. Select repository captures and choose **Mosaics → Assign to panel or completed output**
   from their right-click menu. Choose or create a collection and panel.
2. Alternatively, use **Detect from metadata** to inspect the selection, or the
   visible repository captures when nothing is selected.
3. Review **Suggested** memberships and assign **Unresolved** captures to panels.
   **Declared** memberships have explicit mosaic/panel metadata.
4. Use **Confirm selected** after checking each assignment.
5. Assign stitched results as **completed outputs**, separate from panel inputs.

The panel list shows panels, unresolved captures and outputs. Collection tools
rename collections/panels, add panels and remove memberships. Removing a member
or dismissing a collection retains its files.

Use Repository or Import **Filters** to narrow by mosaic, panel or membership state.
Collection and panel names are searchable.

## Detection and optional solving

Detection uses image headers, session metadata and filename/folder hints.
Filename and sky-overlap suggestions require review; matching target names alone
do not establish a mosaic. Inspect the membership evidence when assignments conflict.

**Tools → Read headers/session metadata** reads evidence from archived copies.
**Solve selected representatives** uses your configured solver for selected panels.
ASTAP needs its star database; online solving needs an Astrometry.net account.
Only supported sky mappings produce trusted footprints; approximate outlines are labelled.
Detection does not run a solver automatically.

## Export

**Export collection** prepares separate stacking inputs for each panel, retaining
compatible camera, filter, exposure and calibration groups. Review suggested or
unresolved membership before exporting. If files belong to several collections,
choose the intended collection.

Exports contain workflow notes, a manifest and folders for panel results and
completed outputs. Source bytes remain unchanged. Stack panels and stitch the
mosaic in external software; keep completed outputs separate from contributing subs.

Move or back up the entire repository, including `.astroarchive`, to retain
collection definitions and memberships.
