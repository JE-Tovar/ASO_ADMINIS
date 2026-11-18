using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Tests;

/// <summary>
/// Regresión de Ventas · Registro (2026-11-18): el filtro de la lista corre dentro del constructor
/// base, antes que el de la pantalla; filtrar con un campo propio daba NullReferenceException al
/// abrir la pantalla con documentos. El filtro por caja tiene que usar <c>Sesion</c> de la base.
/// </summary>
public class FiltroDeSesionTests
{
    [Fact]
    public void Una_pantalla_que_filtra_por_caja_se_abre_con_filas_y_solo_muestra_las_de_su_caja()
    {
        var fuente = new VentasEnMemoria();
        fuente.Add(new Venta { Numero = "VTA-1", CajaId = 1 });
        fuente.Add(new Venta { Numero = "VTA-2", CajaId = 2 });

        var cajero = new SesionTotal { CajaActual = new Caja { Id = 1, Codigo = "CAJA-01", Activa = true } };
        cajero.Negados.Add(Permisos.Ventas.VerTodasLasCajas);

        var pantalla = new PantallaPorCaja(fuente, cajero);   // antes: NullReferenceException aquí

        Assert.Equal(["VTA-1"], pantalla.ItemsView.Cast<Venta>().Select(v => v.Numero).ToArray());
    }

    private sealed class PantallaPorCaja : CrudViewModelBase<Venta, int>
    {
        // Como VentasViewModel: un campo que se asigna en el constructor de la hija.
        private readonly ISesionActual _propia;

        public PantallaPorCaja(VentasEnMemoria fuente, ISesionActual sesion) : base(fuente, new DialogoMudo(), sesion)
        {
            _propia = sesion;
        }

        protected override string ModuloPermiso => "Ventas";
        protected override bool CoincideBusqueda(Venta item, string texto) => true;
        protected override Venta CrearNuevo() => new();
        protected override CrudEditorViewModelBase<Venta> CrearEditor(Venta item) => throw new NotSupportedException();

        protected override bool PasaFiltroExtra(Venta item) =>
            Sesion.Puede(Permisos.Ventas.VerTodasLasCajas) || item.CajaId == Sesion.CajaActual?.Id;
    }

    private sealed class DialogoMudo : IServicioDialogo
    {
        public bool MostrarEditor(CrudEditorViewModelBase editor) => false;
        public bool Confirmar(string titulo, string mensaje) => false;
        public void Informar(string titulo, string mensaje) { }
    }
}
