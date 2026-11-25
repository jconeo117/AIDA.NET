"""Sección del generador para dorados de tokens, recuento, coseno y clasificador (N012)."""

from __future__ import annotations

import json
from pathlib import Path
import struct
import yaml

from aida.engine.pack import load_pack
from aida.intent.classify import build_classifier
from aida.textmatch import cosine, fold, token_spans, tokenize, vectorize


def _to_hex64(val: float) -> str:
    return f"{struct.unpack('>Q', struct.pack('>d', val))[0]:016X}"


TEXTMATCH_TEXTS = [
    "",
    "   ",
    "hola",
    "HOLA",
    "Hola Mundo",
    "áéíóúüñ ÁÉÍÓÚÜÑ",
    "El pingüino y el ñandú están en la montaña",
    "¡Hola! ¿Cómo estás? (Bien, gracias...)",
    "cita 123 para el 2026-11-25 a las 10:30",
    "   muchos    espacios   entre    palabras   ",
    "palabra123con456numeros789",
    "a b c d e f g",
    "CONTROL",
    "consulta de primera vez con el Dr. Pérez",
    "300 555 0142",
    "300-555-0142",
    "cita, control; procedimiento: revisión.",
    "mañana por la tarde a las 3",
    "cancela la cita de Juan Ruiz",
    "agendame una cita de control para Ana",
]

COSINE_PAIRS = [
    (2, 2),
    (2, 3),
    (2, 4),
    (4, 12),
    (0, 4),
    (0, 0),
    (1, 2),
    (6, 6),
    (7, 4),
    (8, 18),
    (13, 19),
    (14, 15),
    (16, 19),
    (17, 18),
    (9, 11),
]

PLANTILLA_PHRASES = [
    "libera espacio en disco",
    "el disco esta lleno",
    "necesito mas espacio libre",
    "borra los archivos temporales",
    "renueva los certificados",
    "los certificados estan por vencer",
    "actualiza las credenciales de acceso",
    "agrega mas replicas",
    "el sistema necesita mas redundancia",
    "escala el servicio",
    "agrega renueva",
    "buenos dias",
    "imprime la factura",
    "hola mundo",
    "disco",
    "certificados",
    "replicas",
    "servicio",
    "temporales",
    "",
]

AGENDA_PHRASES = [
    "cita para Ana Pérez, control",
    "agendame al 3005551234",
    "cita para Carlos Díaz",
    "cita para Ana",
    "agenda una cita",
    "nueva cita",
    "dar una cita",
    "que huecos hay",
    "que hay libre",
    "disponibilidad",
    "cancela la cita de Juan",
    "cancela la cita de Ana del martes",
    "mueve la cita de Ana del martes al jueves a las 11",
    "Ana no va a venir, anula su cita",
    "reprograma la cita para mañana",
    "imprime la factura",
    "buenos días",
    "cuál es la dirección",
    "anula la cita de Pedro",
    "quita la cita por favor",
    "pasa la cita de Luis para el viernes",
    "necesito una cita de control urgente",
    "hay espacio hoy para atender a María",
    "huecos libres en la tarde",
    "reserva una cita para mañana",
    "cambia la cita para las 4",
    "cambiar la hora de la cita",
    "muchas gracias hasta luego",
]


def generate_intent(aida_dir: Path, repo_dir: Path, out_file: Path) -> None:
    lines: list[str] = []
    stop_set = frozenset(["el", "la", "de", "para", "en", "a", "y", "con"])

    # 1. Textmatch: spans y vectorize
    for idx, text in enumerate(TEXTMATCH_TEXTS):
        spans = [
            {"token": tok, "start": st, "end": en}
            for tok, st, en in token_spans(text)
        ]
        tokens = list(tokenize(text))
        v_raw = vectorize(text)
        v_stop = vectorize(text, stopwords=stop_set)
        rec = {
            "kind": "textmatch_text",
            "name": f"text_{idx:02d}",
            "text": text,
            "spans": spans,
            "tokens": tokens,
            "stopwords": sorted(stop_set),
            "vector_raw": v_raw,
            "vector_stopwords": v_stop,
        }
        lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    # 2. Textmatch: cosine de 15 pares
    for idx, (i, j) in enumerate(COSINE_PAIRS):
        t1, t2 = TEXTMATCH_TEXTS[i], TEXTMATCH_TEXTS[j]
        v1 = vectorize(t1)
        v2 = vectorize(t2)
        c = cosine(v1, v2)
        rec = {
            "kind": "textmatch_cosine",
            "name": f"cosine_{idx:02d}",
            "text_a": t1,
            "text_b": t2,
            "vector_a": v1,
            "vector_b": v2,
            "cosine": c,
            "cosine_hex": _to_hex64(c),
            "cosine_float_hex": c.hex(),
        }
        lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    # 3. La plantilla: build_classifier con 20 frases
    plantilla_pack = load_pack(repo_dir / "packs" / "_plantilla" / "pack.yaml")
    plantilla_clf = build_classifier(plantilla_pack)
    assert plantilla_clf is not None, "El pack de plantilla debe declarar intenciones"

    for idx, phrase in enumerate(PLANTILLA_PHRASES):
        expected_id = plantilla_clf.classify(phrase)
        rec = {
            "kind": "plantilla_classify",
            "name": f"plantilla_{idx:02d}",
            "phrase": phrase,
            "expected_intent": expected_id,
        }
        lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    # 4. El catálogo de la agenda: FromCatalog
    agenda_yaml = repo_dir / "packs" / "agenda" / "intents.yaml"
    raw_agenda = yaml.safe_load(agenda_yaml.read_text(encoding="utf-8"))
    threshold = float(raw_agenda["threshold"])
    agenda_stopwords = frozenset(fold(w) for w in raw_agenda.get("stopwords", []))

    agenda_refs: list[tuple[str, dict[str, int]]] = []
    for item in sorted(raw_agenda["intents"], key=lambda x: str(x["id"])):
        iid = str(item["id"])
        for ex in item["examples"]:
            agenda_refs.append((iid, vectorize(fold(ex), stopwords=agenda_stopwords)))

    for idx, phrase in enumerate(AGENDA_PHRASES):
        q = vectorize(fold(phrase), stopwords=agenda_stopwords)
        best_score = 0.0
        best_id = None
        if q:
            for iid, vec in agenda_refs:
                sc = cosine(q, vec)
                if sc > best_score:
                    best_score = sc
                    best_id = iid
        final_id = best_id if (best_id is not None and best_score >= threshold) else None
        rec = {
            "kind": "agenda_classify",
            "name": f"agenda_{idx:02d}",
            "phrase": phrase,
            "expected_intent": final_id,
            "best_score": best_score,
            "best_score_hex": _to_hex64(best_score),
            "best_score_float_hex": best_score.hex(),
        }
        lines.append(json.dumps(rec, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")

    out_file.parent.mkdir(parents=True, exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)
