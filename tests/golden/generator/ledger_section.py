import json
from pathlib import Path


def generate_ledger(out_path: Path) -> None:
    from aida.platform.ledger import GENESIS, chain_digest

    casos = [
        {"at": "2026-11-15T10:20:30-05:00", "kind": "session_opened", "actor": "u1", "actor_name": "Ana Pérez", "workstation": "192.168.1.20", "owner": "", "data": "{}"},
        {"at": "2026-11-15T10:21:00-05:00", "kind": "user_created", "actor": "u1", "actor_name": "Ana Pérez", "workstation": "::1", "owner": "", "data": json.dumps({"role": "recepcion", "user": "u2"}, ensure_ascii=False, sort_keys=True)},
        {"kind": "x", "a\"b": "comillas \" y \\ barra", "ctl": "tab\tnl\nret\rbs\bff\f\x01\x1f\x7f", "uni": "ñ€😀  ", "Z": "mayús", "_": "subrayado", "": "vacía"},
        {"data": json.dumps({"z": "1", "a": "ñ", "m": " "}, ensure_ascii=False, sort_keys=True), "kind": "y"},
    ]
    out_path.parent.mkdir(parents=True, exist_ok=True)
    prev = GENESIS
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        for caso in casos:
            campos = {**caso, "prev": prev}
            digest = chain_digest(campos)
            f.write(json.dumps(campos, ensure_ascii=False, sort_keys=True) + "\t" + digest + "\n")
            prev = digest
