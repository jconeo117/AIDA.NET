"""Generador de archivos dorados para pow (N004)."""

from __future__ import annotations

from pathlib import Path
import random
import struct
import sys


def check_pow_sentinel() -> None:
    sentinel_x = float.fromhex("0x1.21a2961722c3cp+0")
    sentinel_y = float.fromhex("-0x1.376ffab261ba8p-3")
    val = sentinel_x ** sentinel_y
    expected = float.fromhex("0x1.f67a74f8a1349p-1")
    if val.hex() != expected.hex():
        print("el pow del oráculo no es el de glibc con FMA (ADR 0026)", file=sys.stderr)
        sys.exit(4)


def eval_pow(x: float, y: float) -> str:
    try:
        r = x ** y
        if isinstance(r, complex):
            return "complex"
        return f"{struct.unpack('>Q', struct.pack('>d', r))[0]:016X}"
    except OverflowError:
        return "OverflowError"
    except ZeroDivisionError:
        return "ZeroDivisionError"


def build_pow_cases(rng: random.Random) -> list[tuple[float, float]]:
    pairs: list[tuple[float, float]] = []

    # S1: cada entrada de la tabla del log: 128 x 40
    off = 0x3FE6955500000000
    for i in range(128):
        for _ in range(40):
            k = rng.randint(-20, 20)
            low = rng.getrandbits(45)
            tmp = ((k << 52) + (i << 45) + low) & 0xFFFFFFFFFFFFFFFF
            ix = (off + tmp) & 0xFFFFFFFFFFFFFFFF
            x = struct.unpack(">d", struct.pack(">Q", ix))[0]
            y = rng.uniform(-3.0, 3.0)
            pairs.append((x, y))

    # S2: rasgos y desgaste: 4 000
    for _ in range(4000):
        pairs.append((rng.uniform(1e-3, 4.0), rng.uniform(-1.0, 1.0)))

    # S3: urgencia: 4 000
    urg_choices = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0]
    for idx in range(4000):
        x = 0.0 if idx < 50 else rng.random()
        y = rng.choice(urg_choices) if rng.random() < 0.3 else rng.uniform(0.01, 6.0)
        pairs.append((x, y))

    # S4: todo el rango: 2 000
    for _ in range(2000):
        exp = rng.randint(1, 2046)
        mant = rng.getrandbits(52)
        ix = (exp << 52) | mant
        pairs.append((struct.unpack(">d", struct.pack(">Q", ix))[0], rng.uniform(-4.0, 4.0)))

    # S5: cerca de 1: 1 000
    k_vals = [k for k in range(-4096, 4097) if k != 0]
    for _ in range(1000):
        k = rng.choice(k_vals)
        x = 1.0 + k * (2.0 ** -52)
        pairs.append((x, rng.uniform(-(2.0 ** 30), 2.0 ** 30)))

    # S6: x subnormal: 1 000
    for _ in range(1000):
        mant = rng.randint(1, (1 << 52) - 1)
        pairs.append((struct.unpack(">d", struct.pack(">Q", mant))[0], rng.uniform(-1.0, 1.0)))

    # S7: resultados extremos: 2 500
    for _ in range(1500):
        target = rng.uniform(-1080.0, -1015.0)
        lx = rng.uniform(0.1, 10.0) if rng.random() < 0.5 else rng.uniform(-10.0, -0.1)
        pairs.append((2.0 ** lx, target / lx))

    for _ in range(1000):
        target = rng.uniform(1015.0, 1025.0)
        lx = rng.uniform(0.1, 10.0) if rng.random() < 0.5 else rng.uniform(-10.0, -0.1)
        pairs.append((2.0 ** lx, target / lx))

    # S8: y diminuto o enorme: 200
    for _ in range(100):
        x = rng.uniform(0.01, 0.99) if rng.random() < 0.5 else rng.uniform(1.01, 100.0)
        sign = -1.0 if rng.random() < 0.5 else 1.0
        pairs.append((x, sign * (2.0 ** rng.uniform(-1000.0, -66.0))))
    for _ in range(100):
        x = rng.uniform(0.01, 0.99) if rng.random() < 0.5 else rng.uniform(1.01, 100.0)
        sign = -1.0 if rng.random() < 0.5 else 1.0
        pairs.append((x, sign * (2.0 ** rng.uniform(63.0, 100.0))))

    # S9: casos de float_pow: todos
    nan, inf = float("nan"), float("inf")
    s9_cases: list[tuple[float, float]] = [
        (nan, 2.0), (nan, -2.0), (nan, 0.5), (nan, nan),
        (2.0, nan), (-2.0, nan), (1.0, nan), (nan, 0.0), (nan, -0.0),
    ]
    for base in [inf, -inf]:
        for y_val in [2.0, 3.0, 2.5, -2.0, -3.0, -2.5]:
            s9_cases.append((base, y_val))
    for x_val in [0.5, -0.5, 1.0, -1.0, 2.0, -2.0]:
        s9_cases.extend([(x_val, inf), (x_val, -inf)])
    for base in [0.0, -0.0]:
        for y_val in [3.0, 2.0, 2.5, -1.0, -2.0, -2.5]:
            s9_cases.append((base, y_val))
    for base in [-2.0, -1.0]:
        for y_val in [2.0, 3.0, 2.5, -2.0, -3.0, -2.5, 0.5]:
            s9_cases.append((base, y_val))
    s9_cases.append((-1.0, float(2**53 - 1)))
    for x_val in [2.0, -2.0, 0.0, -0.0, 1.0, -1.0, inf, -inf, nan, 0.5, 123.456]:
        s9_cases.extend([(x_val, 0.0), (x_val, -0.0)])

    pairs.extend(s9_cases)
    return pairs


def generate_pow(out_file: Path) -> None:
    check_pow_sentinel()
    rng = random.Random(20261111)
    cases = build_pow_cases(rng)

    lines: list[str] = []
    for x, y in cases:
        bx = struct.unpack(">Q", struct.pack(">d", x))[0]
        by = struct.unpack(">Q", struct.pack(">d", y))[0]
        res = eval_pow(x, y)
        lines.append(f"{bx:016X} {by:016X} {res}\n")

    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)
