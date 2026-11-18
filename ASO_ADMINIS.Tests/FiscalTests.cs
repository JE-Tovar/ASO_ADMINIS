using System.IO;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.Services.Fiscal;

namespace ASO_ADMINIS.Tests;

// ======================================================================== Talonarios

public class RangosContingenciaTests
{
    private static RangoContingencia Rango(string prefijo = "00-", int desde = 1, int hasta = 500, int digitos = 6, int id = 1) =>
        new() { Id = id, Prefijo = prefijo, Desde = desde, Hasta = hasta, Digitos = digitos, Imprenta = "Imprenta", Activo = true };

    [Theory]
    [InlineData("00-000123")]
    [InlineData("00-123")]
    [InlineData("123")]
    [InlineData(" 00-000123 ")]
    public void Un_numero_del_talonario_se_escribe_como_esta_impreso(string escrito)
    {
        var r = RangosContingencia.Resolver(escrito, [Rango()], []);

        Assert.Equal("00-000123", r.NumeroControl);
        Assert.Equal(123, r.Correlativo);
    }

    [Theory]
    [InlineData("00-000501")]   // pasado el último
    [InlineData("00-000000")]   // antes del primero
    [InlineData("01-000123")]   // otro prefijo
    [InlineData("00-0001234")]  // más cifras de las impresas
    [InlineData("00-12A")]      // no es un número
    public void Fuera_del_talonario_se_bloquea(string escrito)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RangosContingencia.Resolver(escrito, [Rango()], []));
        Assert.Contains("talonario", ex.Message);
    }

    [Fact]
    public void Sin_talonarios_cargados_no_se_acepta_ninguno()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RangosContingencia.Resolver("00-000001", [], []));
        Assert.Contains("No hay talonarios", ex.Message);
    }

    [Fact]
    public void Un_talonario_inactivo_no_cuenta()
    {
        var inactivo = Rango();
        inactivo.Activo = false;

        Assert.Throws<InvalidOperationException>(() => RangosContingencia.Resolver("00-000001", [inactivo], []));
    }

    [Fact]
    public void Un_numero_ya_usado_se_rechaza_aunque_se_escriba_distinto()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RangosContingencia.Resolver("00-7", [Rango()], ["00-000007"]));
        Assert.Contains("ya está usado", ex.Message);
    }

    [Fact]
    public void Se_propone_el_siguiente_libre_y_se_avisa_cuando_quedan_pocos()
    {
        var chico = Rango(hasta: 20);
        var usados = Enumerable.Range(1, 19).Select(n => chico.Numero(n)).Where(n => n != "00-000005").ToList();   // 18 usados

        var s = RangosContingencia.Siguiente([chico], usados)!;

        Assert.Equal("00-000005", s.NumeroControl);   // el hueco primero
        Assert.Equal(2, s.Quedan);          // 10 % de 20
        Assert.True(s.PocosQuedan);
    }

    [Fact]
    public void Un_talonario_agotado_no_propone_nada()
    {
        var chico = Rango(hasta: 2);

        Assert.Null(RangosContingencia.Siguiente([chico], ["00-000001", "00-000002"]));
    }

    [Fact]
    public void Dos_talonarios_con_el_mismo_prefijo_no_pueden_solaparse()
    {
        var nuevo = Rango(desde: 400, hasta: 900, id: 0);

        Assert.Contains("solapa", RangosContingencia.Validar(nuevo, [Rango()], []));
        Assert.Null(RangosContingencia.Validar(Rango(desde: 501, hasta: 900, id: 0), [Rango()], []));
        Assert.Null(RangosContingencia.Validar(Rango(prefijo: "01-", id: 0), [Rango()], []));
    }

    [Fact]
    public void No_se_puede_achicar_un_talonario_dejando_afuera_un_numero_usado()
    {
        var editado = Rango(hasta: 100);

        Assert.Contains("ya se usó", RangosContingencia.Validar(editado, [Rango()], ["00-000250"]));
        Assert.Null(RangosContingencia.Validar(editado, [Rango()], ["00-000050"]));
    }

    [Fact]
    public void El_ultimo_tiene_que_caber_en_las_cifras()
    {
        Assert.Contains("no cabe", RangosContingencia.Validar(Rango(hasta: 1_000_000, id: 0), [], []));
    }

    // ---------------------------------------------------------------- En VentasService

    [Fact]
    public void La_contingencia_fuera_del_talonario_no_se_registra()
    {
        var ventas = new VentasEnMemoria();
        var servicio = Escenario.Servicio(ventas);   // talonario 00-000001 al 00-000500

        Assert.Throws<InvalidOperationException>(() =>
            servicio.RegistrarEnContingencia(Escenario.Borrador(), "000999", "00-000999", usuarioId: 1));
        Assert.Empty(ventas.GetAll());
    }

    [Fact]
    public void La_contingencia_guarda_el_numero_como_esta_impreso_y_propone_el_siguiente()
    {
        var ventas = new VentasEnMemoria();
        var servicio = Escenario.Servicio(ventas);

        var registrada = servicio.RegistrarEnContingencia(Escenario.Borrador(), "000101", "1", usuarioId: 1);

        Assert.Equal("00-000001", registrada.NumeroControl);
        Assert.Equal("00-000002", servicio.SugerenciaContingencia()!.NumeroControl);
    }
}

