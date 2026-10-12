# Offline place catalogue attribution

Names and coordinates come from **GeoNames and its contributors**, under
[Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/).
No endorsement of AstroArchive is implied.

- [GeoNames](https://www.geonames.org/)
- [Source downloads](https://download.geonames.org/export/dump/): `cities500.txt`, `countryInfo.txt` and `admin1CodesASCII.txt`.

AstroArchive selects place names, alternate names, regions, countries and coordinates,
and bundles 236,003 records for offline search. The catalogue includes settlements
with at least 500 inhabitants and administrative seats; some smaller localities are absent.
Coordinates describe place centres, so use precise coordinates when your observing
site needs them. Searches do not send your location to a network service.


## Offline timezone lookup

Timezone identifiers for all 236,003 bundled city IDs also come from GeoNames
and its contributors (CC BY 4.0). The existing place catalogue is unchanged;
`city-timezones.tsv.gz` is a separate, compact index. Source hashes and the build
hash are recorded in `city-timezones.tsv.json`.

IANA-to-Windows timezone mapping and alias data come from the Unicode CLDR
release-48 `windowsZones.xml` and `bcp47/timezone.xml` files, under the Unicode
License v3 below. AstroArchive selects and joins their mapping entries to city IDs.

- https://github.com/unicode-org/cldr/tree/release-48/common
- https://www.unicode.org/license.txt

UNICODE LICENSE V3

COPYRIGHT AND PERMISSION NOTICE

Copyright © 1991-2026 Unicode, Inc.

NOTICE TO USER: Carefully read the following legal agreement. BY
DOWNLOADING, INSTALLING, COPYING OR OTHERWISE USING DATA FILES, AND/OR
SOFTWARE, YOU UNEQUIVOCALLY ACCEPT, AND AGREE TO BE BOUND BY, ALL OF THE
TERMS AND CONDITIONS OF THIS AGREEMENT. IF YOU DO NOT AGREE, DO NOT
DOWNLOAD, INSTALL, COPY, DISTRIBUTE OR USE THE DATA FILES OR SOFTWARE.

Permission is hereby granted, free of charge, to any person obtaining a
copy of data files and any associated documentation (the "Data Files") or
software and any associated documentation (the "Software") to deal in the
Data Files or Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, and/or sell
copies of the Data Files or Software, and to permit persons to whom the
Data Files or Software are furnished to do so, provided that either (a)
this copyright and permission notice appear with all copies of the Data
Files or Software, or (b) this copyright and permission notice appear in
associated Documentation.

THE DATA FILES AND SOFTWARE ARE PROVIDED "AS IS", WITHOUT WARRANTY OF ANY
KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF
THIRD PARTY RIGHTS.

IN NO EVENT SHALL THE COPYRIGHT HOLDER OR HOLDERS INCLUDED IN THIS NOTICE
BE LIABLE FOR ANY CLAIM, OR ANY SPECIAL INDIRECT OR CONSEQUENTIAL DAMAGES,
OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION,
ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THE DATA
FILES OR SOFTWARE.

Except as contained in this notice, the name of a copyright holder shall
not be used in advertising or otherwise to promote the sale, use or other
dealings in these Data Files or Software without prior written
authorization of the copyright holder.
