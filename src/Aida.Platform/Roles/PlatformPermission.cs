namespace Aida.Platform.Roles;

/// <summary>Los permisos que conoce la plataforma.</summary>
/// <remarks>Cada permiso entra con quien lo comprueba. En N007 solo hay uno: gestionar usuarios, que lo comprueban
/// <c>UserStore</c> y la página de usuarios. Los brazos declaran los suyos en su unidad (ADR 0020).</remarks>
public static class PlatformPermission
{
    /// <summary>Crear, desactivar y restablecer la contraseña de otros usuarios.</summary>
    public const string ManageUsers = "users.manage";

    /// <summary>Hacer y descargar una copia de la instalación: la base, la configuración y la clave del equipo.</summary>
    /// <remarks>Tiene permiso propio y no va con gestionar usuarios: quien tiene el archivo y la frase lo puede leer todo.
    /// En el pack por defecto solo lo tiene administración (ADR 0036, decisión D7).</remarks>
    public const string CreateSnapshot = "snapshot.create";
}
