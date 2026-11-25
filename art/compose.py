#!/usr/bin/env python3
"""Rebuild Kilnwarden's marketing art from its original generated paintings.

Run: python3 art/compose.py
Only source/ images are used; all outputs stay beside this script. Typography
is drawn with Noto Serif fonts. Generation prompts are recorded in source/.
"""
from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps, PngImagePlugin

OUT = Path(__file__).resolve().parent
SRC = OUT / "source"
TITLE = "Kilnwarden"
MAIN_LINE = "Web admin for Enshrouded servers · a Kiln mod"
# MAIN_LINE drawn on two lines: what it is, then whose mod it is
MAIN_TOP, MAIN_BOTTOM = "Web admin for Enshrouded servers", "a Kiln mod"
SOCIAL_LINE = MAIN_LINE
DISCORD_LINE = "Your server, in any browser"
WIDE = "hero_wide.png"
LANDSCAPE = "hero_landscape.png"
SQUARE = "hero_square.png"
ICON = "icon.png"
BOLD = "/usr/share/fonts/noto/NotoSerifDisplay-Bold.ttf"
REGULAR = "/usr/share/fonts/noto/NotoSerif-Regular.ttf"
TITLE_COLOR = (255, 242, 215, 255)
SECONDARY_COLOR = (236, 226, 208, 255)
ACCENT_COLOR = (249, 199, 79, 255)  # #F9C74F, the Kiln pages' gold
SIZES = {
    "nexus_main_1280x720.png": (1280, 720),
    "thumb_1920x1080.png": (1920, 1080),
    "nexus_banner_1920x480.png": (1920, 480),
    "github_social_1280x640.png": (1280, 640),
    "discord_1200x630.png": (1200, 630),
    "thumb_512.png": (512, 512),
    "icon_256.png": (256, 256),
    "contact_sheet.png": (24 + 7 * 416, 368),
}
SOURCES = {
    "nexus_main_1280x720.png": LANDSCAPE,
    "thumb_1920x1080.png": LANDSCAPE,
    "nexus_banner_1920x480.png": WIDE,
    "github_social_1280x640.png": LANDSCAPE,
    "discord_1200x630.png": SQUARE,
    "thumb_512.png": ICON,
    "icon_256.png": ICON,
    "contact_sheet.png": "All seven final images",
}
TEXT_BOUNDS: dict[str, list[tuple[int, int, int, int]]] = {}


def art(name: str) -> Image.Image:
    with Image.open(SRC / name) as image:
        return image.convert("RGBA")


def cover(name: str, size: tuple[int, int], center=(0.5, 0.5)) -> Image.Image:
    return ImageOps.fit(art(name), size, Image.Resampling.LANCZOS, centering=center)


def gradient_scrim(image: Image.Image, stops: list[tuple[float, int]],
                   vertical=False, color=(5, 20, 26)) -> Image.Image:
    """A smooth opacity ramp preserves the illustration under readable type."""
    length = image.height if vertical else image.width
    ramp = []
    for index in range(length):
        t = index / max(1, length - 1)
        for (a, va), (b, vb) in zip(stops, stops[1:]):
            if a <= t <= b:
                opacity = round(va + (vb - va) * (t - a) / (b - a))
                break
        else:
            opacity = stops[0][1] if t < stops[0][0] else stops[-1][1]
        ramp.append(opacity)
    strip = Image.new("L", (1, length) if vertical else (length, 1))
    strip.putdata(ramp)
    tint = Image.new("RGBA", image.size, (*color, 0))
    tint.putalpha(strip.resize(image.size))
    return Image.alpha_composite(image, tint)


def font_to_fit(text: str, font_path: str, maximum: int, width: int):
    lines = text.split("\n")
    for size in range(maximum, 11, -1):
        font = ImageFont.truetype(font_path, size)
        if max(font.getbbox(line)[2] - font.getbbox(line)[0] for line in lines) <= width:
            return font
    raise ValueError(f"Text does not fit: {text!r}")


