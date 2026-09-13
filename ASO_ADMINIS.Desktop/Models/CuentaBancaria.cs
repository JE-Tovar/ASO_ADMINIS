using System;

namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// De qué clase es el dinero que guarda la cuenta. No cambia ninguna regla: es para que el
/// listado distinga de un vistazo el saldo del banco del efectivo que hay en la caja fuerte,
/// que se cuentan de formas muy distintas.
/// </summary>
public enum TipoCuenta
{
    Banco,
    Caja,
    Divisas
}

/// <summary>
/// Una cuenta del centro: la cuenta del banco, la caja chica, el efectivo en divisas. Es el
/// catálogo contra el que se apunta cada <see cref="MovimientoBanco"/>.
///
/// El sistema NO se conecta con ningún banco: esto es un libro interno. La cuenta existe para
/// saber de qué bolsillo salió o entró cada unidad de dinero, y para poder cuadrarla después
/// contra el extracto que traiga el banco en papel.
///
/// <b>No guarda el saldo.</b> Lo calcula <see cref="Services.BancoService.SaldoDeLibro"/> a
/// partir de <see cref="SaldoInicial"/> más los movimientos. Guardarlo aparte es la vía rápida
/// para que un día no coincida con sus propios movimientos.
/// </summary>
public class CuentaBancaria : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Nucleo (organizacion) duenno de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Como la llama la gente del centro: "Banco Mercantil", "Caja chica".</summary>
    public string Nombre { get; set; } = string.Empty;

    public TipoCuenta Tipo { get; set; }

    /// <summary>Institución financiera. Vacío en una caja, que no tiene banco detrás.</summary>
    public string Banco { get; set; } = string.Empty;

    /// <summary>
    /// Número de cuenta. Se guarda tal como lo escriben, sin validar formato: aquí no se emiten
    /// pagos, solo se anota de qué cuenta salieron.
    /// </summary>
    public string NumeroCuenta { get; set; } = string.Empty;

    /// <summary>
    /// Moneda, como texto ("Bs", "USD"). PROVISIONAL: no hay conversión entre monedas ni tasa
    /// de cambio, así que una cuenta en divisas suma su propio saldo y no se mezcla con las
    /// demás. Pendiente de definir con el socio si hace falta consolidar.
    /// </summary>
    public string Moneda { get; set; } = "Bs";

    /// <summary>
    /// Lo que había en la cuenta el día que arrancó el sistema. Absorbe toda la historia
    /// anterior: sin él, el saldo solo contaría lo que pasó por la aplicación y no sería el
    /// dinero real.
    /// </summary>
    public decimal SaldoInicial { get; set; }

    /// <summary>Fecha a la que corresponde <see cref="SaldoInicial"/>.</summary>
    public DateTime FechaApertura { get; set; }

    /// <summary>
    /// Una cuenta cerrada deja de ofrecerse al registrar movimientos, pero no se borra: sus
    /// asientos viejos siguen citándola.
    /// </summary>
    public bool Activa { get; set; } = true;

    public string Notas { get; set; } = string.Empty;

    public string TipoTexto => Tipo switch
    {
        TipoCuenta.Banco => "Banco",
        TipoCuenta.Caja => "Caja",
        TipoCuenta.Divisas => "Divisas",
        _ => Tipo.ToString()
    };

    /// <summary>Rótulo de una línea: "Banco Mercantil (Bs)".</summary>
    public string NombreConMoneda => $"{Nombre} ({Moneda})";

    /// <summary>
    /// Saldo de hoy: el inicial más todos sus movimientos. NO se persiste (va con Ignore en el
    /// DbContext) y no es derivada como el resto: la rellena la pantalla, porque depende de la
    /// tabla de movimientos y la fila de la cuenta no puede calcularla sola.
    ///
    /// Guardarlo en la tabla sería la vía rápida para que un día no coincida con sus propios
    /// asientos, que es justo lo que este módulo viene a evitar.
    /// </summary>
    public decimal SaldoActual { get; set; }

    public string SaldoActualTexto => SaldoActual.ToString("N2");

    public string EstadoTexto => Activa ? "Activa" : "Cerrada";

    public string SaldoInicialTexto => SaldoInicial.ToString("N2");

    public string AperturaTexto => FechaApertura.ToString("dd/MM/yyyy");

    /// <summary>Identificación en el listado: el banco, o el tipo si no hay banco detrás.</summary>
    public string DetalleTexto => string.IsNullOrWhiteSpace(Banco)
        ? TipoTexto
        : string.IsNullOrWhiteSpace(NumeroCuenta) ? Banco : $"{Banco} — {NumeroCuenta}";

    /// <summary>Copia superficial (solo tipos de valor y cadenas) para no mutar el original.</summary>
    public CuentaBancaria Clonar() => (CuentaBancaria)MemberwiseClone();
}
