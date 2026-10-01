#!/usr/bin/env python3
"""Crop and upscale a rectangular region of an image for legible inspection.

Built for verifying small UI elements in `_staging/screenshot.png` (badges,
chips, cost discs, status floaters) that are illegible at the raw capture
resolution. The capture window is small (typically 1152x648), so a 16-18px
on-tile badge is unreadable until magnified -- this hand-rolled crop+upscale
was repeated ~6 times in one session before it became a tool.

This exists so you can VERIFY a visual element yourself instead of trusting a
"the screenshot confirms it renders" report -- read the artifact, don't relay a
description of it.

Usage:
    python Tools/zoom_region.py <image> <x> <y> <w> <h> [--scale N] [--out PATH]
    python Tools/zoom_region.py <image> --grid [--out PATH]

Args:
    image            Path to a PNG/JPG (e.g. _staging/screenshot.png).
    x y w h          Crop rectangle in source pixels (top-left origin).
    --scale N        Upscale factor (default 8). LANCZOS for photos/icons,
                     pass --nearest for crisp pixel-art edges.
    --nearest        Use NEAREST resampling instead of LANCZOS.
    --out PATH       Output path (default: _staging/screenshots/_zoom.png).
    --grid           Skip cropping; overlay a labeled 8x8 coordinate grid on the
                     whole image so you can read off x/y/w/h for a real crop.

Exit codes: 0 ok, 1 bad args / missing file / Pillow not installed.
"""
import argparse
import os
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:
    print("ERROR: Pillow not installed. `pip install pillow`", file=sys.stderr)
    sys.exit(1)

DEFAULT_OUT = os.path.join("_staging", "screenshots", "_zoom.png")


def _save(img, out):
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    img.save(out)
    print(f"saved {out}  ({img.size[0]}x{img.size[1]})")


def main(argv=None):
    p = argparse.ArgumentParser(description="Crop+upscale an image region for inspection.")
    p.add_argument("image")
    p.add_argument("coords", nargs="*", type=int, metavar="x y w h",
                   help="crop rectangle in source pixels")
    p.add_argument("--scale", type=int, default=8)
    p.add_argument("--nearest", action="store_true")
    p.add_argument("--out", default=DEFAULT_OUT)
    p.add_argument("--grid", action="store_true",
                   help="overlay a labeled coordinate grid on the whole image")
    args = p.parse_args(argv)

    if not os.path.isfile(args.image):
        print(f"ERROR: no such file: {args.image}", file=sys.stderr)
        return 1
    im = Image.open(args.image).convert("RGB")

    if args.grid:
        # Coordinate-finder: draw an 8x8 grid with pixel labels so the next
        # invocation can target the real region without guessing.
        g = im.copy()
        d = ImageDraw.Draw(g)
        w, h = g.size
        for i in range(1, 8):
            gx, gy = w * i // 8, h * i // 8
            d.line([(gx, 0), (gx, h)], fill=(255, 0, 128), width=1)
            d.line([(0, gy), (w, gy)], fill=(255, 0, 128), width=1)
            d.text((gx + 2, 2), str(gx), fill=(255, 0, 128))
            d.text((2, gy + 2), str(gy), fill=(255, 0, 128))
        _save(g, args.out)
        print(f"source size: {w}x{h}. Pick x/y/w/h off the grid, then re-run without --grid.")
        return 0

    if len(args.coords) != 4:
        print("ERROR: provide exactly 4 ints: x y w h (or use --grid to find them).",
              file=sys.stderr)
        return 1
    x, y, w, h = args.coords
    if w <= 0 or h <= 0:
        print("ERROR: w and h must be > 0.", file=sys.stderr)
        return 1

    sw, sh = im.size
    x2, y2 = min(x + w, sw), min(y + h, sh)
    if x >= sw or y >= sh:
        print(f"ERROR: crop origin ({x},{y}) is outside the {sw}x{sh} image.", file=sys.stderr)
        return 1
    crop = im.crop((max(0, x), max(0, y), x2, y2))
    resample = Image.NEAREST if args.nearest else Image.LANCZOS
    crop = crop.resize((crop.width * args.scale, crop.height * args.scale), resample)
    _save(crop, args.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
