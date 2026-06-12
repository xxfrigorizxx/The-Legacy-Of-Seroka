"""Génère la texture item minerai d'étain (166)."""
from pathlib import Path
import math
import random

try:
    from PIL import Image
except ImportError:
    raise SystemExit("Pillow requis: pip install pillow")

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "textures" / "items" / "minerais" / "166_etain.png"
SIZE = 256
SEED = 37166


def _hash(x: float, y: float) -> float:
    return (math.sin(x * 127.1 + y * 311.7) * 43758.5453) % 1.0


def _smooth(t: float) -> float:
    return t * t * (3.0 - 2.0 * t)


def _noise(x: float, y: float) -> float:
    ix, iy = int(math.floor(x)), int(math.floor(y))
    fx, fy = x - ix, y - iy
    fx, fy = _smooth(fx), _smooth(fy)
    a = _hash(ix, iy)
    b = _hash(ix + 1, iy)
    c = _hash(ix, iy + 1)
    d = _hash(ix + 1, iy + 1)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def _fbm(x: float, y: float, octaves: int = 5) -> float:
    v = 0.0
    a = 0.5
    for _ in range(octaves):
        v += a * _noise(x, y)
        x *= 2.07
        y *= 2.07
        a *= 0.5
    return v


def main() -> None:
    rng = random.Random(SEED)
    img = Image.new("RGB", (SIZE, SIZE))
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            rock = _fbm(u * 11.0 + 2.1, v * 11.0 - 0.8)
            cracks = _fbm(u * 28.0, v * 28.0)
            vein = _fbm(u * 4.2 + rock * 1.5, v * 4.2 - rock)
            metal = max(0.0, vein - 0.42) * 2.4
            metal += max(0.0, _noise(u * 18 + 7, v * 18) - 0.78) * 1.8
            metal = min(1.0, metal)
            # matrice roche grise
            base_r = 0.42 + rock * 0.18 - cracks * 0.08
            base_g = 0.41 + rock * 0.17 - cracks * 0.07
            base_b = 0.40 + rock * 0.16 - cracks * 0.06
            # reflets étain argenté
            tin_r = 0.78 + _noise(u * 55, v * 49) * 0.14
            tin_g = 0.79 + _noise(u * 47 + 3, v * 51) * 0.13
            tin_b = 0.82 + _noise(u * 43, v * 57 - 2) * 0.12
            r = base_r * (1 - metal) + tin_r * metal
            g = base_g * (1 - metal) + tin_g * metal
            b = base_b * (1 - metal) + tin_b * metal
            # micro-oxydation sombre dans les fissures
            oxy = max(0.0, cracks - 0.55) * 0.35
            r -= oxy * 0.12
            g -= oxy * 0.10
            b -= oxy * 0.08
            px[x, y] = (
                int(255 * max(0.0, min(1.0, r))),
                int(255 * max(0.0, min(1.0, g))),
                int(255 * max(0.0, min(1.0, b))),
            )
    OUT.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUT)
    print(f"Écrit {OUT}")


if __name__ == "__main__":
    main()