// ======================================================================== Emisión incierta

public class EmisionInciertaTests
{
    [Fact]
    public void Si_no_se_sabe_si_se_emitio_no_se_guarda_ni_se_ofrece_contingencia()
    {
        var ventas = new VentasEnMemoria();
        var carpeta = Path.Combine(Path.GetTempPath(), "aso-pruebas", Guid.NewGuid().ToString("N"));
        try
        {
            var m = Escenario.Montar(ventas, emisor: new EmisorFiscalSimulado(comoMaquina: true, fallar: false, carpeta, incierto: true));

            var ex = Assert.ThrowsAny<InvalidOperationException>(() => m.Ventas.EmitirFactura(Escenario.Borrador(), usuarioId: 1));

            // No es la falla "segura" (la que ofrece contingencia): es la que manda verificar.
            Assert.IsType<EmitidoSinGuardarException>(ex);
            Assert.IsType<EmisionInciertaException>(ex.InnerException);
            Assert.Contains("NO lo vuelva a emitir", ex.Message);
            Assert.Empty(ventas.GetAll());
        }
        finally
        {
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void El_documento_que_devuelve_el_emisor_queda_en_la_venta()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "aso-pruebas", Guid.NewGuid().ToString("N"));
        try
        {
            var m = Escenario.Montar(new VentasEnMemoria(), emisor: new EmisorFiscalSimulado(comoMaquina: true, fallar: false, carpeta));

            var factura = m.Ventas.EmitirFactura(Escenario.Borrador(), usuarioId: 1);

            Assert.True(File.Exists(factura.DocumentoRuta));
        }
        finally
        {
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, recursive: true);
        }
    }
}

// ======================================================================== Contrato de emisores

/// <summary>
/// Lo que todo conector fiscal tiene que cumplir antes de usarse con clientes. Un conector nuevo
/// (HKA, una imprenta digital) hereda esta clase y solo dice cómo crearse; si alguna prueba falla,
/// el conector no está listo. Contra un equipo o ambiente real, estas pruebas emiten documentos de
/// verdad: correrlas solo con el equipo o el ambiente DE PRUEBAS del proveedor.
/// </summary>
public abstract class ContratoEmisorFiscal : IDisposable
{
    protected abstract IEmisorFiscal Crear();

    /// <summary>El mismo emisor, pero sin poder emitir (sin conexión, apagado).</summary>
    protected abstract IEmisorFiscal CrearCaido();

