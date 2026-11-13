using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Tests;

public class BuscadorArticulosTests
{
    private static readonly IReadOnlyList<Articulo> Catalogo =
    [
        new() { Id = 1, Codigo = "ACE-15W40", Descripcion = "Aceite de motor 15W40 mineral", Categoria = "Lubricantes" },
        new() { Id = 2, Codigo = "ACE-20W50", Descripcion = "Aceite de motor 20W50 mineral", Categoria = "Lubricantes" },
        new() { Id = 3, Codigo = "SRV-ACE", Descripcion = "Cambio de aceite y filtro", Categoria = "Servicios", Tipo = TipoArticulo.Servicio },
        new() { Id = 4, Codigo = "PIN-BLA", Descripcion = "Pintura caucho blanco satinado 1 gal", Categoria = "Pinturas" },
        new() { Id = 5, Codigo = "ACE", Descripcion = "Acetona industrial 1 L", Categoria = "Químicos" },
        new() { Id = 6, Codigo = "CLA-2P", Descripcion = "Clavos de acero 2 pulgadas", Categoria = "Ferretería",
                Unidad = UnidadMedida.Kilogramo, CodigoBalanza = "123" }
    ];

    private static BuscadorArticulosViewModel Buscador(List<Articulo>? elegidos = null,
                                                       List<decimal?>? cantidades = null,
                                                       FormatoEtiqueta? etiquetas = null) =>
        new(Catalogo, (a, c) => { elegidos?.Add(a); cantidades?.Add(c); }, etiquetas);

    private static int[] Ids(BuscadorArticulosViewModel b) => [.. b.Resultados.Select(a => a.Id)];

    [Fact]
    public void Busca_por_parte_del_codigo()
    {
        var b = Buscador();
        b.Texto = "20w";
        Assert.Equal([2], Ids(b));
    }

    [Fact]
    public void Varias_palabras_tienen_que_aparecer_todas_en_cualquier_campo()
    {
        var b = Buscador();
        b.Texto = "motor mineral 15w40";
        Assert.Equal([1], Ids(b));
    }

    [Fact]
    public void Busca_por_categoria_y_sin_acentos_ni_mayusculas()
    {
        var b = Buscador();
        b.Texto = "QUIMICOS";
        Assert.Equal([5], Ids(b));
    }

    [Fact]
    public void El_codigo_exacto_va_primero()
    {
        var b = Buscador();
        b.Texto = "ace";
        Assert.Equal(5, b.Resultados[0].Id);   // "ACE" exacto
        Assert.Contains(1, Ids(b));            // los ACE-… y "aceite" también salen
        Assert.Contains(3, Ids(b));
    }

    [Fact]
    public void El_filtro_de_categoria_lista_toda_la_categoria_sin_texto()
    {
        var b = Buscador();
        b.CategoriaSeleccionada = "Lubricantes";
        Assert.Equal([1, 2], Ids(b));

        b.Texto = "20w50";
        Assert.Equal([2], Ids(b));
    }

    [Fact]
    public void Sin_texto_ni_categoria_no_lista_nada()
    {
        var b = Buscador();
        Assert.Empty(b.Resultados);
        Assert.False(b.MostrarResultados);
    }

    [Fact]
    public void Enter_con_codigo_exacto_elige_ese_y_limpia_la_caja()
    {
        var elegidos = new List<Articulo>();
        var b = Buscador(elegidos);
        b.Texto = "pin-bla";

        b.Confirmar();

        Assert.Equal(4, Assert.Single(elegidos).Id);
        Assert.Equal(string.Empty, b.Texto);
        Assert.False(b.MostrarResultados);
    }

    [Fact]
    public void Enter_sin_codigo_exacto_elige_el_resaltado()
    {
        var elegidos = new List<Articulo>();
        var b = Buscador(elegidos);
        b.Texto = "pintura";

        b.Confirmar();

        Assert.Equal(4, Assert.Single(elegidos).Id);
    }

    [Fact]
    public void Elegir_dos_veces_el_mismo_articulo_suma_en_vez_de_duplicar_el_renglon()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());
        var filtro = new Articulo { Id = 98, Codigo = "FIL-01", Descripcion = "Filtro de aceite tipo 1",
                                    Unidad = UnidadMedida.Pieza, PrecioVenta = 4m, Activo = true };
        var editor = new VentaEditorViewModel(new Venta(), [filtro], [], montaje.Ventas, 40m);

        editor.AgregarArticulo(filtro);
        editor.AgregarArticulo(filtro);

        var linea = Assert.Single(editor.Lineas);
        Assert.Equal("2", linea.Cantidad);
    }

    // ---------------------------------------------------------------- Granel

    [Fact]
    public void Cantidad_asterisco_codigo_agrega_esa_cantidad()
    {
        var elegidos = new List<Articulo>();
        var cantidades = new List<decimal?>();
        var b = Buscador(elegidos, cantidades);
        b.Texto = "1,25*cla-2p";

        b.Confirmar();

        Assert.Equal(6, Assert.Single(elegidos).Id);
        Assert.Equal(1.25m, Assert.Single(cantidades));
    }

    [Fact]
    public void Cantidad_asterisco_con_codigo_ambiguo_no_agrega_nada()
    {
        var elegidos = new List<Articulo>();
        var b = Buscador(elegidos);
        b.Texto = "2*aceite";   // dos aceites y el servicio: no se adivina

        b.Confirmar();

        Assert.Empty(elegidos);
        Assert.True(b.HayAviso);
    }

    [Fact]
    public void La_etiqueta_de_la_balanza_agrega_el_articulo_con_su_peso()
    {
        var elegidos = new List<Articulo>();
        var cantidades = new List<decimal?>();
        var b = Buscador(elegidos, cantidades, new FormatoEtiqueta());
        var doce = "20" + "00123" + "01250";
        b.Texto = doce + EtiquetaBalanza.CalcularDigito(doce);

        b.Confirmar();

        Assert.Equal(6, Assert.Single(elegidos).Id);
        Assert.Equal(1.250m, Assert.Single(cantidades));
    }

    [Fact]
    public void Una_etiqueta_de_un_producto_sin_asignar_avisa_y_no_agrega()
    {
        var elegidos = new List<Articulo>();
        var b = Buscador(elegidos, etiquetas: new FormatoEtiqueta());
        var doce = "20" + "00999" + "00500";
        b.Texto = doce + EtiquetaBalanza.CalcularDigito(doce);

        b.Confirmar();

        Assert.Empty(elegidos);
        Assert.Contains("00999", b.Aviso);
    }

    [Fact]
    public void Un_articulo_a_granel_sin_cantidad_abre_renglon_vacio_y_no_suma()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());
        var clavos = new Articulo { Id = 99, Codigo = "CLA-2P", Descripcion = "Clavos de acero 2 pulgadas",
                                    Unidad = UnidadMedida.Kilogramo, PrecioVenta = 3m, Activo = true };
        var editor = new VentaEditorViewModel(new Venta(), [clavos], [], montaje.Ventas, 40m);

        editor.AgregarArticulo(clavos);
        editor.AgregarArticulo(clavos);

        Assert.Equal(2, editor.Lineas.Count);
        Assert.All(editor.Lineas, l => { Assert.Equal(string.Empty, l.Cantidad); Assert.True(l.PideCantidad); });
    }
}
