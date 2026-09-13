using System;
using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Lo que un documento no puede responder cuando se cobra o se paga: de qué cuenta salió el
/// dinero, qué día se movió de verdad y con qué referencia.
///
/// Va como un solo parámetro y no como tres sueltos porque los tres viajan siempre juntos, desde
/// el mismo editor (<c>AsientoBancoEditorViewModel</c>) hasta los tres servicios que registran
/// cobros y pagos.
/// </summary>
public sealed record AsientoBanco(int CuentaId, DateTime Fecha, string Referencia);

/// <summary>
/// Reglas del libro de banco: el saldo de cada cuenta, los asientos que nacen de un documento y
/// los que teclea alguien.
///
/// <b>El sistema no se conecta con ningún banco.</b> Esto es un libro interno que dice cuánto
/// dinero entró y salió por la aplicación; cuadrarlo contra el extracto real es lo que hace la
/// marca de conciliado.
///
/// Regla de oro: las tres transiciones (conciliar, desconciliar, anular) revalidan aquí y lanzan
/// <see cref="InvalidOperationException"/> en español, aunque el botón ya lo hubiera impedido.
///
/// <b>Qué NO entra en el libro, y por qué.</b> Solo entra lo que movió caja de verdad. Un
/// compromiso autorizado pero no pagado (una orden de compra aprobada, por ejemplo) no es una
/// salida todavía, y un costo ya cubierto por una compra que se pagó por otra vía no puede
/// contarse dos veces sin descuadrar el saldo para siempre.
/// </summary>
public sealed class BancoService
{
    private readonly IMovimientoBancoDataSource _movimientos;
    private readonly ICuentaBancariaDataSource _cuentas;
    private readonly ISesionActual _sesion;

