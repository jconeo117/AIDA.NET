namespace Aida.Arms.Agenda.Storage;

/// <summary>Las tablas de la agenda, por versión (ADR 0032).</summary>
/// <remarks>Las de la 0.1 (<c>store.py:110-140</c>), con tres cambios: el prefijo <c>agenda_</c>, para que ningún otro brazo
/// las pise; <c>start_utc</c>, para buscar por rango con un índice; y, desde la versión 2, la tabla de los bloqueos
/// (<c>store.py:131-138</c>). La 0.1 filtraba en Python porque las fechas con distinto desfase no se comparan como texto
/// (<c>store.py:299</c>).</remarks>
internal static class AgendaSchema
{
    internal const string Version1 = """
        CREATE TABLE agenda_clients (
            id INTEGER PRIMARY KEY,
            name BLOB NOT NULL,
            phone BLOB NOT NULL,
            phone_index TEXT NOT NULL UNIQUE
        ) STRICT;
        CREATE TABLE agenda_appointments (
            id INTEGER PRIMARY KEY,
            client INTEGER NOT NULL REFERENCES agenda_clients(id),
            service TEXT NOT NULL,
            resource TEXT NOT NULL,
            start TEXT NOT NULL,
            start_utc INTEGER NOT NULL,
            minutes INTEGER NOT NULL CHECK (minutes > 0),
            state TEXT NOT NULL CHECK (state IN ('booked', 'cancelled', 'attended', 'no_show')),
            created_by TEXT NOT NULL,
            updated_by TEXT NOT NULL
        ) STRICT;
        CREATE INDEX agenda_appointments_start ON agenda_appointments(start_utc);
        """;

    internal const string Version2 = """
        CREATE TABLE agenda_blocks (
            id INTEGER PRIMARY KEY,
            resource TEXT NOT NULL,
            start TEXT NOT NULL,
            start_utc INTEGER NOT NULL,
            ends TEXT NOT NULL,
            ends_utc INTEGER NOT NULL,
            reason BLOB NOT NULL,
            created_by TEXT NOT NULL,
            CHECK (ends_utc > start_utc)
        ) STRICT;
        CREATE INDEX agenda_blocks_start ON agenda_blocks(start_utc);
        """;

    internal static IReadOnlyList<string> Steps { get; } = [Version1, Version2];
}
