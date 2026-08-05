from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "art" / "source" / "easygame-environment-source-v1.png"
OUTPUT = ROOT / "client" / "public" / "assets"
PROCESSED = ROOT / "art" / "processed"
GAME_ATLAS = PROCESSED / "easygame-atlas-alpha-v1.png"
ENVIRONMENT_ATLAS = PROCESSED / "easygame-environment-alpha-v1.png"
HERO_WALK_ATLAS = PROCESSED / "easygame-hero-walk-alpha-v1.png"

TILES = {
    "grass": (18, 18, 308, 308),
    "dirt": (327, 18, 618, 308),
    "wild": (636, 18, 927, 308),
    "soil": (946, 18, 1236, 308),
}

WALK_COLUMNS = [0, 350, 630, 910, 1254]
WALK_ROWS = [0, 340, 630, 910, 1254]
WALK_HEIGHTS = [82, 82, 86, 82]
WALK_CELL_SIZE = 128
WALK_FOOT_Y = 116


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


def save_runtime_webp(
    image: Image.Image,
    filename: str,
    quality: int,
) -> None:
    image.save(
        OUTPUT / filename,
        format="WEBP",
        quality=quality,
        alpha_quality=100,
        method=6,
        exact=True,
    )


def build_normalized_walk_atlas(source: Image.Image) -> Image.Image:
    atlas = Image.new(
        "RGBA",
        (WALK_CELL_SIZE * 4, WALK_CELL_SIZE * 4),
        (0, 0, 0, 0),
    )
    for row in range(4):
        for column in range(4):
            frame = source.crop(
                (
                    WALK_COLUMNS[column],
                    WALK_ROWS[row],
                    WALK_COLUMNS[column + 1],
                    WALK_ROWS[row + 1],
                )
            )
            bounds = frame.getchannel("A").getbbox()
            if bounds is None:
                raise RuntimeError(f"Empty walk frame at row {row}, column {column}")
            character = frame.crop(bounds)
            scale = WALK_HEIGHTS[row] / character.height
            target_width = max(1, round(character.width * scale))
            character = character.resize(
                (target_width, WALK_HEIGHTS[row]),
                Image.Resampling.LANCZOS,
            )
            target_x = (
                column * WALK_CELL_SIZE + (WALK_CELL_SIZE - target_width) // 2
            )
            target_y = row * WALK_CELL_SIZE + WALK_FOOT_Y - WALK_HEIGHTS[row]
            atlas.alpha_composite(character, (target_x, target_y))
    return atlas


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source = Image.open(SOURCE).convert("RGB")
    for name, bounds in TILES.items():
        tile = make_mirrored_tile(source, bounds)
        tile.save(
            OUTPUT / f"terrain-{name}-v2.webp",
            format="WEBP",
            quality=86,
            method=6,
        )

    save_runtime_webp(
        Image.open(GAME_ATLAS).convert("RGBA"),
        "easygame-atlas-v2.webp",
        90,
    )
    save_runtime_webp(
        Image.open(ENVIRONMENT_ATLAS).convert("RGBA"),
        "easygame-environment-v2.webp",
        90,
    )
    walk = build_normalized_walk_atlas(
        Image.open(HERO_WALK_ATLAS).convert("RGBA")
    )
    save_runtime_webp(walk, "easygame-hero-walk-v2.webp", 92)


if __name__ == "__main__":
    main()
