"""Repair known Commons template contamination without shortening real credits.

Applied to the SQLite projection and existing packs. Licence terms, image bytes,
source links and processing acknowledgements are kept. No remote lookup at runtime.
"""
import re

# These Artist fields include page controls or permission prose, rather than just
# the credited creator. Match the exact file, not a target that may change images.
CREATORS = {
    'Iris_Nebula_-_NGC_7023_-_Caldwell_4.jpg': 'Andreigusan',
    'M46-47.jpg': 'Boris Štromar',
    'NGC7293_(2004).jpg': "NASA, ESA, and C.R. O'Dell (Vanderbilt University)",
}


def clean_credit(text):
    if not text:
        return text
    # Keep the attribution's generated file title/licence suffix outside the
    # contaminated Commons Artist/Credit HTML that was flattened into plain text.
    parts = text.rsplit(' — ', 1)
    body = parts[0]
    body = re.sub(r'\s*This is a\s+featured picture\b.*$', '', body, flags=re.S)
    body = re.sub(r'\s*This media was produced by\b.*?See\s+Commons:Licensing\s*\.', '', body, flags=re.S)
    body = re.sub(r'\s*\(\s*talk\s*\)', '', body)
    body = re.sub(r'\s+', ' ', body).strip()
    body = re.sub(r'; source/processing credit:\s*$', '', body)
    return body + (' — ' + parts[1] if len(parts) == 2 else '')


def repair_image(image):
    result = dict(image)
    for field in ('creator', 'attribution', 'rights_holder'):
        if field in result:
            result[field] = clean_credit(result[field])
    filename = image.get('file_page_url', '').split('/wiki/File:')[-1]
    if filename in CREATORS:
        result['creator'] = CREATORS[filename]
        if filename == 'Iris_Nebula_-_NGC_7023_-_Caldwell_4.jpg':
            result['rights_holder'] = result['creator']
    return result
