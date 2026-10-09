# Preview and browsing performance

Preview rendering calculates monochrome values once per pixel. Nonlinear modes
reuse sampled percentile and median statistics while applying the current target,
filter and observation mode on each render. Decoded samples remain read-only.
Full-resolution RGB previews retain the reader's planar sample buffer; native
dimensions, colour depth and Bayer interpolation are preserved.

Sampled SER frames decode one row at a time directly into display bins. Sampled
XISF previews decode into those bins from the codec's byte buffer, avoiding a
full-image double buffer. Compressed/shuffled XISF codecs can still require full
byte buffers. Full-resolution Bayer previews still allocate their RGB samples.

Repository searches calculate session keys and membership once, then construct
session labels and members in final display order. Repository, Import and Edited
column sorting uses the existing cancellable search worker; rapid changes replace
pending work and stale results cannot replace a newer view. The small metrics
table keeps its synchronous sort.

Edited refreshes still enumerate folders and reapply project assignments, target
matching and GIF inheritance. Parsed image headers use an 8 MiB/4,096-entry cache
only when Windows supplies reliable NTFS/ReFS identity and change stamps.
Cloud-backed or unreliable stamps always reread headers. Changed files and
transient errors invalidate entries; cache hits return independent headers.
Case-insensitive path indexes replace repeated source/assignment scans, and a
combined gallery is sorted once.

Archive hashing, destination readback, source deletion checkpoints and protection
are unchanged. No GPU, codec dependency or framework migration is required.

Run `Application_Source/test.ps1 -PreviewOnly` for preview equivalence and format
checks. The full engine suite additionally covers session ordering, header cache
invalidation and Edited refreshes. Run the full native UI checks for sorting,
selection, cancellation and dispatcher responsiveness. Windows checks remain
necessary for WPF, codecs, file sharing, reliable file stamps and protection.
