using Aida.Platform.Audit;
using Aida.Platform.Roles;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Identity;

/// <summary>Los usuarios del servidor: entrar, crear, desactivar y restablecer la contraseña.</summary>
/// <remarks>
/// Es <c>ProfileStore</c> de la 0.1 (<c>identity.py:78-232</c>) con usuario y contraseña en vez de perfil y PIN, y sin
/// códigos de recuperación: la contraseña la restablece un administrador (ADR 0030). Cada cambio y su línea de
/// auditoría van en la misma transacción. El permiso se comprueba aquí y no solo en la página: una acción fuera del
/// rol no se ejecuta aunque alguien llame sin pasar por ella (ADR 0020).
/// </remarks>
public sealed class UserStore
{
    private const string _columns = "id, user_name, display_name, role, active";
    private readonly PlatformDatabase _database;
    private readonly AuditLog _audit;
    private readonly RoleCatalog _roles;
    private readonly PasswordHasher _hasher;
    private readonly LoginThrottle _throttle;
    private readonly string _decoy;

    /// <summary>Crea el almacén.</summary>
    /// <param name="database">La base de datos.</param>
    /// <param name="audit">La auditoría.</param>
    /// <param name="roles">Los roles declarados.</param>
    /// <param name="hasher">El calculador de huellas.</param>
    /// <param name="time">El reloj de las esperas.</param>
    public UserStore(PlatformDatabase database, AuditLog audit, RoleCatalog roles, PasswordHasher hasher, TimeProvider time)
    {
        _database = database;
        _audit = audit;
        _roles = roles;
        _hasher = hasher;
        _throttle = new LoginThrottle(time);
        _decoy = hasher.Hash("aida-señuelo-sin-uso");
    }

