using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Cuentas por cobrar originadas en Ventas. La factura queda inalterable; cada cobranza crea un
/// recibo y un movimiento, y actualiza el saldo en Bs dentro de la misma transacción.
/// </summary>
public sealed class CuentasPorCobrarService
{
    private readonly ICuentaPorCobrarDataSource _cuentas;
    private readonly ICobroClienteDataSource _cobros;
    private readonly IMovimientoCuentaPorCobrarDataSource _movimientos;
    private readonly BancoService _banco;
    private readonly ICuentaBancariaDataSource _cuentasBancarias;
    private readonly ITurnoDataSource _turnos;
    private readonly ITasaCambioDataSource _tasas;
    private readonly IOrganizacionDataSource _organizaciones;
    private readonly ISesionActual _sesion;

    public CuentasPorCobrarService(
        ICuentaPorCobrarDataSource cuentas,
        ICobroClienteDataSource cobros,
        IMovimientoCuentaPorCobrarDataSource movimientos,
        BancoService banco,
        ICuentaBancariaDataSource cuentasBancarias,
        ITurnoDataSource turnos,
        ITasaCambioDataSource tasas,
        IOrganizacionDataSource organizaciones,
        ISesionActual sesion)
    {
        _cuentas = cuentas;
        _cobros = cobros;
        _movimientos = movimientos;
        _banco = banco;
        _cuentasBancarias = cuentasBancarias;
        _turnos = turnos;
        _tasas = tasas;
        _organizaciones = organizaciones;
        _sesion = sesion;
    }

    public CuentaPorCobrar? DeLaFactura(int ventaId) => _cuentas.GetByVenta(ventaId);

    public IReadOnlyList<CobroCliente> Cobros(int cuentaId) =>
        _cobros.GetByCuenta(cuentaId).ToList();

    public IReadOnlyList<MovimientoCuentaPorCobrar> Movimientos(int cuentaId) =>
        _movimientos.GetByCuenta(cuentaId).ToList();

    public IReadOnlyList<CuentaBancaria> CuentasActivas() => _banco.CuentasActivas();

    public decimal TasaParaCobro(DateTime fecha) => _tasas.GetAll()
        .Where(t => t.Fecha.Date <= fecha.Date && t.Valor > 0)
        .OrderByDescending(t => t.Fecha)
        .Select(t => t.Valor)
        .FirstOrDefault();

    /// <summary>La invoca Ventas al guardar la factura; no vuelve a comprobar su permiso.</summary>
    public CuentaPorCobrar? CrearDesdeFactura(Venta factura, int usuarioId)
    {
        if (factura.Tipo != TipoDocumentoVenta.Factura || factura.CondicionPago != CondicionVenta.Credito)
            return null;
        if (factura.Id == 0 || factura.ClienteId is not { } clienteId || clienteId == 0)
            throw new InvalidOperationException("La venta a crédito necesita una factura guardada y un cliente del padrón.");
        if (factura.FechaVencimientoCredito is not { } vencimiento || vencimiento.Date < factura.Fecha.Date)
            throw new InvalidOperationException("El vencimiento del crédito no puede ser anterior a la factura.");
        if (_cuentas.GetByVenta(factura.Id) is not null)
            throw new InvalidOperationException("Esta factura ya tiene una cuenta por cobrar.");

        var pagos = CalculoFiscal.Redondear(factura.Pagos.Sum(p => p.MontoBs));
        var saldo = CalculoFiscal.Redondear(factura.Total - pagos);
        if (saldo <= 0)
            return null;

        var cuenta = new CuentaPorCobrar
        {
            VentaId = factura.Id,
            ClienteId = clienteId,
            NumeroFactura = string.IsNullOrWhiteSpace(factura.NumeroFiscal) ? factura.Numero : factura.NumeroFiscal,
            ClienteRif = factura.ClienteRif,
            ClienteNombre = factura.ClienteNombre,
            FechaEmision = factura.Fecha.Date,
            FechaVencimiento = vencimiento.Date,
            TotalFacturaBs = factura.Total,
            PagoInicialBs = pagos,
            SaldoBs = saldo,
            TasaCambioEmision = factura.TasaCambioUsada,
            Estado = EstadoCuentaPorCobrar.Pendiente,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.Now
        };

        _cuentas.Add(cuenta);
        AgregarMovimiento(cuenta.Id, TipoMovimientoCuentaPorCobrar.Apertura, saldo, factura.Fecha,
                          $"Saldo inicial de la factura {cuenta.NumeroFactura}", usuarioId,
                          documentoVentaId: factura.Id, documentoOrigen: cuenta.NumeroFactura);
        return cuenta;
    }

