"""Sección del generador para archivos dorados de deliberación (N005)."""

from __future__ import annotations

import json
from pathlib import Path
import random
import struct
import sys
from typing import Any

from aida.agents.decide import critical_need, critical_needs, decide, _deliberate
from aida.engine.events import EventLog
from aida.engine.ontology import ActionId, NeedId
from aida.engine.pack import ContentPack, PackError, load_pack
from aida.engine.rng import RngSource
from aida.engine.state import AgentState, ExecutionState
from state_section import _execute_op
from synthetic_packs import write_synthetic_packs

SEEDS = [0, 1, 42, (1 << 63) - 1, 123456789012345]


def _hex_double(val: float) -> str:
    return f"{struct.unpack('>Q', struct.pack('>d', val))[0]:016X}"


def _format_decision(d: Any) -> dict[str, str] | None:
    if d is None:
        return None
    return {
        "action": str(d.action),
        "benefit": _hex_double(d.benefit),
        "cost": _hex_double(d.cost),
        "novelty": _hex_double(d.novelty),
        "utility": _hex_double(d.utility),
    }


def _run_query(
    execution: ExecutionState,
    pack: ContentPack,
    must: NeedId | None,
    forbidden: list[ActionId],
) -> dict[str, Any]:
    f_set = frozenset(forbidden)
    _, discarded = _deliberate(
        execution,
        pack,
        must_address=must,
        phase=None,
        situations=(),
        forbidden=f_set,
    )
    d = decide(
        execution,
        pack,
        must_address=must,
        phase=None,
        situations=(),
        forbidden=f_set,
    )
    return {
        "decision": _format_decision(d),
        "discarded": {str(k): v for k, v in sorted(discarded.items())},
        "forbidden": [str(a) for a in forbidden],
        "must": str(must) if must is not None else None,
    }


def generate_deliberation(aida_dir: Path, out_dir: Path) -> None:
    synth_info = [
        ("sintetico-01", out_dir / "state" / "packs" / "sintetico-01.yaml"),
        ("sintetico-02", out_dir / "state" / "packs" / "sintetico-02.yaml"),
        ("sintetico-03", out_dir / "state" / "packs" / "sintetico-03.yaml"),
    ]

    pack_paths = [
        ("plantilla", aida_dir / "packs" / "_plantilla" / "pack.yaml"),
        ("consultorio", aida_dir / "packs" / "health" / "consultorio" / "pack.yaml"),
    ] + synth_info

    packs: list[tuple[str, ContentPack]] = []
    for name, path in pack_paths:
        try:
            packs.append((name, load_pack(path)))
        except (PackError, Exception) as exc:
            print(f"ERROR: no se pudo cargar pack {name} ({path}): {exc}", file=sys.stderr)
            sys.exit(3)

    other_ops = [
        "decay",
        "floor",
        "recover",
        "refresh",
        "recompose",
        "apply",
        "saturate",
        "begin",
        "advance",
        "idle",
    ]

    cases_lines: list[str] = []
    case_index = 0

    for pack_name, pack in packs:
        sorted_needs = sorted(pack.need_by_id.keys())
        sorted_actions = sorted(pack.action_by_id.keys())

        for agent_spec in pack.agents:
            case_name = f"{pack_name}-{agent_spec.id}"
            seed = SEEDS[case_index % 5]
            case_index += 1

            rng = random.Random(f"estado-{case_name}")
            q_rng = random.Random(f"deliberacion:{case_name}")
            log = EventLog()
            agent = AgentState.from_spec(pack, agent_spec)

            cur_tick = 1
            steps: list[dict[str, Any]] = []

            for i in range(400):
                op_type = "tick" if rng.random() < 0.40 else rng.choice(other_ops)
                _, cur_tick = _execute_op(op_type, pack, agent, log, rng, cur_tick)

                tick = i + 1
                execution = ExecutionState(agent=agent, tick=tick, rng=RngSource(seed))

                queries: list[dict[str, Any]] = [_run_query(execution, pack, None, [])]

                if q_rng.random() < 0.35:
                    queries.append(_run_query(execution, pack, q_rng.choice(sorted_needs), []))

                if q_rng.random() < 0.35:
                    k = q_rng.randint(1, min(3, len(sorted_actions)))
                    forbid = sorted(q_rng.sample(sorted_actions, k))
                    queries.append(_run_query(execution, pack, None, forbid))

                if q_rng.random() < 0.15:
                    k = q_rng.randint(1, min(3, len(sorted_actions)))
                    forbid = sorted(q_rng.sample(sorted_actions, k))
                    queries.append(_run_query(execution, pack, q_rng.choice(sorted_needs), forbid))

                c_needs = [str(nid) for nid in critical_needs(execution, pack)]
                cn = critical_need(execution, pack)
                cn_str = str(cn) if cn is not None else None

                steps.append({
                    "critical_need": cn_str,
                    "critical_needs": c_needs,
                    "queries": queries,
                    "step": i,
                    "tick": tick,
                })

            rec = {
                "agent": str(agent_spec.id),
                "name": case_name,
                "pack": pack_name,
                "seed": seed,
                "steps": steps,
            }
            cases_lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    out_file = out_dir / "deliberation" / "cases.jsonl"
    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(cases_lines)
