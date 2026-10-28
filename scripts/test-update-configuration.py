#!/usr/bin/env python3
"""Exercise signed bundle update configuration without network or private keys."""
import base64
import importlib.util
from pathlib import Path
import plistlib
import tempfile
import unittest
import subprocess
import sys

spec = importlib.util.spec_from_file_location("configure_updates", Path(__file__).with_name("configure-updates.py"))
configuration = importlib.util.module_from_spec(spec)
spec.loader.exec_module(configuration)


class UpdateConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.info = Path(self.directory.name) / "Info.plist"
        self.config = Path(self.directory.name) / "Updates.plist"
        self.key = base64.b64encode(bytes(range(32))).decode()

    def write(self, info, config):
        self.info.write_bytes(plistlib.dumps(info))
        self.config.write_bytes(plistlib.dumps(config))

    def test_disabled_channel_clears_previous_trust_and_feed(self):
        self.write({"CFBundleName": "Lokum", "SUFeedURL": "https://old.example/feed", "SUPublicEDKey": self.key,
                    "LokumPublicUpdateRepository": "previous/repository", "LokumSuggestedGitHubRepository": "previous/repository",
                    "LokumLocalUpdateFeed": True}, {"Enabled": False})
        try:
            configuration.apply_configuration(self.info, self.config, {})
        except ValueError as error:
            self.fail("A disabled Lokum channel must clear inherited trust without requiring a feed: " + str(error))
        result = plistlib.loads(self.info.read_bytes())
        self.assertEqual(result["CFBundleName"], "Lokum")
        for field in ["SUFeedURL", "SUPublicEDKey", "LokumPublicUpdateRepository", "LokumSuggestedGitHubRepository", "LokumLocalUpdateFeed"]:
            self.assertNotIn(field, result)
        self.assertFalse(result["SUEnableAutomaticChecks"])

    def test_configured_channel_preserves_explicit_publisher_trust(self):
        self.write({"CFBundleName": "Lokum"}, {"FeedURL": "https://github.com/example/lokum/releases/latest/download/appcast.xml",
                                               "PublicEDKey": self.key, "PublicRepository": "example/lokum"})
        configuration.apply_configuration(self.info, self.config, {})
        result = plistlib.loads(self.info.read_bytes())
        self.assertEqual(result["SUPublicEDKey"], self.key)
        self.assertEqual(result["LokumPublicUpdateRepository"], "example/lokum")
        self.assertEqual(result["SUFeedURL"], "https://github.com/example/lokum/releases/latest/download/appcast.xml")
        self.assertTrue(result["SURequireSignedFeed"])
        self.assertTrue(result["SUVerifyUpdateBeforeExtraction"])

    def test_absent_default_configuration_does_not_retain_old_signing_trust(self):
        self.write({"SUPublicEDKey": self.key, "LokumPublicUpdateRepository": "previous/repository"}, {})
        self.config.unlink()
        configuration.apply_configuration(self.info, self.config, {})
        result = plistlib.loads(self.info.read_bytes())
        self.assertNotIn("SUPublicEDKey", result)
        self.assertNotIn("LokumPublicUpdateRepository", result)

    def test_bad_key_or_plain_http_is_rejected(self):
        for feed, key in [("http://updates.example/feed", self.key), ("https://updates.example/feed", "bad-key")]:
            with self.subTest(feed=feed, key=key):
                self.write({}, {"FeedURL": feed, "PublicEDKey": key})
                with self.assertRaises(ValueError):
                    configuration.apply_configuration(self.info, self.config, {})

    def test_release_audit_accepts_independent_configured_or_unconfigured_lokum(self):
        for connected in [False, True]:
            with self.subTest(connected=connected):
                bundle = Path(self.directory.name) / ("Connected.app" if connected else "Local.app")
                (bundle / "Contents").mkdir(parents=True)
                info = {"CFBundleIdentifier": "local.lokum.app", "LokumExternalTools": True}
                if connected:
                    info.update({"LokumPublicUpdateRepository": "example/lokum", "SUFeedURL": "https://github.com/example/lokum/releases/latest/download/appcast.xml",
                                 "SUPublicEDKey": self.key, "SURequireSignedFeed": True, "SUVerifyUpdateBeforeExtraction": True})
                (bundle / "Contents" / "Info.plist").write_bytes(plistlib.dumps(info))
                result = subprocess.run([sys.executable, str(Path(__file__).with_name("audit-release.py")), str(bundle)], capture_output=True, text=True)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
