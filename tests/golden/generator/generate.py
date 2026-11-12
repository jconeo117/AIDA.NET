#!/usr/bin/env python3
"""Generador de archivos dorados a partir de Aida 0.1 (oráculo byte a byte)."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import platform
import struct
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from packs_section import generate_packs
from pow_section import check_pow_sentinel, generate_pow
from state_section import generate_state

EXPECTED_COMMIT = "9a96fb0fbfc882bd8b3062b1abad12b345ab9879"


def validate_environment(aida_dir: Path) -> str:
    if sys.version_info[:2] != (3, 12):
        print(f"ERROR: Se requiere Python 3.12, detectado {sys.version}", file=sys.stderr)
        sys.exit(2)
    libc = " ".join(platform.libc_ver())
    if libc != "glibc 2.44":
        print(f"ERROR: libc esperada 'glibc 2.44', detectada {libc!r}", file=sys.stderr)
        sys.exit(2)
    check_pow_sentinel()
    try:
        rev = subprocess.run(
            ["git", "-C", str(aida_dir), "rev-parse", "HEAD"],
            capture_output=True, text=True, check=True,
        ).stdout.strip()
        status = subprocess.run(
            ["git", "-C", str(aida_dir), "status", "--porcelain", "--untracked-files=no"],
            capture_output=True, text=True, check=True,
        ).stdout.strip()
    except subprocess.SubprocessError as exc:
        print(f"ERROR: git falló en {aida_dir}: {exc}", file=sys.stderr)
        sys.exit(2)
    if rev != EXPECTED_COMMIT:
        print(f"ERROR: Commit {rev} != {EXPECTED_COMMIT}", file=sys.stderr)
        sys.exit(2)
    if status:
        print(f"ERROR: Repositorio {aida_dir} sucio:\n{status}", file=sys.stderr)
        sys.exit(2)
    return rev


def generate_blake2b(out_file: Path) -> None:
    lines = [
        f"{sz} {bytes(i % 251 for i in range(ln)).hex()} "
        f"{hashlib.blake2b(bytes(i % 251 for i in range(ln)), digest_size=sz).hexdigest()}\n"
        for sz in [1, 8, 16, 32, 64]
        for ln in [0, 1, 3, 63, 64, 65, 127, 128, 129, 255, 256, 257, 1000]
    ]
    lines.append(f"64 {b'abc'.hex()} {hashlib.blake2b(b'abc', digest_size=64).hexdigest()}\n")
    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)


def generate_floats(out_file: Path) -> None:
    edges = [
        0.0, -0.0, 1.0, 0.1, 0.3, 2.5, 100.0, 123.456, 1 / 3, 2 / 3, 1e-05, 0.0001,
        9.999999999999999e-05, 1e15, 1e16, 9999999999999998.0, 1234567890123456.7,
        123456789012345680.0, 1e22, 1e23, 1e100, 1.5e-07, 5e-324,
        2.2250738585072014e-308, 1.7976931348623157e308,
        float("nan"), float("inf"), float("-inf"),
    ]
    lines = [f"{struct.pack('>d', v).hex()} {json.dumps(v)}\n" for v in edges]
    for i in range(20000):
        b = hashlib.blake2b(str(i).encode("ascii"), digest_size=8).digest()
        x = struct.unpack(">d", b)[0] if i % 2 == 0 else float(f"{int.from_bytes(b, 'big') % 10**17}e{(i % 41) - 37}")
        lines.append(f"{struct.pack('>d', x).hex()} {json.dumps(x)}\n")
    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)


def generate_cases(out_file: Path) -> None:
    from aida.engine.events import EventKind, EventLog

    log = EventLog()
    lines: list[str] = []

    def add_case(
        name: str, tick: int = 0, kind: str = "action_started", owner: str = "a",
        action: str | None = None, data: dict[str, tuple[str, object]] | None = None,
        content: str | None = None,
    ) -> None:
        py_data = {k: v for k, (_, v) in data.items()} if data is not None else None
        inp_data = (
            {k: [t, struct.pack(">d", v).hex() if t == "float" else v] for k, (t, v) in data.items()}
            if data is not None else None
        )
        ev = log.emit(
            tick=tick, kind=EventKind(kind), owner=owner,  # type: ignore[arg-type]
            action=action, data=py_data, content=content,  # type: ignore[arg-type]
        )
        inp: dict[str, object] = {"kind": kind, "owner": owner, "tick": tick}
        if action is not None:
            inp["action"] = action
        if inp_data is not None:
            inp["data"] = inp_data
        if content is not None:
            inp["content"] = content
        rec = {"input": inp, "line": ev.to_json(), "name": name}
        lines.append(json.dumps(rec, ensure_ascii=True, sort_keys=True, separators=(",", ":")) + "\n")

    add_case("minimo")
    add_case("con-accion", action="x")
    for k in [
        "action_started", "action_completed", "action_requested", "action_rejected",
        "action_failed", "action_aborted", "action_proposed", "action_authorized",
        "action_denied", "action_expired", "need_escalated", "input_observed",
        "intent_classified",
    ]:
        add_case(f"cada-tipo-{k}", kind=k)

    add_case("payload-mixto", data={"z": ("int", 1), "b": ("str", "ñ"), "a": ("bool", True), "m": ("float", 0.1)})
    add_case("orden-por-punto-de-codigo", data={"\ue000": ("int", 1), "😀": ("int", 2), "B": ("int", 3), "a": ("int", 4)})
    add_case("payload-vacio", data={})
    add_case("decimales", data={
        "f1": ("float", -0.0), "f2": ("float", 1e16), "f3": ("float", 1e-05), "f4": ("float", 1.0),
        "f5": ("float", float("nan")), "f6": ("float", float("inf")), "f7": ("float", float("-inf")),
    })
    add_case("enteros-extremos", data={
        "i1": ("int", -1), "i2": ("int", 0), "i3": ("int", 9223372036854775807), "i4": ("int", -9223372036854775808),
    })
    add_case("booleano-falso", data={"v": ("bool", False)})
    add_case("contenido-vacio", content="")
    add_case("contenido-escapes", content="\"\\" + "".join(chr(c) for c in range(0x20)) + chr(0x7F) + "\u2028\u2029é😀")
    add_case("contenido-largo", content="ab😀" * 4000)
    add_case("dueno-no-ascii", owner="ñandú", action="acción_1")
    add_case("tick-grande", tick=2**53 + 1)

    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)


def generate_journal(aida_dir: Path, out_file: Path) -> None:
    from aida.engine.loop import Loop
    from aida.engine.pack import load_pack
    from aida.intent.classify import build_classifier

    pack = load_pack(aida_dir / "packs" / "_plantilla" / "pack.yaml")
    loop = Loop.start(pack, pack.agents[0], seed=1312, inbox_capacity=64, intent=build_classifier(pack))
    owner = pack.agents[0].id
    for t, text in [
        (5, "libera espacio en disco"), (40, "renueva los certificados"),
        (90, "hola, ¿qué tal?"), (150, "escala el servicio"),
    ]:
        loop.inbox.push(tick=t, owner=owner, content=text)
    for _ in range(300):
        loop.step()
        for action_id in sorted(loop.authority.pending):
            m = loop.execution.tick % 3
            if m == 0:
                loop.deny(action_id)
            elif m == 1:
                loop.approve(action_id)

    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(loop.log.to_jsonl()) + "\n")


def generate_manifest(out_dir: Path, commit: str) -> None:
    rel_files = [
        "events/cases.jsonl", "hashing/blake2b.txt",
        "journals/plantilla-1312-300.jsonl",
        "numerics/pow.txt",
        "packs/cases.jsonl", "packs/consultorio.json", "packs/plantilla.json",
        "serialization/floats.txt",
        "state/cases.jsonl",
        "state/packs/sintetico-01.yaml",
        "state/packs/sintetico-02.yaml",
        "state/packs/sintetico-03.yaml",
    ]
    files_map = {r: hashlib.sha256((out_dir / r).read_bytes()).hexdigest() for r in sorted(rel_files)}
    manifest = {
        "files": files_map,
        "generator": "tests/golden/generator/generate.py",
        "libc": "glibc 2.44",
        "python": platform.python_version(),
        "source": {"commit": commit, "tag": "v0.1.0"},
    }
    with open(out_dir / "manifest.json", "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(manifest, indent=1, ensure_ascii=False, sort_keys=True) + "\n")


def main() -> None:
    parser = argparse.ArgumentParser(description="Genera archivos dorados desde Aida 0.1.")
    parser.add_argument("--aida", type=Path, required=True, help="Ruta a ~/Dev/Aida")
    parser.add_argument("--out", type=Path, required=True, help="Ruta de salida (tests/golden)")
    args = parser.parse_args()

    aida_dir, out_dir = args.aida.resolve(), args.out.resolve()
    commit = validate_environment(aida_dir)
    sys.path.insert(0, str(aida_dir / "src"))

    generate_blake2b(out_dir / "hashing" / "blake2b.txt")
    generate_floats(out_dir / "serialization" / "floats.txt")
    generate_cases(out_dir / "events" / "cases.jsonl")
    generate_journal(aida_dir, out_dir / "journals" / "plantilla-1312-300.jsonl")
    generate_packs(aida_dir, out_dir)
    generate_pow(out_dir / "numerics" / "pow.txt")
    generate_state(aida_dir, out_dir)
    generate_manifest(out_dir, commit)


if __name__ == "__main__":
    main()
