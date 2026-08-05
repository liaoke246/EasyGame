from pathlib import Path
from statistics import median

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "art" / "source" / "easygame-environment-source-v1.png"
OUTPUT = ROOT / "client" / "public" / "assets"
PROCESSED = ROOT / "art" / "processed"
GAME_ATLAS = PROCESSED / "easygame-atlas-alpha-v1.png"
ENVIRONMENT_ATLAS = PROCESSED / "easygame-environment-alpha-v1.png"
HERO_WALK_ATLAS = PROCESSED / "easygame-hero-walk-alpha-v3.png"
ZOMBIE_WALK_ATLASES = {
    "walker": PROCESSED / "easygame-zombie-walker-walk-alpha-v1.png",
    "runner": PROCESSED / "easygame-zombie-runner-walk-alpha-v1.png",
    "brute": PROCESSED / "easygame-zombie-brute-walk-alpha-v1.png",
}
WEAPON_OVERLAY_ATLAS = PROCESSED / "easygame-weapon-overlay-alpha-v2.png"

TILES = {
    "grass": (18, 18, 308, 308),
    "dirt": (327, 18, 618, 308),
    "wild": (636, 18, 927, 308),
    "soil": (946, 18, 1236, 308),
}

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


def grid_bounds(length: int, count: int) -> list[int]:
    return [round(index * length / count) for index in range(count + 1)]


def build_normalized_walk_atlas(
    source: Image.Image,
    target_heights: list[int],
) -> Image.Image:
    atlas = Image.new(
        "RGBA",
        (WALK_CELL_SIZE * 4, WALK_CELL_SIZE * 4),
        (0, 0, 0, 0),
    )
    columns = grid_bounds(source.width, 4)
    rows = grid_bounds(source.height, 4)
    for row in range(4):
        for column in range(4):
            frame = source.crop(
                (
                    columns[column],
                    rows[row],
                    columns[column + 1],
                    rows[row + 1],
                )
            )
            bounds = frame.getchannel("A").getbbox()
            if bounds is None:
                raise RuntimeError(f"Empty walk frame at row {row}, column {column}")
            character = frame.crop(bounds)
            target_height = target_heights[row]
            scale = target_height / character.height
            target_width = max(1, round(character.width * scale))
            character = character.resize(
                (target_width, target_height),
                Image.Resampling.LANCZOS,
            )
            target_x = (
                column * WALK_CELL_SIZE + (WALK_CELL_SIZE - target_width) // 2
            )
            target_y = row * WALK_CELL_SIZE + WALK_FOOT_Y - target_height
            atlas.alpha_composite(character, (target_x, target_y))
    return atlas


def significant_alpha_bounds(frame: Image.Image) -> tuple[int, int, int, int]:
    """Ignore tiny detached generation specks when measuring a sprite."""
    alpha = frame.getchannel("A")
    width, height = frame.size
    pixels = alpha.tobytes()
    visited = bytearray(width * height)
    components: list[tuple[int, int, int, int, int]] = []

    for start, value in enumerate(pixels):
        if value < 16 or visited[start]:
            continue
        visited[start] = 1
        stack = [start]
        area = 0
        min_x = width
        min_y = height
        max_x = 0
        max_y = 0
        while stack:
            index = stack.pop()
            x = index % width
            y = index // width
            area += 1
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
            for neighbor in (index - 1, index + 1, index - width, index + width):
                if neighbor < 0 or neighbor >= len(pixels) or visited[neighbor]:
                    continue
                neighbor_x = neighbor % width
                if abs(neighbor_x - x) > 1 or pixels[neighbor] < 16:
                    continue
                visited[neighbor] = 1
                stack.append(neighbor)
        components.append((area, min_x, min_y, max_x + 1, max_y + 1))

    if not components:
        raise RuntimeError("Empty walk frame")
    largest_area = max(component[0] for component in components)
    minimum_area = max(12, round(largest_area * 0.02))
    kept = [component for component in components if component[0] >= minimum_area]
    return (
        min(component[1] for component in kept),
        min(component[2] for component in kept),
        max(component[3] for component in kept),
        max(component[4] for component in kept),
    )


def build_consistent_walk_atlas(
    source: Image.Image,
    target_reference_height: int,
) -> Image.Image:
    """Use one scale for every frame so gait silhouettes never pulse in size."""
    atlas = Image.new(
        "RGBA",
        (WALK_CELL_SIZE * 4, WALK_CELL_SIZE * 4),
        (0, 0, 0, 0),
    )
    columns = grid_bounds(source.width, 4)
    rows = grid_bounds(source.height, 4)
    frames: list[tuple[int, int, Image.Image, tuple[int, int, int, int]]] = []
    measured_heights: list[int] = []
    for row in range(4):
        for column in range(4):
            frame = source.crop(
                (
                    columns[column],
                    rows[row],
                    columns[column + 1],
                    rows[row + 1],
                )
            )
            bounds = significant_alpha_bounds(frame)
            frames.append((row, column, frame, bounds))
            measured_heights.append(bounds[3] - bounds[1])

    shared_scale = target_reference_height / median(measured_heights)
    for row, column, frame, bounds in frames:
        character = frame.crop(bounds)
        target_width = max(1, round(character.width * shared_scale))
        target_height = max(1, round(character.height * shared_scale))
        character = character.resize(
            (target_width, target_height),
            Image.Resampling.LANCZOS,
        )
        target_x = column * WALK_CELL_SIZE + (WALK_CELL_SIZE - target_width) // 2
        target_y = row * WALK_CELL_SIZE + WALK_FOOT_Y - target_height
        atlas.alpha_composite(character, (target_x, target_y))
    return atlas


def build_weapon_overlay_atlas(source: Image.Image) -> Image.Image:
    atlas = Image.new(
        "RGBA",
        (WALK_CELL_SIZE * 4, WALK_CELL_SIZE * 3),
        (0, 0, 0, 0),
    )
    columns = grid_bounds(source.width, 4)
    rows = grid_bounds(source.height, 3)
    for row in range(3):
        for column in range(4):
            frame = source.crop(
                (
                    columns[column],
                    rows[row],
                    columns[column + 1],
                    rows[row + 1],
                )
            )
            frame.thumbnail(
                (WALK_CELL_SIZE, WALK_CELL_SIZE),
                Image.Resampling.LANCZOS,
            )
            target_x = column * WALK_CELL_SIZE + (WALK_CELL_SIZE - frame.width) // 2
            target_y = row * WALK_CELL_SIZE + (WALK_CELL_SIZE - frame.height) // 2
            atlas.alpha_composite(frame, (target_x, target_y))
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
    hero_walk = build_normalized_walk_atlas(
        Image.open(HERO_WALK_ATLAS).convert("RGBA"),
        [86, 86, 88, 88],
    )
    save_runtime_webp(hero_walk, "easygame-hero-walk-v3.webp", 92)

    zombie_heights = {
        "walker": 93,
        "runner": 89,
        "brute": 105,
    }
    for kind, source_path in ZOMBIE_WALK_ATLASES.items():
        zombie_walk = build_consistent_walk_atlas(
            Image.open(source_path).convert("RGBA"),
            zombie_heights[kind],
        )
        save_runtime_webp(
            zombie_walk,
            f"easygame-zombie-{kind}-walk-v2.webp",
            92,
        )

    weapon_overlay = build_weapon_overlay_atlas(
        Image.open(WEAPON_OVERLAY_ATLAS).convert("RGBA")
    )
    save_runtime_webp(
        weapon_overlay,
        "easygame-weapon-overlay-v2.webp",
        92,
    )


if __name__ == "__main__":
    main()
