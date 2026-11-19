namespace Aida.Arms.Agenda.Storage;

/// <summary>Un teléfono reducido a sus dígitos.</summary>
/// <remarks>«300 555 0142» y «3005550142» son la misma persona (<c>normalize_phone</c>, <c>store.py:142-144</c>). Se
/// normaliza solo para la huella: el teléfono se guarda tal como se escribió.</remarks>
internal static class PhoneNumbers
{
    internal static string Normalize(string phone) => string.Concat(phone.Where(char.IsDigit));
}
