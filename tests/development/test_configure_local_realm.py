import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from xml.etree import ElementTree

SCRIPT = Path(__file__).resolve().parents[2] / "tools/development/configure_local_realm.py"
spec = importlib.util.spec_from_file_location("configure_local_realm", SCRIPT)
realm = importlib.util.module_from_spec(spec)
spec.loader.exec_module(realm)


class LocalRealmConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.authority = self.root / "publication" / "authority.json"
        self.authority.parent.mkdir()
        self.props = self.root / "config" / "client.local.props"
        self.valid = {
            "catalogUri": "https://localhost/realms.json",
            "signatureUri": "https://localhost/realms.sig",
            "publicKeySpki": "A" * 128,
        }
        self.write_authority(self.valid)

    def write_authority(self, value):
        self.authority.write_text(json.dumps(value), encoding="utf-8")

    def test_generate_and_reapply_are_idempotent_and_parseable(self):
        self.assertEqual("Configured", realm.configure(self.authority, self.props))
        self.assertEqual("Already configured", realm.configure(self.authority, self.props))
        tree = ElementTree.parse(self.props)
        self.assertEqual(str(self.authority), tree.findtext("PropertyGroup/OpenConquerCatalogAuthorityFile"))
        self.assertEqual("true", tree.findtext("PropertyGroup/OpenConquerLocalRealmAuthority"))
        self.assertEqual("Disabled", realm.disable(self.props))
        self.assertEqual("Already disabled", realm.disable(self.props))
        self.assertFalse(self.props.exists())

    def test_missing_authority_is_rejected_without_writing_config(self):
        self.authority.unlink()
        with self.assertRaisesRegex(ValueError, "regular"):
            realm.configure(self.authority, self.props)
        self.assertFalse(self.props.exists())

    def test_mismatched_existing_properties_are_preserved(self):
        self.props.parent.mkdir()
        self.props.write_text("custom", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "not overwriting"):
            realm.configure(self.authority, self.props)
        with self.assertRaisesRegex(ValueError, "unrecognized"):
            realm.disable(self.props)
        self.assertEqual("custom", self.props.read_text())

    def test_untrusted_urls_and_extra_fields_fail_closed(self):
        for uri in ["http://localhost/realms.json", "https://example.com/realms.json", "https://localhost:8443/realms.json", "https://localhost/realms.json?query=1"]:
            with self.subTest(uri=uri):
                self.write_authority({**self.valid, "catalogUri": uri})
                with self.assertRaisesRegex(ValueError, "loopback"):
                    realm.configure(self.authority, self.props)
        self.write_authority({**self.valid, "unexpected": 1})
        with self.assertRaisesRegex(ValueError, "only"):
            realm.configure(self.authority, self.props)
        self.assertFalse(self.props.exists())

    def test_symlinked_input_and_output_fail_closed(self):
        target = self.root / "original.json"
        target.write_bytes(self.authority.read_bytes())
        self.authority.unlink()
        self.authority.symlink_to(target)
        with self.assertRaisesRegex(ValueError, "non-symlinked"):
            realm.configure(self.authority, self.props)
        self.authority.unlink()
        self.write_authority(self.valid)
        self.props.parent.mkdir()
        self.props.symlink_to(target)
        with self.assertRaisesRegex(ValueError, "symbolic link"):
            realm.configure(self.authority, self.props)

    def test_xml_escapes_paths(self):
        name = self.root / "a&b" / "authority.json"
        name.parent.mkdir()
        name.write_bytes(self.authority.read_bytes())
        realm.configure(name, self.props)
        self.assertEqual(str(name), ElementTree.parse(self.props).findtext("PropertyGroup/OpenConquerCatalogAuthorityFile"))


if __name__ == "__main__":
    unittest.main()
