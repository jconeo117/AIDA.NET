"""Sección del generador para archivos dorados de estado (N004)."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import random
import sys
from typing import Any

from aida.engine.events import EventKind, EventLog
from aida.engine.ontology import GoalStatus
from aida.engine.pack import ContentPack, PackError, load_pack
from aida.engine.state import AgentState, PerformingAction
from synthetic_packs import write_synthetic_packs


def canonical_agent_dump(agent: AgentState) -> str:
    d = {
        "agent_id": str(agent.agent_id),
        "caps": {str(k): v for k, v in sorted(agent.caps.items())},
        "costs": {str(k): v for k, v in sorted(agent.costs.items())},
        "debt": {str(k): v for k, v in sorted(agent.debt.items())},
        "floor_streak": {str(k): v for k, v in sorted(agent.floor_streak.items())},
        "gains": {str(k): v for k, v in sorted(agent.gains.items())},
        "goals": {str(k): str(v) for k, v in sorted(agent.goals.items())},
        "need_weights": {str(k): v for k, v in sorted(agent.need_weights.items())},
        "needs": {str(k): v for k, v in sorted(agent.needs.items())},
        "performing": (
            {
                "action": str(agent.performing.action),
                "ticks_remaining": agent.performing.ticks_remaining,
                "total_ticks": agent.performing.total_ticks,
            }
            if agent.performing is not None
            else None
        ),
        "saturation": {str(k): v for k, v in sorted(agent.saturation.items())},
        "strain": agent.strain,
        "tastes": {str(k): v for k, v in sorted(agent.tastes.items())},
        "temperament": {
            "critical_need_threshold": agent.temperament.critical_need_threshold,
            "learning_rate": agent.temperament.learning_rate,
            "loss_aversion": agent.temperament.loss_aversion,
            "novelty_weight": agent.temperament.novelty_weight,
            "switch_cost": agent.temperament.switch_cost,
            "temperature": agent.temperament.temperature,
        },
        "traits": {str(k): v for k, v in sorted(agent.traits.items())},
    }
    return json.dumps(d, sort_keys=True, ensure_ascii=False, separators=(",", ":"))


def _execute_op(
    op_type: str,
    pack: ContentPack,
    agent: AgentState,
    log: EventLog,
    rng: random.Random,
    current_tick: int,
) -> tuple[dict[str, Any], int]:
    if op_type == "tick":
        agent.decay(pack.need_by_id)
        agent.advance_floor_streak(pack.need_by_id, event_log=log, owner=agent.agent_id, tick=current_tick)
        agent.recover_novelty(pack.tuning.saturation_recovery)
        agent.refresh_state(pack.need_by_id)
        agent.recompose(pack)
        return {"op": "tick", "tick": current_tick}, current_tick + 1
    if op_type == "decay":
        agent.decay(pack.need_by_id)
        return {"op": "decay"}, current_tick
    if op_type == "floor":
        agent.advance_floor_streak(pack.need_by_id, event_log=log, owner=agent.agent_id, tick=current_tick)
        return {"op": "floor", "tick": current_tick}, current_tick + 1
    if op_type == "recover":
        agent.recover_novelty(pack.tuning.saturation_recovery)
        return {"op": "recover"}, current_tick
    if op_type == "refresh":
        agent.refresh_state(pack.need_by_id)
        return {"op": "refresh"}, current_tick
    if op_type == "recompose":
        sits = [str(s) for s in rng.choices([s.id for s in pack.situations], k=rng.randint(0, 2))] if pack.situations else []
        agent.recompose(pack, active_situations=sits)
        return {"op": "recompose", "situations": sits}, current_tick
    if op_type == "apply":
        act = rng.choice(pack.actions)
        agent.apply(act.effects, pack.need_by_id)
        return {"op": "apply", "action": str(act.id)}, current_tick
    if op_type == "saturate":
        act = rng.choice(pack.actions)
        agent.saturate(act.id, act.saturation_gain)
        return {"op": "saturate", "action": str(act.id)}, current_tick
    if op_type == "begin":
        act = rng.choice(pack.actions)
        ticks = rng.randint(1, 5)
        total = ticks + rng.randint(0, 3)
        agent.performing = PerformingAction(act.id, ticks, total)
        return {"op": "begin", "action": str(act.id), "ticks": ticks, "total": total}, current_tick
    if op_type == "advance":
        agent.advance()
        return {"op": "advance"}, current_tick
    if op_type == "idle":
        agent.performing = None
        return {"op": "idle"}, current_tick
    raise ValueError(f"Operación desconocida: {op_type}")


def generate_state(aida_dir: Path, out_dir: Path) -> None:
    synth_info = write_synthetic_packs(out_dir)

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

    metrics = {
        "debt_seen": False,
        "escalated_count": 0,
        "cap_clipped_to_1": False,
        "locked_goal_seen": False,
        "unrecognized_goal_seen": False,
        "composed_taste_seen": False,
    }

    cases_lines: list[str] = []
    other_ops = ["decay", "floor", "recover", "refresh", "recompose", "apply", "saturate", "begin", "advance", "idle"]

    for pack_name, pack in packs:
        for agent_spec in pack.agents:
            case_name = f"{pack_name}-{agent_spec.id}"
            rng = random.Random(f"estado-{case_name}")
            log = EventLog()
            agent = AgentState.from_spec(pack, agent_spec)

            # Métricas estáticas del agente
            if any(s is GoalStatus.LOCKED for s in agent.goals.values()):
                metrics["locked_goal_seen"] = True
            if any(g.id not in pack.goal_by_id for g in agent_spec.goals):
                metrics["unrecognized_goal_seen"] = True
            if any(len([t for t in agent_spec.tastes if t.target == tg]) > 1 for tg in {t.target for t in agent_spec.tastes}):
                metrics["composed_taste_seen"] = True
            if any(c == 1.0 for c in agent.caps.values()):
                metrics["cap_clipped_to_1"] = True

            init_dump = canonical_agent_dump(agent)
            ops: list[dict[str, Any]] = []
            steps: list[str] = []
            cur_tick = 1

            for _ in range(400):
                op_type = "tick" if rng.random() < 0.40 else rng.choice(other_ops)
                op_rec, cur_tick = _execute_op(op_type, pack, agent, log, rng, cur_tick)
                ops.append(op_rec)

                if any(v > 0.0 for v in agent.debt.values()):
                    metrics["debt_seen"] = True

                step_dump = canonical_agent_dump(agent)
                step_hash = hashlib.blake2b(step_dump.encode("utf-8"), digest_size=16).hexdigest()
                steps.append(step_hash)

            final_dump = canonical_agent_dump(agent)
            case_events = list(log.to_jsonl())
            metrics["escalated_count"] += sum(1 for e in log.of_kind(EventKind.NEED_ESCALATED))

            rec = {
                "agent": str(agent_spec.id),
                "events": case_events,
                "final": final_dump,
                "initial": init_dump,
                "name": case_name,
                "ops": ops,
                "pack": pack_name,
                "steps": steps,
            }
            cases_lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    # Validación de intención (§B.3, código 3)
    if not metrics["debt_seen"]:
        print("ERROR: No se observó ninguna deuda > 0", file=sys.stderr)
        sys.exit(3)
    if metrics["escalated_count"] < 3:
        print(f"ERROR: Solo se observaron {metrics['escalated_count']} need_escalated (esperados >= 3)", file=sys.stderr)
        sys.exit(3)
    if not metrics["cap_clipped_to_1"]:
        print("ERROR: Ningún techo recortado a 1", file=sys.stderr)
        sys.exit(3)
    if not metrics["locked_goal_seen"]:
        print("ERROR: Ninguna meta locked", file=sys.stderr)
        sys.exit(3)
    if not metrics["unrecognized_goal_seen"]:
        print("ERROR: Ninguna meta del agente ignorada por no estar en el pack", file=sys.stderr)
        sys.exit(3)
    if not metrics["composed_taste_seen"]:
        print("ERROR: Ningún gusto compuesto", file=sys.stderr)
        sys.exit(3)

    out_file = out_dir / "state" / "cases.jsonl"
    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(cases_lines)
