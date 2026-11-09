"""Sección del generador para archivos dorados de packs (N003)."""

from __future__ import annotations

import json
from pathlib import Path
import sys
from typing import Any

from aida.engine.pack import ContentPack, load_pack
from pydantic import ValidationError
import yaml

BASE_YAML = """id: base
label: Base
needs:
  - id: a
    label: A
    decay_per_tick: 0.1
  - id: b
    label: B
    decay_per_tick: 0.1
traits:
  - id: t
    label: T
actions:
  - id: x
    label: X
    reversibility: reversible
    duration_ticks: 1
    satisfies:
      a: 0.5
    effects:
      a: 0.5
agents:
  - id: g
    label: G
"""


def _canonical_pack_dump(pack: ContentPack) -> str:
    return json.dumps(
        pack.model_dump(mode="json"),
        sort_keys=True,
        ensure_ascii=False,
        separators=(",", ":"),
    )


def _build_cases() -> list[tuple[str, str, str]]:
    base = BASE_YAML
    return [
        # Comprobaciones del pack (c01 - c15)
        ("c01-id-repetido", "problems", base.replace("id: b", "id: a")),
        ("c02-accion-y-agente-mismo-id", "problems", base.replace("id: x", "id: g")),
        (
            "c03-necesidad-desconocida",
            "problems",
            base.replace(
                "satisfies:\n      a: 0.5\n    effects:\n      a: 0.5",
                "satisfies:\n      z: 0.5\n    effects:\n      z: 0.5",
            ),
        ),
        ("c04-rasgo-desconocido", "problems", base + "    traits:\n      z: 0.5\n"),
        (
            "c05-franja-desconocida",
            "problems",
            base.replace(
                "effects:\n      a: 0.5",
                "effects:\n      a: 0.5\n    phase_multipliers:\n      p1: 1.5",
            ),
        ),
        ("c05-desfase-sin-franjas", "problems", base.replace("label: T", "label: T\n    phase_shift: 1")),
        (
            "c06-situacion-desconocida",
            "problems",
            base.replace(
                "effects:\n      a: 0.5",
                "effects:\n      a: 0.5\n    situation_multipliers:\n      s1: 1.5",
            ),
        ),
        (
            "c07-meta-de-agente-ciclo",
            "problems",
            base + "    goals:\n      - id: m1\n        label: M1\n        need: a\n        at_least: 0.8\n        requires_goal: m2\n      - id: m2\n        label: M2\n        need: a\n        at_least: 0.8\n        requires_goal: m1\n",
        ),
        (
            "c07-meta-de-pack-espera-inexistente",
            "problems",
            base + "goals:\n  - id: m\n    label: M\n    need: a\n    at_least: 0.8\n    requires_goal: inex\n",
        ),
        (
            "c08-meta-inalcanzable",
            "problems",
            base + "    goals:\n      - id: m\n        label: M\n        need: b\n        at_least: 0.8\n",
        ),
        (
            "c08-rasgo-inalcanzable",
            "problems",
            base + "    goals:\n      - id: m\n        label: M\n        need: a\n        at_traits:\n          t: 0.8\n",
        ),
        (
            "c09-intencion-sin-accion",
            "problems",
            base + "intents:\n  - id: i\n    label: I\n    need: b\n    examples:\n      - \"ayuda con b\"\n",
        ),
        ("c10-gusto-desconocido", "problems", base + "    tastes:\n      - target: z\n        multiplier: 1.5\n"),
        (
            "c11-efecto-negativo-sin-coste",
            "problems",
            base.replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n      b: -0.1"),
        ),
        (
            "c12-requisito-corto",
            "problems",
            base.replace(
                "effects:\n      a: 0.5",
                "effects:\n      a: 0.5\n      b: -0.123456\n    costs:\n      b: 0.1\n    requires:\n      b: 0.1",
            ),
        ),
        (
            "c12-redondeo-empate",
            "problems",
            base.replace(
                "effects:\n      a: 0.5",
                "effects:\n      a: 0.5\n      b: -0.03125\n    costs:\n      b: 0.02\n    requires:\n      b: 0.02",
            ),
        ),
        (
            "c13-requisito-mayor-que-uno",
            "problems",
            base.replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n    requires:\n      a: 1.5"),
        ),
        (
            "c13-nadie-alcanza",
            "problems",
            base.replace(
                "  - id: b\n    label: B\n    decay_per_tick: 0.1",
                "  - id: b\n    label: B\n    decay_per_tick: 0.1\n    initial: 0.5",
            ).replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n    requires:\n      b: 0.8"),
        ),
        ("c14-salud-accion-desconocida", "problems", base + "health:\n  may_go_unused:\n    - z\n"),
        ("c15-cotas-iniciales", "problems", base + "    initial_needs:\n      a: 1.5\n"),
        (
            "c01-metas-repetidas-de-agente",
            "problems",
            base + "    goals:\n      - id: m\n        label: M1\n        need: a\n        at_least: 0.8\n      - id: m\n        label: M2\n        need: b\n        at_least: 0.8\n",
        ),

        # Orden de las comprobaciones
        (
            "orden-dos-comprobaciones",
            "problems",
            base.replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n      b: -0.2")
            + "    tastes:\n      - target: z_desconocido\n        multiplier: 1.5\n",
        ),

        # Definiciones (v-*)
        ("v-progresion", "errors", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    progression: true", 1)),
        ("v-meta-sin-condicion", "errors", base + "goals:\n  - id: m\n    label: M\n    need: a\n"),
        ("v-meta-dos-condiciones", "errors", base + "goals:\n  - id: m\n    label: M\n    need: a\n    at_least: 0.5\n    at_traits:\n      t: 0.5\n"),
        ("v-meta-se-abre-a-si-misma", "errors", base + "goals:\n  - id: m\n    label: M\n    need: a\n    at_least: 0.5\n    requires_goal: m\n"),
        ("v-meta-deriva-fuera", "errors", base + "goals:\n  - id: m\n    label: M\n    need: a\n    at_least: 0.5\n    on_success:\n      t: 2.5\n"),
        ("v-rasgo-de-agente-fuera", "errors", base + "    traits:\n      t: 1.5\n"),
        ("v-ejemplo-en-blanco", "errors", base + "intents:\n  - id: i\n    label: I\n    need: a\n    examples:\n      - \"   \"\n"),
        ("v-situacion-sin-duracion", "errors", base + "situations:\n  - id: s\n    label: S\n    every: 10\n    lasts: 0\n"),
        ("v-situacion-permanente", "errors", base + "situations:\n  - id: s\n    label: S\n    every: 10\n    lasts: 10\n"),
        ("v-situacion-lasts-sin-every", "errors", base + "situations:\n  - id: s\n    label: S\n    every: 0\n    lasts: 5\n"),
        ("v-accion-requisito-de-rasgo", "errors", base.replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n    requires_traits:\n      t: 1.5")),
        ("v-accion-satisfies-sin-efecto", "errors", base.replace("satisfies:\n      a: 0.5", "satisfies:\n      a: 0.5\n      b: 0.5")),
        ("v-accion-coste-sin-efecto-negativo", "errors", base.replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n    costs:\n      a: 0.5")),

        # Campos (f-*)
        ("f-falta-obligatorio", "errors", base.replace("    decay_per_tick: 0.1\n", "", 1)),
        ("f-clave-desconocida", "errors", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    clave_rara: 1", 1)),
        ("f-clave-desconocida-en-raiz", "errors", "extra_root: 1\n" + base),
        ("f-decay-fuera-de-rango", "errors", base.replace("decay_per_tick: 0.1", "decay_per_tick: 1.5", 1)),
        ("f-cap-cero", "errors", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    cap: 0.0", 1)),
        ("f-reversibilidad-mal-escrita", "errors", base.replace("reversibility: reversible", "reversibility: rara")),
        ("f-perilla-desconocida", "errors", base + "tuning:\n  strain_multipliers:\n    not_a_knob: 1.0\n"),
        ("f-ejemplos-vacios", "errors", base + "intents:\n  - id: i\n    label: I\n    need: a\n    examples: []\n"),
        ("f-necesidad-no-es-mapa", "errors", base.replace("- id: a\n    label: A\n    decay_per_tick: 0.1", "- \"no_soy_mapa\"")),
        ("f-raiz-es-lista", "errors", "- 1\n- 2\n- 3\n"),
        ("f-documento-vacio", "errors", ""),
        ("f-label-numero", "errors", base.replace("label: Base", "label: 123")),
        ("f-label-si", "errors", base.replace("label: Base", "label: yes")),
        ("f-fecha", "errors", base.replace("label: Base", "label: 2026-01-01")),
        ("f-orden-de-errores", "errors", "extra_root: 1\n" + base.replace("decay_per_tick: 0.1", "extra_need: 2\n    decay_per_tick: 1.5", 1)),
        ("f-yaml-roto", "errors", "id: [1, 2\n"),

        # Conversiones aceptadas (t-*)
        ("t-decimal-entre-comillas", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: \" 0.5 \"\n    initial: \"0.5\"", 1)),
        ("t-exponente-sin-punto", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: 1e-3", 1)),
        ("t-guion-bajo", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: \"0_0.5\"", 1)),
        ("t-bool-como-decimal", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    initial: yes", 1)),
        ("t-entero-como-decimal", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    initial: 1", 1)),
        ("t-entero-desde-texto", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    escalates_after: \"3\"", 1)),
        ("t-entero-desde-decimal", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    escalates_after: 2.0", 1)),
        ("t-bool-desde-entero", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    progression: 0", 1)),
        ("t-bool-desde-texto", "dump", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    urgent_when_low: \"off\"", 1)),
        (
            "t-hex-y-octal",
            "dump",
            (base + "phases:\n  - id: p\n    label: P\n    starts_at: 010\n").replace(
                "decay_per_tick: 0.1", "decay_per_tick: 0.1\n    escalates_after: 0x10", 1
            ),
        ),
        ("t-etiquetas-repetidas", "dump", base.replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n    tags: [b, a, b]")),
        ("t-nulos-opcionales", "dump", base + "goals:\n  - id: m\n    label: M\n    need: a\n    at_least: ~\n    at_traits:\n      t: 0.0\n"),

        # Conversiones rechazadas (t-*)
        ("t-entero-con-decimales", "errors", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    escalates_after: 2.5", 1)),
        ("t-bool-dos", "errors", base.replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    progression: 2", 1)),
        ("t-decimal-texto", "errors", base.replace("decay_per_tick: 0.1", "decay_per_tick: \"abc\"", 1)),

        # Textos (r-*)
        ("r-comilla-en-id", "problems", base + "    traits:\n      \"it's\": 0.5\n"),
        ("r-id-no-ascii", "problems", base + "    initial_needs:\n      ñandú: 0.5\n"),
    ]


def _evaluate_case(name: str, expected_kind: str, yaml_text: str) -> dict[str, Any]:
    try:
        raw = yaml.safe_load(yaml_text)
    except yaml.YAMLError:
        actual_kind = "errors"
        expect_payload: dict[str, Any] = {"errors": [{"message": None, "path": ""}]}
    else:
        try:
            pack = ContentPack.model_validate(raw)
            actual_kind = "dump"
            expect_payload = {"dump": _canonical_pack_dump(pack)}
        except ValidationError as exc:
            errs = exc.errors()
            if (
                len(errs) == 1
                and errs[0]["loc"] == ()
                and errs[0]["type"] == "value_error"
                and "\n  - " in errs[0]["msg"]
            ):
                actual_kind = "problems"
                problems = [line[4:] for line in errs[0]["msg"].split("\n")[1:]]
                expect_payload = {"problems": problems}
            else:
                actual_kind = "errors"
                error_items = []
                for err in errs:
                    path = ".".join(str(segment) for segment in err["loc"])
                    msg = err["msg"].removeprefix("Value error, ") if err["type"] == "value_error" else None
                    error_items.append({"message": msg, "path": path})
                expect_payload = {"errors": error_items}

    if actual_kind != expected_kind:
        print(
            f"ERROR: Caso {name!r} esperaba {expected_kind!r} pero la 0.1 dio {actual_kind!r}",
            file=sys.stderr,
        )
        sys.exit(3)

    return {
        "expect": expect_payload,
        "name": name,
        "yaml": yaml_text,
    }


def generate_packs(aida_dir: Path, out_dir: Path) -> None:
    packs_dir = out_dir / "packs"
    packs_dir.mkdir(parents=True, exist_ok=True)

    plantilla = load_pack(aida_dir / "packs" / "_plantilla" / "pack.yaml")
    (packs_dir / "plantilla.json").write_text(
        _canonical_pack_dump(plantilla) + "\n", encoding="utf-8", newline="\n"
    )

    consultorio = load_pack(aida_dir / "packs" / "health" / "consultorio" / "pack.yaml")
    (packs_dir / "consultorio.json").write_text(
        _canonical_pack_dump(consultorio) + "\n", encoding="utf-8", newline="\n"
    )

    cases = _build_cases()
    lines = []
    for name, expected_kind, yaml_text in cases:
        record = _evaluate_case(name, expected_kind, yaml_text)
        line = json.dumps(record, ensure_ascii=True, sort_keys=True, separators=(",", ":")) + "\n"
        lines.append(line)

    (packs_dir / "cases.jsonl").write_text("".join(lines), encoding="utf-8", newline="\n")
