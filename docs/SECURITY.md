# File and metadata safety

AstroArchive is a local archive manager. Imports and ordinary exports verify
SHA-256 copies and preserve source bytes. Verification detects changed files;
it does not certify an image or external application as safe.

## Boundaries

- Archive and Edited actions validate repository membership and file identity.
  Linked paths and entries that escape managed folders are rejected.
- Export destinations stay outside the archive. Existing files are retained;
  filename collisions receive suffixes. Copies are verified before handoff.
- Native SQLite loads from Windows System32 rather than the working directory.
  Optional scientific codecs are installed separately by the user.
- CSV exports prefix formula-like metadata with a tab, keeping it as spreadsheet
  text. Numeric columns keep their numeric values.
- Image decoding has explicit size/layout limits. Full-resolution previews use
  separate buffers from the sampled sidebar cache.

## Deletion and recovery

Archive deletion and Edited deletion remove managed copies after confirmation;
source originals remain. Import's separate **Delete originals** option only removes
verified, safely removable new/restored source captures. Cloud deletions sync.
Deletion history controls later reimport; it is not an undo facility.

Back up the whole archive to a separate drive. Optional NTFS protection reduces
accidental deletion; the Windows owner can change permissions.

## Reporting

Report reproducible issues through the repository's available GitHub reporting
options. Include the app/package version, affected operation and a minimal example;
omit private images, credentials and personal paths unless needed for diagnosis.
