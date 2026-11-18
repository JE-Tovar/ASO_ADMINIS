using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>Lee un monto escrito a mano: vacío es cero; negativo o ilegible, error.</summary>
internal static class Montos
{
    public static bool Leer(string texto, out decimal valor)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            valor = 0m;
            return true;
        }

        return decimal.TryParse(texto.Trim(), out valor) && valor >= 0;
    }
}

/// <summary>Abrir turno: el fondo con que arranca la gaveta, en bolívares y en divisas.</summary>
public sealed class AbrirTurnoEditorViewModel(Caja caja, string cajero) : CrudEditorViewModelBase
{
    public override string Titulo => $"Abrir turno en {caja.Codigo}";
    public override string TextoAccion => "Abrir turno";

    public string Descripcion { get; } =
        $"{caja.Etiqueta} · {cajero}. Cuenta el efectivo con que arranca la gaveta: al cerrar se compara " +
        "contra lo que debería haber.";

    private string _fondoBs = string.Empty;
    public string FondoBs
    {
        get => _fondoBs;
        set => SetProperty(ref _fondoBs, value);
    }

    private string _fondoDivisa = string.Empty;
    public string FondoDivisa
    {
        get => _fondoDivisa;
        set => SetProperty(ref _fondoDivisa, value);
    }

    public decimal FondoBsValor => Montos.Leer(FondoBs, out var v) ? v : 0m;
    public decimal FondoDivisaValor => Montos.Leer(FondoDivisa, out var v) ? v : 0m;

    protected override bool Validar(out string? error)
    {
        if (!Montos.Leer(FondoBs, out _) || !Montos.Leer(FondoDivisa, out _))
        {
            error = "El fondo debe ser un número mayor o igual a cero (vacío = 0).";
            return false;
        }

        error = null;
        return true;
    }
}

/// <summary>
/// Cerrar turno: el arqueo. Muestra lo esperado (fondo + efectivo cobrado − vuelto) y pide lo
/// contado; la diferencia se ve mientras se escribe.
/// </summary>
public sealed class CerrarTurnoEditorViewModel : CrudEditorViewModelBase
{
    private readonly Turno _turno;

    public CerrarTurnoEditorViewModel(Turno turno, decimal esperadoBs, decimal esperadoDivisa, bool esAjeno)
    {
        _turno = turno;
        EsperadoBs = esperadoBs;
        EsperadoDivisa = esperadoDivisa;
        Descripcion = $"{turno.CajaCodigo} · {turno.UsuarioNombre} · abierto {turno.AperturaTexto} con fondo {turno.FondoTexto}.";
        AvisoAjeno = esAjeno
            ? $"Estás cerrando el turno de {turno.UsuarioNombre}: quedará firmado a tu nombre."
            : string.Empty;
    }

    public override string Titulo => $"Cerrar turno en {_turno.CajaCodigo}";
    public override string TextoAccion => "Cerrar turno";

    public string Descripcion { get; }
    public string AvisoAjeno { get; }
    public bool HayAvisoAjeno => AvisoAjeno.Length > 0;

    public decimal EsperadoBs { get; }
    public decimal EsperadoDivisa { get; }
    public string EsperadoBsTexto => EsperadoBs.ToString("N2");
    public string EsperadoDivisaTexto => EsperadoDivisa.ToString("N2");

    private string _contadoBs = string.Empty;
    public string ContadoBs
    {
        get => _contadoBs;
        set { if (SetProperty(ref _contadoBs, value)) OnPropertyChanged(nameof(DiferenciaTexto)); }
    }

    private string _contadoDivisa = string.Empty;
    public string ContadoDivisa
    {
        get => _contadoDivisa;
        set { if (SetProperty(ref _contadoDivisa, value)) OnPropertyChanged(nameof(DiferenciaTexto)); }
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    public decimal ContadoBsValor => Montos.Leer(ContadoBs, out var v) ? v : 0m;
    public decimal ContadoDivisaValor => Montos.Leer(ContadoDivisa, out var v) ? v : 0m;

    public string DiferenciaTexto
    {
        get
        {
            var bs = ContadoBsValor - EsperadoBs;
            var divisa = ContadoDivisaValor - EsperadoDivisa;
            if (bs == 0 && divisa == 0)
                return "Cuadra.";

            static string Parte(decimal d, string moneda) =>
                d > 0 ? $"sobran {moneda} {d:N2}" : $"faltan {moneda} {-d:N2}";

            var partes = new List<string>();
            if (bs != 0) partes.Add(Parte(bs, "Bs."));
            if (divisa != 0) partes.Add(Parte(divisa, "US$"));
            return "No cuadra: " + string.Join(" y ", partes) + ".";
        }
    }

    protected override bool Validar(out string? error)
    {
        if (string.IsNullOrWhiteSpace(ContadoBs) && string.IsNullOrWhiteSpace(ContadoDivisa))
        {
            error = "Indica lo contado en la gaveta (escribe 0 si no hay nada).";
            return false;
        }

        if (!Montos.Leer(ContadoBs, out _) || !Montos.Leer(ContadoDivisa, out _))
        {
            error = "Lo contado debe ser un número mayor o igual a cero.";
            return false;
        }

        var cuadra = ContadoBsValor == EsperadoBs && ContadoDivisaValor == EsperadoDivisa;
        if (!cuadra && string.IsNullOrWhiteSpace(Observaciones))
        {
            error = "El arqueo no cuadra: explica la diferencia en las observaciones.";
            return false;
        }

        error = null;
        return true;
    }
}
