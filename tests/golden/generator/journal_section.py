"""Sección del generador para archivos dorados del bucle (N006)."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from aida.engine.loop import Loop
from aida.engine.pack import load_pack
from aida.intent.classify import build_classifier


class TableClassifier:
    """Clasificador basado en tabla de correspondencia directa."""

    def __init__(self, table: dict[str, str | None]) -> None:
        self.table = table

    def classify(self, utterance: str) -> str | None:
        return self.table.get(utterance)


def generate_journals(aida_dir: Path, repo_dir: Path, out_dir: Path) -> None:
    p_path = "packs/_plantilla/pack.yaml"
    c_path = "packs/health/consultorio/pack.yaml"
    s1_path = "tests/golden/state/packs/sintetico-01.yaml"
    s2_path = "tests/golden/state/packs/sintetico-02.yaml"
    s3_path = "tests/golden/state/packs/sintetico-03.yaml"

    scripts = [
        dict(
            name="plantilla-1312-300",
            pack=p_path,
            agent="centinela_01",
            seed=1312,
            ticks=300,
            capacity=64,
            policy="mod3",
            classifier="real",
            inbox=[
                [5, "libera espacio en disco"],
                [40, "renueva los certificados"],
                [90, "hola, ¿qué tal?"],
                [150, "escala el servicio"],
            ],
        ),
        dict(
            name="plantilla-sin-respuesta",
            pack=p_path,
            agent="centinela_01",
            seed=7,
            ticks=400,
            capacity=8,
            policy="none",
            classifier={
                "disco": "pedir_liberar_espacio",
                "certificados": "pedir_renovar_certificados",
                "réplicas": "pedir_escalar_replicas",
                "??": "intencion_que_no_existe",
            },
            inbox=[
                [3, "disco"],
                [3, "certificados"],
                [60, "??"],
                [61, "nada"],
                [120, "réplicas"],
                [200, "certificados"],
                [399, "disco"],
            ],
        ),
        dict(
            name="consultorio-sin-clasificador",
            pack=c_path,
            agent="asistente_01",
            seed=42,
            ticks=300,
            capacity=4,
            policy="none",
            classifier=None,
            inbox=[[1, "buenos días"], [10, "¿hay cita mañana?"], [10, "gracias"]],
        ),
        dict(
            name="sintetico01-ag1",
            pack=s1_path,
            agent="ag1",
            seed=0,
            ticks=600,
            capacity=4,
            policy="none",
            classifier={},
            inbox=[[30, "hola"]],
        ),
        dict(
            name="sintetico02-agente01",
            pack=s2_path,
            agent="agente_01",
            seed=2**63 - 1,
            ticks=600,
            capacity=4,
            policy="none",
            classifier=None,
            inbox=[],
        ),
        dict(
            name="sintetico02-agente04",
            pack=s2_path,
            agent="agente_04",
            seed=123456789012345,
            ticks=600,
            capacity=4,
            policy="none",
            classifier=None,
            inbox=[],
        ),
        dict(
            name="plantilla-peticiones",
            pack=p_path,
            agent="centinela_01",
            seed=0,
            ticks=400,
            capacity=400,
            policy="none",
            classifier={"disco": "pedir_liberar_espacio"},
            inbox=[[t, "disco"] for t in range(1, 400, 3)],
        ),
        dict(
            name="sintetico03-robot01",
            pack=s3_path,
            agent="robot_01",
            seed=1,
            ticks=400,
            capacity=4,
            policy="none",
            classifier=None,
            inbox=[],
        ),
    ]

    journals_dir = out_dir / "journals"
    journals_dir.mkdir(parents=True, exist_ok=True)

    resolved_scripts: list[dict[str, Any]] = []

    for sc in scripts:
        pack_file = repo_dir / sc["pack"]
        pack = load_pack(pack_file)
        spec = next(a for a in pack.agents if a.id == sc["agent"])

        clf_obj: Any = None
        resolved_classifier: dict[str, str | None] | None = None

        if sc["classifier"] == "real":
            real_clf = build_classifier(pack)
            resolved_classifier = {
                text: real_clf.classify(text) for _, text in sc["inbox"]
            }
            clf_obj = real_clf
        elif sc["classifier"] is not None:
            resolved_classifier = dict(sc["classifier"])
            clf_obj = TableClassifier(resolved_classifier)
        else:
            resolved_classifier = None
            clf_obj = None

        final_script = dict(sc)
        final_script["classifier"] = resolved_classifier
        resolved_scripts.append(final_script)

        loop = Loop.start(
            pack,
            spec,
            seed=sc["seed"],
            inbox_capacity=sc["capacity"],
            intent=clf_obj,
        )

        for t, text in sc["inbox"]:
            loop.inbox.push(tick=t, owner=spec.id, content=text)

        for _ in range(sc["ticks"]):
            loop.step()
            if sc["policy"] == "mod3":
                for action_id in sorted(loop.authority.pending):
                    m = loop.execution.tick % 3
                    if m == 0:
                        loop.deny(action_id)
                    elif m == 1:
                        loop.approve(action_id)

        journal_file = journals_dir / f"{sc['name']}.jsonl"
        with open(journal_file, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(list(loop.log.to_jsonl())) + "\n")

    scripts_file = journals_dir / "scripts.json"
    with open(scripts_file, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(resolved_scripts, ensure_ascii=False, indent=1) + "\n")
