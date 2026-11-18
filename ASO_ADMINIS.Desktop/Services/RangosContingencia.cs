using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Las reglas de los talonarios de contingencia. Funciones puras sobre los rangos y los números
/// de control ya usados, para poder probarlas sin base de datos.
///
/// <para><b>Fuera de rango se bloquea siempre:</b> un número de control que no está en un
/// talonario autorizado y activo no tiene validez fiscal. Se corrige cargando el rango que falta
/// en Administración · Contingencia, nunca registrándolo igual.</para>
/// </summary>
public static class RangosContingencia
{
    /// <summary>Por debajo de esta fracción libre se avisa que el talonario se acaba.</summary>
    public const double UmbralAviso = 0.10;

    public sealed record Resolucion(RangoContingencia Rango, int Correlativo, string NumeroControl);

    public sealed record Sugerencia(string NumeroControl, RangoContingencia Rango, int Quedan, bool PocosQuedan);

    /// <summary>
    /// Ubica un número de control en los rangos activos y lo devuelve escrito como en el
    /// talonario (prefijo + ceros). Acepta que se escriba sin el prefijo o sin los ceros, mientras
    /// no sea ambiguo entre dos talonarios.
    /// </summary>
    public static Resolucion Resolver(string texto, IEnumerable<RangoContingencia> rangos, IEnumerable<string> usados)
    {
        var activos = rangos.Where(r => r.Activo).ToList();
        if (activos.Count == 0)
            throw new InvalidOperationException(
                "No hay talonarios de contingencia cargados. Un administrador tiene que registrar en " +
                "Administración · Contingencia el rango de formatos autorizados por la imprenta antes de usarlos.");

        var limpio = (texto ?? string.Empty).Trim();
        if (limpio.Length == 0)
            throw new InvalidOperationException("Indique el número de control impreso en el formato.");

        var candidatos = activos
            .Select(r => (Rango: r, Correlativo: Correlativo(limpio, r)))
            .Where(c => c.Correlativo is { } n && n >= c.Rango.Desde && n <= c.Rango.Hasta)
            .ToList();

        if (candidatos.Count == 0)
            throw new InvalidOperationException(
                $"El número de control {limpio} no está en ningún talonario de contingencia autorizado y activo " +
                $"({string.Join("; ", activos.Select(r => r.RangoTexto))}). Revise el formato, o cargue su rango en " +
                "Administración · Contingencia.");

        if (candidatos.Count > 1)
            throw new InvalidOperationException(
                $"El número {limpio} encaja en más de un talonario. Escríbalo completo, con su prefijo, tal como está impreso.");

        var (rango, correlativo) = (candidatos[0].Rango, candidatos[0].Correlativo!.Value);
        var numero = rango.Numero(correlativo);

        if (usados.Any(u => string.Equals(u?.Trim(), numero, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"El número de control {numero} ya está usado en otro documento.");

        return new Resolucion(rango, correlativo, numero);
    }

    /// <summary>
    /// El siguiente número libre: el menor sin usar del primer talonario activo que tenga lugar.
    /// Null si no hay ninguno con números libres.
    /// </summary>
    public static Sugerencia? Siguiente(IEnumerable<RangoContingencia> rangos, IEnumerable<string> usados)
    {
        var ocupados = new HashSet<string>(usados.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()),
                                           StringComparer.OrdinalIgnoreCase);

        foreach (var rango in rangos.Where(r => r.Activo).OrderBy(r => r.Prefijo).ThenBy(r => r.Desde))
        {
            var usadosAqui = UsadosEn(rango, ocupados);
            for (var n = rango.Desde; n <= rango.Hasta; n++)
            {
                var numero = rango.Numero(n);
                if (ocupados.Contains(numero))
                    continue;

                var quedan = rango.Total - usadosAqui;
                return new Sugerencia(numero, rango, quedan, quedan <= Math.Ceiling(rango.Total * UmbralAviso));
            }
        }

        return null;
    }

    /// <summary>Cuántos números del rango ya figuran como usados.</summary>
    public static int UsadosEn(RangoContingencia rango, IEnumerable<string> usados) =>
        usados.Count(u => Correlativo(u?.Trim() ?? string.Empty, rango) is { } n
                          && n >= rango.Desde && n <= rango.Hasta
                          && string.Equals(u!.Trim(), rango.Numero(n), StringComparison.OrdinalIgnoreCase));

    /// <returns>El problema del rango, o null si está bien.</returns>
    public static string? Validar(RangoContingencia rango, IEnumerable<RangoContingencia> otros, IEnumerable<string> usados)
    {
        if (string.IsNullOrWhiteSpace(rango.Imprenta))
            return "Indique la imprenta autorizada que imprimió los formatos.";
        if (rango.Prefijo.Length > 10 || rango.Prefijo.Any(char.IsWhiteSpace))
            return "El prefijo no puede llevar espacios ni pasar de 10 caracteres.";
        if (rango.Digitos is < 1 or > 12)
            return "Las cifras del correlativo deben estar entre 1 y 12.";
        if (rango.Desde < 1)
            return "El primer número del rango debe ser 1 o mayor.";
        if (rango.Hasta < rango.Desde)
            return "El último número del rango no puede ser menor que el primero.";
        if (rango.Hasta.ToString().Length > rango.Digitos)
            return $"El último número ({rango.Hasta}) no cabe en {rango.Digitos} cifras.";

        var solapado = otros.FirstOrDefault(o => o.Id != rango.Id
                                                 && string.Equals(o.Prefijo, rango.Prefijo, StringComparison.OrdinalIgnoreCase)
                                                 && o.Desde <= rango.Hasta && rango.Desde <= o.Hasta);
        if (solapado is not null)
            return $"Se solapa con el talonario {solapado.RangoTexto} ({solapado.Imprenta}). Cada número de control es de un solo talonario.";

        // Al editar no se puede dejar afuera un número que ya se usó: esa factura quedaría
        // apuntando a un talonario que ya no la incluye.
        var anterior = otros.FirstOrDefault(o => o.Id == rango.Id && rango.Id != 0);
        if (anterior is not null)
        {
            var fuera = usados.Where(u => Correlativo(u.Trim(), anterior) is { } n
                                          && n >= anterior.Desde && n <= anterior.Hasta
                                          && (Correlativo(u.Trim(), rango) is not { } m || m < rango.Desde || m > rango.Hasta))
                              .FirstOrDefault();
            if (fuera is not null)
                return $"El número {fuera.Trim()} ya se usó en una factura y quedaría fuera del rango. No se puede achicar por ese lado.";
        }

        return null;
    }

    /// <summary>El correlativo de un número escrito con o sin el prefijo del rango; null si no es de ese formato.</summary>
    private static int? Correlativo(string texto, RangoContingencia rango)
    {
        var resto = texto;
        if (rango.Prefijo.Length > 0 && resto.StartsWith(rango.Prefijo, StringComparison.OrdinalIgnoreCase))
            resto = resto[rango.Prefijo.Length..];
        else if (rango.Prefijo.Length > 0 && resto.Any(c => !char.IsDigit(c)))
            return null;   // trae otro prefijo

        if (resto.Length == 0 || resto.Length > rango.Digitos || !resto.All(char.IsDigit))
            return null;

        return int.TryParse(resto, out var n) ? n : null;
    }
}
