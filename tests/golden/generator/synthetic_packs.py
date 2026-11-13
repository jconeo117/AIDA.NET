"""Escritura de los packs sintéticos (N004)."""

from __future__ import annotations

from pathlib import Path

from pack_data_01 import SINTETICO_01
from pack_data_02 import SINTETICO_02
from pack_data_03 import SINTETICO_03


def write_synthetic_packs(out_dir: Path) -> list[tuple[str, Path]]:
    packs_dir = out_dir / "state" / "packs"
    packs_dir.mkdir(parents=True, exist_ok=True)
    packs = [
        ("sintetico-01", packs_dir / "sintetico-01.yaml", SINTETICO_01),
        ("sintetico-02", packs_dir / "sintetico-02.yaml", SINTETICO_02),
        ("sintetico-03", packs_dir / "sintetico-03.yaml", SINTETICO_03),
    ]
    for _, path, content in packs:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
    return [(name, path) for name, path, _ in packs]
