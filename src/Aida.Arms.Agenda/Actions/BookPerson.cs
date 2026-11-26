using System.Globalization;
using Aida.Engine.Actions;
using Aida.Engine.Text;
using Aida.Platform.People;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Quién viene a una cita nueva: la persona que se dice con <c>client</c>, o la que la regla de la 0.1 encuentra
/// por teléfono (N015, D4).</summary>
/// <remarks>
/// <para>Sin <c>client</c>, es la regla de la 0.1 tal cual: con ninguna persona con ese teléfono se crea; con una, se usa y
/// se le cambia el nombre si cambió; con dos o más, se rechaza. Así los dorados de agendar siguen valiendo.</para>
/// <para><c>client=new</c> es una persona nueva, salvo que ya exista una con el mismo teléfono y el mismo nombre plegado: así
/// un doble clic o un «ana perez» tecleado sin tildes no crean a la misma persona dos veces.</para>
/// <para><c>client=&lt;id&gt;</c> es esa persona, sin tocarle el nombre ni el teléfono. Es lo que manda la pantalla cuando
/// la cita sale de una ficha: el nombre del formulario solo rellena, no renombra.</para>
/// </remarks>
internal static class BookPerson
{
    internal const string New = "new";

    /// <summary>Si el argumento <c>client</c> se puede resolver: vacío, <c>new</c>, o el id de una persona que existe.</summary>
    /// <param name="people">El almacén de las personas de la petición.</param>
    /// <param name="client">El argumento, ya recortado.</param>
    /// <returns><see langword="true"/> si se puede resolver; <see langword="false"/> da <c>unknown_client</c>.</returns>
    internal static bool IsKnown(PersonStore people, string client) =>
        client.Length == 0
        || client == New
        || (long.TryParse(client, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && people.Find(id) is not null);

    /// <summary>La persona de la cita, creándola o cambiándole el nombre si hace falta.</summary>
    /// <param name="people">El almacén de las personas de la petición.</param>
    /// <param name="client">El argumento <c>client</c>, ya validado por <see cref="IsKnown"/>.</param>
    /// <param name="name">El nombre que llegó en la petición, recortado.</param>
    /// <param name="phone">El teléfono que llegó en la petición, recortado.</param>
    /// <param name="personId">La persona que lleva la cita, si no hay rechazo.</param>
    /// <returns>El rechazo <c>client_ambiguous</c>, o <see langword="null"/> si la persona está resuelta.</returns>
    /// <remarks>Escribe (alta o cambio de nombre) solo cuando resuelve; si rechaza, no ha cambiado nada.</remarks>
    internal static ToolResult? Resolve(PersonStore people, string client, string name, string phone, out long personId)
    {
        personId = 0;
        if (client.Length > 0 && client != New)
        {
            personId = long.Parse(client, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return null;
        }

        var samePhone = people.ByPhone(phone);
        if (client == New)
        {
            personId = samePhone.FirstOrDefault(person => TextFold.Fold(person.Name) == TextFold.Fold(name))?.Id
                ?? people.Add(name, phone).Id;
            return null;
        }

        switch (samePhone.Count)
        {
            case 0:
                personId = people.Add(name, phone).Id;
                return null;
            case 1:
                var only = samePhone[0];
                if (only.Name != name)
                {
                    people.UpdateContact(only.Id, name, phone);
                }

                personId = only.Id;
                return null;
            default:
                return ToolResult.Rejected("client_ambiguous");
        }
    }
}
