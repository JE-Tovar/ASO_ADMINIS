using System.Collections;
using System.Globalization;
using System.Text.Json;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// La parte de la bitácora que no toca la base: qué cambió entre dos juegos de valores, cómo se
/// escribe cada valor y qué no se anota nunca. La usa <c>AsoAdminisDbContext.SaveChanges</c>; está
/// aparte para poder probarla sin SQL Server.
/// </summary>
public static class Auditoria
{
    /// <summary>Campos que no se anotan: técnicos o derivados de otros que ya se anotan.</summary>
    private static readonly HashSet<string> Ignorados =
    [
        nameof(IConVersion.Version),
        nameof(IDeOrganizacion.OrganizacionId)
    ];

    /// <summary>Campos cuyo valor nunca se escribe: solo que cambiaron.</summary>
    private static readonly HashSet<string> Secretos =
    [
        nameof(Usuario.PasswordHash),
        nameof(Usuario.PasswordSalt)
    ];

    public const string Oculto = "(cambiada)";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>
    /// Los campos que cambiaron. <paramref name="antes"/> es null en un alta (todo es nuevo) y
    /// <paramref name="despues"/> es null en una baja (se anota cómo estaba).
    /// </summary>
    public static IReadOnlyList<CambioDeCampo> Diferencias(
        IReadOnlyDictionary<string, object?>? antes,
        IReadOnlyDictionary<string, object?>? despues)
    {
        var campos = (antes?.Keys ?? []).Union(despues?.Keys ?? []).Where(c => !Ignorados.Contains(c));
        var cambios = new List<CambioDeCampo>();

        foreach (var campo in campos.OrderBy(c => c, StringComparer.Ordinal))
        {
            var a = antes is null ? null : antes.GetValueOrDefault(campo);
            var d = despues is null ? null : despues.GetValueOrDefault(campo);

            var textoAntes = antes is null ? string.Empty : Texto(a);
            var textoDespues = despues is null ? string.Empty : Texto(d);

            // En un alta solo interesa lo que trae valor; en una modificación, lo que cambió.
            if (antes is null && despues is not null && EsVacio(d))
                continue;
            if (antes is not null && despues is not null && textoAntes == textoDespues)
                continue;
            if (despues is null && EsVacio(a))
                continue;

            if (Secretos.Contains(campo))
            {
                textoAntes = antes is null ? string.Empty : Oculto;
                textoDespues = despues is null ? string.Empty : Oculto;
            }

            cambios.Add(new CambioDeCampo(campo, textoAntes, textoDespues));
        }

        return cambios;
    }

    public static string Serializar(IReadOnlyList<CambioDeCampo> cambios) => JsonSerializer.Serialize(cambios, Json);

    public static IReadOnlyList<CambioDeCampo> Leer(string detalle)
    {
        if (string.IsNullOrWhiteSpace(detalle))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<CambioDeCampo>>(detalle, Json) ?? [];
        }
        catch (JsonException)
        {
            return [new CambioDeCampo("(detalle ilegible)", string.Empty, detalle)];
        }
    }

    public static string Resumen(AccionAuditoria accion, string entidad, string id, IReadOnlyList<CambioDeCampo> cambios)
    {
        var verbo = accion switch
        {
            AccionAuditoria.Alta => "Alta",
            AccionAuditoria.Baja => "Baja",
            _ => "Modificación"
        };

        var cabecera = $"{verbo} de {NombreLegible(entidad)} #{id}";
        if (accion != AccionAuditoria.Modificacion || cambios.Count == 0)
            return cabecera;

        var lista = string.Join(", ", cambios.Take(6).Select(c => c.Campo));
        return cambios.Count > 6 ? $"{cabecera}: {lista} y {cambios.Count - 6} más" : $"{cabecera}: {lista}";
    }

    /// <summary>"CuentaBancaria" → "Cuenta bancaria", para la grilla.</summary>
    public static string NombreLegible(string entidad)
    {
        if (string.IsNullOrEmpty(entidad))
            return entidad;

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < entidad.Length; i++)
        {
            var c = entidad[i];
            if (i > 0 && char.IsUpper(c))
                sb.Append(' ').Append(char.ToLowerInvariant(c));
            else
                sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Cómo se escribe un valor en la bitácora: estable, sin depender de la cultura.</summary>
    public static string Texto(object? valor) => valor switch
    {
        null => "∅",
        string s => s,
        bool b => b ? "Sí" : "No",
        DateTime f => f.TimeOfDay == TimeSpan.Zero ? f.ToString("dd/MM/yyyy") : f.ToString("dd/MM/yyyy HH:mm:ss"),
        decimal m => m.ToString("0.####", CultureInfo.InvariantCulture),
        double m => m.ToString("0.####", CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        byte[] => "(binario)",
        IEnumerable lista and not string => $"({lista.Cast<object?>().Count()} elementos)",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? string.Empty
    };

    private static bool EsVacio(object? valor) => valor switch
    {
        null => true,
        string s => s.Length == 0,
        _ => false
    };
}