    /// <summary>
    /// Registra un abono. En Bs se llena MontoBs; en divisa, MontoDivisa y el servicio calcula
    /// su equivalente en Bs con la tasa BCV de la fecha del recibo.
    /// </summary>
    public CobroCliente RegistrarCobro(CuentaPorCobrar cuenta, CobroCliente datos, int usuarioId)
    {
        if (!_sesion.Puede(Permisos.CuentasPorCobrar.Cobrar))
            throw new InvalidOperationException("No tienes permiso para registrar cobros de clientes.");

        return UnidadDeTrabajo.Ejecutar(UnidadDeTrabajo.Documentos, () =>
        {
            var actual = _cuentas.GetById(cuenta.Id)?.Clonar()
                ?? throw new InvalidOperationException("La cuenta por cobrar ya no existe.");
            if (actual.SaldoBs <= 0 || actual.Estado != EstadoCuentaPorCobrar.Pendiente)
                throw new InvalidOperationException("La cuenta no tiene un saldo pendiente por cobrar.");
            if (datos.Fecha.Date > DateTime.Today)
                throw new InvalidOperationException("La fecha del cobro no puede ser futura.");
            if (datos.Fecha.Date < actual.FechaEmision.Date)
                throw new InvalidOperationException("La fecha del cobro no puede ser anterior a la factura.");

            var esDivisa = Venta.EsDivisa(datos.Metodo);
            if (esDivisa && EsContribuyenteEspecial())
                throw new InvalidOperationException(
                    "Los cobros posteriores en divisas están bloqueados para contribuyentes especiales " +
                    "hasta definir con el contador o proveedor fiscal el tratamiento del IGTF. Registre el cobro en Bs.");

            var tasa = 0m;
            if (esDivisa)
            {
                if (datos.MontoDivisa <= 0)
                    throw new InvalidOperationException("Indique el monto en divisa que recibió.");
                tasa = TasaParaCobro(datos.Fecha);
                if (tasa <= 0)
                    throw new InvalidOperationException("No hay una tasa BCV disponible para la fecha del cobro.");
                datos.MontoBs = CalculoFiscal.Redondear(datos.MontoDivisa * tasa);
                if (Math.Abs(datos.MontoBs - actual.SaldoBs) <= 0.005m * tasa + 0.005m)
                    datos.MontoBs = actual.SaldoBs;
            }
            else
            {
                datos.MontoDivisa = 0;
                datos.MontoBs = CalculoFiscal.Redondear(datos.MontoBs);
            }

            if (datos.MontoBs <= 0)
                throw new InvalidOperationException("El monto del cobro debe ser mayor que cero.");
            if (datos.MontoBs - actual.SaldoBs > 0.005m)
                throw new InvalidOperationException($"El cobro excede el saldo pendiente de Bs {actual.SaldoBs:N2}.");

            datos.CuentaPorCobrarId = actual.Id;
            datos.VentaId = actual.VentaId;
            var cuentaBancariaSolicitada = datos.CuentaBancariaId;
            datos.Numero = SiguienteNumero();
            datos.Fecha = datos.Fecha.Date;
            datos.TasaCambio = tasa;
            datos.Referencia = (datos.Referencia ?? string.Empty).Trim();
            datos.Estado = EstadoCobroCliente.Vigente;
            datos.CreadoPorId = usuarioId;
            datos.FechaCreacion = DateTime.Now;
            datos.CuentaBancariaId = null;
            datos.CuentaBancariaNombre = string.Empty;
            datos.CajaId = null;
            datos.TurnoId = null;

            if (datos.EsEfectivo)
            {
                var turno = TurnoActualPropio();
                datos.CajaId = turno.CajaId;
                datos.TurnoId = turno.Id;
            }
            else if (RequiereBanco(datos.Metodo))
            {
                var bancaria = _cuentasBancarias.GetById(cuentaBancariaSolicitada ?? 0)
                    ?? throw new InvalidOperationException("Seleccione la cuenta bancaria de destino.");
                if (!bancaria.Activa)
                    throw new InvalidOperationException($"La cuenta {bancaria.Nombre} está cerrada.");
                var moneda = bancaria.Moneda.Trim();
                var monedaCorrecta = esDivisa
                    ? moneda.Equals("USD", StringComparison.OrdinalIgnoreCase) || moneda.Equals("US$", StringComparison.OrdinalIgnoreCase)
                    : moneda.Equals("Bs", StringComparison.OrdinalIgnoreCase) || moneda.Equals("VES", StringComparison.OrdinalIgnoreCase);
                if (!monedaCorrecta)
                    throw new InvalidOperationException(
                        $"La cuenta {bancaria.Nombre} es en {bancaria.Moneda}; el cobro requiere una cuenta en {(esDivisa ? "USD" : "Bs")}.");
                datos.CuentaBancariaId = bancaria.Id;
                datos.CuentaBancariaNombre = bancaria.Nombre;
            }

            _cobros.Add(datos);
            if (datos.CuentaBancariaId is not null)
                _banco.RegistrarCobroCliente(datos, actual, usuarioId);

            actual.SaldoBs = CalculoFiscal.Redondear(actual.SaldoBs - datos.MontoBs);
            ActualizarEstado(actual, datos.Fecha);
            _cuentas.Update(actual);
            AgregarMovimiento(actual.Id, TipoMovimientoCuentaPorCobrar.Cobro, datos.MontoBs, datos.Fecha,
                              $"Recibo {datos.Numero} · {datos.MetodoTexto}", usuarioId,
                              cobroId: datos.Id, documentoOrigen: datos.Numero);
            return datos;
        });
    }

