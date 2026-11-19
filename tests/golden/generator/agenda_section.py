"""Sección del generador para los dorados de la agenda (N008).

Ejecuta la puerta de acciones y la herramienta de la agenda de la 0.1 sobre guiones de peticiones, con una base
SQLite temporal, una clave Fernet de prueba y un reloj que fija cada guion. Escribe, por guion, los eventos de la
puerta en JSON canónico, el estado final de las citas y los huecos libres de cada servicio el día del guion.
"""

from __future__ import annotations

import json
import tempfile
from datetime import date, datetime, timedelta, timezone
from pathlib import Path

TZ = timezone(timedelta(hours=-5))


def _s(hm: str, day: str = "2026-11-16") -> str:
    return f"{day}T{hm}:00-05:00"


SCRIPTS = [
    {
        "name": "consultorio-dia",
        "template": "consultorio",
        "day": "2026-11-16",
        "now": "2026-11-16T07:30:00-05:00",
        "requests": [
            ["agendar", {"name": "Ana Pérez", "phone": "300 555 0142", "service": "control", "start": _s("08:00")}],
            ["agendar", {"name": "Luis Gómez", "phone": "3105550000", "service": "control", "start": _s("08:00")}],
            ["agendar", {"name": "Luis Gómez", "phone": "3105550000", "service": "control", "start": _s("09:00")}],
            ["agendar", {"name": "Luis Gómez", "phone": "3105550000", "service": "control", "start": _s("09:00")}],
            ["agendar", {"name": "Eva", "phone": "1", "service": "primera_vez", "start": _s("09:00"), "resource": "consultorio_2"}],
            ["agendar", {"name": "Eva", "phone": "1", "service": "control", "start": _s("07:00")}],
            ["agendar", {"name": "", "phone": "1", "service": "control", "start": _s("10:00")}],
            ["agendar", {"name": "Eva", "phone": "1", "service": "x", "start": _s("10:00")}],
            ["agendar", {"name": "Eva", "phone": "1", "service": "control", "start": "2026-11-16T10:00:00"}],
            ["agendar", {"name": "Eva", "phone": "1", "service": "control", "start": _s("13:00")}],
            ["reprogramar", {"appointment": "1", "start": _s("08:30")}],
            ["reprogramar", {"appointment": "2", "start": _s("08:30")}],
            ["cancelar", {"appointment": "2"}],
            ["cancelar", {"appointment": "2"}],
            ["atendida", {"appointment": "1"}],
            ["no_asistio", {"appointment": "99"}],
            ["volar", {}],
            ["agendar", {"name": "Ana P.", "phone": "3005550142", "service": "procedimiento", "start": _s("10:00")}],
        ],
    },
    {
        "name": "consultorio-bordes",
        "template": "consultorio",
        "day": "2026-11-16",
        "now": "2026-11-16T07:30:00-05:00",
        "requests": [
            ["agendar", {"name": "A", "phone": "11", "service": "control", "start": _s("08:00"), "resource": "consultorio_1"}],
            ["agendar", {"name": "B", "phone": "22", "service": "control", "start": _s("08:30"), "resource": "consultorio_1"}],
            ["agendar", {"name": "C", "phone": "33", "service": "control", "start": _s("08:15"), "resource": "consultorio_1"}],
            ["agendar", {"name": "D", "phone": "44", "service": "procedimiento", "start": _s("12:30"), "resource": "consultorio_1"}],
            ["agendar", {"name": "D", "phone": "44", "service": "procedimiento", "start": _s("12:00"), "resource": "consultorio_1"}],
            ["cancelar", {"appointment": "2"}],
            ["agendar", {"name": "E", "phone": "55", "service": "control", "start": _s("08:30"), "resource": "consultorio_1"}],
            ["reprogramar", {"appointment": "1", "start": _s("08:00")}],
            ["reprogramar", {"appointment": "1", "start": _s("09:00"), "resource": "consultorio_2"}],
            ["reprogramar", {"appointment": "4", "start": _s("09:00"), "resource": "consultorio_2"}],
            ["reprogramar", {"appointment": "4", "start": _s("09:00"), "resource": "nadie"}],
            ["reprogramar", {"appointment": "3", "start": _s("06:00")}],
            ["reprogramar", {"appointment": "3", "start": "mañana"}],
            ["reprogramar", {"appointment": "abc", "start": _s("10:00")}],
            ["agendar", {"name": "F", "phone": "66", "service": "primera_vez", "start": _s("09:30"), "resource": "consultorio_2"}],
            ["agendar", {"name": "G", "phone": "77", "service": "control", "start": _s("09:30")}],
            ["atendida", {"appointment": "1"}, "2026-11-16T09:10:00-05:00"],
            ["no_asistio", {"appointment": "3"}, "2026-11-16T09:10:00-05:00"],
            ["atendida", {"appointment": "1"}, "2026-11-16T09:10:00-05:00"],
            ["cancelar", {"appointment": "3"}, "2026-11-16T09:10:00-05:00"],
            ["agendar", {"name": "H", "phone": "88", "service": "control", "start": _s("09:00")}, "2026-11-16T09:10:00-05:00"],
        ],
    },
    {
        "name": "peluqueria-sabado",
        "template": "peluqueria",
        "day": "2026-11-21",
        "now": "2026-11-21T07:00:00-05:00",
        "requests": [
            ["agendar", {"name": "Uno", "phone": "301", "service": "corte", "start": _s("08:00", "2026-11-21")}],
            ["agendar", {"name": "Dos", "phone": "302", "service": "corte", "start": _s("08:00", "2026-11-21")}],
            ["agendar", {"name": "Tres", "phone": "303", "service": "corte", "start": _s("08:00", "2026-11-21")}],
            ["agendar", {"name": "Cuatro", "phone": "304", "service": "color", "start": _s("14:45", "2026-11-21")}],
            ["agendar", {"name": "Cuatro", "phone": "304", "service": "color", "start": _s("14:30", "2026-11-21")}],
            ["agendar", {"name": "Cinco", "phone": "305", "service": "manicure", "start": _s("08:30", "2026-11-21")}],
            ["agendar", {"name": "Cinco", "phone": "305", "service": "manicure", "start": _s("09:00", "2026-11-21")}],
            ["agendar", {"name": "Uno bis", "phone": "301", "service": "barba", "start": _s("08:30", "2026-11-21"), "resource": "silla_2"}],
            ["reprogramar", {"appointment": "4", "start": _s("08:15", "2026-11-21")}],
        ],
    },
]


