using System.Globalization;
using Aida.Engine.Actions;
using Aida.Platform.People;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Los datos opcionales de una persona que se cambian desde su ficha: el documento, la fecha de nacimiento y el sexo (N015, D6).</summary>
/// <remarks>
/// <para>Las reglas van en el orden de D6, y la primera que falla es la que se explica a quien pidió. Un campo que no llega en
/// la petición no cambia; uno que llega vacío se borra. Así una ficha que no muestra un campo no lo borra por no mandarlo, y
/// una que lo muestra vacío sí lo borra.</para>
/// <para>El documento se compara normalizado (<see cref="PersonDocument"/>), pero se guarda como se escribió, recortado: lo
/// que ve la recepcionista es lo que escribió.</para>
/// </remarks>
internal static class ContactDetails
{
    /// <summary>El número más corto de documento que se acepta, sin separadores.</summary>
    internal const int MinDocumentLength = 3;

    /// <summary>El número más largo de documento que se acepta, sin separadores.</summary>
    internal const int MaxDocumentLength = 20;

    private const string BirthFormat = "yyyy-MM-dd";

    /// <summary>Lo que cambia la ficha en los datos opcionales de una persona.</summary>
    /// <param name="people">El almacén de las personas de la petición, para ver si el documento es de otra.</param>
    /// <param name="catalog">Los tipos de documento y los sexos aceptados.</param>
    /// <param name="today">La fecha de hoy.</param>
    /// <param name="current">La persona, con sus datos de hoy.</param>
    /// <param name="arguments">Los argumentos de la petición.</param>
    /// <param name="details">Los datos que quedan si no hay rechazo.</param>
    /// <returns>El primer rechazo en el orden de D6, o <see langword="null"/> si los datos son válidos.</returns>
    internal static ToolResult? Merge(PersonStore people, PersonCatalog catalog, DateOnly today, Person current, IReadOnlyDictionary<string, string> arguments, out PersonDetails details)
    {
        details = current.Details;
        if (Document(people, catalog, current.Id, arguments, details, out var afterDocument) is { } documentRejection)
        {
            return documentRejection;
        }

        if (Birth(today, arguments, afterDocument, out var afterBirth) is { } birthRejection)
        {
            return birthRejection;
        }

        return Sex(catalog, arguments, afterBirth, out details);
    }

    private static ToolResult? Document(PersonStore people, PersonCatalog catalog, long person, IReadOnlyDictionary<string, string> arguments, PersonDetails current, out PersonDetails updated)
    {
        updated = current;
        var hasType = arguments.ContainsKey("document_type");
        var hasNumber = arguments.ContainsKey("document_number");
        if (!hasType && !hasNumber)
        {
            return null;
        }

        if (hasType != hasNumber)
        {
            return ToolResult.Rejected("incomplete_document");
        }

        var type = ActionArguments.Text(arguments, "document_type").Trim();
        var number = ActionArguments.Text(arguments, "document_number").Trim();
        if (type.Length == 0 && number.Length == 0)
        {
            updated = current with { DocumentType = null, DocumentNumber = null };
            return null;
        }

        if (type.Length == 0 || number.Length == 0)
        {
            return ToolResult.Rejected("incomplete_document");
        }

        if (!catalog.HasDocumentType(type))
        {
            return ToolResult.Rejected("unknown_document_type");
        }

        var normalized = PersonDocument.Normalize(number);
        if (normalized.Length is < MinDocumentLength or > MaxDocumentLength || !normalized.All(char.IsAsciiLetterOrDigit))
        {
            return ToolResult.Rejected("bad_document");
        }

        if (people.ByDocument(type, number) is { } holder && holder.Id != person)
        {
            return ToolResult.Rejected("document_taken");
        }

        updated = current with { DocumentType = type, DocumentNumber = number };
        return null;
    }

    private static ToolResult? Birth(DateOnly today, IReadOnlyDictionary<string, string> arguments, PersonDetails current, out PersonDetails updated)
    {
        updated = current;
        if (!arguments.TryGetValue("birth_date", out var text))
        {
            return null;
        }

        text = text.Trim();
        if (text.Length == 0)
        {
            updated = current with { BirthDate = null };
            return null;
        }

        if (!DateOnly.TryParseExact(text, BirthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birth)
            || birth > today
            || birth < new DateOnly(1900, 1, 1))
        {
            return ToolResult.Rejected("bad_birth_date");
        }

        updated = current with { BirthDate = birth };
        return null;
    }

    private static ToolResult? Sex(PersonCatalog catalog, IReadOnlyDictionary<string, string> arguments, PersonDetails current, out PersonDetails updated)
    {
        updated = current;
        if (!arguments.TryGetValue("sex", out var text))
        {
            return null;
        }

        text = text.Trim();
        if (text.Length == 0)
        {
            updated = current with { Sex = null };
            return null;
        }

        if (!catalog.HasSex(text))
        {
            return ToolResult.Rejected("unknown_sex");
        }

        updated = current with { Sex = text };
        return null;
    }
}
