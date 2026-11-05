using System.IO;
using System.Text;
using System.Text.Json;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services.Fiscal;

/// <summary>
/// Emisor de desarrollo y demostración. <b>Lo que emite NO tiene valor fiscal</b> y cada
/// documento lo dice en grande.
///
/// Se porta como una máquina fiscal (con reportes X/Z) o como una imprenta digital (sin ellos)
/// según <c>EmisorFiscal:Modo</c>, para poder probar los dos caminos de la aplicación antes de
/// tener el equipo o el proveedor real. Con <c>EmisorFiscal:Fallar = true</c> rechaza todo,
/// para probar el modo contingencia.
///
/// Lleva sus correlativos (factura, NC, ND, control, Z) y los acumulados del día en un JSON en
/// %AppData%\ASO Adminis\emisor-simulado.json, y escribe cada documento como texto en
/// %AppData%\ASO Adminis\documentos\.
/// </summary>
public sealed class EmisorFiscalSimulado : IEmisorFiscal
{
    private static readonly string Carpeta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ASO Adminis");

    private readonly string _rutaEstado;
    private readonly string _carpetaDocumentos;
    private readonly bool _comoMaquina;
    private readonly bool _fallar;

    public EmisorFiscalSimulado(bool comoMaquina, bool fallar, string? carpeta = null)
    {
        _comoMaquina = comoMaquina;
        _fallar = fallar;
        var raiz = carpeta ?? Carpeta;
        _rutaEstado = Path.Combine(raiz, "emisor-simulado.json");
        _carpetaDocumentos = Path.Combine(raiz, "documentos");
    }

    public string Nombre => _comoMaquina ? "Simulador de máquina fiscal" : "Simulador de imprenta digital";
    public bool TieneReportesXZ => _comoMaquina;
    public bool EsFiscal => false;

    private string Serial => _comoMaquina ? "SIM-MF-0001" : "SIM-ID-0001";

    public ResultadoFiscal Emitir(Venta documento, Organizacion emisor)
    {
        if (_fallar)
            throw new EmisorFiscalException(
                "El emisor fiscal no responde (simulación de falla). Puede registrar el documento en papel de contingencia.");

        var estado = LeerEstado();
        var numero = documento.Tipo switch
        {
            TipoDocumentoVenta.NotaCredito => ++estado.UltimaNotaCredito,
            TipoDocumentoVenta.NotaDebito => ++estado.UltimaNotaDebito,
            _ => ++estado.UltimaFactura
        };
        var control = ++estado.UltimoControl;

        Acumular(estado, documento);
        GuardarEstado(estado);

        var prefijo = documento.Tipo switch
        {
            TipoDocumentoVenta.NotaCredito => "NC",
            TipoDocumentoVenta.NotaDebito => "ND",
            _ => "F"
        };

        var resultado = new ResultadoFiscal(
            $"{prefijo}{numero:D8}",
            $"00-{control:D8}",
            Serial,
            DateTime.Now,
            null);

        var ruta = EscribirDocumento(documento, emisor, resultado);
        return resultado with { RutaDocumento = ruta };
    }

    public void TransmitirContingencia(Venta documento, Organizacion emisor)
    {
        if (_fallar)
            throw new EmisorFiscalException("El emisor fiscal sigue sin responder (simulación de falla).");

        var estado = LeerEstado();
        Acumular(estado, documento);
        GuardarEstado(estado);

        EscribirDocumento(documento, emisor,
            new ResultadoFiscal(documento.NumeroFiscal, documento.NumeroControl, "CONTINGENCIA", documento.Fecha, null));
    }

    public ResumenFiscal ReporteX(string caja) => Resumen(LeerEstado(), DateTime.Now);

    public ResumenFiscal ReporteZ(string caja)
    {
        if (!_comoMaquina)
            throw new EmisorFiscalException("Una imprenta digital no tiene reporte Z.");
        if (_fallar)
            throw new EmisorFiscalException("La máquina fiscal no responde (simulación de falla).");

        var estado = LeerEstado();
        estado.UltimoZ++;
        var resumen = Resumen(estado, DateTime.Now);
        estado.Dia = new Acumulado();
        GuardarEstado(estado);
        return resumen;
    }

    // ---- Estado persistido ----

    private sealed class Acumulado
    {
        public int Documentos { get; set; }
        public decimal Exento { get; set; }
        public decimal BaseGeneral { get; set; }
        public decimal IvaGeneral { get; set; }
        public decimal BaseReducida { get; set; }
        public decimal IvaReducida { get; set; }
        public decimal BaseAdicional { get; set; }
        public decimal IvaAdicional { get; set; }
        public decimal Igtf { get; set; }
        public decimal Total { get; set; }
    }

    private sealed class Estado
    {
        public int UltimaFactura { get; set; }
        public int UltimaNotaCredito { get; set; }
        public int UltimaNotaDebito { get; set; }
        public int UltimoControl { get; set; }
        public int UltimoZ { get; set; }
        public Acumulado Dia { get; set; } = new();
    }

    /// <summary>Como una máquina fiscal real: la nota de crédito resta de los acumulados del día.</summary>
    private static void Acumular(Estado estado, Venta d)
    {
        var s = d.Signo;
        var a = estado.Dia;
        a.Documentos++;
        a.Exento += s * d.MontoExento;
        a.BaseGeneral += s * d.BaseGeneral;
        a.IvaGeneral += s * d.IvaGeneral;
        a.BaseReducida += s * d.BaseReducida;
        a.IvaReducida += s * d.IvaReducida;
        a.BaseAdicional += s * d.BaseAdicional;
        a.IvaAdicional += s * d.IvaAdicional;
        a.Igtf += s * d.Igtf;
        a.Total += s * d.Total;
    }

