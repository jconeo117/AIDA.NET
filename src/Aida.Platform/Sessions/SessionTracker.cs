using System.Security.Cryptography;
using Aida.Platform.Audit;
using Aida.Platform.Identity;

namespace Aida.Platform.Sessions;

/// <summary>Las sesiones abiertas, una por puesto: entrar, bloquear por inactividad, desbloquear y salir.</summary>
/// <remarks>
/// Es la máquina de estados de <c>shell.py:204-262, 410-470</c> de la 0.1. La inactividad cuenta solo lo que hace la
/// persona (<see cref="Touch"/>), nunca lo que la pantalla pide sola: si contara, una pestaña abierta impediría
/// bloquear (<c>shell.py:238-243</c>). Las sesiones viven en memoria: reiniciar el servidor obliga a entrar de nuevo,
/// que es lo seguro. Cada transición escribe su hecho, a nombre de quien está en la sesión.
/// </remarks>
public sealed class SessionTracker
{
    private readonly UserStore _users;
    private readonly AuditLog _audit;
    private readonly TimeProvider _time;
    private readonly TimeSpan _idleLock;
    private readonly Dictionary<string, Entry> _sessions = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Crea el registro de sesiones.</summary>
    /// <param name="users">Los usuarios.</param>
    /// <param name="audit">La auditoría.</param>
    /// <param name="time">El reloj.</param>
    /// <param name="idleLock">Tras cuánto tiempo sin uso se bloquea, de <c>ui.yaml</c>.</param>
    public SessionTracker(UserStore users, AuditLog audit, TimeProvider time, TimeSpan idleLock)
    {
        _users = users;
        _audit = audit;
        _time = time;
        _idleLock = idleLock;
    }

    /// <summary>Abre una sesión para alguien que acaba de entrar.</summary>
    /// <param name="user">Quién entra, ya verificado.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <returns>El identificador de la sesión, que va en la cookie.</returns>
    public string Open(UserAccount user, string workstation)
    {
        var id = RandomNumberGenerator.GetHexString(32, lowercase: true);
        lock (_gate)
        {
            _sessions[id] = new Entry(user.Id, workstation, _time.GetUtcNow(), false);
        }

        _audit.Record(AuditKind.SessionOpened, user.Actor, workstation, new Dictionary<string, string>());
        return id;
    }

    /// <summary>Cómo está una sesión ahora. Si pasó el tiempo sin uso, la bloquea.</summary>
    /// <param name="sessionId">La sesión.</param>
    /// <returns>Su estado. Una sesión de un usuario desactivado ya no existe.</returns>
    public SessionView Check(string sessionId)
    {
        Entry? entry;
        lock (_gate)
        {
            _sessions.TryGetValue(sessionId, out entry);
        }

        var user = entry is null ? null : _users.Find(entry.UserId);
        if (entry is null || user is not { Active: true })
        {
            Forget(sessionId);
            return new SessionView(SessionStatus.None, null);
        }

        if (!entry.Locked && _time.GetUtcNow() - entry.LastActivity >= _idleLock && Transition(sessionId, entry, locked: true))
        {
            _audit.Record(AuditKind.Locked, user.Actor, entry.Workstation, new Dictionary<string, string>());
            return new SessionView(SessionStatus.Locked, user);
        }

        return new SessionView(entry.Locked ? SessionStatus.Locked : SessionStatus.Active, user);
    }

    /// <summary>Anota que la persona hizo algo. No desbloquea.</summary>
    /// <param name="sessionId">La sesión.</param>
    public void Touch(string sessionId)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var entry) && !entry.Locked)
            {
                _sessions[sessionId] = entry with { LastActivity = _time.GetUtcNow() };
            }
        }
    }

    /// <summary>La misma persona vuelve tras el bloqueo con su contraseña.</summary>
    /// <param name="sessionId">La sesión bloqueada.</param>
    /// <param name="password">Su contraseña.</param>
    /// <returns>El usuario.</returns>
    /// <exception cref="IdentityException">Si no hay sesión, o la contraseña no es la suya (con la misma espera que al entrar).</exception>
    public UserAccount Unlock(string sessionId, string password)
    {
        var view = Check(sessionId);
        if (view.User is null)
        {
            throw new IdentityException(IdentityError.Unknown);
        }

        var user = _users.Verify(view.User.UserName, password);
        Entry? entry;
        lock (_gate)
        {
            _sessions.TryGetValue(sessionId, out entry);
        }

        if (entry is not null && Transition(sessionId, entry, locked: false))
        {
            _audit.Record(AuditKind.Unlocked, user.Actor, entry.Workstation, new Dictionary<string, string>());
        }

        return user;
    }

    /// <summary>Cierra la sesión. Cerrar una que no existe no hace nada.</summary>
    /// <param name="sessionId">La sesión.</param>
    public void Close(string sessionId)
    {
        Entry? entry;
        lock (_gate)
        {
            _sessions.Remove(sessionId, out entry);
        }

        var user = entry is null ? null : _users.Find(entry.UserId);
        if (entry is not null && user is not null)
        {
            _audit.Record(AuditKind.SessionClosed, user.Actor, entry.Workstation, new Dictionary<string, string>());
        }
    }

    private bool Transition(string sessionId, Entry seen, bool locked)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var current) || current != seen)
            {
                return false;
            }

            _sessions[sessionId] = current with { Locked = locked, LastActivity = _time.GetUtcNow() };
            return true;
        }
    }

    private void Forget(string sessionId)
    {
        lock (_gate)
        {
            _sessions.Remove(sessionId);
        }
    }

    private sealed record Entry(long UserId, string Workstation, DateTimeOffset LastActivity, bool Locked);
}
