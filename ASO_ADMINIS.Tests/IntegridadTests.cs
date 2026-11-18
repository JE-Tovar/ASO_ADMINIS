using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Tests;

/// <summary>Bitácora (la parte pura) y manejo de conflictos en las pantallas, sin SQL Server.</summary>
public class IntegridadTests
{
    // ---------------------------------------------------------------- Auditoria.Diferencias

    [Fact]
    public void En_una_modificacion_solo_se_anota_lo_que_cambio()
    {
        var antes = new Dictionary<string, object?> { ["Nombre"] = "Aceite", ["PrecioVenta"] = 5m, ["Activo"] = true };
        var despues = new Dictionary<string, object?> { ["Nombre"] = "Aceite", ["PrecioVenta"] = 5.5m, ["Activo"] = false };

        var cambios = Auditoria.Diferencias(antes, despues);

        Assert.Equal(2, cambios.Count);
        Assert.Contains(new CambioDeCampo("PrecioVenta", "5", "5.5"), cambios);
        Assert.Contains(new CambioDeCampo("Activo", "Sí", "No"), cambios);
    }

    [Fact]
    public void La_contrasena_no_se_escribe_nunca()
    {
        var antes = new Dictionary<string, object?> { ["PasswordHash"] = "AAA", ["PasswordSalt"] = "s1" };
        var despues = new Dictionary<string, object?> { ["PasswordHash"] = "BBB", ["PasswordSalt"] = "s2" };

        var cambios = Auditoria.Diferencias(antes, despues);

        Assert.All(cambios, c =>
        {
            Assert.Equal(Auditoria.Oculto, c.Antes);
            Assert.Equal(Auditoria.Oculto, c.Despues);
        });
        Assert.DoesNotContain(cambios, c => c.Antes.Contains("AAA") || c.Despues.Contains("BBB"));
    }

    [Fact]
    public void Version_y_organizacion_no_se_anotan()
    {
        var antes = new Dictionary<string, object?> { ["Version"] = new byte[] { 1 }, ["OrganizacionId"] = 1 };
        var despues = new Dictionary<string, object?> { ["Version"] = new byte[] { 2 }, ["OrganizacionId"] = 2 };

        Assert.Empty(Auditoria.Diferencias(antes, despues));
    }

    [Fact]
    public void En_un_alta_se_anota_lo_que_trae_valor()
    {
        var despues = new Dictionary<string, object?> { ["Codigo"] = "CAJA-01", ["Nombre"] = "", ["Activa"] = true };

        var cambios = Auditoria.Diferencias(null, despues);

        Assert.Equal(["Activa", "Codigo"], cambios.Select(c => c.Campo).ToArray());
        Assert.All(cambios, c => Assert.Equal(string.Empty, c.Antes));
    }

    [Fact]
    public void En_una_baja_se_anota_como_estaba()
    {
        var antes = new Dictionary<string, object?> { ["Codigo"] = "PRV-1" };

        var cambio = Assert.Single(Auditoria.Diferencias(antes, null));

        Assert.Equal(new CambioDeCampo("Codigo", "PRV-1", string.Empty), cambio);
    }

    [Fact]
    public void El_detalle_se_lee_tal_como_se_guardo()
    {
        IReadOnlyList<CambioDeCampo> cambios = [new("PrecioVenta", "5", "5.5"), new("Nombre", "A \"B\"", "C")];

        Assert.Equal(cambios, Auditoria.Leer(Auditoria.Serializar(cambios)));
    }

    [Fact]
    public void El_resumen_dice_que_campos_cambiaron()
    {
        var resumen = Auditoria.Resumen(AccionAuditoria.Modificacion, "CuentaBancaria", "7",
                                        [new("Nombre", "a", "b"), new("Activa", "Sí", "No")]);

        Assert.Equal("Modificación de Cuenta bancaria #7: Nombre, Activa", resumen);
    }

    // ---------------------------------------------------------------- Conflictos en pantalla

    [Fact]
    public void Un_conflicto_de_edicion_se_avisa_y_no_tumba_la_pantalla()
    {
        var fuente = new FuenteQueChoca();
        fuente.Add(new GastoVario { Concepto = "Luz", Version = [1] });
        var dialogo = new DialogoQueResponde();
        var pantalla = new PantallaGastos(fuente, dialogo, new SesionTotal());
        pantalla.SelectedItem = pantalla.Items[0];

        pantalla.EditarCommand.Execute(null);   // no debe lanzar

        Assert.Contains("Otro usuario", Assert.Single(dialogo.Avisos));
    }

    [Fact]
    public void La_edicion_lleva_la_version_del_registro_abierto()
    {
        var fuente = new FuenteQueAnota();
        fuente.Add(new GastoVario { Concepto = "Luz", Version = [9, 9] });
        var pantalla = new PantallaGastos(fuente, new DialogoQueResponde(), new SesionTotal());
        pantalla.SelectedItem = pantalla.Items[0];

        pantalla.EditarCommand.Execute(null);

        // El editor arma su resultado con `new` (sin versión): la pone la pantalla.
        Assert.Equal(new byte[] { 9, 9 }, fuente.UltimaVersion);
    }

    private sealed class FuenteQueChoca : FuenteEnMemoria<GastoVario>
    {
        public override void Update(GastoVario item) =>
            throw new ConflictoDeEdicionException(new InvalidOperationException("versión distinta"));
    }

    private sealed class FuenteQueAnota : FuenteEnMemoria<GastoVario>
    {
        public byte[]? UltimaVersion { get; private set; }

        public override void Update(GastoVario item)
        {
            UltimaVersion = item.Version;
            base.Update(item);
        }
    }

    private sealed class PantallaGastos(ICrudDataSource<GastoVario, int> fuente, IServicioDialogo dialogo, ISesionActual sesion)
        : CrudViewModelBase<GastoVario, int>(fuente, dialogo, sesion)
    {
        protected override string ModuloPermiso => "Prueba";
        protected override bool CoincideBusqueda(GastoVario item, string texto) => true;
        protected override GastoVario CrearNuevo() => new();

        // Como los editores reales que construyen su resultado con `new`: sin la versión.
        protected override CrudEditorViewModelBase<GastoVario> CrearEditor(GastoVario item) =>
            new EditorSinVersion(new GastoVario { Id = item.Id, Concepto = item.Concepto + " (editado)" });
    }

    private sealed class EditorSinVersion(GastoVario resultado) : CrudEditorViewModelBase<GastoVario>
    {
        public override string Titulo => "Editar";
        public override GastoVario ObtenerResultado() => resultado;

        protected override bool Validar(out string? error)
        {
            error = null;
            return true;
        }
    }

    private sealed class DialogoQueResponde : IServicioDialogo
    {
        public List<string> Avisos { get; } = [];
        public bool MostrarEditor(CrudEditorViewModelBase editor) => true;
        public bool Confirmar(string titulo, string mensaje) => true;
        public void Informar(string titulo, string mensaje) => Avisos.Add(mensaje);
    }
}
