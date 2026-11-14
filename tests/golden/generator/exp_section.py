"""Generador de archivos dorados para exp (N005)."""

from __future__ import annotations

import math
from pathlib import Path
import random
import struct
import sys


def check_exp_sentinel() -> None:
    sentinel_x = float.fromhex("-0x1.fd0ca47d7e028p+7")
    val = math.exp(sentinel_x)
    expected = float.fromhex("0x1.bd414fcbb3b83p-368")
    if val.hex() != expected.hex():
        print("la exp del oráculo no es la de glibc con FMA (ADR 0026)", file=sys.stderr)
        sys.exit(4)


def eval_exp(x: float) -> str:
    try:
        r = math.exp(x)
        return f"{struct.unpack('>Q', struct.pack('>d', r))[0]:016X}"
    except OverflowError:
        return "OverflowError"


def build_exp_cases(rng: random.Random) -> list[float]:
    cases: list[float] = []

    # E1: cada entrada de la tabla: 128 x 20
    displacements = [-1e-6, -1e-12, 0.0, 1e-12, 1e-6]
    for i in range(128):
        possible_k = [k for k in range(-1024, 1024) if k % 128 == i]
        for d in displacements:
            for _ in range(4):
                k = rng.choice(possible_k)
                cases.append(k * math.log(2) / 128.0 + d)

    # E2: [-745, 0], el rango de decide: 8 000
    for _ in range(8000):
        cases.append(rng.uniform(-745.0, 0.0))

    # E3: [-10, 10]: 3 000
    for _ in range(3000):
        cases.append(rng.uniform(-10.0, 10.0))

    # E4: Cerca de 0: +-2^u, con u en [-60, -1]: 2 000
    for _ in range(2000):
        u = rng.uniform(-60.0, -1.0)
        sign = -1.0 if rng.random() < 0.5 else 1.0
        cases.append(sign * (2.0 ** u))

    # E5: Resultado subnormal: [-745.2, -708]: 2 000
    for _ in range(2000):
        cases.append(rng.uniform(-745.2, -708.0))

    # E6: [512, 710] y [-708, -512]: 2 000
    for _ in range(1000):
        cases.append(rng.uniform(512.0, 710.0))
    for _ in range(1000):
        cases.append(rng.uniform(-708.0, -512.0))

    # E7: Los especiales de §1: 23 casos
    e7 = [
        0.0,
        -0.0,
        float("inf"),
        float("-inf"),
        float("nan"),
        709.78271289338399,
        710.0,
        -745.1332191019412,
        -745.2,
        2.0 ** -54,
        -(2.0 ** -54),
        2.0 ** -55,
        512.0,
        -512.0,
        1024.0,
        -1024.0,
        sys.float_info.max,
        -sys.float_info.max,
        5e-324,
        -5e-324,
        1e-310,
        -1e-310,
        float.fromhex("-0x1.fd0ca47d7e028p+7"),
    ]
    cases.extend(e7)

    return cases


def generate_exp(out_file: Path) -> None:
    check_exp_sentinel()
    rng = random.Random(20261112)
    cases = build_exp_cases(rng)

    lines: list[str] = []
    for x in cases:
        bx = struct.unpack(">Q", struct.pack(">d", x))[0]
        res = eval_exp(x)
        lines.append(f"{bx:016X} {res}\n")

    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)