    public CobroCliente AnularCobro(CobroCliente cobro, string motivo, int usuarioId)
    {
        if (!_sesion.Puede(Permisos.CuentasPorCobrar.AnularCobro))
            throw new InvalidOperationException("No tienes permiso para anular cobros de clientes.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        return UnidadDeTrabajo.Ejecutar(UnidadDeTrabajo.Documentos, () =>
        {
            var actual = _cobros.GetById(cobro.Id)?.Clonar()
                ?? throw new InvalidOperationException("El recibo ya no existe.");
            if (!actual.Vigente)
                throw new InvalidOperationException("El recibo ya está anulado.");
            var cuenta = _cuentas.GetById(actual.CuentaPorCobrarId)?.Clonar()
                ?? throw new InvalidOperationException("La cuenta por cobrar ya no existe.");

            if (actual.TurnoId is { } turnoId)
            {
                var turno = _turnos.GetById(turnoId)
                    ?? throw new InvalidOperationException("El turno del cobro ya no existe.");
                if (!turno.EstaAbierto)
                    throw new InvalidOperationException(
                        "El cobro en efectivo pertenece a un turno cerrado. No se puede anular sin reabrir contablemente ese arqueo.");
            }
            if (actual.CuentaBancariaId is not null)
                _banco.AnularCobroCliente(actual, motivo.Trim());

            actual.Estado = EstadoCobroCliente.Anulado;
            actual.MotivoAnulacion = motivo.Trim();
            actual.FechaAnulacion = DateTime.Now;
            actual.AnuladoPorId = usuarioId;
            _cobros.Update(actual);

            cuenta.SaldoBs = CalculoFiscal.Redondear(cuenta.SaldoBs + actual.MontoBs);
            ActualizarEstado(cuenta, null);
            _cuentas.Update(cuenta);
            AgregarMovimiento(cuenta.Id, TipoMovimientoCuentaPorCobrar.AnulacionCobro, actual.MontoBs,
                              DateTime.Today, $"Anulación de {actual.Numero}: {actual.MotivoAnulacion}", usuarioId,
                              cobroId: actual.Id, documentoOrigen: actual.Numero);
            return actual;
        });
    }

    /// <summary>Aplica una retención ya validada; no genera caja ni banco.</summary>
    public void AplicarRetencion(int ventaId, string numeroComprobante, decimal monto, DateTime fecha, int usuarioId)
    {
        if (_cuentas.GetByVenta(ventaId)?.Clonar() is not { } cuenta || cuenta.SaldoBs <= 0)
            return;
        if (monto - cuenta.SaldoBs > 0.005m)
            throw new InvalidOperationException($"La retención excede el saldo pendiente de Bs {cuenta.SaldoBs:N2}.");

        cuenta.SaldoBs = CalculoFiscal.Redondear(cuenta.SaldoBs - monto);
        ActualizarEstado(cuenta, fecha);
        _cuentas.Update(cuenta);
        AgregarMovimiento(cuenta.Id, TipoMovimientoCuentaPorCobrar.RetencionIva, monto, fecha,
                          $"Comprobante de retención {numeroComprobante}", usuarioId, documentoOrigen: numeroComprobante);
    }

