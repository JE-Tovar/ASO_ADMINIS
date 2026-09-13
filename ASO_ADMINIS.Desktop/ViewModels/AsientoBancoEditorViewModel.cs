using System;
using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// "¿A qué cuenta, con qué fecha y con qué referencia?" — lo único que un documento no puede
/// responder cuando se cobra o se paga.
///
/// Un solo editor reutilizable para cualquier transición que asiente dinero (hoy, el pago de
/// una factura de proveedor), con el mismo criterio que hace que
/// <see cref="MotivoEditorViewModel"/> sirva a todas las anulaciones: la pregunta es idéntica
/// sin importar qué documento la dispare, y repetirla en cada uno sería el mismo error en
/// varios sitios en vez de uno.
///
/// No es un paso de más: sin cuenta no hay libro, y preguntarlo aquí es lo que evita tener que
/// adivinar después de qué bolsillo salió el dinero.
/// </summary>
public sealed class AsientoBancoEditorViewModel : CrudEditorViewModelBase
{
    private readonly string _titulo;
    private readonly string _textoAccion;

    /// <param name="titulo">Encabezado, p. ej. "Registrar cobro de FC-0012".</param>
    /// <param name="descripcion">Resumen del documento, para que se vea qué se está firmando.</param>
    /// <param name="monto">Lo que va a entrar o salir. Ya está congelado: aquí no se edita.</param>
    /// <param name="esEntrada">Entrada o salida, solo para el rótulo del monto.</param>
    /// <param name="cuentas">Cuentas activas donde puede caer el movimiento.</param>
    /// <param name="textoAccion">Lo que dice el botón de confirmar.</param>
    public AsientoBancoEditorViewModel(string titulo,
                                       string descripcion,
                                       decimal monto,
                                       bool esEntrada,
                                       IReadOnlyList<CuentaBancaria> cuentas,
                                       string textoAccion = "Registrar")
    {
        _titulo = titulo;
        _textoAccion = textoAccion;

        Descripcion = descripcion;
        Monto = monto;
        EsEntrada = esEntrada;
        Cuentas = cuentas;

        // Con una sola cuenta no hay nada que elegir: preguntarlo sería teclear la única
        // respuesta posible.
        _cuentaSeleccionada = cuentas.Count == 1 ? cuentas[0] : null;
    }

    public override string Titulo => _titulo;

    public override string TextoAccion => _textoAccion;

    public string Descripcion { get; }

    public decimal Monto { get; }

    public bool EsEntrada { get; }

    /// <summary>El monto con su rótulo, para que no haga falta leer el título para saber el sentido.</summary>
    public string MontoTexto => $"{(EsEntrada ? "Entra" : "Sale")} {Monto:N2}";

    public IReadOnlyList<CuentaBancaria> Cuentas { get; }

    /// <summary>
    /// Sin cuentas dadas de alta no se puede asentar nada. La vista lo dice en vez de mostrar un
    /// desplegable vacío que no explica por qué no deja continuar.
    /// </summary>
    public bool HayCuentas => Cuentas.Count > 0;

    /// <summary>El negado, para el aviso de la vista: no hay converter de bool invertido.</summary>
    public bool NoHayCuentas => !HayCuentas;

    public string AvisoSinCuentas =>
        "No hay cuentas activas. Dé de alta una en Finanzas · Banco, pestaña Cuentas.";

    private CuentaBancaria? _cuentaSeleccionada;
    public CuentaBancaria? CuentaSeleccionada
    {
        get => _cuentaSeleccionada;
        set => SetProperty(ref _cuentaSeleccionada, value);
    }

    /// <summary>
    /// Fecha valor: el día en que el dinero se movió de verdad. Arranca en hoy, que es el caso
    /// normal, pero se puede atrasar — una transferencia se captura el lunes y salió el viernes.
    /// </summary>
    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private string _referencia = string.Empty;
    public string Referencia
    {
        get => _referencia;
        set => SetProperty(ref _referencia, value);
    }

    /// <summary>Lo que se le pasa al servicio de dominio, ya validado.</summary>
    public AsientoBanco Resultado =>
        new(CuentaSeleccionada?.Id ?? 0, Fecha, Referencia);

    protected override bool Validar(out string? error)
    {
        if (CuentaSeleccionada is null)
        {
            error = HayCuentas
                ? "Seleccione la cuenta donde se registra el movimiento."
                : AvisoSinCuentas;
            return false;
        }

        if (Fecha.Date < CuentaSeleccionada.FechaApertura.Date)
        {
            error = $"La fecha no puede ser anterior a la apertura de la cuenta " +
                    $"({CuentaSeleccionada.FechaApertura:dd/MM/yyyy}).";
            return false;
        }

        error = null;
        return true;
    }
}
