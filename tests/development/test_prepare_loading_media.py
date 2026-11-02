import importlib.util
import struct
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


def png(width, height, pixels):
    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    rows = b"".join(b"\0" + bytes(pixels[y * width * 4:(y + 1) * width * 4])
                    for y in range(height))
    return (b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", header) +
            png_chunk(b"IDAT", zlib.compress(rows)) + png_chunk(b"IEND", b""))


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


class MediaPreparationTests(unittest.TestCase):
    def test_round_trip_preserves_pixels_and_orientation(self):
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "image.png"
            pixels = bytes((255, 0, 10, 255, 255, 0, 10, 255,
                            4, 22, 188, 63, 200, 178, 10, 0))
            source.write_bytes(png(2, 2, pixels))
            width, height, rows = media.png_rows(source)
            self.assertEqual((width, height), (2, 2))
            self.assertEqual(decode_tga(media.tga(width, height, rows)), pixels)

    def test_rejects_corrupted_png(self):
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "image.png"
            data = bytearray(png(1, 1, bytes((4, 22, 188, 63))))
            data[-8] ^= 1
            source.write_bytes(data)
            with self.assertRaises(ValueError):
                media.png_rows(source)


if __name__ == "__main__":
    unittest.main()
