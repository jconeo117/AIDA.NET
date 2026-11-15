using Aida.Platform.Identity;

namespace Aida.Platform.Sessions;

/// <summary>Lo que sabe el armazón de la sesión de un puesto.</summary>
/// <remarks>El usuario va también cuando está bloqueada: la pantalla de bloqueo dice a nombre de quién está.</remarks>
/// <param name="Status">El estado.</param>
/// <param name="User">El usuario, salvo con <see cref="SessionStatus.None"/>.</param>
public sealed record SessionView(SessionStatus Status, UserAccount? User);
