using Aida.Arms.Agenda.Storage;
using Aida.Engine.Text;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Buscar contactos por nombre o por teléfono.</summary>
/// <remarks>
/// <para>Es <c>find_clients</c> de la 0.1 (<c>tool.py:441-456</c>). Si la búsqueda tiene alguna letra, se busca por nombre
/// plegado (<see cref="TextFold"/>); si no, por dígitos. Hasta <see cref="Limit"/>, por nombre plegado y luego por id.</para>
/// <para>Cambia respecto a la 0.1 (debate de N010, D4): los dígitos se buscan en el teléfono normalizado y no en el que se
/// escribió. En la 0.1, «3005550142» no encontraba a quien se guardó como «300 555 0142».</para>
/// <para>El orden por nombre es ordinal, en unidades UTF-16. La 0.1 ordena por punto de código, y solo difieren con
/// caracteres fuera del plano básico, como un emoji en un nombre.</para>
/// </remarks>
internal static class ClientSearch
{
    internal const int Limit = 20;

    /// <summary>Los contactos que casan con la búsqueda.</summary>
    /// <param name="store">El almacén de la petición.</param>
    /// <param name="query">Lo tecleado.</param>
    /// <returns>Hasta <see cref="Limit"/> contactos; ninguno si la búsqueda está vacía o no tiene dígitos ni letras.</returns>
    internal static IReadOnlyList<Client> Find(AgendaStore store, string query)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        IEnumerable<Client> found;
        if (text.Any(char.IsLetter))
        {
            var needle = TextFold.Fold(text);
            found = store.Clients().Where(client => TextFold.Fold(client.Name).Contains(needle, StringComparison.Ordinal));
        }
        else
        {
            var digits = PhoneNumbers.Normalize(text);
            if (digits.Length == 0)
            {
                return [];
            }

            found = store.Clients().Where(client => PhoneNumbers.Normalize(client.Phone).Contains(digits, StringComparison.Ordinal));
        }

        return [.. found
            .OrderBy(client => TextFold.Fold(client.Name), StringComparer.Ordinal)
            .ThenBy(client => client.Id)
            .Take(Limit)];
    }
}