    public virtual void Dispose() { }

    private static Venta Factura(TipoDocumentoVenta tipo = TipoDocumentoVenta.Factura) => new()
    {
        Tipo = tipo,
        Fecha = DateTime.Now,
        CajaCodigo = "CAJA-01",
        TasaCambioUsada = 40m,
        ClienteRif = "V-11111111",
        ClienteNombre = "Cliente de contrato",
        ClienteDireccion = "Caracas",
        MontoExento = 400m,
        Total = 400m,
        Lineas = [new VentaLinea { Descripcion = "Servicio de prueba", Cantidad = 1, Factor = 1, PrecioUnitario = 400m, Monto = 400m, Alicuota = TipoAlicuota.Exento }],
        Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = 400m }]
    };

    [Fact]
    public void Responde_a_la_prueba_de_conexion_sin_emitir()
    {
        Assert.True(Crear().ProbarConexion().Disponible);
        Assert.False(CrearCaido().ProbarConexion().Disponible);
    }

    [Fact]
    public void Emitir_devuelve_numero_control_serial_y_fecha()
    {
        var r = Crear().Emitir(Factura(), Escenario.Organizacion());

        Assert.False(string.IsNullOrWhiteSpace(r.NumeroDocumento));
        Assert.False(string.IsNullOrWhiteSpace(r.NumeroControl));
        Assert.False(string.IsNullOrWhiteSpace(r.SerialEmisor));
        Assert.True(r.FechaHora > DateTime.Now.AddDays(-1));
    }

    [Fact]
    public void Dos_emisiones_no_repiten_numero_ni_control()
    {
        var emisor = Crear();
        var a = emisor.Emitir(Factura(), Escenario.Organizacion());
        var b = emisor.Emitir(Factura(), Escenario.Organizacion());

        Assert.NotEqual(a.NumeroDocumento, b.NumeroDocumento);
        Assert.NotEqual(a.NumeroControl, b.NumeroControl);
    }

    [Fact]
    public void Caido_lanza_la_falla_segura_y_no_otra_cosa()
    {
        Assert.Throws<EmisorFiscalException>(() => CrearCaido().Emitir(Factura(), Escenario.Organizacion()));
    }

    [Fact]
    public void Los_reportes_X_y_Z_solo_si_es_maquina()
    {
        var emisor = Crear();
        emisor.Emitir(Factura(), Escenario.Organizacion());

        if (emisor.TieneReportesXZ)
        {
            var x = emisor.ReporteX("CAJA-01");
            var z = emisor.ReporteZ("CAJA-01");
            Assert.True(z.Documentos >= 1);
            Assert.True(x.Documentos >= 1);
        }
        else
        {
            Assert.ThrowsAny<Exception>(() => emisor.ReporteZ("CAJA-01"));
        }
    }
}

/// <summary>El simulador, en sus dos modos, tiene que cumplir el mismo contrato que un conector real.</summary>
public sealed class ContratoSimuladorMaquina : ContratoEmisorFiscal
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "aso-pruebas", Guid.NewGuid().ToString("N"));
    protected override IEmisorFiscal Crear() => new EmisorFiscalSimulado(comoMaquina: true, fallar: false, _carpeta);
    protected override IEmisorFiscal CrearCaido() => new EmisorFiscalSimulado(comoMaquina: true, fallar: true, _carpeta);
    public override void Dispose() { if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true); }
}

public sealed class ContratoSimuladorImprentaDigital : ContratoEmisorFiscal
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "aso-pruebas", Guid.NewGuid().ToString("N"));
    protected override IEmisorFiscal Crear() => new EmisorFiscalSimulado(comoMaquina: false, fallar: false, _carpeta);
    protected override IEmisorFiscal CrearCaido() => new EmisorFiscalSimulado(comoMaquina: false, fallar: true, _carpeta);
    public override void Dispose() { if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true); }
}
