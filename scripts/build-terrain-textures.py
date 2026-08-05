from pathlib import Path
from statistics import median

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "art" / "source" / "easygame-environment-source-v1.png"
OUTPUT = ROOT / "client" / "public" / "assets"
PROCESSED = ROOT / "art" / "processed"
GAME_ATLAS = PROCESSED / "easygame-atlas-alpha-v1.png"
ENVIRONMENT_ATLAS = PROCESSED / "easygame-environment-alpha-v1.png"
HERO_WALK_ATLAS = PROCESSED / "easygame-hero-body-armless-alpha-v1.png"
WEAPON_OVERLAY_ATLAS = PROCESSED / "easygame-weapon-overlay-alpha-v2.png"
EXPLOSION_ATLAS = PROCESSED / "easygame-explosion-alpha-v3.png"
ZOMBIE_WALK_ATLASES = {
    "walker": PROCESSED / "easygame-zombie-walker-walk-alpha-v1.png",
    "runner": PROCESSED / "easygame-zombie-runner-walk-alpha-v1.png",
    "brute": PROCESSED / "easygame-zombie-brute-walk-alpha-v1.png",
}

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


def content_grid_bounds(
    source: Image.Image,
    count: int,
    axis: str,
) -> list[int]:
    """Find real whitespace gutters near nominal grid boundaries."""
    alpha = source.getchannel("A")
    length = source.width if axis == "x" else source.height
    cross_length = source.height if axis == "x" else source.width
    pixels = alpha.load()
    projection: list[int] = []
    for coordinate in range(length):
        occupied = 0
        for cross in range(cross_length):
            value = pixels[coordinate, cross] if axis == "x" else pixels[cross, coordinate]
            if value >= 24:
                occupied += 1
        projection.append(occupied)

    nominal_cell = length / count
    search_radius = max(8, round(nominal_cell * 0.32))
    smoothing_radius = 4
    boundaries = [0]
    for index in range(1, count):
        expected = round(index * nominal_cell)
        search_start = max(boundaries[-1] + 8, expected - search_radius)
        search_end = min(length - 8, expected + search_radius)
        boundary = min(
            range(search_start, search_end + 1),
            key=lambda position: sum(
                projection[max(0, position - smoothing_radius) : position + smoothing_radius + 1]
            ),
        )
        boundaries.append(boundary)
    boundaries.append(length)
    return boundaries


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
    return build_consistent_character_atlas(source, 4, 4, target_reference_height)


def build_consistent_character_atlas(
    source: Image.Image,
    column_count: int,
    row_count: int,
    target_reference_height: int,
) -> Image.Image:
    """Use one scale and one foot anchor for every frame in a character atlas."""
    atlas = Image.new(
        "RGBA",
        (WALK_CELL_SIZE * column_count, WALK_CELL_SIZE * row_count),
        (0, 0, 0, 0),
    )
    columns = content_grid_bounds(source, column_count, "x")
    rows = content_grid_bounds(source, row_count, "y")
    frames: list[tuple[int, int, Image.Image, tuple[int, int, int, int]]] = []
    measured_heights: list[int] = []
    for row in range(row_count):
        for column in range(column_count):
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
        cell = Image.new("RGBA", (WALK_CELL_SIZE, WALK_CELL_SIZE), (0, 0, 0, 0))
        target_x = (WALK_CELL_SIZE - target_width) // 2
        target_y = WALK_FOOT_Y - target_height
        cell.alpha_composite(character, (target_x, target_y))
        atlas.alpha_composite(
            cell,
            (column * WALK_CELL_SIZE, row * WALK_CELL_SIZE),
        )
    return atlas


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


def build_uniform_grid_atlas(
    source: Image.Image,
    column_count: int,
    row_count: int,
    cell_size: int,
) -> Image.Image:
    """Crop every frame with one shared window so scale and anchors never pulse."""
    columns = grid_bounds(source.width, column_count)
    rows = grid_bounds(source.height, row_count)
    frames: list[tuple[int, int, Image.Image]] = []
    normalized_bounds: list[tuple[float, float, float, float]] = []

    for row in range(row_count):
        for column in range(column_count):
            frame = source.crop(
                (
                    columns[column],
                    rows[row],
                    columns[column + 1],
                    rows[row + 1],
                )
            )
            bounds = significant_alpha_bounds(frame)
            frames.append((row, column, frame))
            normalized_bounds.append(
                (
                    bounds[0] / frame.width,
                    bounds[1] / frame.height,
                    bounds[2] / frame.width,
                    bounds[3] / frame.height,
                )
            )

    padding = 0.018
    shared_left = max(0.0, min(bounds[0] for bounds in normalized_bounds) - padding)
    shared_top = max(0.0, min(bounds[1] for bounds in normalized_bounds) - padding)
    shared_right = min(1.0, max(bounds[2] for bounds in normalized_bounds) + padding)
    shared_bottom = min(1.0, max(bounds[3] for bounds in normalized_bounds) + padding)
    atlas = Image.new(
        "RGBA",
        (cell_size * column_count, cell_size * row_count),
        (0, 0, 0, 0),
    )

    for row, column, frame in frames:
        crop = frame.crop(
            (
                round(shared_left * frame.width),
                round(shared_top * frame.height),
                round(shared_right * frame.width),
                round(shared_bottom * frame.height),
            )
        )
        scale = min((cell_size - 4) / crop.width, (cell_size - 4) / crop.height)
        target_width = max(1, round(crop.width * scale))
        target_height = max(1, round(crop.height * scale))
        crop = crop.resize(
            (target_width, target_height),
            Image.Resampling.LANCZOS,
        )
        target_x = column * cell_size + (cell_size - target_width) // 2
        target_y = row * cell_size + (cell_size - target_height) // 2
        atlas.alpha_composite(crop, (target_x, target_y))

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
    save_runtime_webp(hero_walk, "easygame-hero-body-armless-v1.webp", 92)

    weapon_overlay = build_weapon_overlay_atlas(
        Image.open(WEAPON_OVERLAY_ATLAS).convert("RGBA")
    )
    save_runtime_webp(
        weapon_overlay,
        "easygame-weapon-overlay-v2.webp",
        92,
    )

    explosion = build_uniform_grid_atlas(
        Image.open(EXPLOSION_ATLAS).convert("RGBA"),
        4,
        3,
        192,
    )
    save_runtime_webp(explosion, "easygame-explosion-v3.webp", 95)

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

if __name__ == "__main__":
    main()
