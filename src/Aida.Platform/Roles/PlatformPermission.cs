namespace Aida.Platform.Roles;

/// <summary>Los permisos que conoce la plataforma.</summary>
/// <remarks>Cada permiso entra con quien lo comprueba. En N007 solo hay uno: gestionar usuarios, que lo comprueban
/// <c>UserStore</c> y la página de usuarios. Los brazos declaran los suyos en su unidad (ADR 0020).</remarks>
public static class PlatformPermission
{
    /// <summary>Crear, desactivar y restablecer la contraseña de otros usuarios.</summary>
    public const string ManageUsers = "users.manage";
}
