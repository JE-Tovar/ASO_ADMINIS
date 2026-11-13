using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Tests;

public class ClientesServiceTests
{
    private static Venta FacturaA(string rif, string nombre, string telefono = "", string correo = "", string direccion = "Caracas")
    {
        var f = Escenario.Borrador();
        f.ClienteRif = rif;
        f.ClienteNombre = nombre;
        f.ClienteTelefono = telefono;
        f.ClienteCorreo = correo;
        f.ClienteDireccion = direccion;
        return f;
    }

    [Fact]
    public void Facturar_a_una_cedula_nueva_crea_el_cliente_con_telefono_y_correo()
    {
        var m = Escenario.Montar(new VentasEnMemoria());

        var factura = m.Ventas.EmitirFactura(FacturaA("v12345678", "María Pérez", "0414-1234567", "maria@correo.com"), usuarioId: 1);

        var cliente = Assert.Single(m.ClientesFuente.GetAll());
        Assert.Equal("V-12345678", cliente.Rif);
        Assert.Equal("0414-1234567", cliente.Telefono);
        Assert.Equal("maria@correo.com", cliente.Correo);
        Assert.Equal(cliente.Id, factura.ClienteId);
        Assert.Equal("maria@correo.com", factura.ClienteCorreo);
    }

    [Fact]
    public void Un_cliente_existente_escrito_en_otro_formato_no_se_duplica_y_se_actualiza()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var existente = m.ClientesFuente.Add(new Cliente { Rif = "V-12345678", Nombre = "María Pérez", Telefono = "0414-0000000", Activo = true });

        var factura = m.Ventas.EmitirFactura(FacturaA("V 12.345.678", "María Pérez", "0424-9999999"), usuarioId: 1);

        var cliente = Assert.Single(m.ClientesFuente.GetAll());
        Assert.Equal("0424-9999999", cliente.Telefono);
        Assert.Equal(existente.Id, factura.ClienteId);
    }

    [Fact]
    public void Un_telefono_vacio_en_la_factura_no_borra_el_de_la_ficha()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        m.ClientesFuente.Add(new Cliente { Rif = "V-12345678", Nombre = "María Pérez", Telefono = "0414-0000000", Activo = true });

        m.Ventas.EmitirFactura(FacturaA("V-12345678", "María Pérez"), usuarioId: 1);

        Assert.Equal("0414-0000000", Assert.Single(m.ClientesFuente.GetAll()).Telefono);
    }

    [Fact]
    public void Sin_permiso_para_crear_clientes_se_factura_igual()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        m.Sesion.Negados.Add(Permisos.Clientes.Crear);

        var factura = m.Ventas.EmitirFactura(FacturaA("V-12345678", "María Pérez"), usuarioId: 1);

        Assert.Empty(m.ClientesFuente.GetAll());
        Assert.Null(factura.ClienteId);
        Assert.Equal("V-12345678", factura.ClienteRif);
    }

    [Theory]
    [InlineData("", "María Pérez", "Caracas", "RIF o la cédula")]
    [InlineData("V-12345678", "", "Caracas", "nombre")]
    [InlineData("V-12345678", "María Pérez", "", "dirección")]
    public void Sin_datos_del_cliente_no_se_emite(string rif, string nombre, string direccion, string falta)
    {
        var m = Escenario.Montar(new VentasEnMemoria());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            m.Ventas.EmitirFactura(FacturaA(rif, nombre, direccion: direccion), usuarioId: 1));

        Assert.Contains(falta, ex.Message);
        Assert.Empty(m.ClientesFuente.GetAll());
    }

    [Fact]
    public void Telefono_y_correo_son_opcionales()
    {
        var m = Escenario.Montar(new VentasEnMemoria());

        var factura = m.Ventas.EmitirFactura(FacturaA("V-12345678", "María Pérez"), usuarioId: 1);

        Assert.Equal("María Pérez", factura.ClienteNombre);
    }

    [Fact]
    public void Un_correo_mal_escrito_no_deja_emitir()
    {
        var m = Escenario.Montar(new VentasEnMemoria());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            m.Ventas.EmitirFactura(FacturaA("V-12345678", "María Pérez", correo: "maria-arroba-correo"), usuarioId: 1));
        Assert.Contains("correo", ex.Message);
    }

    [Fact]
    public void Sugiere_por_parte_del_nombre_sin_acentos_y_por_digitos_de_la_cedula()
    {
        var fuente = new ClientesEnMemoria();
        fuente.Add(new Cliente { Rif = "V-12345678", Nombre = "María Pérez", Activo = true });
        fuente.Add(new Cliente { Rif = "J-00002961-0", Nombre = "Ferretería El Clavo, C.A.", DireccionFiscal = "Caracas", Activo = true });
        fuente.Add(new Cliente { Rif = "V-87654321", Nombre = "Mario Pereira", Activo = false });
        var servicio = new ClientesService(fuente, new SesionTotal());

        Assert.Equal(["María Pérez"], servicio.Sugerencias("maria").Select(c => c.Nombre));
        Assert.Equal(["María Pérez"], servicio.Sugerencias("12345").Select(c => c.Nombre));
        Assert.Equal(["Ferretería El Clavo, C.A."], servicio.Sugerencias("ferreteria").Select(c => c.Nombre));
    }

    [Fact]
    public void El_editor_autocompleta_al_escribir_una_identificacion_exacta()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        m.ClientesFuente.Add(new Cliente { Rif = "V-12345678", Nombre = "María Pérez", Telefono = "0414-1234567",
                                          Correo = "maria@correo.com", Activo = true });
        var editor = new VentaEditorViewModel(new Venta(), [], m.Clientes, m.Ventas, 40m);

        editor.ClienteRif = "v12345678";

        Assert.Equal("V-12345678", editor.ClienteRif);
        Assert.Equal("María Pérez", editor.ClienteNombre);
        Assert.Equal("0414-1234567", editor.ClienteTelefono);
        Assert.True(editor.EsClienteRegistrado);
    }

    [Fact]
    public void Si_la_identificacion_deja_de_coincidir_se_borra_lo_autocompletado()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        m.ClientesFuente.Add(new Cliente { Rif = "V-1234567", Nombre = "Otra Persona", Activo = true });
        var editor = new VentaEditorViewModel(new Venta(), [], m.Clientes, m.Ventas, 40m);

        editor.ClienteRif = "V-1234567";    // a medio camino coincide con otra persona
        editor.ClienteRif = "V-12345678";   // se sigue escribiendo

        Assert.Equal(string.Empty, editor.ClienteNombre);
        Assert.True(editor.EsClienteNuevo);
    }
}
