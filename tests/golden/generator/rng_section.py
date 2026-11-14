"""Generador de archivos dorados para aleatoriedad (N005)."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import random
import struct


def _hex_double(val: float) -> str:
    return f"{struct.unpack('>Q', struct.pack('>d', val))[0]:016X}"


def generate_mt(out_file: Path) -> None:
    seeds: list[int] = [
        0,
        1,
        2,
        3,
        (1 << 31) - 1,
        (1 << 32) - 1,
        1 << 32,
        (1 << 32) + 1,
        (1 << 63) - 1,
        1 << 63,
        (1 << 64) - 1,
    ]

    rng_seed = random.Random(7)
    for b in range(1, 65):
        for _ in range(4):
            s = rng_seed.randint(1 << (b - 1), (1 << b) - 1)
            seeds.append(s)

    lines: list[str] = []
    for seed in seeds:
        rng_u32 = random.Random(seed)
        u32_vals = [rng_u32.getrandbits(32) for _ in range(700)]

        rng_f = random.Random(seed)
        f_vals = [_hex_double(rng_f.random()) for _ in range(400)]

        rec = {
            "f": f_vals,
            "seed": str(seed),
            "u32": u32_vals,
        }
        lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)


def generate_streams(out_file: Path) -> None:
    seeds: list[int] = [0, 1, 42, (1 << 63) - 1, 123456789012345]
    keys: list[tuple[str, ...]] = []
    for t in range(60):
        keys.append(("decide", "agente", str(t)))
    keys.append(("decide", "ñandú😀", "-3"))
    keys.append(("x",))
    keys.append(("a\x1fb", "7"))
    keys.append(())

    lines: list[str] = []
    for seed in seeds:
        for k in keys:
            material = "\x1f".join([str(seed)] + list(k))
            mat_bytes = material.encode("utf-8")
            digest = hashlib.blake2b(mat_bytes, digest_size=8).digest()
            u64 = int.from_bytes(digest, "big")
            rng = random.Random(u64)
            f_vals = [_hex_double(rng.random()) for _ in range(3)]

            rec = {
                "digest": str(u64),
                "f": f_vals,
                "key": list(k),
                "material": mat_bytes.hex(),
                "seed": seed,
            }
            lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)


def generate_choices(out_file: Path) -> None:
    rng_master = random.Random(20261112)
    lines: list[str] = []

    for _ in range(20000):
        seed = rng_master.getrandbits(64)
        rng = random.Random(seed)
        n = rng_master.randint(1, 6)

        p = rng_master.random()
        if p < 0.30:
            weights = [1.0] * n
        elif p < 0.50:
            weights = [1.0] + [rng_master.choice([0.0, 0.5, 1.0, 2.0]) for _ in range(n - 1)]
        else:
            weights = [1.0] + [10.0 ** rng_master.uniform(-300.0, 8.0) for _ in range(n - 1)]

        idx = rng.choices(range(len(weights)), weights=weights, k=1)[0]
        w_hex = [_hex_double(w) for w in weights]

        rec = {
            "idx": idx,
            "seed": str(seed),
            "w": w_hex,
        }
        lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)


def generate_randomness(out_dir: Path) -> None:
    generate_mt(out_dir / "mt.jsonl")
    generate_streams(out_dir / "streams.jsonl")
    generate_choices(out_dir / "choices.jsonl")
