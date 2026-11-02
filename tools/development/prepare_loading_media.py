#!/usr/bin/env python3
"""Build local loading-screen content from the verified 5517 extraction."""

import argparse
import hashlib
import json
import shutil
import struct
import tempfile
import zlib
from pathlib import Path

ARTWORK = (
    "images_374", "images_376", "images_381", "images_382",
    "shapes_261", "shapes_375", "shapes_377", "shapes_379",
    "shapes_383", "shapes_389", "shapes_390", "shapes_391",
    "shapes_392", "sprite380",
    *(f"sprite387_f{i}" for i in range(1, 8)),
)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def png_rows(path):
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Invalid PNG: {path}")
    offset = 8
    compressed = []
    width = height = None
    while offset < len(data):
        if offset + 12 > len(data):
            raise ValueError(f"Truncated PNG: {path}")
        size = int.from_bytes(data[offset:offset + 4], "big")
        kind = data[offset + 4:offset + 8]
        if size > len(data) - offset - 12:
            raise ValueError(f"Invalid PNG chunk: {path}")
        payload = data[offset + 8:offset + 8 + size]
        checksum = int.from_bytes(data[offset + 8 + size:offset + size + 12], "big")
        if zlib.crc32(kind + payload) & 0xffffffff != checksum:
            raise ValueError(f"PNG CRC mismatch: {path}")
        offset += size + 12
        if kind == b"IHDR":
            if width is not None or len(payload) != 13:
                raise ValueError(f"Invalid PNG header: {path}")
            width, height, depth, color, compression, filtering, interlace = struct.unpack(">IIBBBBB", payload)
            if (width < 1 or height < 1 or width * height > 1048576 or
                    (depth, color, compression, filtering, interlace) != (8, 6, 0, 0, 0)):
                raise ValueError(f"Unsupported PNG format: {path}")
        elif kind == b"IDAT":
            if width is None:
                raise ValueError(f"PNG data before header: {path}")
            compressed.append(payload)
        elif kind == b"IEND":
            if payload or offset != len(data):
                raise ValueError(f"Unexpected data after PNG end: {path}")
            break
        elif not kind[0] & 0x20:
            raise ValueError(f"Unsupported PNG chunk: {kind!r}")
    else:
        raise ValueError(f"PNG has no end marker: {path}")
    if not compressed:
        raise ValueError(f"PNG has no image data: {path}")
    stride = width * 4
    expected = height * (stride + 1)
    inflater = zlib.decompressobj()
    decoded = inflater.decompress(b"".join(compressed), expected + 1)
    if (len(decoded) != expected or not inflater.eof or
            inflater.unused_data or inflater.unconsumed_tail):
        raise ValueError(f"Invalid decompressed PNG length: {path}")
    result = []
    previous = bytearray(stride)
    for y in range(height):
        pos = y * (stride + 1)
        kind = decoded[pos]
        row = bytearray(decoded[pos + 1:pos + 1 + stride])
        for i in range(stride):
            a = row[i - 4] if i >= 4 else 0
            b = previous[i]
            c = previous[i - 4] if i >= 4 else 0
            if kind == 0:
                delta = 0
            elif kind == 1:
                delta = a
            elif kind == 2:
                delta = b
            elif kind == 3:
                delta = (a + b) // 2
            elif kind == 4:
                p = a + b - c
                errors = (abs(p - a), abs(p - b), abs(p - c))
                delta = (a, b, c)[errors.index(min(errors))]
            else:
                raise ValueError(f"Unknown PNG filter: {kind}")
            row[i] = (row[i] + delta) & 255
        result.append(row)
        previous = row
    return width, height, result


def tga(width, height, rows):
    header = bytearray(18)
    header[2] = 10
    struct.pack_into("<HH", header, 12, width, height)
    header[16:18] = bytes((32, 8))
    result = bytearray(header)
    for row in reversed(rows):
        pixels = [bytes((row[i + 2], row[i + 1], row[i], row[i + 3]))
                  for i in range(0, width * 4, 4)]
        i = 0
        while i < width:
            run = 1
            while i + run < width and run < 128 and pixels[i] == pixels[i + run]:
                run += 1
            if run > 1:
                result.append(0x80 | (run - 1))
                result.extend(pixels[i])
                i += run
                continue
            start = i
            i += 1
            while i < width and i - start < 128:
                if i + 1 < width and pixels[i] == pixels[i + 1]:
                    break
                i += 1
            result.append(i - start - 1)
            for pixel in pixels[start:i]:
                result.extend(pixel)
    return bytes(result)


def prepare(extracted, client_root, destination):
    if destination.exists():
        raise FileExistsError(f"Output already exists: {destination}")
    manifest = json.loads((extracted / "manifest.json").read_text(encoding="utf-8"))
    verification = json.loads((extracted / "verification.json").read_text(encoding="utf-8"))
    timelines = json.loads((extracted / "timelines.json").read_text(encoding="utf-8"))
    source_hash = sha256((client_root / "data/main/start.dat").read_bytes())
    if (source_hash != manifest["sourceSha256"] or
            source_hash != verification["sourceSha256"] or
            verification.get("assetPixelOriginIndependentlyReexported") is not False or
            set(verification.get("sourceTimelinesVerified", {})) != {"262", "380", "387", "393", "394"} or
            timelines.get("sources", {}).get("start.dat") != source_hash):
        raise ValueError("Extracted loading media is not verified against the client source")
    expected = {"timelines.json", *(f"assets/{name}.png" for name in ARTWORK)}
    if set(manifest["files"]) != expected:
        raise ValueError("Unexpected prepared-media inputs")
    for path in sorted(expected):
        data = (extracted / path).read_bytes()
        entry = manifest["files"][path]
        if len(data) != entry["length"] or sha256(data) != entry["sha256"]:
            raise ValueError(f"Source media integrity mismatch: {path}")
    timeline = {str(symbol): timelines[f"start.dat:{symbol}"] for symbol in (262, 380, 387, 393, 394)}
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix=f".{destination.name}-", dir=destination.parent))
    try:
        output = staging / "ui/loading"
        art = output / "artwork"
        art.mkdir(parents=True)
        (output / "timeline.json").write_text(
            json.dumps(timeline, separators=(",", ":")) + "\n", encoding="utf-8")
        for name in ARTWORK:
            width, height, rows = png_rows(extracted / "assets" / f"{name}.png")
            (art / f"{name}.tga").write_bytes(tga(width, height, rows))
        staging.rename(destination)
    finally:
        if staging.exists():
            shutil.rmtree(staging)
    print(f"Prepared {len(ARTWORK)} artwork images and timeline at {destination}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--extracted", type=Path, required=True)
    parser.add_argument("--client-root", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    options = parser.parse_args()
    prepare(options.extracted.expanduser().resolve(),
            options.client_root.expanduser().resolve(),
            options.destination.expanduser().resolve())


if __name__ == "__main__":
    main()
