#!/usr/bin/env python3
"""Audit every bundled thumbnail credit and exercise the Commons export repair."""
import hashlib
import json
from pathlib import Path
import unittest
import zipfile
from target_image_credits import repair_image


class CreditsTests(unittest.TestCase):
    def test_page_templates_are_removed_but_licensing_and_processing_survive(self):
        image = {
            'file_page_url': 'https://commons.wikimedia.org/wiki/File:Iris_Nebula_-_NGC_7023_-_Caldwell_4.jpg',
            'creator': 'Andreigusan\n\n This is a featured picture on Wikipedia. If you think this file should be featured, nominate it.',
            'attribution': 'Andreigusan\n This is a featured picture on Wikipedia. Nominate it. — Iris.jpg (CC-BY-SA-4.0), via Wikimedia Commons',
            'rights_holder': 'Andreigusan\n This is a featured picture on Wikipedia. Nominate it.',
            'licence': 'CC-BY-SA-4.0', 'licence_url': 'https://creativecommons.org/licenses/by-sa/4.0',
            'modifications': 'Cropped and processed by Example Person.', 'sha256': 'unchanged',
        }
        repaired = repair_image(image)
        self.assertEqual(repaired['creator'], 'Andreigusan')
        self.assertEqual(repaired['rights_holder'], 'Andreigusan')
        self.assertEqual(repaired['attribution'], 'Andreigusan — Iris.jpg (CC-BY-SA-4.0), via Wikimedia Commons')
        for field in ('licence', 'licence_url', 'file_page_url', 'modifications', 'sha256'):
            self.assertEqual(repaired[field], image[field])
        self.assertEqual(repair_image(repaired), repaired)
        image['attribution'] = 'NASA, ESA; Image processing: A. Scientist.\nAcknowledgements: B. Scientist. — Example.jpg (CC-BY-4.0), via Wikimedia Commons'
        self.assertIn('Image processing: A. Scientist. Acknowledgements: B. Scientist.', repair_image(image)['attribution'])

    def test_all_bundled_assignments_have_intact_images_and_clean_credits(self):
        pack = Path(__file__).resolve().parents[1] / 'Application_Source/TargetInfo.zip'
        count = 0
        with zipfile.ZipFile(pack) as archive:
            for name in archive.namelist():
                if not name.startswith('cards/'):
                    continue
                for card in json.loads(archive.read(name)).values():
                    image = card.get('image')
                    if not image:
                        continue
                    count += 1
                    with self.subTest(target=card['id']):
                        for field in ('creator', 'attribution', 'rights_holder'):
                            credit = image.get(field) or ''
                            self.assertNotIn('featured picture', credit)
                            self.assertNotIn('Commons:Licensing', credit)
                            self.assertNotIn('( talk )', credit)
                        for field in ('creator', 'attribution', 'licence', 'licence_url', 'file_page_url'):
                            self.assertTrue(image.get(field), field)
                        self.assertEqual(hashlib.sha256(archive.read(image['local_path'])).hexdigest(), image['sha256'])
                        self.assertEqual(repair_image(image), image)
            # The essentials pack keeps a credited subset; its manifest lists every thumbnail it ships.
            self.assertEqual(count, len(json.loads(archive.read('essentials.json'))['images']))
            self.assertEqual(count, 200)


if __name__ == '__main__':
    unittest.main()
