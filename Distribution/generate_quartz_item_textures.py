"""Génère les textures item quartz (164) et quartz pur (165)."""
from pathlib import Path
import math
import random

try:
    from PIL import Image
except ImportError:
    raise SystemExit("Pillow requis: pip install pillow")

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "textures" / "items" / "minerais"
SIZE = 256
SEED = 424242


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
        x *= 2.05
        y *= 2.05
        a *= 0.5
    return v


def _gen_quartz_normal(rng: random.Random) -> Image.Image:
    img = Image.new("RGB", (SIZE, SIZE))
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            n1 = _fbm(u * 9.0 + 1.3, v * 9.0 + 0.7)
            n2 = _fbm(u * 22.0, v * 22.0)
            veins = _fbm(u * 3.5 + n1 * 2.0, v * 3.5 - n1)
            milky = 0.55 + n1 * 0.28 + veins * 0.18
            grain = n2 * 0.12
            # légères inclusions gris-beige (pas unicolore)
            warm = 0.97 + _noise(u * 40, v * 40) * 0.04
            cool = 0.94 + _noise(u * 31 + 5, v * 27) * 0.05
            r = int(255 * min(1.0, milky * warm + grain * 0.08))
            g = int(255 * min(1.0, milky * warm * 0.99 + grain * 0.06))
            b = int(255 * min(1.0, milky * cool * 0.97 + grain * 0.04))
            # micro-fissures sombres
            crack = 1.0 - max(0.0, veins - 0.72) * 2.8
            r = int(r * crack)
            g = int(g * crack)
            b = int(b * crack)
            px[x, y] = (r, g, b)
    return img


def _gen_quartz_pur(rng: random.Random) -> Image.Image:
    img = Image.new("RGBA", (SIZE, SIZE))
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            # facettes cristallines
            facet = abs(math.sin(u * 28.0 + _noise(u * 8, v * 8) * 4.0))
            facet *= abs(math.cos(v * 24.0 - _noise(v * 7, u * 6) * 3.5))
            sparkle = _fbm(u * 18.0, v * 18.0, 4)
            bright = 0.78 + facet * 0.18 + sparkle * 0.12
            r = int(255 * min(1.0, bright * 1.02))
            g = int(255 * min(1.0, bright * 1.01))
            b = int(255 * min(1.0, bright * 0.99))
            # alpha : zones internes plus transparentes, arêtes plus opaques
            alpha = 0.28 + facet * 0.38 + sparkle * 0.22
            alpha = int(255 * min(1.0, max(0.18, alpha)))
            px[x, y] = (r, g, b, alpha)
    return img


def main() -> None:
    rng = random.Random(SEED)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    p164 = OUT_DIR / "164_quartz.png"
    p165 = OUT_DIR / "165_quartz_pur.png"
    _gen_quartz_normal(rng).save(p164)
    _gen_quartz_pur(rng).save(p165)
    print(f"OK: {p164}")
    print(f"OK: {p165}")


if __name__ == "__main__":
    main()
