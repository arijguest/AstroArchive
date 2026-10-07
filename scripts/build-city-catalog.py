"""Build the offline picker resource from GeoNames cities500 + country/admin files.

Download cities500.zip, countryInfo.txt and admin1CodesASCII.txt from
https://download.geonames.org/export/dump/ into one directory, then run:
  python scripts/build-city-catalog.py DIRECTORY
Only developers need Python; the Windows app reads the bundled gzip resource.
"""
import argparse
import gzip
import hashlib
import io
import math
from pathlib import Path
import zipfile


def build(source, output):
    countries = {}
    for line in (source / "countryInfo.txt").read_text(encoding="utf-8").splitlines():
        if line and not line.startswith("#"):
            fields = line.split("\t")
            countries[fields[0]] = fields[4]
    regions = {}
    for line in (source / "admin1CodesASCII.txt").read_text(encoding="utf-8").splitlines():
        fields = line.split("\t")
        regions[fields[0]] = fields[1]
    output.parent.mkdir(parents=True, exist_ok=True)
    count = 0
    seen = set()
    with zipfile.ZipFile(source / "cities500.zip") as archive:
        with archive.open("cities500.txt") as data, output.open("wb") as raw:
            with gzip.GzipFile(filename="", fileobj=raw, mode="wb", mtime=0) as zipped:
                with io.TextIOWrapper(zipped, encoding="utf-8", newline="\n") as writer:
                    writer.write("Id\tName\tAscii\tCode\tCountry\tRegion\tLatitude\tLongitude\tPopulation\tAliases\n")
                    for line in io.TextIOWrapper(data, encoding="utf-8"):
                        fields = line.rstrip("\r\n").split("\t")
                        identity, latitude, longitude = int(fields[0]), float(fields[4]), float(fields[5])
                        if identity in seen or not math.isfinite(latitude) or not math.isfinite(longitude) or not -90 <= latitude <= 90 or not -180 <= longitude <= 180:
                            raise ValueError("Invalid place: " + fields[0])
                        seen.add(identity)
                        selected = [fields[0], fields[1], fields[2], fields[8], countries.get(fields[8], fields[8]), regions.get(fields[8] + "." + fields[10], ""), fields[4], fields[5], fields[14], fields[3]]
                        writer.write("\t".join(selected) + "\n")
                        count += 1
    print(str(output), count, "places;", output.stat().st_size, "compressed bytes")
    for filename in ["cities500.zip", "countryInfo.txt", "admin1CodesASCII.txt"]:
        print(filename, hashlib.sha256((source / filename).read_bytes()).hexdigest())


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parent.parent / "Application_Source" / "cities.tsv.gz")
    arguments = parser.parse_args()
    build(arguments.source, arguments.output)
