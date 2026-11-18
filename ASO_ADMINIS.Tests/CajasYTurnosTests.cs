using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Tests;

public class CajasYTurnosTests
{
    // ---------------------------------------------------------------- Turnos

    [Fact]
    public void No_se_abren_dos_turnos_en_la_misma_caja()
    {
        var m = Escenario.Montar(new VentasEnMemoria());   // ya abrió uno en CAJA-01

        var ex = Assert.Throws<InvalidOperationException>(() => m.Turnos.Abrir(10m, 0m));

        Assert.Contains("ya tiene un turno abierto", ex.Message);
        Assert.Single(m.TurnosFuente.GetAll());
    }

    [Fact]
    public void Sin_caja_en_la_sesion_no_se_abre_turno()
    {
        var m = Escenario.Montar(new VentasEnMemoria(), abrirTurno: false);
        m.Sesion.CajaActual = null;

        Assert.Throws<InvalidOperationException>(() => m.Turnos.Abrir(0m, 0m));
    }

    [Fact]
    public void El_esperado_suma_fondo_y_efectivo_del_turno_menos_el_vuelto()
    {
        var ventas = new VentasEnMemoria();
        var m = Escenario.Montar(ventas, abrirTurno: false);
        var turno = m.Turnos.Abrir(fondoBs: 100m, fondoDivisa: 5m);

        // Del turno: 400 en efectivo Bs pagando 450 (vuelto 50), y 10 US$ en divisa.
        var conVuelto = Escenario.Borrador();
        conVuelto.Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = 450m }];
        m.Ventas.EmitirFactura(conVuelto, usuarioId: 1);

        var enDivisa = Escenario.Borrador();
        enDivisa.Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoDivisa, MontoBs = 400m, MontoDivisa = 10m }];
        m.Ventas.EmitirFactura(enDivisa, usuarioId: 1);

        // No es efectivo: no entra al arqueo.
        var conPagoMovil = Escenario.Borrador();
        conPagoMovil.Pagos = [new VentaPago { Metodo = MetodoPago.PagoMovil, MontoBs = 400m }];
        m.Ventas.EmitirFactura(conPagoMovil, usuarioId: 1);

        // De otro turno: tampoco.
        ventas.Add(new Venta
        {
            Numero = "VTA-999999", Tipo = TipoDocumentoVenta.Factura, TurnoId = turno.Id + 1,
            Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = 1000m }]
        });

        var (bs, divisa) = m.Turnos.Esperado(turno);

        Assert.Equal(100m + 450m - 50m, bs);
        Assert.Equal(5m + 10m, divisa);
    }

    [Fact]
    public void Cerrar_guarda_esperado_y_contado_y_libera_la_caja()
    {
        var m = Escenario.Montar(new VentasEnMemoria(), abrirTurno: false);
        var turno = m.Turnos.Abrir(100m, 0m);

        var cerrado = m.Turnos.Cerrar(turno, contadoBs: 90m, contadoDivisa: 0m, observaciones: "Faltan 10");

        Assert.False(cerrado.EstaAbierto);
        Assert.Equal(100m, cerrado.EsperadoBs);
        Assert.Equal(-10m, cerrado.DiferenciaBs);
        Assert.Null(m.Turnos.TurnoAbierto());

        // Con la caja libre se puede abrir el siguiente.
        m.Turnos.Abrir(0m, 0m);
    }

    [Fact]
    public void Cerrar_el_turno_de_otro_cajero_exige_CerrarAjeno()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var deCajero1 = m.Turnos.TurnoAbierto()!;

        m.Sesion.UsuarioActual = new Usuario { Id = 2, NombreCompleto = "Otro cajero" };
        m.Sesion.Negados.Add(Permisos.Turnos.CerrarAjeno);

        var ex = Assert.Throws<InvalidOperationException>(() => m.Turnos.Cerrar(deCajero1, 0m, 0m, ""));
        Assert.Contains("administrador", ex.Message);

        m.Sesion.Negados.Remove(Permisos.Turnos.CerrarAjeno);
        var cerrado = m.Turnos.Cerrar(deCajero1, 0m, 0m, "");
        Assert.Equal(2, cerrado.CerradoPorId);
    }

    // ---------------------------------------------------------------- Ventas con caja y turno

    [Fact]
    public void Sin_turno_abierto_no_se_factura()
    {
        var m = Escenario.Montar(new VentasEnMemoria(), abrirTurno: false);

        Assert.False(m.Ventas.PuedeFacturar(out var error));
        Assert.Contains("turno", error);
        Assert.Throws<InvalidOperationException>(() => m.Ventas.EmitirFactura(Escenario.Borrador(), usuarioId: 1));
    }

    [Fact]
    public void Con_la_caja_tomada_por_otro_cajero_no_se_factura()
    {
        var m = Escenario.Montar(new VentasEnMemoria());   // turno del usuario 1
        m.Sesion.UsuarioActual = new Usuario { Id = 2, NombreCompleto = "Otro cajero" };

        Assert.False(m.Ventas.PuedeFacturar(out _));
    }

    [Fact]
    public void Sin_caja_no_se_factura()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        m.Sesion.CajaActual = null;

        Assert.False(m.Ventas.PuedeEmitir(out var error));
        Assert.Contains("caja", error);
    }

    [Fact]
    public void La_factura_queda_con_su_caja_y_su_turno()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var turno = m.Turnos.TurnoAbierto()!;

        var factura = m.Ventas.EmitirFactura(Escenario.Borrador(), usuarioId: 1);

        Assert.Equal(1, factura.CajaId);
        Assert.Equal("CAJA-01", factura.CajaCodigo);
        Assert.Equal(turno.Id, factura.TurnoId);
    }

    // ---------------------------------------------------------------- Rol Cajero

    [Fact]
    public void El_cajero_solo_ve_ventas()
    {
        var cajero = MatrizPermisos.Base(Rol.Cajero);

        Assert.DoesNotContain(cajero, p => p.StartsWith("Ver.Finanzas") || p.StartsWith("Ver.Inventario")
                                           || p.StartsWith("Ver.Reportes") || p == "Ver.Administracion");
        Assert.DoesNotContain(Permisos.Ventas.VerTodasLasCajas, cajero);
        Assert.Contains(Permisos.Ventas.Crear, cajero);
        Assert.Contains(Permisos.Turnos.Abrir, cajero);
        Assert.Contains(Permisos.Turnos.Cerrar, cajero);
    }

    [Fact]
    public void Supervisor_ve_todas_las_cajas_y_cierra_turnos_ajenos()
    {
        Assert.Contains(Permisos.Ventas.VerTodasLasCajas, MatrizPermisos.Base(Rol.Supervisor));
        Assert.Contains(Permisos.Turnos.CerrarAjeno, MatrizPermisos.Base(Rol.Supervisor));
        Assert.Empty(MatrizPermisos.Base(Rol.OperadorRetirado));
    }

    [Theory]
    [InlineData(Rol.AdministradorOrganizacion, true)]
    [InlineData(Rol.Desarrollador, true)]
    [InlineData(Rol.Supervisor, true)]
    [InlineData(Rol.Cajero, false)]
    [InlineData(Rol.OperadorRetirado, false)]
    public void Cerrar_turnos_ajenos_solo_supervision_aunque_se_conceda_a_mano(Rol rol, bool puede)
    {
        var usuario = new Usuario { Id = 7, Rol = rol };
        var concedidoAMano = new PermisoUsuario { UsuarioId = 7, Permiso = Permisos.Turnos.CerrarAjeno, Concedido = true };

        Assert.Equal(puede, SesionActual.Calcular(usuario, [concedidoAMano]).Contains(Permisos.Turnos.CerrarAjeno));
    }
}
