#!/usr/bin/env python3
"""Regenerate the WVFLDPK V1 golden binary fixtures from canonical sidecars.

Run from the repository root:
    python scripts/field-package/regenerate_golden_fixtures.py

Tile bytes are sourced from the checked-in decoded_hex values in golden-fixtures.json.
The script intentionally supports only the fixture-only codec-0 profiles; it does not
select production sample encodings, quantization, category codes, or compression.
"""
from __future__ import annotations

import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / "docs/architecture/core-domain/fixtures/field-package-v1"
INVENTORY_PATH = FIXTURES / "golden-fixtures.json"
HEADER_SIZE = 72
DIRECTORY_HEADER_SIZE = 16
DIRECTORY_ENTRY_SIZE = 72


def align8(value: int) -> int:
    return (value + 7) & ~7


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def build_package(manifest: bytes, tiles: list[dict]) -> bytes:
    parsed = json.loads(manifest)
    if manifest.startswith(b"\\xef\\xbb\\xbf") or manifest.endswith((b"\\n", b"\\r")):
        raise ValueError("Manifest sidecar must be UTF-8 JCS without BOM or trailing newline.")
    if parsed["format"] != {"major": 1, "minor": 0}:
        raise ValueError("Only WVFLDPK 1.0 fixtures are supported.")
    ordered = sorted(tiles, key=lambda item: (item["tile_y"], item["tile_x"]))
    grid = parsed["grid"]
    expected_count = ((grid["width"] + grid["tile_width"] - 1) // grid["tile_width"]) * (
        (grid["height"] + grid["tile_height"] - 1) // grid["tile_height"]
    )
    if len(ordered) != expected_count:
        raise ValueError("Fixture inventory does not contain the complete dense native grid.")

    directory_offset = align8(HEADER_SIZE + len(manifest))
    directory_length = DIRECTORY_HEADER_SIZE + DIRECTORY_ENTRY_SIZE * len(ordered)
    payload_offset = align8(directory_offset + directory_length)
    cursor = payload_offset
    payloads: list[tuple[int, bytes]] = []
    entries: list[bytes] = []

    planes = parsed["value_encoding"]["planes"]
    bytes_per_cell = sum(plane["bytes_per_cell"] for plane in planes)
    codec_ids = {codec["codec_id"] for codec in parsed["codec_registry"]}
    if 0 not in codec_ids:
        raise ValueError("V1 fixture must declare codec 0 (none).")

    for tile in ordered:
        raw = bytes.fromhex(tile["decoded_hex"])
        width, height = tile["valid_width"], tile["valid_height"]
        expected_decoded = width * height * bytes_per_cell
        digest = sha256(raw)
        if len(raw) != expected_decoded or len(raw) != tile["decoded_length"]:
            raise ValueError("Decoded tile length does not match the manifest plane layout.")
        if digest != tile["tile_sha256"]:
            raise ValueError("Decoded tile SHA-256 does not match the fixture inventory.")
        cursor = align8(cursor)
        payloads.append((cursor, raw))
        entries.append(
            struct.pack(
                "<IIHHHHQQQ32s",
                tile["tile_x"],
                tile["tile_y"],
                width,
                height,
                0,
                0,
                cursor,
                len(raw),
                len(raw),
                bytes.fromhex(digest),
            )
        )
        cursor += len(raw)

    package_length = cursor
    directory = struct.pack("<HHIQ", 1, 0, DIRECTORY_ENTRY_SIZE, len(entries)) + b"".join(entries)
    header = struct.pack(
        "<8sHHIIIQQQQQQ",
        b"WVFLDPK\\0",
        1,
        0,
        HEADER_SIZE,
        0,
        0,
        HEADER_SIZE,
        len(manifest),
        directory_offset,
        directory_length,
        payload_offset,
        package_length,
    )
    output = bytearray(header)
    output.extend(manifest)
    output.extend(b"\\0" * (directory_offset - len(output)))
    output.extend(directory)
    output.extend(b"\\0" * (payload_offset - len(output)))
    for offset, raw in payloads:
        if len(output) > offset:
            raise ValueError("Calculated tile payload ranges overlap.")
        output.extend(b"\\0" * (offset - len(output)))
        output.extend(raw)
    if len(output) != package_length:
        raise ValueError("Calculated package length does not match emitted bytes.")
    return bytes(output)


def main() -> None:
    inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
    for fixture in inventory["fixtures"]:
        manifest_path = FIXTURES / fixture["manifest_file"]
        manifest = manifest_path.read_bytes()
        package = build_package(manifest, fixture["tiles"])
        package_path = FIXTURES / fixture["package_file"]
        package_path.write_bytes(package)
        fixture["manifest_length"] = len(manifest)
        fixture["manifest_sha256"] = sha256(manifest)
        fixture["package_length"] = len(package)
        fixture["package_sha256"] = sha256(package)

    inventory["status"] = (
        "regenerated canonical WVFLDPK 1.0 golden packages whose embedded manifests are "
        "byte-identical to the current canonical sidecars; tile payload bytes and per-tile "
        "hashes are preserved. Fixture-only profiles remain test-only."
    )
    inventory["manifest_contract"] = (
        "Each package embeds the exact current JCS UTF-8 sidecar manifest. Header, directory "
        "and payload offsets/lengths are recalculated with 8-byte alignment; tile payload "
        "bytes and tile hashes are unchanged."
    )
    INVENTORY_PATH.write_text(json.dumps(inventory, indent=2) + "\\n", encoding="utf-8")


if __name__ == "__main__":
    main()
