namespace Aida.Platform.People;

/// <summary>Un teléfono reducido a sus dígitos.</summary>
/// <remarks>«300 555 0142» y «3005550142» son la misma persona (<c>normalize_phone</c>, <c>store.py:142-144</c>). Se
/// normaliza solo para la huella: el teléfono se guarda tal como se escribió. Vive en la plataforma porque la huella de la
/// persona y la búsqueda de los brazos lo necesitan los dos (ADR 0038).</remarks>
public static class PhoneNumbers
{
    /// <summary>Los dígitos del teléfono, sin nada más.</summary>
    /// <param name="phone">El teléfono, tal como se escribió.</param>
    /// <returns>Solo sus dígitos; vacío si no tiene ninguno.</returns>
    public static string Normalize(string phone) => string.Concat(phone.Where(char.IsDigit));
}
