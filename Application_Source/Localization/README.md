# Interface translations

Each `lang-<code>.txt` file is embedded in AstroArchive.exe and the engine tests by
`build.ps1` and `test.ps1`. English is the source text and the fallback: a missing
line shows the English original, never an empty label.

- One entry per line: `English<TAB>Translation`, UTF-8. Lines starting with `#` are comments.
- `\n` is a line break, `\t` a tab and `\\` a backslash.
- `{0}` and `{1}` stand for inserted text and `{#0}` only matches a number, so
  `{#0} subs<TAB>{0} Subs` translates "12 subs". Write plain `{0}` in the translation;
  placeholders may be reordered but every one must be kept.
- Composed status lines are translated part by part (each line, ` · ` part or sentence).
- `Theme|Light` style keys apply only where the interface sets that context, so the
  Light/Dark theme choices can differ from the Light/Dark frame types.
- Keep a trailing `…` (the command opens a dialog), product names, file formats,
  keyboard keys, and the folder names Edited and Dump.

`LocalizationTests.cs` checks every bundled table for placeholders, line breaks and
dialog ellipses. The offline guide (`Quick_Start.txt`) stays in English.
