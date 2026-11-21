using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Desk;

/// <summary>Un bloqueo como lo pinta la pantalla: con el nombre de quien lo puso.</summary>
/// <remarks>Los campos de cada bloqueo de <c>_day</c> en la 0.1 (<c>app.py:316-326</c>). El nombre se resuelve aquí, como en
/// <see cref="AppointmentView"/>, y no en cada página.</remarks>
/// <param name="Block">El bloqueo.</param>
/// <param name="CreatedByName">El nombre de quien lo puso, o su identificador si ya no existe.</param>
public sealed record BlockView(Block Block, string CreatedByName);
