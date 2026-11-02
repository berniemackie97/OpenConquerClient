"""Exercise the compiled command-line host in independent processes."""

import json
from pathlib import Path
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / "tools/OpenConquerStudio.Tool/bin/Release/net10.0/OpenConquerStudio.Tool.dll"


class StudioCliTests(unittest.TestCase):
    def run_tool(self, *args, expected=0):
        result = subprocess.run(
            ["dotnet", str(TOOL), *map(str, args)],
            capture_output=True, text=True, timeout=30, check=False,
        )
        self.assertEqual(expected, result.returncode, result.stdout + result.stderr)
        return result

    def test_authoring_build_and_verification_across_processes(self):
        with tempfile.TemporaryDirectory(prefix="studio-cli-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Independent game")
            header = bytearray(18)
            header[2], header[12], header[14], header[16], header[17] = 10, 2, 1, 32, 8
            source = root / "two-colors.tga"
            payload = header + bytes([1, 0, 0, 255, 255, 0, 255, 0, 255])
            source.write_bytes(payload)
            asset = self.run_tool("import", project, source, "Sprite").stdout.strip()
            self.run_tool("edit", project, asset, "Edited sprite", "true", "1")
            self.run_tool("new-texture", project, "New color", 16, 8, 20, 40, 80)
            self.run_tool("build", project, root / "first")
            self.run_tool("build", project, root / "second")
            self.run_tool("verify", root / "first")
            first = {p.name: p.read_bytes() for p in (root / "first").iterdir()}
            second = {p.name: p.read_bytes() for p in (root / "second").iterdir()}
            self.assertEqual(first, second)
            manifest = json.loads(first["content.json"])
            entry = next(t for t in manifest["textures"] if t["name"] == "Edited sprite")
            self.assertEqual((1, 2), (entry["width"], entry["height"]))
            texture = root / "first" / (asset.replace("-", "") + ".octex")
            self.assertEqual(bytes([0, 255, 0, 255, 255, 0, 0, 255]), texture.read_bytes()[68:])
            self.assertEqual(payload, source.read_bytes())
            self.run_tool("build", project, root / "first", expected=1)
            texture.write_bytes(texture.read_bytes()[:-1])
            self.run_tool("verify", root / "first", expected=1)

    def test_existing_client_artwork_can_be_authored_and_compiled(self):
        with tempfile.TemporaryDirectory(prefix="studio-cli-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Game artwork")
            catalog = self.run_tool("list-artwork").stdout
            key = "conquer/login/account/row"
            self.assertIn(key, catalog)
            asset = self.run_tool("import-artwork", project, key).stdout.strip()
            self.run_tool("edit", project, asset, "New row", "true", "1")
            self.run_tool("build", project, root / "build")
            self.run_tool("verify", root / "build")
            header = bytearray(18)
            header[2], header[12], header[14], header[16], header[17] = 10, 2, 1, 32, 8
            replacement = root / "replacement.tga"
            replacement.write_bytes(header + bytes([1, 0, 0, 255, 255, 0, 255, 0, 255]))
            self.run_tool("replace", project, asset, replacement)
            self.run_tool("build", project, root / "replacement-build")
            replaced = json.loads((root / "replacement-build/content.json").read_text())
            self.assertEqual(asset, replaced["textures"][0]["id"])
            self.assertEqual((1, 2), (replaced["textures"][0]["width"], replaced["textures"][0]["height"]))
            authored = json.loads((project / "project.ocstudio").read_text())
            self.assertEqual(key, authored["assets"][0]["sourceKey"])
            manifest = json.loads((root / "build/content.json").read_text())
            self.assertEqual((25, 166), (manifest["textures"][0]["width"], manifest["textures"][0]["height"]))

    def test_bad_import_leaves_saved_project_unchanged(self):
        with tempfile.TemporaryDirectory(prefix="studio-cli-") as temporary:
            root = Path(temporary).resolve()
            project = root / "project"
            project.mkdir()
            self.run_tool("create", project, "Game")
            before = (project / "project.ocstudio").read_bytes()
            source = root / "broken.tga"
            source.write_bytes(b"broken")
            self.run_tool("import", project, source, "Bad", expected=1)
            self.assertEqual(before, (project / "project.ocstudio").read_bytes())
            self.run_tool("edit", project, "invalid-id", "Bad", "true", "0", expected=1)
            self.run_tool("unknown", expected=2)


if __name__ == "__main__":
    unittest.main()
