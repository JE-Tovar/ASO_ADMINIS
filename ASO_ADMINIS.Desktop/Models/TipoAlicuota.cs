namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Alícuota de IVA de un artículo o de un renglón. Se persiste como ORDINAL: miembros nuevos al
/// final.
///
/// El porcentaje de cada una NO va aquí: lo fija la organización (<see cref="Organizacion"/>
/// <c>IvaGeneral</c>/<c>IvaReducida</c>/<c>IvaAdicional</c>) porque lo decreta el Ejecutivo y
/// cambia sin tocar el código. Cada renglón de un documento guarda su porcentaje como snapshot.
/// </summary>
public enum TipoAlicuota
{
    /// <summary>Exento o exonerado: no causa IVA.</summary>
    Exento,

    /// <summary>Alícuota general (16 % hoy).</summary>
    General,

    /// <summary>Alícuota reducida (8 % hoy): ciertos alimentos, insumos…</summary>
    Reducida,

    /// <summary>General más la adicional de bienes suntuarios (16 % + 15 % hoy).</summary>
    Adicional
}

public static class Alicuotas
{
    public static readonly TipoAlicuota[] Todas =
        [TipoAlicuota.General, TipoAlicuota.Reducida, TipoAlicuota.Adicional, TipoAlicuota.Exento];

    public static string Texto(TipoAlicuota tipo) => tipo switch
    {
        TipoAlicuota.Exento => "Exento",
        TipoAlicuota.General => "General",
        TipoAlicuota.Reducida => "Reducida",
        TipoAlicuota.Adicional => "Adicional (suntuario)",
        _ => tipo.ToString()
    };

    /// <summary>Marca corta de un renglón en el documento: "(E)" exento, "(G)", "(R)", "(A)".</summary>
    public static string Marca(TipoAlicuota tipo) => tipo switch
    {
        TipoAlicuota.Exento => "E",
        TipoAlicuota.General => "G",
        TipoAlicuota.Reducida => "R",
        TipoAlicuota.Adicional => "A",
        _ => "?"
    };

    /// <summary>Porcentaje vigente según la configuración de la organización.</summary>
    public static decimal Porcentaje(TipoAlicuota tipo, Organizacion organizacion) => tipo switch
    {
        TipoAlicuota.General => organizacion.IvaGeneral,
        TipoAlicuota.Reducida => organizacion.IvaReducida,
        TipoAlicuota.Adicional => organizacion.IvaAdicional,
        _ => 0m
    };
}

/// <summary>Opción para un ComboBox: valor + texto.</summary>
public sealed record OpcionAlicuota(TipoAlicuota Valor, string Texto)
{
    public static OpcionAlicuota[] Todas { get; } =
        [.. Alicuotas.Todas.Select(a => new OpcionAlicuota(a, Alicuotas.Texto(a)))];
}
