using System.Globalization;
using Aida.Engine.Actions;
using Aida.Engine.Text;
using Aida.Platform.People;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Quién viene a una cita nueva: la persona que se dice con <c>client</c>, o la que la regla de la 0.1 encuentra
/// por teléfono (N015, D4; N015-fix, F1 y F2).</summary>
/// <remarks>
/// <para>Sin <c>client</c>, es la regla de la 0.1 tal cual: con ninguna persona con ese teléfono se crea; con una, se usa y
/// se le cambia el nombre si cambió; con dos o más, se rechaza. Así los dorados de agendar siguen valiendo.</para>
/// <para><c>client=new</c> es una persona nueva solo si ningún otro tiene ese teléfono. Si alguien lo tiene, no crea ni
/// reutiliza a nadie: rechaza con <c>phone_known</c> y lista a esas personas, para que quien atiende diga si es una de ellas.
/// N015 reutilizaba en silencio a quien tuviera el mismo nombre plegado; un nombre mal escrito dejaba un duplicado, y por eso
/// ya no se decide sin preguntar (N015-fix).</para>
/// <para><c>client=new_confirmed</c> crea siempre una persona nueva, con ese nombre y ese teléfono: es la respuesta «es una
/// persona nueva» de la pantalla, tras el rechazo anterior.</para>
/// <para><c>client=&lt;id&gt;</c> es esa persona, sin tocarle el nombre ni el teléfono. Es lo que manda la pantalla cuando
/// la cita sale de una ficha, o cuando el operador confirma «es esta persona»: el nombre del formulario solo rellena, no
/// renombra.</para>
/// </remarks>
internal static class BookPerson
{
    internal const string New = "new";

    /// <summary>La persona nueva que se confirma tras un rechazo <c>phone_known</c>.</summary>
    internal const string NewConfirmed = "new_confirmed";

    /// <summary>Si el argumento <c>client</c> se puede resolver: vacío, <c>new</c>, <c>new_confirmed</c>, o el id de una persona que existe.</summary>
    /// <param name="people">El almacén de las personas de la petición.</param>
    /// <param name="client">El argumento, ya recortado.</param>
    /// <returns><see langword="true"/> si se puede resolver; <see langword="false"/> da <c>unknown_client</c>.</returns>
    internal static bool IsKnown(PersonStore people, string client) =>
        client.Length == 0
        || client == New
        || client == NewConfirmed
        || (long.TryParse(client, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && people.Find(id) is not null);

    /// <summary>La persona de la cita, creándola o cambiándole el nombre si hace falta.</summary>
    /// <param name="people">El almacén de las personas de la petición.</param>
    /// <param name="client">El argumento <c>client</c>, ya validado por <see cref="IsKnown"/>.</param>
    /// <param name="name">El nombre que llegó en la petición, recortado.</param>
    /// <param name="phone">El teléfono que llegó en la petición, recortado.</param>
    /// <param name="personId">La persona que lleva la cita, si no hay rechazo.</param>
    /// <returns>El rechazo <c>client_ambiguous</c> o <c>phone_known</c>, o <see langword="null"/> si la persona está resuelta.</returns>
    /// <remarks>Escribe (alta o cambio de nombre) solo cuando resuelve; si rechaza, no ha cambiado nada: el rechazo
    /// <c>phone_known</c> no da de alta a nadie, porque la decisión es de quien atiende.</remarks>
    internal static ToolResult? Resolve(PersonStore people, string client, string name, string phone, out long personId)
    {
        personId = 0;
        if (client.Length > 0 && client is not (New or NewConfirmed))
        {
            personId = long.Parse(client, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return null;
        }

        if (client == NewConfirmed)
        {
            personId = people.Add(name, phone).Id;
            return null;
        }

        var samePhone = people.ByPhone(phone);
        if (client == New)
        {
            if (samePhone.Count > 0)
            {
                return ToolResult.Rejected("phone_known", new Dictionary<string, string>
                {
                    ["people"] = string.Join(',', samePhone.Select(person => person.Id.ToString(CultureInfo.InvariantCulture))),
                });
            }

            personId = people.Add(name, phone).Id;
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