    public BancoService(IMovimientoBancoDataSource movimientos, ICuentaBancariaDataSource cuentas, ISesionActual sesion)
    {
        _movimientos = movimientos;
        _cuentas = cuentas;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    /// <summary>
    /// Solo el movimiento manual se edita, y solo mientras nadie lo haya conciliado. Uno derivado
    /// de un documento no: su verdad está en la factura o la liquidación, y corregirlo aquí lo
    /// dejaría diciendo algo distinto de lo que dice el documento que lo originó.
    /// </summary>
    public bool PuedeEditar(MovimientoBanco m) =>
        !m.EsDerivado && m.Estado == EstadoMovimientoBanco.Registrado;

    public bool PuedeEliminar(MovimientoBanco m) =>
        !m.EsDerivado && m.Estado == EstadoMovimientoBanco.Registrado;

    public bool PuedeConciliar(MovimientoBanco m) => m.Estado == EstadoMovimientoBanco.Registrado;

    public bool PuedeDesconciliar(MovimientoBanco m) => m.Estado == EstadoMovimientoBanco.Conciliado;

    public bool PuedeAnular(MovimientoBanco m) => m.Estado != EstadoMovimientoBanco.Anulado;

    // --- Catálogo de cuentas ---

    public IReadOnlyList<CuentaBancaria> CuentasActivas() => _cuentas.GetActivas().ToList();

    /// <summary>
    /// Valida una cuenta antes de guardarla. El nombre no puede repetirse: es lo que se lee en
    /// cada asiento, y dos "Caja chica" harían imposible saber cuál se eligió.
    /// </summary>
    public bool ValidarCuenta(CuentaBancaria cuenta, out string? error)
    {
        if (string.IsNullOrWhiteSpace(cuenta.Nombre))
        {
            error = "Indique el nombre de la cuenta.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(cuenta.Moneda))
        {
            error = "Indique la moneda de la cuenta.";
            return false;
        }

        var repetida = _cuentas.GetAll()
            .Where(c => c.Id != cuenta.Id)
            .Any(c => string.Equals(c.Nombre.Trim(), cuenta.Nombre.Trim(),
                                    StringComparison.OrdinalIgnoreCase));

        if (repetida)
        {
            error = $"Ya existe una cuenta llamada {cuenta.Nombre.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Una cuenta con asientos no se borra: los movimientos viejos la citan y quedarían huérfanos.
    /// Para sacarla de circulación se desmarca <see cref="CuentaBancaria.Activa"/>.
    /// </summary>
    public bool PuedeEliminarCuenta(CuentaBancaria cuenta) =>
        !_movimientos.GetByCuenta(cuenta.Id).Any();

    // --- Movimiento manual ---

    public bool Validar(MovimientoBanco movimiento, out string? error)
    {
        if (movimiento.CuentaId == 0)
        {
            error = "Seleccione la cuenta del movimiento.";
            return false;
        }

        if (movimiento.Monto <= 0)
        {
            error = "El monto debe ser mayor que cero.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(movimiento.Concepto))
        {
            error = "Indique el concepto del movimiento.";
            return false;
        }

        if (_cuentas.GetById(movimiento.CuentaId) is not { } cuenta)
        {
            error = "La cuenta seleccionada ya no existe.";
            return false;
        }

        // La fecha valor anterior a la apertura caería antes del saldo inicial, que ya absorbe
        // toda la historia previa: el asiento se contaría dos veces.
        if (movimiento.Fecha.Date < cuenta.FechaApertura.Date)
        {
            error = $"La fecha no puede ser anterior a la apertura de la cuenta " +
                    $"({cuenta.FechaApertura:dd/MM/yyyy}).";
            return false;
        }

        error = null;
        return true;
    }

    // --- Asientos que nacen de un documento ---

    /// <summary>
    /// Salida por el pago de una factura de proveedor. Lo llama
    /// <see cref="CuentasPorPagarService.RegistrarPago"/> en la misma operación que marca la
    /// factura como pagada: el usuario no teclea nada aquí.
    ///
    /// No valida <c>_sesion.Puede</c> aquí adentro a propósito (composición, no doble chequeo):
    /// el permiso ya lo exigió el servicio que llama (<c>Finanzas.Pagar</c>), que es justamente
    /// el reparto de roles documentado más arriba.
    /// </summary>
    public MovimientoBanco RegistrarPagoProveedor(FacturaProveedor factura, AsientoBanco datos, int usuarioId)
        => Asentar(TipoMovimientoBanco.Salida,
                   factura.Monto,
                   $"Pago factura Nº {factura.NumeroDocumento} — {factura.ProveedorNombre}",
                   CategoriaMovimiento.PagoProveedor,
                   OrigenMovimiento.FacturaProveedor,
                   factura.Id,
                   datos,
                   usuarioId);

    /// <summary>
    /// El cuerpo común de los tres. Congela el monto que dice el documento en este instante: si
    /// mañana alguien corrige la factura, el libro no se mueve — mismo criterio que
    /// <c>TarifaMonto</c> en los documentos que citan una tarifa.
    /// </summary>
    private MovimientoBanco Asentar(TipoMovimientoBanco tipo,
                                    decimal monto,
                                    string concepto,
                                    CategoriaMovimiento categoria,
                                    OrigenMovimiento origen,
                                    int origenId,
                                    AsientoBanco datos,
                                    int usuarioId)
    {
        if (monto <= 0)
            throw new InvalidOperationException(
                "El documento no tiene monto: no se puede registrar el movimiento en el banco.");

        // Anti-doble-asiento: el documento no puede tener dos asientos vivos.
        if (_movimientos.GetByOrigen(origen, origenId)
                        .Any(m => m.Estado != EstadoMovimientoBanco.Anulado))
            throw new InvalidOperationException(
                "Este documento ya tiene su movimiento registrado en el banco.");

        var cuenta = ExigirCuentaActiva(datos.CuentaId);

        var movimiento = new MovimientoBanco
        {
            CuentaId = cuenta.Id,
            CuentaNombre = cuenta.Nombre,
            Fecha = datos.Fecha.Date,
            Tipo = tipo,
            Monto = monto,
            Concepto = concepto,
            Referencia = datos.Referencia.Trim(),
            Categoria = categoria,
            Origen = origen,
            OrigenId = origenId,
            Estado = EstadoMovimientoBanco.Registrado,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.Now
        };

        return _movimientos.Add(movimiento);
    }

    // --- Transferencia entre cuentas ---

    /// <summary>
    /// Mueve dinero de una cuenta a otra. Escribe DOS asientos enlazados por
    /// <see cref="MovimientoBanco.ContraparteId"/> —una salida y una entrada— porque el dinero
    /// sale de un saldo y entra en otro; un solo asiento dejaría una de las dos cuentas mintiendo.
    /// El disponible total no cambia.
    /// </summary>
    public (MovimientoBanco Salida, MovimientoBanco Entrada) Transferir(int cuentaOrigenId,
                                                                       int cuentaDestinoId,
                                                                       decimal monto,
                                                                       DateTime fecha,
                                                                       string concepto,
                                                                       string referencia,
                                                                       int usuarioId)
    {
        if (!_sesion.Puede(Permisos.Banco.Transferir))
            throw new InvalidOperationException("No tienes permiso para transferir entre cuentas.");

        if (cuentaOrigenId == cuentaDestinoId)
            throw new InvalidOperationException("La cuenta de origen y la de destino deben ser distintas.");

        if (monto <= 0)
            throw new InvalidOperationException("El monto de la transferencia debe ser mayor que cero.");

        var origen = ExigirCuentaActiva(cuentaOrigenId);
        var destino = ExigirCuentaActiva(cuentaDestinoId);

        // Sin conversión de moneda: mover Bs a una cuenta en USD daría un saldo que no significa
        // nada. PROVISIONAL hasta que se defina la tasa con el socio.
        if (!string.Equals(origen.Moneda.Trim(), destino.Moneda.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"No se puede transferir entre cuentas de distinta moneda ({origen.Moneda} y {destino.Moneda}).");

        var texto = string.IsNullOrWhiteSpace(concepto)
            ? $"Transferencia {origen.Nombre} → {destino.Nombre}"
            : concepto.Trim();

        var salida = _movimientos.Add(new MovimientoBanco
        {
            CuentaId = origen.Id,
            CuentaNombre = origen.Nombre,
            Fecha = fecha.Date,
            Tipo = TipoMovimientoBanco.Salida,
            Monto = monto,
            Concepto = texto,
            Referencia = referencia.Trim(),
            Categoria = CategoriaMovimiento.Transferencia,
            Origen = OrigenMovimiento.Transferencia,
            Estado = EstadoMovimientoBanco.Registrado,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.Now
        });

        var entrada = _movimientos.Add(new MovimientoBanco
        {
            CuentaId = destino.Id,
            CuentaNombre = destino.Nombre,
            Fecha = fecha.Date,
            Tipo = TipoMovimientoBanco.Entrada,
            Monto = monto,
            Concepto = texto,
            Referencia = referencia.Trim(),
            Categoria = CategoriaMovimiento.Transferencia,
            Origen = OrigenMovimiento.Transferencia,
            ContraparteId = salida.Id,
            Estado = EstadoMovimientoBanco.Registrado,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.Now
        });

        // El enlace se completa en los dos sentidos recién ahora: al crear la salida todavía no
        // existía el Id de la entrada.
        salida.ContraparteId = entrada.Id;
        _movimientos.Update(salida);

        return (salida, entrada);
    }

    // --- Transiciones ---

    /// <summary>
    /// Marca que el movimiento ya apareció en el extracto del banco. Queda quién lo concilió y
    /// cuándo: es la misma auditoría de la decisión que lleva <see cref="PeticionCambio"/>.
    /// </summary>
    public MovimientoBanco Conciliar(MovimientoBanco movimiento, int usuarioId)
    {
        if (!PuedeConciliar(movimiento))
            throw new InvalidOperationException(
                movimiento.Estado == EstadoMovimientoBanco.Conciliado
                    ? "Este movimiento ya está conciliado."
                    : "Un movimiento anulado no se concilia.");

        if (!_sesion.Puede(Permisos.Banco.Conciliar))
            throw new InvalidOperationException("No tienes permiso para conciliar movimientos de banco.");

        var copia = movimiento.Clonar();
        copia.Estado = EstadoMovimientoBanco.Conciliado;
        copia.FechaConciliacion = DateTime.Now;
        copia.ConciliadoPorId = usuarioId;
        _movimientos.Update(copia);
        return copia;
    }

    /// <summary>Deshace la marca, para cuando se concilió el renglón equivocado.</summary>
    public MovimientoBanco Desconciliar(MovimientoBanco movimiento)
    {
        if (!PuedeDesconciliar(movimiento))
            throw new InvalidOperationException("Solo se puede desconciliar un movimiento conciliado.");

        if (!_sesion.Puede(Permisos.Banco.Conciliar))
            throw new InvalidOperationException("No tienes permiso para desconciliar movimientos de banco.");

        var copia = movimiento.Clonar();
        copia.Estado = EstadoMovimientoBanco.Registrado;
        copia.FechaConciliacion = null;
        copia.ConciliadoPorId = null;
        _movimientos.Update(copia);
        return copia;
    }

    /// <summary>
    /// Saca el asiento del saldo sin borrarlo, con su motivo. Si es media transferencia, anula
    /// también la otra mitad: dejar una sola haría aparecer o desaparecer dinero.
    /// </summary>
    public MovimientoBanco Anular(MovimientoBanco movimiento, string motivo)
    {
        if (!PuedeAnular(movimiento))
            throw new InvalidOperationException("Este movimiento ya está anulado.");

        if (!_sesion.Puede(Permisos.Banco.Anular))
            throw new InvalidOperationException("No tienes permiso para anular movimientos de banco.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var copia = AnularUno(movimiento, motivo);

        if (movimiento.ContraparteId is { } contraparteId
            && _movimientos.GetById(contraparteId) is { } contraparte
            && contraparte.Estado != EstadoMovimientoBanco.Anulado)
        {
            AnularUno(contraparte, motivo);
        }

        return copia;
    }

    private MovimientoBanco AnularUno(MovimientoBanco movimiento, string motivo)
    {
        var copia = movimiento.Clonar();
        copia.Estado = EstadoMovimientoBanco.Anulado;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _movimientos.Update(copia);
        return copia;
    }

    // --- Saldos ---

    /// <summary>
    /// Lo que dice el libro: el saldo inicial más todo lo que entró y salió. Los anulados no
    /// cuentan (su <see cref="MovimientoBanco.Efecto"/> es cero).
    /// </summary>
    public decimal SaldoDeLibro(int cuentaId)
    {
        var inicial = _cuentas.GetById(cuentaId)?.SaldoInicial ?? 0m;
        return inicial + _movimientos.GetByCuenta(cuentaId).Sum(m => m.Efecto);
    }

    /// <summary>
    /// Lo que el banco ya confirmó: solo los movimientos conciliados. La diferencia contra
    /// <see cref="SaldoDeLibro"/> es lo que todavía está en camino — un cheque girado que nadie
    /// ha cobrado.
    /// </summary>
    public decimal SaldoConciliado(int cuentaId)
    {
        var inicial = _cuentas.GetById(cuentaId)?.SaldoInicial ?? 0m;
        return inicial + _movimientos.GetByCuenta(cuentaId)
            .Where(m => m.Estado == EstadoMovimientoBanco.Conciliado)
            .Sum(m => m.Efecto);
    }

    /// <summary>
    /// Dinero disponible en todas las cuentas activas. Es la cifra del dashboard de Finanzas, y
    /// no debe confundirse con el "Saldo neto" de al lado: aquel es por cobrar menos por pagar,
    /// o sea la diferencia entre dos deudas, no dinero que se pueda gastar hoy.
    ///
    /// PROVISIONAL: suma monedas distintas sin convertir, porque no hay tasa de cambio. Mientras
    /// solo haya cuentas en bolívares la cifra es correcta.
    /// </summary>
    public decimal DisponibleTotal() => _cuentas.GetActivas().Sum(c => SaldoDeLibro(c.Id));

    private CuentaBancaria ExigirCuentaActiva(int cuentaId)
    {
        if (_cuentas.GetById(cuentaId) is not { } cuenta)
            throw new InvalidOperationException("Seleccione la cuenta del movimiento.");

        if (!cuenta.Activa)
            throw new InvalidOperationException($"La cuenta {cuenta.Nombre} está cerrada.");

        return cuenta;
    }
}
