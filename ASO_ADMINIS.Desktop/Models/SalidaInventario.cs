using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// A qué proceso de la planta va lo que sale del almacén. Lista fija en código, no catálogo:
/// mientras los procesos reales no estén cerrados, un enum se revisa de un vistazo y no hay que
/// mantener una pantalla para él. Se persiste como ORDINAL: miembros nuevos al final.
/// </summary>
public enum AreaDestino
{
    ControlDeCalidad,
    Lavado,
    Empacado,
    Etiquetado,
    Mantenimiento,
    Administracion,
    Otro
}

/// <summary>Por qué sale. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum MotivoSalida
{
    Consumo,
    Merma,
    Devolucion,
    Traslado
}

/// <summary>Estados de una salida. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoSalida
{
    Registrada,
    Anulada
}

/// <summary>
/// Boleto de salida del almacén: qué se llevaron, a qué proceso, quién lo retiró y quién lo
/// autorizó.
///
/// Es la raíz de un agregado (ver <see cref="Lineas"/>). No tiene documento aguas abajo: a
/// diferencia de <see cref="EntradaInventario"/>, una salida no le debe nada a nadie, así que
/// anularla solo devuelve la existencia.
/// </summary>
public class SalidaInventario : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo del boleto, "SAL-000123". Lo asigna
    /// <c>SalidasInventarioService</c> al registrar, no el formulario.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public AreaDestino Destino { get; set; }

    /// <summary>Detalle libre del destino; obligatorio cuando <see cref="Destino"/> es
    /// <see cref="AreaDestino.Otro"/>.</summary>
    public string DestinoDetalle { get; set; } = string.Empty;

    public MotivoSalida Motivo { get; set; }

    /// <summary>Persona que se lleva el material. Texto libre: no todos los que retiran son
    /// usuarios del sistema.</summary>
    public string RetiradoPor { get; set; } = string.Empty;

    /// <summary>Quien tenía la sesión abierta al emitir el boleto. No es un campo del
    /// formulario: lo pone el servicio a partir de la sesión.</summary>
    public int AutorizadoPorId { get; set; }

    public string AutorizadoPorNombre { get; set; } = string.Empty;  // snapshot

    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Lo que se llevaron, artículo por artículo.</summary>
    public List<SalidaInventarioLinea> Lineas { get; set; } = [];

    public EstadoSalida Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    /// <summary>Si esta salida resta de las existencias. Un boleto anulado deja de contar, que
    /// es lo que hace que anular devuelva el stock sin tocar ninguna otra fila.</summary>
    public bool CuentaEnKardex => Estado == EstadoSalida.Registrada;

    public string DestinoTexto => Destino == AreaDestino.Otro && !string.IsNullOrWhiteSpace(DestinoDetalle)
        ? DestinoDetalle
        : Destino switch
        {
            AreaDestino.ControlDeCalidad => "Control de calidad",
            AreaDestino.Lavado => "Lavado",
            AreaDestino.Empacado => "Empacado",
            AreaDestino.Etiquetado => "Etiquetado",
            AreaDestino.Mantenimiento => "Mantenimiento",
            AreaDestino.Administracion => "Administración",
            _ => "Otro"
        };

    public string MotivoTexto => Motivo switch
    {
        MotivoSalida.Consumo => "Consumo",
        MotivoSalida.Merma => "Merma",
        MotivoSalida.Devolucion => "Devolución",
        _ => "Traslado"
    };

    public string EstadoTexto => Estado == EstadoSalida.Registrada ? "Registrada" : "Anulada";

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

    public int CantidadLineas => Lineas.Count;

    public decimal TotalUnidades => Lineas.Sum(l => l.Cantidad);

    /// <summary>
    /// Copia con las líneas duplicadas de verdad: <c>MemberwiseClone</c> compartiría la misma
    /// lista entre el original y la copia, y editar la copia mutaría lo que está en pantalla.
    /// </summary>
    public SalidaInventario Clonar()
    {
        var copia = (SalidaInventario)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Renglón de un boleto de salida. Mismo criterio que
/// <see cref="EntradaInventarioLinea"/>: el <c>ArticuloId</c> para el kardex, y código, nombre y
/// unidad congelados como snapshot de texto.
/// </summary>
public class SalidaInventarioLinea
{
    public int ArticuloId { get; set; }
    public string ArticuloCodigo { get; set; } = string.Empty;  // snapshot
    public string ArticuloNombre { get; set; } = string.Empty;  // snapshot
    public string UnidadTexto { get; set; } = string.Empty;     // snapshot

    public decimal Cantidad { get; set; }

    public string CantidadTexto => $"{Cantidad:N2} {UnidadTexto}".Trim();
    public string ArticuloTexto => $"{ArticuloCodigo} · {ArticuloNombre}";

    public SalidaInventarioLinea Clonar() => (SalidaInventarioLinea)MemberwiseClone();
}