def generate_agenda(aida_dir: Path, repo_dir: Path, out_path: Path) -> None:
    from cryptography.fernet import Fernet

    from aida.arms.agenda.setup import load_setup
    from aida.arms.agenda.store import SqliteAgendaStore
    from aida.arms.agenda.tool import ACTIONS, AgendaTool, free_slots
    from aida.engine.actions import ActionGate, ActionRequest
    from aida.engine.events import EventLog
    from aida.engine.ontology import AgentId

    lines: list[str] = []
    for script in SCRIPTS:
        setup = load_setup(repo_dir / "packs" / "agenda" / script["template"] / "agenda.yaml")
        with tempfile.TemporaryDirectory() as tmp:
            store = SqliteAgendaStore(Path(tmp) / "agenda.sqlite", Fernet.generate_key())
            reloj = {"now": datetime.fromisoformat(script["now"])}
            log = EventLog()
            ticks = [0]

            def tick() -> int:
                ticks[0] += 1
                return ticks[0]

            gate = ActionGate(
                actions=ACTIONS,
                tool=AgendaTool(store, setup, lambda: reloj["now"]),
                log=log,
                owner=AgentId("agenda"),
                tick=tick,
            )
            requests = []
            for entry in script["requests"]:
                action, arguments = entry[0], dict(entry[1])
                if len(entry) > 2:
                    reloj["now"] = datetime.fromisoformat(entry[2])
                arguments["by"] = "u1"
                gate.submit(ActionRequest(action=action, arguments=arguments, requested_by="u1"))
                requests.append({"action": action, "arguments": entry[1], "now": reloj["now"].isoformat()})
            day = date.fromisoformat(script["day"])
            start = datetime.combine(day, datetime.min.time(), tzinfo=TZ)
            appointments = []
            for c in store.between(start - timedelta(days=7), start + timedelta(days=7)):
                client = store.client(c.client)
                appointments.append({
                    "id": c.id, "name": client.name, "phone": client.phone, "service": c.service,
                    "resource": c.resource, "start": c.start.isoformat(), "minutes": c.minutes,
                    "state": c.state.value, "created_by": c.created_by, "updated_by": c.updated_by,
                })
            free = {
                s.id: [[r, h.isoformat()] for r, h in free_slots(store, setup, day, s.id, reloj["now"])]
                for s in setup.services
            }
            lines.append(json.dumps({
                "appointments": appointments,
                "day": script["day"],
                "events": list(log.to_jsonl()),
                "free": free,
                "name": script["name"],
                "requests": requests,
                "start_now": script["now"],
                "template": script["template"],
            }, ensure_ascii=False, sort_keys=True) + "\n")
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)
