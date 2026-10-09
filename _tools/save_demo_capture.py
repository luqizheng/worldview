#!/usr/bin/env python3
"""Save the Godot MCP screenshot (base64) of world_canvas_demo.tscn to a local PNG.

Usage:
    python _tools/save_demo_capture.py <base64_string> <out_png_path>
"""
from __future__ import annotations

import base64
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) < 3:
        print("usage: save_demo_capture.py <base64> <out_png>")
        return 2
    raw = sys.argv[1].strip()
    out = Path(sys.argv[2])
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(base64.b64decode(raw))
    print(f"wrote {out} ({out.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
