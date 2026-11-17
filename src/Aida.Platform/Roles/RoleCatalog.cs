using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Platform.Roles;

/// <summary>Los roles del servidor, en el orden en que los declara el pack.</summary>
/// <remarks>
/// Se lee una vez al arrancar y revienta si algo no cuadra: un rol mal declarado se descubre con alguien esperando
/// delante (<c>wording.py:72-74</c> de la 0.1 lo dice de los textos). Un usuario cuyo rol ya no está declarado no tiene
/// ningún permiso: se falla cerrado.
/// </remarks>
public sealed partial class RoleCatalog
{
    private readonly FrozenDictionary<string, RoleSpec> _byId;

    /// <summary>Crea el catálogo.</summary>
    /// <param name="roles">Los roles, en orden.</param>
    /// <param name="armPermissions">Los permisos que declaran los brazos; <c>users.manage</c> se conoce siempre (ADR 0035).</param>
    /// <exception cref="InvalidDataException">Si no cumple las reglas del catálogo.</exception>
    public RoleCatalog(IReadOnlyList<RoleSpec> roles, IEnumerable<string>? armPermissions = null)
    {
        var known = new HashSet<string>(armPermissions ?? [], StringComparer.Ordinal) { PlatformPermission.ManageUsers };
        var problems = new List<string>();
        if (roles.Count == 0)
        {
            problems.Add("no declara ningún rol");
        }

        foreach (var role in roles)
        {
            if (!IdPattern().IsMatch(role.Id))
            {
                problems.Add($"el rol «{role.Id}» no es un identificador válido");
            }

            if (string.IsNullOrWhiteSpace(role.Label))
            {
                problems.Add($"el rol «{role.Id}» no tiene etiqueta");
            }

            problems.AddRange(role.Permissions.Where(p => !known.Contains(p)).Order(StringComparer.Ordinal)
                .Select(p => $"el rol «{role.Id}» declara un permiso desconocido: «{p}»"));
        }

        problems.AddRange(roles.GroupBy(r => r.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => $"el rol «{g.Key}» está repetido"));
        if (!roles.Any(r => r.Permissions.Contains(PlatformPermission.ManageUsers)))
        {
            problems.Add($"ningún rol tiene «{PlatformPermission.ManageUsers}»: nadie podría gestionar usuarios");
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException($"roles: {string.Join("; ", problems)}");
        }

        Roles = roles;
        _byId = roles.ToFrozenDictionary(r => r.Id, StringComparer.Ordinal);
    }

    /// <summary>Los roles, en el orden del pack.</summary>
    public IReadOnlyList<RoleSpec> Roles { get; }

    /// <summary>El rol del primer administrador: el primero del pack que gestiona usuarios.</summary>
    public RoleSpec FirstAdministratorRole => Roles.First(r => r.Permissions.Contains(PlatformPermission.ManageUsers));

    /// <summary>Lee el catálogo de <c>roles.yaml</c>.</summary>
    /// <param name="path">La ruta del archivo.</param>
    /// <param name="armPermissions">Los permisos que declaran los brazos.</param>
    /// <returns>El catálogo.</returns>
    /// <exception cref="InvalidDataException">Si no se puede leer o no es válido.</exception>
    public static RoleCatalog Load(string path, IEnumerable<string>? armPermissions = null)
    {
        var root = PlatformYaml.ReadRoot(path);
        if (PlatformYaml.Child(root, "roles") is not YamlSequenceNode list)
        {
            throw new InvalidDataException($"{path}: falta la lista 'roles'");
        }

        var roles = new List<RoleSpec>();
        foreach (var item in list)
        {
            if (item is not YamlMappingNode map)
            {
                throw new InvalidDataException($"{path}: cada rol es un mapa");
            }

            var permissions = PlatformYaml.Child(map, "permissions") is YamlSequenceNode grants
                ? grants.Select(PlatformYaml.Text).OfType<string>().ToFrozenSet(StringComparer.Ordinal)
                : FrozenSet<string>.Empty;
            roles.Add(new RoleSpec(PlatformYaml.Text(PlatformYaml.Child(map, "id")) ?? string.Empty, PlatformYaml.Text(PlatformYaml.Child(map, "label")) ?? string.Empty, permissions));
        }

        return new RoleCatalog(roles, armPermissions);
    }

    /// <summary>Si un rol tiene un permiso. Un rol que no está declarado no tiene ninguno.</summary>
    /// <param name="roleId">El rol.</param>
    /// <param name="permission">El permiso.</param>
    /// <returns><see langword="true"/> si lo tiene.</returns>
    public bool Allows(string roleId, string permission) => _byId.TryGetValue(roleId, out var role) && role.Permissions.Contains(permission);

    /// <summary>Si el rol está declarado.</summary>
    /// <param name="roleId">El rol.</param>
    /// <returns><see langword="true"/> si lo está.</returns>
    public bool Contains(string roleId) => _byId.ContainsKey(roleId);

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
