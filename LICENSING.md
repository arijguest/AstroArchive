# AstroArchive licensing

AstroArchive is source-available under the **PolyForm Noncommercial License 1.0.0**.
Required Notice: Copyright 2026 Ari J. Guest (https://arijguest.com)

## Permissions

| Activity | Terms |
| --- | --- |
| Noncommercial use | Permitted under the licence, including its specified personal uses and qualifying organisations. |
| Modification and sharing | Permitted for purposes allowed by the licence. Include the terms or their official URL and preserve the Required Notice. |
| Commercial use or resale | Requires a separate licence from the rights holder where the activity is outside the licence's permitted purposes. |

Read [LICENSE](LICENSE) for the complete, governing terms. This summary does not
replace or extend them. PolyForm Noncommercial restricts commercial use; AstroArchive
is **source-available**, rather than open source under the Open Source Definition.
For commercial licensing enquiries, contact Ari J. Guest through
[the AstroArchive website](https://astroarchive.arijguest.com/#contact).

## Scope and third-party material

The licence applies to AstroArchive's first-party application, installer, launcher,
source code, build scripts and documentation made available with this notice.
Separately licensed third-party material retains its own terms:

- **OpenNGC:** CC BY-SA 4.0; see [Catalogue_Notice.md](Application_Source/Catalogue_Notice.md) and [OpenNGC_README.md](Application_Source/OpenNGC_README.md).
- **GeoNames:** CC BY 4.0; see [City_Catalogue_Notice.md](Application_Source/City_Catalogue_Notice.md).
- **D3-Celestial constellation data:** BSD 3-clause; see [Sky_Catalogue_Notice.md](Application_Source/Sky_Catalogue_Notice.md).
- **SMBLibrary 1.5.8:** LGPL-3.0-or-later; a separate, replaceable DLL with matching
  source and licence texts in [Remote/lib](Application_Source/Remote/lib/README.md).
- External processing tools and optional codecs are governed by their own licences.

Your imported images, capture data and processing outputs are not relicensed by
using AstroArchive. This notice does not revoke any rights validly granted for
previous copies under other terms.

The first-party licence does not restrict replacement of SMBLibrary with a compatible
modified version, or reverse engineering of the combined application solely to debug
those modifications, as permitted by LGPL section 4. These permissions apply even
where the first-party licence would otherwise restrict those activities.

## Bundled and downloaded target information

`Application_Source/TargetInfo.zip` contains a runtime projection of AstroArchive
object database 2.1.0 and its approved images. Catalogue data and images retain
their individual licences. The pack contains `LICENSES_AND_ATTRIBUTION.md`,
OpenNGC source/licence notices, and full per-image attribution, representation,
modification descriptions and licence/source links. These are also accessible
through the information pane. Preserve the pack when distributing the programme;
the application licence does not replace these third-party terms.

The 3.2.9 essentials projection preserves OpenNGC identities, measurements, aliases,
ambiguities, corrections, CC BY-SA 4.0 notices and acknowledgements. Its 640-pixel
photographic thumbnails document resizing, black compositing of transparent pixels
and JPEG conversion in `essentials.json`, with original file credits and exact
licence versions. Full ESA/Hubble mandatory image credits stay visible.

Provider capability decisions are embedded in
[InformationProviders.json](Application_Source/InformationProviders.json). Unknown
permissions deny display/cache/bundle/redistribution. Wikidata structured identifiers
are CC0; Wikipedia introductions are independently checked for CC BY-SA 4.0 with
revision/history attribution and share-alike for adaptations. Commons files require
an independently reviewed astronomical association and current per-file rights.
A NASA-hosted file is not automatically public domain. SIMBAD, VizieR and NED
enrichment remain disabled until selected dataset rights are documented.

The 3.2.9 distance and comet companions (snapshot 13 October 2026) carry these terms:

- **Galaxy and nebula distances:** Wikidata property P2583, structured data released under
  CC0 1.0 (<https://www.wikidata.org/wiki/Wikidata:Licensing>). Each distance links its Wikidata
  item and keeps its original quantity. They are community-collected literature estimates, not a
  uniform survey. Catalogue identities remain OpenNGC (CC BY-SA 4.0, Mattia Verga and contributors);
  the adapted crosswalk and pack stay under CC BY-SA 4.0 and AstroArchive's noncommercial licence
  does not narrow that. Wikipedia prose and Commons images are not part of this dataset.
- **Comet orbits:** NASA/JPL-Caltech Small-Body Database (API 1.0), `CometCatalog.tsv.gz`.
  Numerical orbital elements, uncertainties and designations are factual data used with
  acknowledgement and no implied endorsement; they are not asserted to be CC0 and carry no JPL
  prose, images, logos or software. The API fair-use policy applies to any live request.
- **Meteor-shower parents:** NASA/JPL Solar System Dynamics Meteor Streams (which credits the
  IAU Meteor Data Center and American Meteor Society) and NASA Science, `MeteorShowers.json`.
  Association facts only, with source URLs; NASA's factual-use guidance applies.
- No GPL data is included. Stellarium's catalogue is deliberately not used.

The audited JPL snapshots and coefficient table contain scientific numerical facts,
with the factual-data reuse basis (17 USC 102(b)), source, solution, epoch, units and
uncertainties recorded. This is not a blanket CC0 claim or a licence for JPL prose,
media or software. The separate JPL API fair-use policy requires single requests,
identifying User-Agent, caching and backoff. Physical measurement reports retain
their original references; no physical records are newly bundled. Runtime content
and its rights metadata are cached together. The native orbit engine and viewer are
original application code, governed by the first-party licence.
