using ASO_ADMINIS.Desktop.Configuration;

namespace ASO_ADMINIS.Desktop.Services.Fiscal;

/// <summary>
/// Elige el emisor fiscal según <c>appsettings(.local).json</c>:
/// <code>
/// "EmisorFiscal": { "Tipo": "Simulado", "Modo": "Maquina" | "ImprentaDigital", "Fallar": false }
/// </code>
/// Un tipo real nuevo (p. ej. "Hka" o el nombre de una imprenta digital) se agrega aquí con su
/// implementación de <see cref="IEmisorFiscal"/>.
/// </summary>
public static class EmisoresFiscales
{
    public static IEmisorFiscal Crear()
    {
        var tipo = AppConfig.Valor("EmisorFiscal:Tipo") ?? "Simulado";
        var modo = AppConfig.Valor("EmisorFiscal:Modo") ?? "Maquina";
        var fallar = string.Equals(AppConfig.Valor("EmisorFiscal:Fallar"), "true", StringComparison.OrdinalIgnoreCase);

        return tipo switch
        {
            "Simulado" => new EmisorFiscalSimulado(
                comoMaquina: !string.Equals(modo, "ImprentaDigital", StringComparison.OrdinalIgnoreCase),
                fallar: fallar),
            _ => throw new InvalidOperationException(
                $"Emisor fiscal \"{tipo}\" no reconocido. Revise EmisorFiscal:Tipo en appsettings.local.json.")
        };
    }
}
