# OpenNGC catalogue attribution

AstroArchive embeds a selected-field, selected-row derivative of the OpenNGC catalogue by Mattia Verga and OpenNGC contributors.

- Original project: https://github.com/mattiaverga/OpenNGC
- Data files: `database_files/NGC.csv` and `database_files/addendum.csv`.
- Retrieved NGC.csv Git blob SHA: `5e7e53b8fd45c1222debddc2c5c01c7cdd239f49`.
- Retrieved addendum.csv Git blob SHA: `564c669f441b9be35dc4c1ee3b6348acde3464c2`.
- Licence: Creative Commons Attribution-ShareAlike 4.0 International.
- Licence terms: https://creativecommons.org/licenses/by-sa/4.0/legalcode.en
- Additional source acknowledgements are reproduced in `OpenNGC_README.md`.

Changes: select Name, Type, RA, Dec, MajAx, V-Mag, M, Common names and Identifiers columns; retain rows with valid coordinates; exclude duplicate, nonexistent, individual-star, double-star and Other records. Combine both source files. The resulting semicolon-separated catalogue contains 12,163 objects. No scientific measurements were altered. The derived `catalog.csv` remains under CC BY-SA 4.0. Catalogue credit and a licence link are also available in the app guide.

The catalogue provides candidates based on solved sky position. It cannot establish the observer's intent in an ambiguous field. Uncatalogued objects and mosaics may require a user-supplied target label.
