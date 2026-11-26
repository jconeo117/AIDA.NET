using Aida.Platform.Keys;

namespace Aida.Platform.People;

/// <summary>Las dos claves de las huellas de la persona: la del teléfono y la del documento.</summary>
/// <remarks>Se derivan de la clave del equipo con sus propósitos (ADR 0034). Si fueran la misma, una huella de teléfono
/// serviría para buscar un documento, y el que tiene una no tendría por qué tener la otra. Son distintas porque cada
/// huella sale de su propio propósito, no de un sufijo.</remarks>
public sealed class PersonKeys
{
    /// <summary>El propósito de la clave de la huella del teléfono.</summary>
    public const string PhonePurpose = "aida-person-phone";

    /// <summary>El propósito de la clave de la huella del documento.</summary>
    public const string DocumentPurpose = "aida-person-document";

    private PersonKeys(byte[] phone, byte[] document)
    {
        Phone = phone;
        Document = document;
    }

    /// <summary>La clave de la huella del teléfono.</summary>
    internal byte[] Phone { get; }

    /// <summary>La clave de la huella del documento.</summary>
    internal byte[] Document { get; }

    /// <summary>Deriva las dos claves de la del equipo.</summary>
    /// <param name="key">La clave del equipo.</param>
    /// <returns>Las dos claves.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="key"/> es <see langword="null"/>.</exception>
    public static PersonKeys From(MachineKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new PersonKeys(key.Derive(PhonePurpose), key.Derive(DocumentPurpose));
    }
}
