using System;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Alta/edición de un cliente. La identificación se valida con el dígito verificador del RIF
/// (o se acepta como cédula) y se guarda normalizada, para que "j123456789" y "J-12345678-9"
/// no den dos fichas del mismo cliente.
/// </summary>
public sealed class ClienteEditorViewModel : CrudEditorViewModelBase<Cliente>
{
    private readonly Cliente _original;
    private readonly IClienteDataSource _clientes;

    public ClienteEditorViewModel(Cliente original, IClienteDataSource clientes)
    {
        _original = original;
        _clientes = clientes;

        Rif = original.Rif;
        Nombre = original.Nombre;
        DireccionFiscal = original.DireccionFiscal;
        Telefono = original.Telefono;
        Correo = original.Correo;
        Activo = original.Activo;
    }

    public override string Titulo => _original.Id == 0 ? "Nuevo cliente" : $"Editar cliente {_original.Rif}";

    private string _rif = string.Empty;
    public string Rif
    {
        get => _rif;
        set
        {
            if (SetProperty(ref _rif, value))
                OnPropertyChanged(nameof(RifAyuda));
        }
    }

    /// <summary>Retroalimentación en vivo: cómo quedará normalizado, o qué está mal.</summary>
    public string RifAyuda => string.IsNullOrWhiteSpace(Rif)
        ? "RIF (J-12345678-9) o cédula (V-12345678)."
        : Services.Rif.Error(Rif) ?? $"✓ {Services.Rif.Normalizar(Rif)}"
            + (Services.Rif.EsCedula(Rif) ? " · cédula" : " · RIF válido");

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _direccionFiscal = string.Empty;
    public string DireccionFiscal
    {
        get => _direccionFiscal;
        set => SetProperty(ref _direccionFiscal, value);
    }

    private string _telefono = string.Empty;
    public string Telefono
    {
        get => _telefono;
        set => SetProperty(ref _telefono, value);
    }

    private string _correo = string.Empty;
    public string Correo
    {
        get => _correo;
        set => SetProperty(ref _correo, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error)
    {
        return ClienteValidacion.Validar(ObtenerResultado(), _clientes, out error);
    }

    public override Cliente ObtenerResultado()
    {
        var cliente = _original.Clonar();
        cliente.Rif = Services.Rif.Normalizar(Rif) ?? Rif.Trim().ToUpperInvariant();
        cliente.Nombre = Nombre.Trim();
        cliente.DireccionFiscal = DireccionFiscal.Trim();
        cliente.Telefono = Telefono.Trim();
        cliente.Correo = Correo.Trim();
        cliente.Activo = Activo;
        return cliente;
    }
}

/// <summary>
/// Reglas del cliente, compartidas por el editor del padrón y el alta rápida desde una venta.
/// </summary>
public static class ClienteValidacion
{
    public static bool Validar(Cliente cliente, IClienteDataSource clientes, out string? error)
    {
        if (Rif.Error(cliente.Rif) is { } errorRif)
        {
            error = errorRif;
            return false;
        }

        if (string.IsNullOrWhiteSpace(cliente.Nombre))
        {
            error = "Indique el nombre y apellido, o la razón social.";
            return false;
        }

        // Un contribuyente (RIF completo) necesita la dirección fiscal en la factura para
        // poder usarla como crédito fiscal.
        if (Rif.EsRifValido(cliente.Rif) && string.IsNullOrWhiteSpace(cliente.DireccionFiscal))
        {
            error = "Un cliente con RIF necesita su dirección fiscal: figura en la factura.";
            return false;
        }

        var repetido = clientes.GetAll()
            .Any(c => c.Id != cliente.Id
                      && string.Equals(c.Rif, cliente.Rif, StringComparison.OrdinalIgnoreCase));
        if (repetido)
        {
            error = $"Ya existe un cliente con la identificación {cliente.Rif}.";
            return false;
        }

        error = null;
        return true;
    }
}