    private static ResumenFiscal Resumen(Estado e, DateTime cuando) => new(
        e.UltimoZ, cuando, e.Dia.Documentos, e.Dia.Exento, e.Dia.BaseGeneral, e.Dia.IvaGeneral,
        e.Dia.BaseReducida, e.Dia.IvaReducida, e.Dia.BaseAdicional, e.Dia.IvaAdicional, e.Dia.Igtf, e.Dia.Total);

    private Estado LeerEstado()
    {
        try
        {
            return File.Exists(_rutaEstado)
                ? JsonSerializer.Deserialize<Estado>(File.ReadAllText(_rutaEstado)) ?? new Estado()
                : new Estado();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new EmisorFiscalException($"No se pudo leer el estado del simulador: {ex.Message}", ex);
        }
    }

    private void GuardarEstado(Estado estado)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_rutaEstado)!);
        File.WriteAllText(_rutaEstado, JsonSerializer.Serialize(estado, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ---- Documento en texto ----

    private string EscribirDocumento(Venta d, Organizacion emisor, ResultadoFiscal r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("****************************************");
        sb.AppendLine("*        SIN VALOR FISCAL (SIMULADOR)   *");
        sb.AppendLine("****************************************");
        sb.AppendLine(emisor.RazonSocial);
        sb.AppendLine($"RIF: {emisor.Rif}");
        sb.AppendLine(emisor.DireccionFiscal);
        if (!string.IsNullOrWhiteSpace(emisor.Telefono)) sb.AppendLine($"Tel.: {emisor.Telefono}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"{Venta.TextoDe(d.Tipo).ToUpperInvariant()} N° {r.NumeroDocumento}");
        sb.AppendLine($"N° de control: {r.NumeroControl}");
        sb.AppendLine($"Fecha: {d.Fecha:dd/MM/yyyy}  Hora: {d.Fecha:HH:mm}");
        sb.AppendLine($"Caja: {d.CajaCodigo}   Emisor: {r.SerialEmisor}");
        if (d.EnContingencia) sb.AppendLine("EMITIDO EN CONTINGENCIA");
        if (d.Tipo != TipoDocumentoVenta.Factura)
        {
            sb.AppendLine($"Factura afectada: {d.DocumentoAfectadoNumero} del {d.DocumentoAfectadoFecha:dd/MM/yyyy}");
            sb.AppendLine($"Motivo: {d.Concepto}");
        }
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Cliente: {(d.EsConsumidorFinal ? "Consumidor final" : d.ClienteNombre)}");
        if (!d.EsConsumidorFinal)
        {
            sb.AppendLine($"RIF/C.I.: {d.ClienteRif}");
            if (!string.IsNullOrWhiteSpace(d.ClienteDireccion)) sb.AppendLine($"Dirección: {d.ClienteDireccion}");
        }
        sb.AppendLine("----------------------------------------");
        foreach (var l in d.Lineas)
        {
            sb.AppendLine($"{l.Codigo} {l.Descripcion}");
            sb.AppendLine($"   {l.Cantidad:N2} {l.PresentacionNombre} x {l.PrecioUnitario:N2}  ({Alicuotas.Marca(l.Alicuota)}) {l.Monto,12:N2}");
        }
        sb.AppendLine("----------------------------------------");
        if (d.MontoExento != 0) sb.AppendLine($"Exento (E)                 {d.MontoExento,12:N2}");
        if (d.BaseGeneral != 0) sb.AppendLine($"BI G {d.PorcentajeGeneral:0.##}%  {d.BaseGeneral,12:N2}  IVA {d.IvaGeneral,10:N2}");
        if (d.BaseReducida != 0) sb.AppendLine($"BI R {d.PorcentajeReducida:0.##}%   {d.BaseReducida,12:N2}  IVA {d.IvaReducida,10:N2}");
        if (d.BaseAdicional != 0) sb.AppendLine($"BI A {d.PorcentajeAdicional:0.##}%  {d.BaseAdicional,12:N2}  IVA {d.IvaAdicional,10:N2}");
        if (d.Igtf != 0) sb.AppendLine($"IGTF {d.PorcentajeIgtf:0.##}% s/ {d.BaseIgtf:N2}       {d.Igtf,12:N2}");
        sb.AppendLine($"TOTAL Bs                   {d.Total,12:N2}");
        sb.AppendLine($"Tasa BCV {d.TasaCambioUsada:N4} Bs/US$ · Ref. US$ {d.TotalUsd:N2}");
        foreach (var p in d.Pagos)
            sb.AppendLine($"  {p.MetodoTexto,-20} {p.MontoBs,12:N2}" + (p.EsDivisa ? $"  (US$ {p.MontoDivisa:N2})" : ""));
        if (d.Vuelto > 0) sb.AppendLine($"  Vuelto               {d.Vuelto,12:N2}");
        sb.AppendLine("****************************************");
        sb.AppendLine("*        SIN VALOR FISCAL (SIMULADOR)   *");
        sb.AppendLine("****************************************");

        Directory.CreateDirectory(_carpetaDocumentos);
        var ruta = Path.Combine(_carpetaDocumentos, $"{r.NumeroDocumento}.txt");
        File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
        return ruta;
    }
}
