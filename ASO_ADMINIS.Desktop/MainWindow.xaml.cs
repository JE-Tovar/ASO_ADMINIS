using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.ViewModels;
using ASO_ADMINIS.Desktop.Views;

namespace ASO_ADMINIS.Desktop;

/// <summary>
/// Shell de la aplicación: menú de módulos/submódulos a la izquierda y, a la derecha,
/// el resumen del módulo o el submódulo abierto.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MainSidebar.NavegacionSolicitada += (_, e) => Navegar(e.Modulo, e.Submodulo);

        AplicarEscala();
        Ajustes.Cambiaron += AplicarEscala;
        Closed += OnCerrada;

        Navegar(SeccionDeArranque(), null);

        MostrarCabecera();
    }

    /// <summary>
    /// Al cerrar se anota dónde se quedó, y solo aquí: <see cref="Navegar"/> lo lleva en
    /// memoria porque escribir el archivo en cada clic del menú sería un archivo por
    /// navegación. Cerrar sesión también pasa por aquí — la ventana se cierra y se abre otra.
    ///
    /// La baja del evento va en el mismo sitio: <c>Ajustes.Cambiaron</c> es estático y vive más
    /// que la ventana, así que una ventana cerrada que siguiera suscrita se quedaría colgando
    /// de él intentando escalar un árbol que ya no existe.
    /// </summary>
    private void OnCerrada(object? sender, EventArgs e)
    {
        Ajustes.Cambiaron -= AplicarEscala;
        DesconectarPantallaActual();

        if (Ajustes.Actual.AbrirEnUltimaSeccion)
            Ajustes.Guardar();
    }

    /// <summary>
    /// Escala de la interfaz. Se reaplica cada vez que cambian los ajustes, no solo al abrir,
    /// para que el selector de Configuración se vea al instante.
    /// </summary>
    private void AplicarEscala()
        => EscalaInterfaz.ScaleX = EscalaInterfaz.ScaleY = EscalaVentana.Factor;

    /// <summary>
    /// Dónde abrir. Por defecto Inicio; con la preferencia activada, donde se quedó la última
    /// vez. Si esa sección ya no existe o el rol dejó de verla, se cae a Inicio en vez de
    /// dejar la ventana en una pantalla vacía — de eso se encarga <see cref="CrearVistaModulo"/>.
    /// </summary>
    private static Modulo SeccionDeArranque()
    {
        if (!Ajustes.Actual.AbrirEnUltimaSeccion)
            return ModuloCatalogo.Inicio;

        return ModuloCatalogo.BuscarModulo(Ajustes.Actual.UltimaSeccion) ?? ModuloCatalogo.Inicio;
    }

    /// <summary>
    /// Quién está dentro y en qué organización. Se muestra siempre porque saber dónde se está
    /// escribiendo importa antes de guardar nada.
    /// </summary>
    private void MostrarCabecera()
    {
        var sesion = SesionActual.Instancia;

        if (sesion.UsuarioActual is { } usuario)
            UsuarioActualLabel.Text = $"{usuario.NombreCompleto} · {usuario.RolTexto}";

        if (Ambito.Actual is { } organizacion)
        {
            OrganizacionActualLabel.Text = organizacion.Etiqueta;
            OrganizacionChip.Visibility = Visibility.Visible;
        }
    }

    private void OnCerrarSesion(object sender, RoutedEventArgs e)
    {
        SesionActual.Instancia.CerrarSesion();

        var login = new LoginView();
        if (login.ShowDialog() == true)
        {
            var nuevaVentana = new MainWindow();
            Application.Current.MainWindow = nuevaVentana;
            nuevaVentana.Show();
        }
        else
        {
            Application.Current.Shutdown();
            return;
        }

        Close();
    }

    /// <summary>
    /// Único punto de cambio de sección: sin submódulo muestra el resumen del módulo,
    /// con submódulo abre su pantalla. El menú lateral se sincroniza con lo que se muestra.
    /// </summary>
    private void Navegar(Modulo modulo, Submodulo? submodulo)
    {
        MainSidebar.Sincronizar(modulo, submodulo);
        Ruta.Segmentos = ArmarRuta(modulo, submodulo);

        // Solo en memoria: el archivo se escribe al guardar desde Configuración, no una vez
        // por clic del menú.
        Ajustes.Actual.UltimaSeccion = modulo.Clave;

        // La pantalla que se va deja de escuchar el bus de cambios. Este es el único sitio por
        // el que se cambia de sección, así que basta hacerlo aquí; si no, cada pantalla visitada
        // seguiría viva y recargándose contra la base a espaldas del usuario.
        DesconectarPantallaActual();

        ContentArea.Content = submodulo is not null
            ? CrearVistaSubmodulo(modulo, submodulo)
            : CrearVistaModulo(modulo);
    }

    private void DesconectarPantallaActual()
    {
        if (ContentArea.Content is IRecargable saliente)
            saliente.Desconectar();
    }

    /// <summary>
    /// Recarga manual con F5. No hay ningún botón "Actualizar" en las barras —la actualización
    /// es automática—, pero queda el atajo como red de seguridad.
    /// </summary>
    private void OnRecargar(object sender, ExecutedRoutedEventArgs e)
    {
        if (ContentArea.Content is IRecargable seccion)
            seccion.Recargar();
    }

    /// <summary>
    /// Que ViewModel abre cada submodulo. La vista la resuelve WPF por DataTemplate
    /// (ver <c>Styles/PantallaTemplates.xaml</c>), asi que aqui no se nombra ninguna.
    ///
    /// Antes esto era un switch de catorce casos que decian los tres mismos pasos con otros
    /// nombres. Una pantalla nueva se da de alta con una linea aqui y su plantilla alla.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<Modulo, Submodulo, IPantalla>> Pantallas =
        new Dictionary<string, Func<Modulo, Submodulo, IPantalla>>
        {
            ["Catalogo.Modelos"] = (m, s) => new ModelosViewModel(m, s),

            ["Ventas.Registro"] = (m, s) => new VentasViewModel(m, s),

            ["Reportes.Ventas"] = (m, s) => new ReporteVentasViewModel(m, s),
            ["Reportes.Gastos"] = (m, s) => new ReporteGastosViewModel(m, s),

            ["Finanzas.CuentasPorPagar"] = (m, s) => new CuentasPorPagarViewModel(m, s),
            ["Finanzas.Banco"] = (m, s) => new BancoViewModel(m, s),
            ["Finanzas.Proveedores"] = (m, s) => new ProveedoresViewModel(m, s),

            ["Inventario.Almacen"] = (m, s) => new AlmacenViewModel(m, s),
            ["Inventario.Entradas"] = (m, s) => new EntradasViewModel(m, s),
            ["Inventario.Salidas"] = (m, s) => new SalidasViewModel(m, s),
        };

    private object CrearVistaModulo(Modulo modulo)
    {
        // Los modulos fijados (Peticiones, Usuarios) no tienen submodulos, asi que la guarda
        // de CrearVistaSubmodulo no los alcanza: se comprueban aqui.
        if (!SesionActual.Instancia.Ve(modulo))
            return CrearInicio();

        if (modulo.Clave == ModuloCatalogo.Administracion.Clave)
            return Conectar(new AdministracionViewModel(modulo), ModuloCatalogo.Inicio);

        if (modulo.Clave == ModuloCatalogo.Peticiones.Clave)
            return Conectar(new PeticionesViewModel(modulo), ModuloCatalogo.Inicio);

        if (modulo.Clave == ModuloCatalogo.Configuracion.Clave)
            return Conectar(new ConfiguracionViewModel(modulo), ModuloCatalogo.Inicio);

        if (modulo.Clave == ModuloCatalogo.Inicio.Clave)
            return CrearInicio();

        var dashboard = new ModuloDashboardViewModel(modulo);
        dashboard.SubmoduloSolicitado += (_, s) => Navegar(modulo, s);
        return dashboard;
    }

    private InicioViewModel CrearInicio()
    {
        var inicio = new InicioViewModel();
        inicio.ModuloSolicitado += (_, m) => Navegar(m, null);
        return inicio;
    }

    /// <summary>
    /// Los submodulos ya implementados se enrutan a su pantalla real; el resto cae en el
    /// marcador de posicion hasta que tengan la suya.
    /// </summary>
    private object CrearVistaSubmodulo(Modulo modulo, Submodulo submodulo)
    {
        // Ultima barrera: el menu ya oculta lo que el rol no ve, pero a una pantalla tambien
        // se llega desde el lanzador de Inicio y desde las tarjetas del dashboard. Comprobarlo
        // aqui, en el unico punto por el que pasan las tres, cierra los tres caminos a la vez.
        if (!SesionActual.Instancia.Ve(submodulo))
            return Conectar(SubmoduloViewModel.SinPermiso(modulo, submodulo), modulo);

        var pantalla = Pantallas.TryGetValue(submodulo.Clave, out var crear)
            ? crear(modulo, submodulo)
            : new SubmoduloViewModel(modulo, submodulo);

        return Conectar(pantalla, modulo);
    }

    /// <summary>Deja la pantalla lista para devolver el control cuando el usuario pida volver.</summary>
    private IPantalla Conectar(IPantalla pantalla, Modulo destinoAlVolver)
    {
        pantalla.VolverSolicitado += (_, _) => Navegar(destinoAlVolver, null);
        return pantalla;
    }

    /// <summary>
    /// La ruta que se ve arriba: "Inicio > Finanzas > Cuentas por Pagar", con todo lo
    /// anterior al ultimo tramo clicable.
    ///
    /// Es lo que sustituye a los quince botones "Volver al modulo" de las pantallas, que ademas
    /// venian en tres estilos distintos y faltaban en Peticiones. Un boton de volver solo ofrece
    /// un salto; la ruta ofrece todos los del camino y ademas dice donde se esta, que era la otra
    /// mitad del problema: el titulo de la seccion se repetia tres veces (el menu lo marcaba, el
    /// resumen del modulo lo titulaba y la pantalla lo volvia a titular).
    /// </summary>
    private List<SegmentoRuta> ArmarRuta(Modulo modulo, Submodulo? submodulo)
    {
        var esInicio = modulo.Clave == ModuloCatalogo.Inicio.Clave;
        var ruta = new List<SegmentoRuta>
        {
            new(ModuloCatalogo.Inicio.Nombre,
                esInicio ? null : new RelayCommand(() => Navegar(ModuloCatalogo.Inicio, null)),
                esPrimero: true),
        };

        if (esInicio)
            return ruta;

        // El modulo solo es clicable si hay un submodulo despues; si no, es donde se esta.
        ruta.Add(new SegmentoRuta(
            modulo.Nombre,
            submodulo is null ? null : new RelayCommand(() => Navegar(modulo, null)),
            esPrimero: false));

        if (submodulo is not null)
            ruta.Add(new SegmentoRuta(submodulo.Nombre, null, esPrimero: false));

        return ruta;
    }
}
