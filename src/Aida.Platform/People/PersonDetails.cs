namespace Aida.Platform.People;

/// <summary>Lo que se sabe de una persona además del nombre y el teléfono; todo opcional.</summary>
/// <param name="DocumentType">El id del tipo de documento, de <c>people.yaml</c>.</param>
/// <param name="DocumentNumber">El número, tal como se escribió (recortado).</param>
/// <param name="BirthDate">La fecha de nacimiento.</param>
/// <param name="Sex">El id del sexo, de <c>people.yaml</c>.</param>
/// <remarks>Son opcionales porque el día a día no los pide: los documentos y el cobro los piden después, y una
/// persona sin ellos sigue siendo la misma persona. El tipo y el número van juntos o no van (la base lo exige).</remarks>
public sealed record PersonDetails(string? DocumentType, string? DocumentNumber, DateOnly? BirthDate, string? Sex)
{
    /// <summary>Sin ningún dato.</summary>
    /// <remarks>Es lo que tiene una persona recién dada de alta: no se inventa un documento ni una fecha.</remarks>
    public static PersonDetails None { get; } = new(null, null, null, null);
}
