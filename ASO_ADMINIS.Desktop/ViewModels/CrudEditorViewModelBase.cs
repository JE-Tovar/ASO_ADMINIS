using System;
using System.Windows.Input;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Base no genérica del editor modal de alta/edición, para que <see cref="Services.IServicioDialogo"/>
/// y <c>CrudEditorWindow</c> puedan trabajar con cualquier editor sin conocer la entidad concreta.
/// </summary>
public abstract class CrudEditorViewModelBase : ViewModelBase
{
    public abstract string Titulo { get; }

    private string? _errorValidacion;
    public string? ErrorValidacion
    {
        get => _errorValidacion;
        protected set => SetProperty(ref _errorValidacion, value);
    }

    /// <summary>Se dispara cuando el editor quiere cerrarse: <c>true</c> si guardó, <c>false</c> si canceló.</summary>
    public event EventHandler<bool>? SolicitarCierre;

    public ICommand GuardarCommand { get; }
    public ICommand CancelarCommand { get; }

    /// <summary>
    /// Los tres anchos de ventana que existen. Antes habia ocho (420, 460, 480, 500, 520, 560,
    /// 640 y 760), elegidos uno a uno segun lo que ocupara cada formulario: dos editores con el
    /// mismo numero de campos salian de distinto tamano, y abrir uno detras de otro hacia saltar
    /// la ventana.
    /// </summary>
    public static class Ancho
    {
        /// <summary>Una o dos preguntas: un motivo, una confirmacion.</summary>
        public const double Compacto = 440;

        /// <summary>Un formulario normal, de una columna.</summary>
        public const double Estandar = 560;

        /// <summary>Formularios de dos columnas o con una lista dentro. "Comparar proveedores" y
        /// "Recepción de mercancía" llegaron a nueve columnas en su grilla de detalle — con 760 el
        /// contenido mínimo (~700 px) casi no dejaba margen y recortaba las últimas columnas
        /// (Precio, Subtotal) sin scroll utilizable, porque la ventana es <c>ResizeMode="NoResize"</c>.</summary>
        public const double Amplio = 960;
    }

    /// <summary>Ancho de la ventana modal. Uno de los tres de <see cref="Ancho"/>.</summary>
    public virtual double AnchoEditor => Ancho.Compacto;

    /// <summary>
    /// Lo que dice el boton de confirmar.
    ///
    /// Por defecto "Guardar", que es lo que hacen los editores de alta y edicion. Pero la misma
    /// ventana la usan acciones que no guardan nada: "Anular remesa Nº 12", "Registrar salida",
    /// "Generar factura al ingenio", "Registrar recepcion". Un boton que dice Guardar delante de
    /// una anulacion describe mal lo que va a pasar al pulsarlo.
    /// </summary>
    public virtual string TextoAccion => "Guardar";

    /// <summary>
    /// Si la ventana pinta el botón Cancelar.
    ///
    /// Lo pinta siempre salvo en las fichas de solo lectura, donde no hay nada que cancelar y
    /// "Cancelar" junto a "Cerrar" son dos botones para lo mismo. Por defecto true: los editores
    /// de alta y edición no se enteran de que esto existe.
    /// </summary>
    public virtual bool MuestraCancelar => true;

    protected CrudEditorViewModelBase()
    {
        GuardarCommand = new RelayCommand(Guardar);
        CancelarCommand = new RelayCommand(() => SolicitarCierre?.Invoke(this, false));
    }

    private void Guardar()
    {
        if (!Validar(out var error))
        {
            ErrorValidacion = error;
            return;
        }

        ErrorValidacion = null;
        SolicitarCierre?.Invoke(this, true);
    }

    /// <summary>Valida los campos editables. Si devuelve <c>false</c>, <paramref name="error"/> se muestra al usuario.</summary>
    protected abstract bool Validar(out string? error);
}

/// <summary>
/// Editor de alta/edición para una entidad concreta. Trabaja siempre sobre una copia
/// editable; la entidad original de la lista no se modifica hasta que la fuente de
/// datos confirma el guardado.
/// </summary>
public abstract class CrudEditorViewModelBase<T> : CrudEditorViewModelBase
{
    /// <summary>Reconstruye la entidad con los valores editados (conservando el <c>Id</c> original).</summary>
    public abstract T ObtenerResultado();
}
