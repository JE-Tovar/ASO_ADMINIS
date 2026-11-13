using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services.Fiscal;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas del documento fiscal de venta: factura, nota de crédito y nota de débito.
///
/// <b>Orden de cada emisión:</b> validar → emitir en el <see cref="IEmisorFiscal"/> → guardar →
/// mover el almacén. El emisor va antes de guardar porque es la operación que puede rechazar
/// (sin conexión, sin papel): si falla, no se guarda nada. Si lo que falla es el guardado
/// DESPUÉS de emitir, el documento ya existe para el fisco: se anota en un diario local
/// (%AppData%\ASO Adminis\diario-fiscal.jsonl) para conciliarlo a mano, y se avisa.
///
/// <b>Nada se anula.</b> Un documento emitido se corrige con una nota de crédito (devuelve
/// mercancía al almacén) o de débito (cargo adicional). La base lo hace cumplir además en
/// <c>AsoAdminisDbContext.SaveChanges</c>.
///
/// El almacén se mueve escribiendo directo contra las fuentes de Salidas/Entradas, no por sus
/// servicios: las reglas de un boleto (quién retira, a qué área) o de una compra (proveedor,
/// cuenta por pagar) no aplican a una venta.
/// </summary>
public sealed class VentasService
{
    private const string Prefijo = "VTA-";
    private const string PrefijoSalida = "SAL-";
    private const string PrefijoEntrada = "ENT-";

    private readonly IVentaDataSource _ventas;
    private readonly IArticuloDataSource _articulos;
    private readonly ISalidaInventarioDataSource _salidas;
    private readonly IEntradaInventarioDataSource _entradas;
    private readonly InventarioService _inventario;
    private readonly ISesionActual _sesion;
    private readonly IEmisorFiscal _emisor;
    private readonly Func<Organizacion?> _organizacion;
    private readonly Func<string> _caja;

    public VentasService(IVentaDataSource ventas,
                         IArticuloDataSource articulos,
                         ISalidaInventarioDataSource salidas,
                         IEntradaInventarioDataSource entradas,
                         InventarioService inventario,
                         ISesionActual sesion,
                         IEmisorFiscal emisor,
                         Func<Organizacion?>? organizacion = null,
                         Func<string>? caja = null)
    {
        _ventas = ventas;
        _articulos = articulos;
        _salidas = salidas;
        _entradas = entradas;
        _inventario = inventario;
        _sesion = sesion;
        _emisor = emisor;
        _organizacion = organizacion ?? (() => Ambito.Actual);
        _caja = caja ?? (() => Ajustes.Actual.CajaCodigo);
    }

    public IEmisorFiscal Emisor => _emisor;

    private Organizacion Emisora =>
        _organizacion() ?? throw new InvalidOperationException("No hay organización activa.");

    public bool PercibeIgtf => _organizacion()?.ContribuyenteEspecial == true;

    // ---------------------------------------------------------------- Cálculo

    /// <summary>
    /// Completa renglones y totales de una factura en borrador con la tasa del documento. Lo
    /// usa el editor en vivo y lo repite <see cref="EmitirFactura"/> antes de emitir.
    /// </summary>
    public void Preparar(Venta factura)
    {
        var org = Emisora;
        foreach (var linea in factura.Lineas)
            CalculoFiscal.CalcularLinea(linea, org, factura.TasaCambioUsada);

        foreach (var pago in factura.Pagos.Where(p => p.EsDivisa))
            pago.MontoBs = CalculoFiscal.Redondear(pago.MontoDivisa * factura.TasaCambioUsada);

        CalculoFiscal.Totalizar(factura, org, PercibeIgtf);
    }

    // ---------------------------------------------------------------- Validación

