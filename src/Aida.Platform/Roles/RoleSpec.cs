namespace Aida.Platform.Roles;

/// <summary>Un rol declarado en <c>packs/platform/roles.yaml</c>.</summary>
/// <remarks>Lo que puede hacer cada rol lo declara el pack, no el código (ADR 0020); el nombre del rol es vocabulario
/// de la persona atendida y vive en el pack (ADR 0002).</remarks>
/// <param name="Id">El identificador, que guarda cada usuario.</param>
/// <param name="Label">Cómo se muestra.</param>
/// <param name="Permissions">Lo que permite.</param>
public sealed record RoleSpec(string Id, string Label, IReadOnlySet<string> Permissions);
