from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "art" / "source" / "easygame-environment-source-v1.png"
OUTPUT = ROOT / "client" / "public" / "assets"

TILES = {
    "grass": (18, 18, 308, 308),
    "dirt": (327, 18, 618, 308),
    "wild": (636, 18, 927, 308),
    "soil": (946, 18, 1236, 308),
}


def make_mirrored_tile(source: Image.Image, bounds: tuple[int, int, int, int]) -> Image.Image:
    left, top, right, bottom = bounds
    inset = 5
    tile = source.crop((left + inset, top + inset, right - inset, bottom - inset))
    width, height = tile.size
    output = Image.new("RGB", (width * 2, height * 2))
    output.paste(tile, (0, 0))
    output.paste(tile.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (width, 0))
    output.paste(tile.transpose(Image.Transpose.FLIP_TOP_BOTTOM), (0, height))
    output.paste(
        tile.transpose(Image.Transpose.FLIP_LEFT_RIGHT).transpose(
            Image.Transpose.FLIP_TOP_BOTTOM
        ),
        (width, height),
    )
    return output


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source = Image.open(SOURCE).convert("RGB")
    for name, bounds in TILES.items():
        tile = make_mirrored_tile(source, bounds)
        tile.save(
            OUTPUT / f"terrain-{name}-v1.webp",
            format="WEBP",
            lossless=True,
            method=6,
        )


if __name__ == "__main__":
    main()