    /// <summary>Lo que debe cumplir el sistema antes de emitir cualquier documento.</summary>
    public bool PuedeEmitir(out string? error)
    {
        var org = _organizacion();
        if (org is null || !org.DatosFiscalesCompletos)
        {
            error = "Faltan los datos fiscales de la organización (razón social, RIF válido y dirección fiscal). " +
                    "Complételos en Administración · Organización.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_caja()))
        {
            error = "Esta máquina no tiene caja asignada. Indíquela en Configuración · Aplicación.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Valida una factura ya preparada contra la existencia REAL del momento, no la que tenía el
    /// formulario al abrirse — mismo criterio que <c>SalidasInventarioService.Validar</c>.
    /// </summary>
    public bool Validar(Venta factura, out string? error)
    {
        if (!PuedeEmitir(out error))
            return false;

        if (factura.TasaCambioUsada <= 0)
        {
            error = "No hay tasa de cambio BCV registrada: actualícela en Administración · Organización.";
            return false;
        }

        if (!ValidarCliente(factura, out error))
            return false;

        if (factura.Lineas.Count == 0)
        {
            error = "Agregue al menos un artículo o servicio.";
            return false;
        }

        foreach (var linea in factura.Lineas)
        {
            // Un renglón suelto (sin artículo) traería su propio precio. En la factura todo sale
            // del catálogo; los conceptos libres son solo de la nota de débito.
            if (linea.ArticuloId is null)
            {
                error = $"\"{linea.Descripcion}\" no está en el catálogo: dé de alta el artículo o el servicio antes de facturarlo.";
                return false;
            }

            if (linea.Cantidad <= 0)
            {
                error = $"La cantidad de {linea.Descripcion} debe ser mayor que cero.";
                return false;
            }

            // El precio no lo decide quien factura: sale del catálogo (FijarDesdeCatalogo). Un
            // artículo sin precio no se vende hasta que se le asigne en su catálogo.
            if (linea.PrecioUnitarioUsd <= 0)
            {
                error = $"{linea.Descripcion} no tiene precio de venta; asígnelo en " +
                        (linea.EsServicio ? "Ventas · Servicios." : "Inventario · Almacén.");
                return false;
            }

            if (linea.Factor <= 0)
            {
                error = $"Elija la presentación de {linea.Descripcion}.";
                return false;
            }

            // La columna guarda 3 decimales: con más, la factura guardada no cuadraría con su monto.
            if (decimal.Round(linea.Cantidad, 3) != linea.Cantidad)
            {
                error = $"La cantidad de {linea.Descripcion} admite hasta 3 decimales (p. ej. 1,250 kg).";
                return false;
            }

            // Solo lo que se vende a granel (kg, L, m) lleva decimales; lo demás va por unidad entera.
            if (decimal.Truncate(linea.Cantidad) != linea.Cantidad
                && _articulos.GetById(linea.ArticuloId.Value) is { EsGranel: false } entero)
            {
                error = $"{entero.Codigo} · {entero.Descripcion} se vende por unidad entera: no se puede facturar {linea.Cantidad:0.###} {linea.PresentacionNombre}.";
                return false;
            }

            if (InventarioService.DescripcionGenerica(linea.Descripcion))
            {
                error = $"\"{linea.Descripcion}\" es una descripción genérica; corrija el nombre del artículo en el almacén.";
                return false;
            }
        }

        // Existencia: se suma por artículo, porque el mismo artículo puede ir en dos renglones
        // (en presentaciones distintas) y lo que sale del almacén es la suma. Un servicio no
        // tiene existencia.
        foreach (var grupo in factura.Lineas.Where(l => l.ArticuloId is not null && !l.EsServicio).GroupBy(l => l.ArticuloId!.Value))
        {
            var articulo = _articulos.GetById(grupo.Key);
            if (articulo is null)
            {
                error = $"El artículo {grupo.First().Descripcion} ya no existe en el almacén.";
                return false;
            }

            var pedido = grupo.Sum(l => l.CantidadBase);
            var disponible = _inventario.Existencia(articulo.Id);
            if (pedido > disponible)
            {
                error = $"No hay existencia suficiente de {articulo.Codigo} · {articulo.Descripcion}: hay {disponible:N2} {articulo.UnidadCorta} " +
                        $"y se piden {pedido:N2} {articulo.UnidadCorta}.";
                return false;
            }
        }

        if (factura.Pagos.Count == 0)
        {
            error = "Indique cómo se pagó.";
            return false;
        }

        if (factura.Pagos.Any(p => p.MontoBs <= 0))
        {
            error = "Cada pago debe tener un monto mayor que cero.";
            return false;
        }

        var pendiente = CalculoFiscal.Pendiente(factura);
        if (pendiente > 0.005m)
        {
            error = $"Faltan Bs. {pendiente:N2} por cobrar.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool ValidarCliente(Venta documento, out string? error)
    {
        if (!string.IsNullOrWhiteSpace(documento.ClienteRif))
        {
            if (Rif.Error(documento.ClienteRif) is { } errorRif)
            {
                error = errorRif;
                return false;
            }

            if (string.IsNullOrWhiteSpace(documento.ClienteNombre))
            {
                error = "Indique el nombre o razón social del cliente.";
                return false;
            }

            if (Rif.EsRifValido(documento.ClienteRif) && string.IsNullOrWhiteSpace(documento.ClienteDireccion))
            {
                error = "Un cliente con RIF necesita su dirección fiscal en la factura.";
                return false;
            }
        }

        error = null;
        return true;
    }

    // ---------------------------------------------------------------- Factura

    /// <summary>Emite la factura en el emisor fiscal, la guarda y descuenta el almacén.</summary>
    public Venta EmitirFactura(Venta borrador, int usuarioId)
    {
        if (!_sesion.Puede(Permisos.Ventas.Crear))
            throw new InvalidOperationException("No tienes permiso para emitir facturas.");

        var factura = PrepararNueva(borrador, TipoDocumentoVenta.Factura, usuarioId);
        FijarDesdeCatalogo(factura);
        Preparar(factura);
        if (!Validar(factura, out var error))
            throw new InvalidOperationException(error);

        var guardada = EmitirYGuardar(factura);
        RegistrarSalida(guardada, usuarioId);
        return guardada;
    }

    /// <summary>
    /// Registra una factura hecha en papel de contingencia porque el emisor no respondía. Lleva
    /// el número y el control impresos en el formato de imprenta, y queda pendiente de
    /// transmitir (<see cref="TransmitirPendientes"/>).
    /// </summary>
    public Venta RegistrarEnContingencia(Venta borrador, string numeroDocumento, string numeroControl, int usuarioId)
    {
        if (!_sesion.Puede(Permisos.Ventas.Contingencia))
            throw new InvalidOperationException("No tienes permiso para registrar documentos de contingencia.");

        if (string.IsNullOrWhiteSpace(numeroDocumento) || string.IsNullOrWhiteSpace(numeroControl))
            throw new InvalidOperationException("Indique el número de documento y el número de control del formato de contingencia.");

        var control = numeroControl.Trim();
        if (_ventas.GetAll().Any(v => string.Equals(v.NumeroControl, control, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"El número de control {control} ya está usado en otro documento.");

        var factura = PrepararNueva(borrador, TipoDocumentoVenta.Factura, usuarioId);
        FijarDesdeCatalogo(factura);
        Preparar(factura);
        if (!Validar(factura, out var error))
            throw new InvalidOperationException(error);

        factura.NumeroFiscal = numeroDocumento.Trim();
        factura.NumeroControl = control;
        factura.SerialEmisor = "CONTINGENCIA";
        factura.EnContingencia = true;
        factura.Estado = EstadoVenta.PendienteTransmitir;
        // El número interno también hace falta aquí: el índice único (OrganizacionId, Numero)
        // rechazaría la segunda contingencia si quedara vacío.
        factura.Numero = SiguienteNumeroVenta();

        var guardada = _ventas.Add(factura);
        RegistrarSalida(guardada, usuarioId);
        return guardada;
    }

    public IReadOnlyList<Venta> PendientesDeTransmitir() =>
        [.. _ventas.GetAll().Where(v => v.Estado == EstadoVenta.PendienteTransmitir).OrderBy(v => v.Fecha)];

    /// <summary>
    /// Envía al emisor los documentos de contingencia. Se detiene en el primero que falle (el
    /// emisor sigue caído) y devuelve cuántos se transmitieron.
    /// </summary>
    public int TransmitirPendientes()
    {
        if (!_sesion.Puede(Permisos.Ventas.Contingencia))
            throw new InvalidOperationException("No tienes permiso para transmitir documentos de contingencia.");

        var transmitidos = 0;
        foreach (var documento in PendientesDeTransmitir())
        {
            _emisor.TransmitirContingencia(documento, Emisora);

            _ventas.MarcarTransmitido(documento.Id, DateTime.Now);
            transmitidos++;
        }

        return transmitidos;
    }

    // ---------------------------------------------------------------- Notas

    public bool PuedeEmitirNota(Venta? factura) =>
        factura is { Tipo: TipoDocumentoVenta.Factura, EsHistorico: false }
        && factura.Estado != EstadoVenta.Anulada;

    /// <summary>
    /// Lo que todavía se puede devolver de cada renglón de la factura: lo facturado menos lo
    /// que ya devolvieron las notas de crédito previas. Clave: artículo + presentación.
    /// </summary>
    public Dictionary<(int? ArticuloId, string Presentacion), decimal> DisponibleParaDevolver(Venta factura)
    {
        var devuelto = _ventas.GetAll()
            .Where(v => v.Tipo == TipoDocumentoVenta.NotaCredito && v.DocumentoAfectadoId == factura.Id && v.Vigente)
            .SelectMany(v => v.Lineas)
            .GroupBy(l => (l.ArticuloId, l.PresentacionNombre))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));

        return factura.Lineas
            .GroupBy(l => (l.ArticuloId, l.PresentacionNombre))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad) - devuelto.GetValueOrDefault(g.Key));
    }

    /// <summary>
    /// Nota de crédito sobre una factura: devuelve (total o parcialmente) renglones de la
    /// factura, a sus mismos precios y alícuotas, y reingresa la mercancía al almacén.
    /// </summary>
    public Venta EmitirNotaCredito(Venta factura,
                                   IReadOnlyList<(VentaLinea LineaFactura, decimal Cantidad)> devoluciones,
                                   string motivo,
                                   int usuarioId)
    {
        if (!_sesion.Puede(Permisos.Ventas.NotaCredito))
            throw new InvalidOperationException("No tienes permiso para emitir notas de crédito.");

        ValidarNota(factura, motivo);

        var lineas = devoluciones.Where(d => d.Cantidad > 0).ToList();
        if (lineas.Count == 0)
            throw new InvalidOperationException("Indique qué cantidad se devuelve de al menos un renglón.");

        var disponible = DisponibleParaDevolver(factura);
        foreach (var grupo in lineas.GroupBy(d => (d.LineaFactura.ArticuloId, d.LineaFactura.PresentacionNombre)))
        {
            var pedido = grupo.Sum(d => d.Cantidad);
            var queda = disponible.GetValueOrDefault(grupo.Key);
            if (pedido > queda)
                throw new InvalidOperationException(
                    $"De {grupo.First().LineaFactura.Descripcion} solo quedan {queda:#,0.###} {grupo.Key.PresentacionNombre} por devolver.");
        }

        var nota = NotaSobre(factura, TipoDocumentoVenta.NotaCredito, motivo, usuarioId);
        nota.Lineas = [.. lineas.Select(d =>
        {
            var l = d.LineaFactura.Clonar();
            l.Cantidad = d.Cantidad;
            // Mismo precio en Bs y misma alícuota que la factura: la nota revierte lo facturado.
            l.Monto = CalculoFiscal.Redondear(l.Cantidad * l.PrecioUnitario);
            l.Iva = CalculoFiscal.Redondear(l.Monto * l.PorcentajeIva / 100m);
            return l;
        })];

        CalculoFiscal.Totalizar(nota, Emisora, percibeIgtf: false);

        var guardada = EmitirYGuardar(nota);
        RegistrarDevolucion(guardada, usuarioId);
        return guardada;
    }

    /// <summary>
    /// Nota de débito sobre una factura: cargos adicionales (fletes, diferencias de precio…)
    /// como conceptos libres. No mueve el almacén.
    /// </summary>
    public Venta EmitirNotaDebito(Venta factura,
                                  IReadOnlyList<(string Descripcion, decimal MontoBs, TipoAlicuota Alicuota)> cargos,
                                  string motivo,
                                  int usuarioId)
    {
        if (!_sesion.Puede(Permisos.Ventas.NotaDebito))
            throw new InvalidOperationException("No tienes permiso para emitir notas de débito.");

        ValidarNota(factura, motivo);

        var validos = cargos.Where(c => !string.IsNullOrWhiteSpace(c.Descripcion) || c.MontoBs != 0).ToList();
        if (validos.Count == 0)
            throw new InvalidOperationException("Agregue al menos un cargo.");

        if (validos.Any(c => string.IsNullOrWhiteSpace(c.Descripcion) || c.MontoBs <= 0))
            throw new InvalidOperationException("Cada cargo necesita una descripción y un monto mayor que cero.");

        var generica = validos.Select(c => c.Descripcion).FirstOrDefault(InventarioService.DescripcionGenerica);
        if (generica is not null)
            throw new InvalidOperationException($"\"{generica.Trim()}\" es una descripción genérica: diga en qué consiste el cargo.");

        var org = Emisora;
        var nota = NotaSobre(factura, TipoDocumentoVenta.NotaDebito, motivo, usuarioId);
        nota.Lineas = [.. validos.Select(c =>
        {
            var l = new VentaLinea
            {
                Descripcion = c.Descripcion.Trim(),
                Cantidad = 1,
                Factor = 1,
                PresentacionNombre = string.Empty,
                PrecioUnitario = CalculoFiscal.Redondear(c.MontoBs),
                PrecioUnitarioUsd = factura.TasaCambioUsada > 0 ? Math.Round(c.MontoBs / factura.TasaCambioUsada, 2) : 0m,
                Alicuota = c.Alicuota
            };
            CalculoFiscal.CalcularLinea(l, org, factura.TasaCambioUsada, precioEnUsd: false);
            return l;
        })];

        CalculoFiscal.Totalizar(nota, org, percibeIgtf: false);
        return EmitirYGuardar(nota);
    }

    private void ValidarNota(Venta factura, string motivo)
    {
        if (!PuedeEmitirNota(factura))
            throw new InvalidOperationException("Solo se emiten notas sobre una factura fiscal vigente.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la nota.");

        if (!PuedeEmitir(out var error))
            throw new InvalidOperationException(error);
    }

    private Venta NotaSobre(Venta factura, TipoDocumentoVenta tipo, string motivo, int usuarioId)
    {
        var nota = PrepararNueva(new Venta(), tipo, usuarioId);
        nota.DocumentoAfectadoId = factura.Id;
        nota.DocumentoAfectadoNumero = factura.NumeroFiscal;
        nota.DocumentoAfectadoFecha = factura.Fecha;
        nota.Concepto = motivo.Trim();
        nota.ClienteId = factura.ClienteId;
        nota.ClienteRif = factura.ClienteRif;
        nota.ClienteNombre = factura.ClienteNombre;
        nota.ClienteDireccion = factura.ClienteDireccion;
        nota.TasaCambioUsada = factura.TasaCambioUsada;
        return nota;
    }

    // ---------------------------------------------------------------- Emisión y almacén

    /// <summary>
    /// El precio, la alícuota y la descripción de cada renglón salen del catálogo, no de lo que
    /// mande la pantalla: quien factura no puede cambiar el precio de un artículo ni de un
    /// servicio. Se lee el artículo de la base en el momento de emitir, así que un precio
    /// cambiado en el catálogo mientras el editor estaba abierto también se respeta.
    /// </summary>
    private void FijarDesdeCatalogo(Venta factura)
    {
        foreach (var linea in factura.Lineas.Where(l => l.ArticuloId is not null))
        {
            var articulo = _articulos.GetById(linea.ArticuloId!.Value)
                ?? throw new InvalidOperationException($"{linea.Descripcion} ya no existe en el catálogo.");

            if (!articulo.Activo)
                throw new InvalidOperationException($"{articulo.Etiqueta} está inactivo y no se puede facturar.");

            var opcion = articulo.OpcionesDePresentacion()
                .FirstOrDefault(o => string.Equals(o.Nombre, linea.PresentacionNombre, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"La presentación \"{linea.PresentacionNombre}\" ya no existe para {articulo.Etiqueta}; vuelva a elegirla.");

            linea.Codigo = articulo.Codigo;
            linea.Descripcion = articulo.Descripcion;
            linea.EsServicio = articulo.EsServicio;
            linea.PresentacionNombre = opcion.Nombre;
            linea.Factor = opcion.Factor;
            linea.PrecioUnitarioUsd = opcion.PrecioVenta;
            linea.Alicuota = articulo.Alicuota;
        }
    }

    private Venta PrepararNueva(Venta borrador, TipoDocumentoVenta tipo, int usuarioId)
    {
        var documento = borrador.Clonar();
        documento.Id = 0;
        documento.Tipo = tipo;
        documento.Fecha = DateTime.Now;
        documento.CajaCodigo = _caja().Trim();
        documento.Estado = EstadoVenta.Registrada;
        documento.EsHistorico = false;
        documento.CreadoPorId = usuarioId;
        documento.FechaCreacion = DateTime.Now;
        documento.ClienteRif = Rif.Normalizar(documento.ClienteRif) ?? documento.ClienteRif.Trim();
        documento.ClienteNombre = documento.ClienteNombre.Trim();
        documento.ClienteDireccion = documento.ClienteDireccion.Trim();
        return documento;
    }

    private Venta EmitirYGuardar(Venta documento)
    {
        ResultadoFiscal resultado;
        try
        {
            resultado = _emisor.Emitir(documento, Emisora);
        }
        catch (EmisorFiscalException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

        documento.NumeroFiscal = resultado.NumeroDocumento;
        documento.NumeroControl = resultado.NumeroControl;
        documento.SerialEmisor = resultado.SerialEmisor;
        documento.Fecha = resultado.FechaHora;

        // El número interno (VTA-) es "el último + 1" y no es fiscal: si otra caja de la LAN
        // tomó el mismo número entre la lectura y el guardado, el índice único lo rechaza y se
        // reintenta con el siguiente. Cualquier otro fallo va al diario.
        const int intentos = 3;
        for (var intento = 1; ; intento++)
        {
            documento.Numero = SiguienteNumeroVenta();
            try
            {
                return _ventas.Add(documento);
            }
            catch (Exception) when (intento < intentos && NumeroYaTomado(documento.Numero))
            {
                documento.Id = 0;
            }
            catch (Exception ex)
            {
                DiarioFiscal.Anotar("Documento", documento, ex);
                throw new InvalidOperationException(
                    $"El documento {documento.NumeroFiscal} (control {documento.NumeroControl}) YA SE EMITIÓ, " +
                    $"pero no se pudo guardar en la base: {ex.Message}\n\nQuedó anotado en el diario fiscal " +
                    $"({DiarioFiscal.RutaTexto}) para registrarlo a mano.", ex);
            }
        }
    }

    private bool NumeroYaTomado(string numero)
    {
        try
        {
            return _ventas.GetAll().Any(v => v.Numero == numero);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>La factura descuenta el almacén con UNA salida de N renglones, en unidad base.</summary>
    private void RegistrarSalida(Venta factura, int usuarioId)
    {
        // Los servicios no salen del almacén.
        var lineas = factura.Lineas.Where(l => l.ArticuloId is not null && !l.EsServicio).ToList();
        if (lineas.Count == 0)
            return;

        var articulos = _articulos.GetAll().ToDictionary(a => a.Id);

        _salidas.Add(new SalidaInventario
        {
            Numero = SiguienteNumero(_salidas.GetAll().Select(s => s.Numero), PrefijoSalida),
            Fecha = factura.Fecha.Date,
            Destino = AreaDestino.Venta,
            Motivo = MotivoSalida.Venta,
            RetiradoPor = factura.EsConsumidorFinal ? "Consumidor final" : factura.ClienteNombre,
            AutorizadoPorId = usuarioId,
            AutorizadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.Now,
            Estado = EstadoSalida.Registrada,
            VentaId = factura.Id,
            Observaciones = $"Factura {factura.NumeroFiscal}",
            Lineas = [.. lineas.Select(l => new SalidaInventarioLinea
            {
                ArticuloId = l.ArticuloId!.Value,
                ArticuloCodigo = l.Codigo,
                ArticuloNombre = l.Descripcion,
                UnidadTexto = articulos.TryGetValue(l.ArticuloId.Value, out var a) ? a.UnidadCorta : string.Empty,
                Cantidad = Math.Round(l.CantidadBase, 4)
            })]
        });
    }

    /// <summary>La nota de crédito reingresa la mercancía con una entrada de devolución.</summary>
    private void RegistrarDevolucion(Venta nota, int usuarioId)
    {
        // Devolver un servicio es solo una reversa fiscal: no hay nada que reingresar.
        var lineas = nota.Lineas.Where(l => l.ArticuloId is not null && !l.EsServicio).ToList();
        if (lineas.Count == 0)
            return;

        var articulos = _articulos.GetAll().ToDictionary(a => a.Id);

        _entradas.Add(new EntradaInventario
        {
            Numero = SiguienteNumero(_entradas.GetAll().Select(e => e.Numero), PrefijoEntrada),
            Tipo = TipoEntrada.DevolucionVenta,
            Fecha = nota.Fecha.Date,
            ProveedorNombre = nota.EsConsumidorFinal ? "Consumidor final" : nota.ClienteNombre,
            NumeroDocumento = nota.NumeroFiscal,
            Observaciones = $"Nota de crédito {nota.NumeroFiscal} sobre factura {nota.DocumentoAfectadoNumero}: {nota.Concepto}",
            Estado = EstadoEntrada.Registrada,
            VentaId = nota.Id,
            CreadoPorId = usuarioId,
            CreadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty,
            FechaCreacion = DateTime.Now,
            Lineas = [.. lineas.Select(l => new EntradaInventarioLinea
            {
                ArticuloId = l.ArticuloId!.Value,
                ArticuloCodigo = l.Codigo,
                ArticuloNombre = l.Descripcion,
                UnidadTexto = articulos.TryGetValue(l.ArticuloId.Value, out var a) ? a.UnidadCorta : string.Empty,
                PresentacionNombre = l.PresentacionNombre,
                Factor = l.Factor,
                CantidadPresentacion = l.Cantidad,
                Cantidad = Math.Round(l.CantidadBase, 4)
            })]
        });
    }

    private string SiguienteNumeroVenta() => SiguienteNumero(_ventas.GetAll().Select(v => v.Numero), Prefijo);

    private static string SiguienteNumero(IEnumerable<string> numeros, string prefijo)
    {
        var ultimo = numeros
            .Where(n => n.StartsWith(prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefijo}{ultimo + 1:D6}";
    }
}
