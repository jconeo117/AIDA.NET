namespace Aida.Engine.Ontology;

/// <summary>Hasta dónde puede llegar el asistente por su cuenta.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:930-950</c>. Puerta dura, no rasgo: no se compensa con utilidad. Lo que queda por
/// encima del techo se propone y espera un visto bueno.
/// </remarks>
public sealed class AuthoritySpec
{
    internal AuthoritySpec(Reversibility autonomousUpTo, long confirmationTtlTicks)
    {
        AutonomousUpTo = autonomousUpTo;
        ConfirmationTtlTicks = confirmationTtlTicks;
    }

    /// <summary>La clasificación máxima que el asistente puede empezar solo. Por defecto, la más estrecha.</summary>
    public Reversibility AutonomousUpTo { get; }

    /// <summary>Cuántos instantes vive una propuesta sin respuesta antes de caducar.</summary>
    public long ConfirmationTtlTicks { get; }
}
