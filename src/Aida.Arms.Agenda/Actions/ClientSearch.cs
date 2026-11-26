using Aida.Arms.Agenda.Storage;
using Aida.Engine.Text;
using Aida.Platform.People;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Buscar personas por nombre, por teléfono o por documento.</summary>
/// <remarks>
/// <para>Es <c>find_clients</c> de la 0.1 (<c>tool.py:441-456</c>). Si la búsqueda tiene alguna letra, se busca por nombre
/// plegado (<see cref="TextFold"/>); si no, por dígitos. Hasta <see cref="Limit"/>, por nombre plegado y luego por id.</para>
/// <para>Cambia respecto a la 0.1 (debate de N010, D4): los dígitos se buscan en el teléfono normalizado y no en el que se
/// escribió. En la 0.1, «3005550142» no encontraba a quien se guardó como «300 555 0142».</para>
/// <para>Con dígitos, también encuentra por el número de documento (N015, D7): sin esto, quien tiene el documento en la mano
/// y no sabe el teléfono de su madre no encontraría al niño. Es la misma búsqueda de siempre, con una columna más.</para>
/// <para>El orden por nombre es ordinal, en unidades UTF-16. La 0.1 ordena por punto de código, y solo difieren con
/// caracteres fuera del plano básico, como un emoji en un nombre.</para>
/// </remarks>
internal static class ClientSearch
{
    internal const int Limit = 20;

    /// <summary>Las personas que casan con la búsqueda.</summary>
    /// <param name="people">El almacén de las personas de la petición.</param>
    /// <param name="query">Lo tecleado.</param>
    /// <returns>Hasta <see cref="Limit"/> personas; ninguna si la búsqueda está vacía o no tiene dígitos ni letras.</returns>
    internal static IReadOnlyList<Client> Find(PersonStore people, string query)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        IEnumerable<Person> found;
        if (text.Any(char.IsLetter))
        {
            var needle = TextFold.Fold(text);
            found = people.All().Where(person => TextFold.Fold(person.Name).Contains(needle, StringComparison.Ordinal));
        }
        else
        {
            var digits = PhoneNumbers.Normalize(text);
            if (digits.Length == 0)
            {
                return [];
            }

            found = people.All().Where(person =>
                PhoneNumbers.Normalize(person.Phone).Contains(digits, StringComparison.Ordinal)
                || Digits(person.Details.DocumentNumber).Contains(digits, StringComparison.Ordinal));
        }

        return [.. found
            .OrderBy(person => TextFold.Fold(person.Name), StringComparer.Ordinal)
            .ThenBy(person => person.Id)
            .Take(Limit)
            .Select(Client.From)];
    }

    private static string Digits(string? number) => number is null ? string.Empty : string.Concat(number.Where(char.IsDigit));
}
