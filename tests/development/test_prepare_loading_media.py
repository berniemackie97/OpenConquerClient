import hashlib
import importlib.util
import json
import struct
import tempfile
import unittest
import zlib
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[2] / "tools/development/prepare_loading_media.py"
SPEC = importlib.util.spec_from_file_location("prepare_loading_media", SCRIPT)
media = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(media)


def png_chunk(kind, content):
    return (struct.pack(">I", len(content)) + kind + content +
            struct.pack(">I", zlib.crc32(kind + content) & 0xffffffff))


def png(width, height, pixels, filter_type=0):
    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    scanlines = bytearray()
    previous = bytes(width * 4)
    for y in range(height):
        row = pixels[y * width * 4:(y + 1) * width * 4]
        filtered = bytearray()
        for i, value in enumerate(row):
            left = row[i - 4] if i >= 4 else 0
            above = previous[i]
            upper_left = previous[i - 4] if i >= 4 else 0
            if filter_type == 0:
                prediction = 0
            elif filter_type == 1:
                prediction = left
            elif filter_type == 2:
                prediction = above
            elif filter_type == 3:
                prediction = (left + above) // 2
            else:
                predictor = left + above - upper_left
                distances = (abs(predictor - left), abs(predictor - above),
                             abs(predictor - upper_left))
                prediction = (left, above, upper_left)[distances.index(min(distances))]
            filtered.append((value - prediction) & 255)
        scanlines.append(filter_type)
        scanlines.extend(filtered)
        previous = row
    return (b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", header) +
            png_chunk(b"IDAT", zlib.compress(scanlines)) + png_chunk(b"IEND", b""))


def decode_tga(data):
    assert data[2] == 10 and data[16:18] == bytes((32, 8))
    width, height = struct.unpack_from("<HH", data, 12)
    pixels = []
    offset = 18
    while len(pixels) < width * height:
        flag = data[offset]
        offset += 1
        count = (flag & 127) + 1
        if flag & 128:
            pixel = data[offset:offset + 4]
            offset += 4
            pixels.extend([pixel] * count)
        else:
            for _ in range(count):
                pixels.append(data[offset:offset + 4])
                offset += 4
    assert offset == len(data)
    lines = [pixels[i:i + width] for i in range(0, len(pixels), width)]
    return b"".join(bytes((p[2], p[1], p[0], p[3])) for row in reversed(lines) for p in row)


def create_extraction(root):
    extracted = root / "extracted"
    client = root / "client"
    (client / "data/main").mkdir(parents=True)
    (extracted / "assets").mkdir(parents=True)
    original = b"source-client-start.dat"
    (client / "data/main/start.dat").write_bytes(original)
    original_hash = hashlib.sha256(original).hexdigest()
    timelines = {"sources": {"start.dat": original_hash}}
    for symbol in (262, 380, 387, 393, 394):
        timelines[f"start.dat:{symbol}"] = {"frames": [symbol]}
    (extracted / "timelines.json").write_text(json.dumps(timelines), encoding="utf-8")
    for name in media.ARTWORK:
        (extracted / "assets" / f"{name}.png").write_bytes(
            png(1, 1, bytes((255, 12, 40, 190))))
    paths = ["timelines.json", *(f"assets/{name}.png" for name in media.ARTWORK)]
    manifest = {
        "sourceSha256": original_hash,
        "files": {
            name: {
                "length": len((extracted / name).read_bytes()),
                "sha256": hashlib.sha256((extracted / name).read_bytes()).hexdigest(),
            }
            for name in paths
        },
    }
    (extracted / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    verification = {
        "sourceSha256": original_hash,
        "assetPixelOriginIndependentlyReexported": False,
        "sourceTimelinesVerified": {str(symbol): True for symbol in (262, 380, 387, 393, 394)},
    }
    (extracted / "verification.json").write_text(json.dumps(verification), encoding="utf-8")
    return extracted, client


class MediaPreparationTests(unittest.TestCase):
    def test_png_filters_preserve_pixels_and_orientation(self):
        pixels = bytes((255, 0, 10, 255, 255, 0, 10, 255,
                        4, 22, 188, 63, 200, 178, 10, 0,
                        3, 17, 245, 255, 254, 2, 0, 70))
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "image.png"
            for filter_type in range(5):
                with self.subTest(filter_type=filter_type):
                    source.write_bytes(png(3, 2, pixels, filter_type))
                    width, height, rows = media.png_rows(source)
                    self.assertEqual((width, height), (3, 2))
                    self.assertEqual(b"".join(rows), pixels)
                    self.assertEqual(decode_tga(media.tga(width, height, rows)), pixels)

    def test_rejects_corrupted_png(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "image.png"
            data = bytearray(png(1, 1, bytes((4, 22, 188, 63))))
            data[-1] ^= 1
            source.write_bytes(data)
            with self.assertRaises(ValueError):
                media.png_rows(source)

    def test_prepare_publishes_complete_verified_media(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            extracted, client = create_extraction(root)
            destination = root / "prepared"
            media.prepare(extracted, client, destination)
            artwork = destination / "ui/loading/artwork"
            self.assertEqual(len(list(artwork.glob("*.tga"))), len(media.ARTWORK))
            self.assertEqual(
                decode_tga((artwork / "images_374.tga").read_bytes()),
                bytes((255, 12, 40, 190)),
            )
            timeline = json.loads((destination / "ui/loading/timeline.json").read_text())
            self.assertEqual(timeline["394"], {"frames": [394]})

    def test_prepare_rejects_source_mismatch_without_publishing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            extracted, client = create_extraction(root)
            (client / "data/main/start.dat").write_bytes(b"modified")
            destination = root / "prepared"
            with self.assertRaises(ValueError):
                media.prepare(extracted, client, destination)
            self.assertFalse(destination.exists())


if __name__ == "__main__":
    unittest.main()
