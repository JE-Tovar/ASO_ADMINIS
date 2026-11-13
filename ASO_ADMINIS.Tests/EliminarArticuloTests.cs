using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Tests;

/// <summary>Cuándo se puede borrar un artículo del catálogo (Inventario · Almacén).</summary>
public class EliminarArticuloTests
{
    private readonly ArticulosEnMemoria _articulos = new();
    private readonly EntradasEnMemoria _entradas = new();
    private readonly SalidasEnMemoria _salidas = new();
    private readonly InventarioService _servicio;

    public EliminarArticuloTests() =>
        _servicio = new InventarioService(_articulos, _entradas, _salidas);

    private Articulo Articulo(bool activo) =>
        _articulos.Add(new Articulo { Codigo = "A1", Descripcion = "Pintura blanca", Activo = activo });

    private void Entrada(Articulo a, decimal cantidad) =>
        _entradas.Add(new EntradaInventario
        {
            Lineas = [new EntradaInventarioLinea { ArticuloId = a.Id, Cantidad = cantidad }]
        });

    private void Salida(Articulo a, decimal cantidad) =>
        _salidas.Add(new SalidaInventario
        {
            Lineas = [new SalidaInventarioLinea { ArticuloId = a.Id, Cantidad = cantidad }]
        });

    [Fact]
    public void Sin_movimientos_se_borra_aunque_este_activo()
    {
        Assert.True(_servicio.PuedeEliminar(Articulo(activo: true), out _));
    }

    [Fact]
    public void Con_movimientos_y_activo_pide_desactivarlo()
    {
        var a = Articulo(activo: true);
        Entrada(a, 5);
        Salida(a, 5);

        Assert.False(_servicio.PuedeEliminar(a, out var motivo));
        Assert.Contains("Activo", motivo);
    }

    [Fact]
    public void Inactivo_con_existencia_pide_darle_salida()
    {
        var a = Articulo(activo: false);
        Entrada(a, 5);
        Salida(a, 2);

        Assert.False(_servicio.PuedeEliminar(a, out var motivo));
        Assert.Contains("existencia", motivo);
    }

    [Fact]
    public void Inactivo_sin_existencia_se_borra_y_sus_movimientos_quedan()
    {
        var a = Articulo(activo: false);
        Entrada(a, 5);
        Salida(a, 5);

        Assert.True(_servicio.PuedeEliminar(a, out _));

        _articulos.Delete(a.Id);

        Assert.Single(_entradas.GetAll());
        Assert.Single(_salidas.GetAll());
    }

    [Theory]
    [InlineData(false, true, true)]   // sin facturar, activo: se borra
    [InlineData(true, true, false)]   // facturado y activo: pide desactivarlo
    [InlineData(true, false, true)]   // facturado e inactivo: se borra
    public void Servicio(bool facturado, bool activo, bool sePuede)
    {
        var servicio = new Articulo
        {
            Codigo = "S1", Descripcion = "Cambio de aceite", Tipo = TipoArticulo.Servicio, Activo = activo
        };

        Assert.Equal(sePuede, InventarioService.PuedeEliminarServicio(servicio, facturado, out var motivo));
        Assert.Equal(sePuede, motivo is null);
    }
}
