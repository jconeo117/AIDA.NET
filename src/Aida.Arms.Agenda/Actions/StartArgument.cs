using System.Globalization;
using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Una hora de inicio escrita como la escribe la pantalla: con segundos y con desfase.</summary>
/// <remarks>Es <c>_date</c> de la 0.1 (<c>tool.py:54-62</c>): sin desfase no se sabe qué hora es, y no se agenda. Más
/// estricto que <c>datetime.fromisoformat</c>: solo <c>yyyy-MM-ddTHH:mm:ss±HH:mm</c>, la única forma que produce la
/// pantalla (debate de N008).</remarks>
internal static class StartArgument
{
    internal static bool TryParse(string? text, out DateTimeOffset start) =>
        DateTimeOffset.TryParseExact(text, AgendaStore.StartFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out start);

    internal static string Format(DateTimeOffset start) => start.ToString(AgendaStore.StartFormat, CultureInfo.InvariantCulture);
}