def lettering(image: Image.Image, text: str, xy: tuple[int, int],
              font: ImageFont.FreeTypeFont, color=TITLE_COLOR,
              line_spacing=0, shadow_offset=(0, 3)):
    """Draw exact copy at its visible top-left and reject clipped lettering."""
    x, y = xy
    advance = font.size + line_spacing
    mask = Image.new("L", image.size)
    draw = ImageDraw.Draw(mask)
    for index, line in enumerate(text.split("\n")):
        left, top, right, bottom = font.getbbox(line)
        bounds = (x, y + index * advance, x + right - left,
                  y + index * advance + bottom - top)
        assert bounds[0] >= 0 and bounds[1] >= 0
        assert bounds[2] <= image.width and bounds[3] <= image.height, (text, bounds)
        draw.text((x - left, y + index * advance - top), line, font=font, fill=255)
    shadow_mask = Image.new("L", image.size)
    shadow_mask.paste(mask, shadow_offset)
    shadow_mask = shadow_mask.filter(ImageFilter.GaussianBlur(image.width / 380))
    shadow = Image.new("RGBA", image.size, (0, 5, 10, 0))
    shadow.putalpha(shadow_mask.point(lambda value: round(value * .78)))
    image.alpha_composite(shadow)
    ink = Image.new("RGBA", image.size, color)
    ink.putalpha(mask)
    image.alpha_composite(ink)
    return mask.getbbox()


def save(image: Image.Image, name: str, text: list[str], boxes=()):
    assert image.size == SIZES[name], (name, image.size)
    TEXT_BOUNDS[name] = list(boxes)
    meta = PngImagePlugin.PngInfo()
    meta.add_text("Title", TITLE)
    meta.add_text("Artwork", SOURCES[name])
    meta.add_text("Visible copy", json.dumps(text, ensure_ascii=False))
    meta.add_text("Text bounds", json.dumps(boxes))
    meta.add_text("Creation", "Built-in image_gen artwork; Pillow composition")
    image.convert("RGB").save(OUT / name, pnginfo=meta, optimize=True)


def make_main(name="nexus_main_1280x720.png"):
    """Title and line in the dark stone wall on the left (about 45% of hero_landscape)."""
    size = SIZES[name]
    k = size[0] / 1280
    px = lambda value: round(value * k)
    image = cover(LANDSCAPE, size)
    image = gradient_scrim(image, [(0, 120), (.30, 90), (.44, 10), (.55, 0), (1, 0)])
    title_font = font_to_fit(TITLE, BOLD, px(104), px(500))
    boxes = [lettering(image, TITLE, (px(64), px(250)), title_font, shadow_offset=(0, px(3)))]
    top = boxes[0][3] + px(26)
    line_font = font_to_fit(MAIN_TOP, REGULAR, px(30), px(500))
    boxes.append(lettering(image, MAIN_TOP, (px(67), top), line_font, SECONDARY_COLOR, shadow_offset=(0, px(2))))
    boxes.append(lettering(image, MAIN_BOTTOM, (px(67), boxes[-1][3] + px(14)), line_font, ACCENT_COLOR, shadow_offset=(0, px(2))))
    for box in boxes:
        assert box[2] <= size[0] * .45, box
    save(image, name, [TITLE, MAIN_LINE], boxes)


def make_banner():
    """The empty teal sky on the left of hero_wide (about 40%)."""
    name = "nexus_banner_1920x480.png"
    image = cover(WIDE, SIZES[name], center=(.5, .55))
    image = gradient_scrim(image, [(0, 90), (.30, 60), (.40, 0), (1, 0)])
    font = font_to_fit(TITLE, BOLD, 132, 640)
    box = lettering(image, TITLE, (96, 150), font)
    line = font_to_fit(MAIN_TOP, REGULAR, 36, 640)
    box2 = lettering(image, MAIN_TOP, (100, box[3] + 30), line, SECONDARY_COLOR, shadow_offset=(0, 2))
    assert max(box[2], box2[2]) < image.width * .40 and box2[3] <= image.height - 48, (box, box2)
    save(image, name, [TITLE, MAIN_TOP], [box, box2])


def make_social():
    name = "github_social_1280x640.png"
    image = cover(LANDSCAPE, SIZES[name], center=(.5, .55))
    image = gradient_scrim(image, [(0, 130), (.30, 95), (.45, 5), (.55, 0), (1, 0)])
    font = font_to_fit(TITLE, BOLD, 96, 470)
    boxes = [lettering(image, TITLE, (96, 214), font)]
    line_font = font_to_fit(MAIN_TOP, REGULAR, 28, 470)
    boxes.append(lettering(image, MAIN_TOP, (99, boxes[0][3] + 26), line_font, SECONDARY_COLOR, shadow_offset=(0, 2)))
    boxes.append(lettering(image, MAIN_BOTTOM, (99, boxes[-1][3] + 12), line_font, ACCENT_COLOR, shadow_offset=(0, 2)))
    # Keep GitHub's copy at least 80 pixels inside all four edges.
    for left, top, right, bottom in boxes:
        assert left >= 80 and top >= 80 and right <= 1200 and bottom <= 560
    save(image, name, [TITLE, SOCIAL_LINE], boxes)
    jpg = OUT / "github_social_1280x640.jpg"
    image.convert("RGB").save(jpg, quality=92, optimize=True, progressive=True, subsampling=0)
    assert jpg.stat().st_size < 1_000_000, (jpg.name, jpg.stat().st_size)