    /// <summary>Aplica el total fiscal de una nota a la factura afectada.</summary>
    public void AjustarPorNota(Venta nota, int usuarioId)
    {
        if (nota.DocumentoAfectadoId is not { } facturaId || _cuentas.GetByVenta(facturaId)?.Clonar() is not { } cuenta)
            return;
        var tipo = nota.Tipo == TipoDocumentoVenta.NotaCredito
            ? TipoMovimientoCuentaPorCobrar.NotaCredito
            : TipoMovimientoCuentaPorCobrar.NotaDebito;
        var delta = tipo == TipoMovimientoCuentaPorCobrar.NotaCredito ? -nota.Total : nota.Total;
        cuenta.SaldoBs = CalculoFiscal.Redondear(cuenta.SaldoBs + delta);
        ActualizarEstado(cuenta, null);
        _cuentas.Update(cuenta);
        var documento = string.IsNullOrWhiteSpace(nota.NumeroFiscal) ? nota.Numero : nota.NumeroFiscal;
        AgregarMovimiento(cuenta.Id, tipo, nota.Total, nota.Fecha,
                          $"{Venta.TextoDe(nota.Tipo)} {documento}", usuarioId,
                          documentoVentaId: nota.Id, documentoOrigen: documento);
    }

    public decimal TotalPorCobrar() => _cuentas.GetAll().Where(c => c.SaldoBs > 0).Sum(c => c.SaldoBs);
    public decimal TotalVencido() => _cuentas.GetAll().Where(c => c.EstaVencida).Sum(c => c.SaldoBs);
    public decimal TotalSaldoAFavor() => _cuentas.GetAll().Where(c => c.SaldoBs < 0).Sum(c => -c.SaldoBs);

    private bool EsContribuyenteEspecial() =>
        _sesion.UsuarioActual is { } usuario
        && _organizaciones.GetById(usuario.OrganizacionId)?.ContribuyenteEspecial == true;

    private Turno TurnoActualPropio()
    {
        var caja = _sesion.CajaActual
            ?? throw new InvalidOperationException("Para cobrar en efectivo necesitas una caja asignada.");
        var turno = _turnos.AbiertoDe(caja.Id)
            ?? throw new InvalidOperationException("Para cobrar en efectivo debes tener un turno abierto.");
        if (turno.UsuarioId != _sesion.UsuarioActual?.Id)
            throw new InvalidOperationException("El turno abierto pertenece a otro cajero.");
        return turno;
    }

    private string SiguienteNumero()
    {
        var ultimo = _cobros.GetAll()
            .Select(c => c.Numero)
            .Where(n => n.StartsWith("REC-", StringComparison.OrdinalIgnoreCase))
            .Select(n => int.TryParse(n.AsSpan(4), out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"REC-{ultimo + 1:000000}";
    }

    private static bool RequiereBanco(MetodoPago metodo) =>
        metodo is MetodoPago.Debito or MetodoPago.Credito or MetodoPago.PagoMovil
            or MetodoPago.Transferencia or MetodoPago.Zelle;

    private static void ActualizarEstado(CuentaPorCobrar cuenta, DateTime? fechaCobro)
    {
        cuenta.Estado = cuenta.SaldoBs > 0
            ? EstadoCuentaPorCobrar.Pendiente
            : cuenta.SaldoBs < 0 ? EstadoCuentaPorCobrar.SaldoAFavor : EstadoCuentaPorCobrar.Cobrada;
        cuenta.FechaCobro = cuenta.SaldoBs > 0 ? null : fechaCobro ?? cuenta.FechaCobro ?? DateTime.Today;
    }

    private void AgregarMovimiento(int cuentaId, TipoMovimientoCuentaPorCobrar tipo, decimal monto,
                                   DateTime fecha, string descripcion, int usuarioId, int? cobroId = null,
                                   int? documentoVentaId = null, string documentoOrigen = "")
    {
        _movimientos.Add(new MovimientoCuentaPorCobrar
        {
            CuentaPorCobrarId = cuentaId,
            CobroClienteId = cobroId,
            DocumentoVentaId = documentoVentaId,
            DocumentoOrigen = documentoOrigen,
            Fecha = fecha.Date,
            Tipo = tipo,
            MontoBs = Math.Abs(monto),
            Descripcion = descripcion,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.Now
        });
    }
}