    /// <summary>Si ya hay algún usuario.</summary>
    /// <returns><see langword="true"/> si lo hay.</returns>
    public bool HasUsers()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM users);";
        return (long)command.ExecuteScalar()! == 1;
    }

    /// <summary>Crea el primer usuario, con el primer rol que gestiona usuarios. Firma su propia creación.</summary>
    /// <param name="userName">El usuario.</param>
    /// <param name="displayName">El nombre.</param>
    /// <param name="password">La contraseña.</param>
    /// <param name="workstation">Desde qué puesto. La página solo lo admite desde el propio servidor.</param>
    /// <returns>El usuario creado.</returns>
    /// <exception cref="IdentityException">Si ya hay usuarios o algún dato no tiene la forma pedida.</exception>
    public UserAccount CreateFirstAdministrator(string userName, string displayName, string password, string workstation)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        using (var exists = Command(connection, transaction, "SELECT EXISTS (SELECT 1 FROM users);"))
        {
            if ((long)exists.ExecuteScalar()! == 1)
            {
                throw new IdentityException(IdentityError.AlreadyInitialized);
            }
        }

        var created = Insert(connection, transaction, userName, displayName, _roles.FirstAdministratorRole.Id, password);
        _audit.Record(connection, transaction, AuditKind.UserCreated, created.Actor, workstation, Data(created));
        transaction.Commit();
        return created;
    }

    /// <summary>Comprueba el usuario y la contraseña.</summary>
    /// <param name="userName">El usuario escrito.</param>
    /// <param name="password">La contraseña escrita.</param>
    /// <returns>El usuario, si es suyo y está activo.</returns>
    /// <exception cref="IdentityException"><see cref="IdentityError.Wait"/> o <see cref="IdentityError.BadCredentials"/>,
    /// que no dice si falló el usuario o la contraseña.</exception>
    public UserAccount Verify(string userName, string password)
    {
        var key = userName.Trim().ToLowerInvariant();
        if (_throttle.Wait(key) > TimeSpan.Zero)
        {
            throw new IdentityException(IdentityError.Wait);
        }

        var (account, stored) = FindByUserName(key);
        var matches = _hasher.Verify(password, stored ?? _decoy);
        if (account is null || !account.Active || !matches)
        {
            throw new IdentityException(_throttle.Fail(key) ? IdentityError.Wait : IdentityError.BadCredentials);
        }

        _throttle.Succeed(key);
        return account;
    }

    /// <summary>Cuántos fallos quedan antes de tener que esperar.</summary>
    /// <param name="userName">El usuario escrito.</param>
    /// <returns>De 1 a 5.</returns>
    public int AttemptsLeft(string userName) => _throttle.AttemptsLeft(userName.Trim().ToLowerInvariant());

    /// <summary>Crea un usuario.</summary>
    /// <param name="actorId">Quién lo crea; tiene que poder gestionar usuarios.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="userName">El usuario.</param>
    /// <param name="displayName">El nombre.</param>
    /// <param name="roleId">El rol.</param>
    /// <param name="password">La contraseña inicial.</param>
    /// <returns>El usuario creado.</returns>
    /// <exception cref="IdentityException">Si quien lo pide no puede, o algún dato no vale.</exception>
    public UserAccount Create(long actorId, string workstation, string userName, string displayName, string roleId, string password)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var actor = Manager(connection, transaction, actorId);
        var created = Insert(connection, transaction, userName, displayName, roleId, password);
        _audit.Record(connection, transaction, AuditKind.UserCreated, actor.Actor, workstation, Data(created));
        transaction.Commit();
        return created;
    }

    /// <summary>Pone una contraseña nueva a otro usuario.</summary>
    /// <param name="actorId">Quién la pone; tiene que poder gestionar usuarios.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="userId">A quién.</param>
    /// <param name="password">La contraseña nueva.</param>
    /// <exception cref="IdentityException">Si quien lo pide no puede, el usuario no existe o la contraseña no vale.</exception>
    public void ResetPassword(long actorId, string workstation, long userId, string password)
    {
        CredentialRules.Password(password);
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var actor = Manager(connection, transaction, actorId);
        var target = ById(connection, transaction, userId) ?? throw new IdentityException(IdentityError.Unknown);
        using var update = Command(connection, transaction, "UPDATE users SET password = $password WHERE id = $id;");
        update.Parameters.AddWithValue("$password", _hasher.Hash(password));
        update.Parameters.AddWithValue("$id", userId);
        update.ExecuteNonQuery();
        _audit.Record(connection, transaction, AuditKind.PasswordReset, actor.Actor, workstation, Data(target));
        transaction.Commit();
        _throttle.Succeed(target.UserName);
    }

    /// <summary>Desactiva un usuario: deja de poder entrar, y sus hechos siguen a su nombre.</summary>
    /// <param name="actorId">Quién lo desactiva; tiene que poder gestionar usuarios.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="userId">A quién.</param>
    /// <exception cref="IdentityException">Si quien lo pide no puede, el usuario no existe o es el último que gestiona usuarios.</exception>
    public void Disable(long actorId, string workstation, long userId)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var actor = Manager(connection, transaction, actorId);
        var target = ById(connection, transaction, userId) ?? throw new IdentityException(IdentityError.Unknown);
        var managers = All(connection, transaction).Count(u => u.Active && _roles.Allows(u.RoleId, PlatformPermission.ManageUsers));
        if (target.Active && _roles.Allows(target.RoleId, PlatformPermission.ManageUsers) && managers <= 1)
        {
            throw new IdentityException(IdentityError.LastAdministrator);
        }

        using var update = Command(connection, transaction, "UPDATE users SET active = 0 WHERE id = $id;");
        update.Parameters.AddWithValue("$id", userId);
        update.ExecuteNonQuery();
        _audit.Record(connection, transaction, AuditKind.UserDisabled, actor.Actor, workstation, Data(target));
        transaction.Commit();
    }

    /// <summary>Un usuario por su identificador.</summary>
    /// <param name="userId">El identificador.</param>
    /// <returns>El usuario, o <see langword="null"/> si no existe.</returns>
    public UserAccount? Find(long userId)
    {
        using var connection = _database.Open();
        return ById(connection, null, userId);
    }

    /// <summary>Todos los usuarios, por nombre y, a igualdad, por identificador.</summary>
    /// <returns>La lista. El orden no depende del de la tabla (<c>identity.py:105-112</c>).</returns>
    public IReadOnlyList<UserAccount> List()
    {
        using var connection = _database.Open();
        return All(connection, null);
    }

    private static Dictionary<string, string> Data(UserAccount user) => new(StringComparer.Ordinal)
    {
        ["user"] = user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["user_name"] = user.UserName,
        ["role"] = user.RoleId,
    };

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static UserAccount Read(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4) == 1);

    private static UserAccount? ById(SqliteConnection connection, SqliteTransaction? transaction, long userId)
    {
        using var select = Command(connection, transaction, $"SELECT {_columns} FROM users WHERE id = $id;");
        select.Parameters.AddWithValue("$id", userId);
        using var reader = select.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    private static List<UserAccount> All(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var select = Command(connection, transaction, $"SELECT {_columns} FROM users;");
        using var reader = select.ExecuteReader();
        var users = new List<UserAccount>();
        while (reader.Read())
        {
            users.Add(Read(reader));
        }

        return [.. users.OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.Id)];
    }

    private (UserAccount? Account, string? Stored) FindByUserName(string userName)
    {
        using var connection = _database.Open();
        using var select = Command(connection, null, $"SELECT {_columns}, password FROM users WHERE user_name = $name;");
        select.Parameters.AddWithValue("$name", userName);
        using var reader = select.ExecuteReader();
        return reader.Read() ? (Read(reader), reader.GetString(5)) : (null, null);
    }

    private UserAccount Manager(SqliteConnection connection, SqliteTransaction transaction, long actorId)
    {
        var actor = ById(connection, transaction, actorId);
        return actor is { Active: true } && _roles.Allows(actor.RoleId, PlatformPermission.ManageUsers)
            ? actor
            : throw new IdentityException(IdentityError.Forbidden);
    }

    private UserAccount Insert(SqliteConnection connection, SqliteTransaction transaction, string userName, string displayName, string roleId, string password)
    {
        var name = CredentialRules.UserName(userName);
        var display = CredentialRules.DisplayName(displayName);
        CredentialRules.Password(password);
        if (!_roles.Contains(roleId))
        {
            throw new IdentityException(IdentityError.RoleUnknown);
        }

        using var insert = Command(connection, transaction, "INSERT INTO users (user_name, display_name, role, password, active) VALUES ($name, $display, $role, $password, 1) ON CONFLICT (user_name) DO NOTHING RETURNING id;");
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$display", display);
        insert.Parameters.AddWithValue("$role", roleId);
        insert.Parameters.AddWithValue("$password", _hasher.Hash(password));
        var id = insert.ExecuteScalar() as long? ?? throw new IdentityException(IdentityError.Duplicate);
        return new UserAccount(id, name, display, roleId, true);
    }
}