def make_discord():
    name = "discord_1200x630.png"
    # the background is the painting's empty sky (its top quarter), stretched and blurred: no blurred copy of the tower
    sky = art(SQUARE).crop((0, 0, 1024, 220))
    image = sky.resize(SIZES[name], Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(30))
    image = gradient_scrim(image, [(0, 150), (.4, 110), (.62, 20), (1, 0)])
    subject = art(SQUARE).resize((630, 630), Image.Resampling.LANCZOS)
    feather = Image.new("L", (630, 1))
    feather.putdata([round(255 * min(1, x / 180)) for x in range(630)])
    image.paste(subject, (570, 0), feather.resize(subject.size))
    font = font_to_fit(TITLE, BOLD, 92, 500)
    boxes = [lettering(image, TITLE, (58, 225), font)]
    secondary = font_to_fit(DISCORD_LINE, REGULAR, 29, 500)
    boxes.append(lettering(image, DISCORD_LINE, (61, boxes[0][3] + 34), secondary,
                           SECONDARY_COLOR, shadow_offset=(0, 2)))
    save(image, name, [TITLE, DISCORD_LINE], boxes)


def make_icons():
    for name in ("thumb_512.png", "icon_256.png"):
        save(art(ICON).resize(SIZES[name], Image.Resampling.LANCZOS), name, [])


def make_contact_sheet():
    image = Image.new("RGBA", SIZES["contact_sheet.png"], (10, 25, 33, 255))
    draw = ImageDraw.Draw(image)
    draw.text((24, 14), TITLE, font=ImageFont.truetype(BOLD, 29), fill=TITLE_COLOR)
    label = ImageFont.truetype(REGULAR, 17)
    for index, name in enumerate(n for n in SIZES if n != "contact_sheet.png"):
        x = 24 + index * 416
        with Image.open(OUT / name) as final:
            tile = final.convert("RGBA")
        tile.thumbnail((392, 222), Image.Resampling.LANCZOS)
        image.alpha_composite(tile, (x + (392 - tile.width) // 2,
                                     78 + (222 - tile.height) // 2))
        draw.text((x, 323), name, font=label, fill=SECONDARY_COLOR)
    save(image, "contact_sheet.png", [TITLE, "Previews of all seven finals"])


def verify():
    expected_copy = {
        "nexus_main_1280x720.png": [TITLE, MAIN_LINE],
        "thumb_1920x1080.png": [TITLE, MAIN_LINE],
        "nexus_banner_1920x480.png": [TITLE, MAIN_TOP],
        "github_social_1280x640.png": [TITLE, SOCIAL_LINE],
        "discord_1200x630.png": [TITLE, DISCORD_LINE],
        "thumb_512.png": [],
        "icon_256.png": [],
    }
    for name, size in SIZES.items():
        with Image.open(OUT / name) as image:
            image.load()
            assert image.size == size, (name, image.size, size)
            assert image.mode == "RGB"
            assert image.info["Artwork"] == SOURCES[name]
            if name in expected_copy:
                assert json.loads(image.info["Visible copy"]) == expected_copy[name]
            for left, top, right, bottom in json.loads(image.info["Text bounds"]):
                assert 0 <= left < right <= size[0] and 0 <= top < bottom <= size[1]
        print(f"{name}: {size[0]}x{size[1]} | {(OUT / name).stat().st_size:,} bytes")
    with Image.open(OUT / "github_social_1280x640.jpg") as image:
        image.load()
        assert image.size == SIZES["github_social_1280x640.png"]
        assert image.mode == "RGB"
    print("github_social_1280x640.jpg: 1280x640 | size below 1 MB")


def main():
    make_main()
    make_main("thumb_1920x1080.png")
    make_banner()
    make_social()
    make_discord()
    make_icons()
    make_contact_sheet()
    verify()


if __name__ == "__main__":
    main()
