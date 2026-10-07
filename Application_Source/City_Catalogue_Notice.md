# Offline town/city catalogue

Place names, alternate names, administrative regions, countries and coordinates
are derived from [GeoNames](https://www.geonames.org/), downloaded on 7 October
2026 from [the GeoNames export](https://download.geonames.org/export/dump/).
GeoNames data is licensed under [Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/).
Credit: GeoNames and its contributors. No endorsement of AstroArchive is implied.

AstroArchive joins `cities500.txt` with `countryInfo.txt` and
`admin1CodesASCII.txt`, selects the fields required by the picker, and bundles
236,003 records as a gzip-compressed UTF-8 TSV resource. It includes settlements
with at least 500 inhabitants and administrative seats; it is not a complete
list of every locality. Coordinates refer to each place's centre. Choosing the
nearest listed town/city provides an approximate observing site for rotation
analysis. Searches do not submit the user's location to a network service.

To regenerate the resource, download the three source files into one directory
and run `python scripts/build-city-catalog.py DIRECTORY` from the repository.
The script validates coordinates and writes deterministic gzip output. GeoNames
updates its exports, so a later download may produce different results.

SHA-256 of the source snapshot used for this release:

| File | SHA-256 |
| --- | --- |
| cities500.zip | 65c15e7b01862c0b439f9979fc9013fda2676e5b731ea8674c11fb1e3cba7e52 |
| countryInfo.txt | 93bafc525813f22e4711ff9ed6d626343094ce48c26388dc7c49189b3d7d5512 |
| admin1CodesASCII.txt | 1da92a6323a5fec3176f3f743bf4cf4040fd56a876da55e46fbca23c863aa60a |
