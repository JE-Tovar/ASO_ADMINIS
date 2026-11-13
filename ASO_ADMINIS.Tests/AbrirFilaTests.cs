using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Tests;

/// <summary>
/// Doble clic en una fila (<c>CrudViewModelBase.AbrirCommand</c>): abre el editor si la fila se
/// puede editar, y si no, su ficha de solo lectura.
/// </summary>
public class AbrirFilaTests
{
    private readonly GastosEnMemoria _fuente = new();
    private readonly DialogoQueAnota _dialogo = new();
    private readonly SesionTotal _sesion = new();

    private Pantalla Crear(bool editable, bool conDetalle)
    {
        _fuente.Add(new GastoVario { Numero = "GAS-000001", Concepto = "Luz" });
        var pantalla = new Pantalla(_fuente, _dialogo, _sesion, editable, conDetalle);
        pantalla.SelectedItem = pantalla.Items[0];
        return pantalla;
    }

    [Fact]
    public void Si_se_puede_editar_abre_el_editor()
    {
        var pantalla = Crear(editable: true, conDetalle: true);

        pantalla.AbrirCommand.Execute(null);

        Assert.IsType<EditorFalso>(Assert.Single(_dialogo.Mostrados));
    }

    [Fact]
    public void Si_no_se_puede_editar_abre_la_ficha()
    {
        var pantalla = Crear(editable: false, conDetalle: true);

        pantalla.AbrirCommand.Execute(null);

        var ficha = Assert.IsType<GastoVarioDetalleViewModel>(Assert.Single(_dialogo.Mostrados));
        Assert.Equal("GAS-000001", ficha.Documento.Numero);
        Assert.Equal("Cerrar", ficha.TextoAccion);
        Assert.False(ficha.MuestraCancelar);
    }

    [Fact]
    public void Sin_permiso_de_editar_igual_muestra_la_ficha()
    {
        _sesion.Negados.Add("Prueba.Editar");
        var pantalla = Crear(editable: true, conDetalle: true);

        pantalla.AbrirCommand.Execute(null);

        Assert.IsType<GastoVarioDetalleViewModel>(Assert.Single(_dialogo.Mostrados));
    }

    [Fact]
    public void Un_maestro_sin_permiso_de_editar_no_abre_nada()
    {
        _sesion.Negados.Add("Prueba.Editar");
        var pantalla = Crear(editable: true, conDetalle: false);

        pantalla.AbrirCommand.Execute(null);

        Assert.Empty(_dialogo.Mostrados);
    }

    [Fact]
    public void Sin_fila_seleccionada_no_se_puede_abrir()
    {
        var pantalla = Crear(editable: true, conDetalle: true);
        pantalla.SelectedItem = null;

        Assert.False(pantalla.AbrirCommand.CanExecute(null));
    }

    [Fact]
    public void Cerrar_la_ficha_no_guarda_nada()
    {
        var pantalla = Crear(editable: false, conDetalle: true);
        _dialogo.Respuesta = true; // como si hubiera pulsado "Cerrar"

        pantalla.AbrirCommand.Execute(null);

        Assert.Equal("Luz", Assert.Single(_fuente.GetAll()).Concepto);
    }

    private sealed class Pantalla(GastosEnMemoria fuente, IServicioDialogo dialogo, ISesionActual sesion,
                                  bool editable, bool conDetalle)
        : CrudViewModelBase<GastoVario, int>(fuente, dialogo, sesion)
    {
        protected override string ModuloPermiso => "Prueba";
        protected override bool CoincideBusqueda(GastoVario item, string texto) => true;
        protected override GastoVario CrearNuevo() => new();
        protected override CrudEditorViewModelBase<GastoVario> CrearEditor(GastoVario item) => new EditorFalso(item);
        protected override bool PuedeEditar(GastoVario item) => editable;
        protected override CrudEditorViewModelBase? CrearDetalle(GastoVario item) =>
            conDetalle ? new GastoVarioDetalleViewModel(item) : null;
    }

    private sealed class EditorFalso(GastoVario gasto) : CrudEditorViewModelBase<GastoVario>
    {
        public override string Titulo => "Editar";
        public override GastoVario ObtenerResultado() => gasto;

        protected override bool Validar(out string? error)
        {
            error = null;
            return true;
        }
    }

    private sealed class DialogoQueAnota : IServicioDialogo
    {
        public List<CrudEditorViewModelBase> Mostrados { get; } = [];
        public bool Respuesta { get; set; }

        public bool MostrarEditor(CrudEditorViewModelBase editor)
        {
            Mostrados.Add(editor);
            return Respuesta;
        }

        public bool Confirmar(string titulo, string mensaje) => true;
        public void Informar(string titulo, string mensaje) { }
    }
}
