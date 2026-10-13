import hashlib
import importlib.util
import json
import subprocess
import sys
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('release_report', Path(__file__).with_name('verify-release-report.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PublicationGateTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.directory = Path(self.scratch.name)
        self.installer = 'AstroArchive1.8.4.2.exe'
        self.alias = 'AstroArchive-1.8.4-Windows-x64-Offline-Setup.exe'
        self.publisher = 'CN=Test Publisher'
        data = b'signed installer fixture'
        digest = hashlib.sha256(data).hexdigest()
        for name in (self.installer, self.alias):
            (self.directory / name).write_bytes(data)
            (self.directory / f'{name}.sha256').write_text(f'{digest}  {name}\n', encoding='ascii')
        self.feed = {'package_version': '1.8.4.2', 'application_version': '1.8.4', 'sha256': digest, 'size': len(data), 'authenticode_signed': True}
        self.report = {'schema': 1, 'files': [
            {'file': name, 'sha256': digest, 'status': 'Valid', 'publisher': self.publisher,
             'thumbprint': 'TEST', 'timestamp_publisher': 'CN=Timestamp'}
            for name in (self.installer, 'AstroArchive.exe', 'Start.exe')]}
        self.write_metadata()

    def write_metadata(self):
        (self.directory / 'update.json').write_text(json.dumps(self.feed), encoding='utf-8')
        (self.directory / 'signatures.json').write_text(json.dumps(self.report), encoding='utf-8')

    def test_verified_release_is_accepted(self):
        module.verify(self.directory, self.publisher)

    def test_missing_trust_evidence_is_rejected(self):
        (self.directory / 'signatures.json').unlink()
        with self.assertRaisesRegex(ValueError, 'Unsigned release blocked'):
            module.verify(self.directory, self.publisher)

    def test_wrong_publisher_and_incomplete_payload_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Unverified signature'):
            module.verify(self.directory, 'CN=Other Publisher')
        self.report['files'].pop()
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, 'Incomplete Windows signature report'):
            module.verify(self.directory, self.publisher)

    def test_bytes_changed_after_windows_verification_are_rejected(self):
        (self.directory / self.installer).write_bytes(b'tampered installer')
        with self.assertRaisesRegex(ValueError, 'changed after Windows signature verification'):
            module.verify(self.directory, self.publisher)

    def test_alias_and_feed_drift_are_rejected(self):
        (self.directory / self.alias).write_bytes(b'different installer')
        with self.assertRaisesRegex(ValueError, 'Compatibility installer differs'):
            module.verify(self.directory, self.publisher)
        self.feed['size'] += 1
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, 'changed after Windows signature verification'):
            module.verify(self.directory, self.publisher)

    def test_extra_unsigned_executable_is_rejected(self):
        (self.directory / 'unsigned.exe').write_bytes(b'unsigned')
        with self.assertRaisesRegex(ValueError, 'Unexpected executable'):
            module.verify(self.directory, self.publisher)

    def test_explicit_unsigned_mode_accepts_only_integrity_verified_unsigned_package(self):
        self.feed['authenticode_signed'] = False
        self.write_metadata()
        (self.directory / 'signatures.json').unlink()
        with self.assertRaisesRegex(ValueError, 'Unsigned release blocked'):
            module.verify(self.directory, '')
        module.verify(self.directory, '', allow_unsigned=True)
        (self.directory / self.installer).write_bytes(b'tampered installer')
        with self.assertRaisesRegex(ValueError, 'changed after Windows signature verification'):
            module.verify(self.directory, '', allow_unsigned=True)

    def test_required_signing_cannot_be_overridden_by_unsigned_mode(self):
        self.feed['authenticode_signed'] = False
        self.write_metadata()
        (self.directory / 'signatures.json').unlink()
        with self.assertRaisesRegex(ValueError, 'Unsigned release blocked'):
            module.verify(self.directory, '', allow_unsigned=True, require_signed=True)

    def test_required_signing_accepts_verified_publisher_and_installer(self):
        module.verify(self.directory, self.publisher, require_signed=True)

    def test_cli_obeys_optional_or_required_package_signing_policy(self):
        self.feed['authenticode_signed'] = False
        self.write_metadata()
        (self.directory / 'signatures.json').unlink()
        policy_path = Path(__file__).resolve().parent.parent / 'Installer' / 'release.json'
        required = json.loads(policy_path.read_text(encoding='utf-8')).get('require_signing') is True
        result = subprocess.run([sys.executable, str(spec.origin), str(self.directory), '--allow-unsigned'], capture_output=True, text=True)
        if required:
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Unsigned release blocked', result.stderr)
        else:
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn('PASS', result.stdout)

    def test_unsigned_mode_cannot_downgrade_a_package_marked_as_signed(self):
        (self.directory / 'signatures.json').unlink()
        with self.assertRaisesRegex(ValueError, 'Unsigned release blocked'):
            module.verify(self.directory, self.publisher, allow_unsigned=True)


if __name__ == '__main__':
    unittest.main()
