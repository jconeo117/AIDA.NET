using Aida.Platform.People;

namespace Aida.Arms.Agenda.Storage;

/// <summary>Quien viene a la cita, ya abierto: nombre y teléfono.</summary>
/// <remarks>Es <c>Client</c> de la 0.1 (<c>store.py:53-57</c>). En la base van sellados (ADR 0029); aquí ya están en claro, y
/// solo los ve quien tiene sesión.</remarks>
/// <param name="Id">El identificador.</param>
/// <param name="Name">El nombre, tal como se escribió.</param>
/// <param name="Phone">El teléfono, tal como se escribió.</param>
public sealed record Client(long Id, string Name, string Phone)
{
    /// <summary>La vista de la agenda de una persona de la plataforma.</summary>
    /// <param name="person">La persona.</param>
    /// <returns>Su id, su nombre y su teléfono.</returns>
    internal static Client From(Person person) => new(person.Id, person.Name, person.Phone);
}
