using System;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Alta/edición de un proveedor. El RIF identifica al proveedor ante el fisco: si se escribe,
/// tiene que ser un RIF completo cuyo dígito verificador cuadre (<see cref="Services.Rif"/>), se
/// guarda normalizado y no puede repetirse — dos fichas del mismo proveedor partirían su cuenta
/// en dos.
/// </summary>
public sealed class ProveedorEditorViewModel : CrudEditorViewModelBase<Proveedor>
{
    private readonly Proveedor _original;
    private readonly IProveedorDataSource _proveedores;

    public ProveedorEditorViewModel(Proveedor original, IProveedorDataSource proveedores)
    {
        _original = original;
        _proveedores = proveedores;

        Nombre = original.Nombre;
        Rif = original.Rif;
        Telefono = original.Telefono;
        Notas = original.Notas;
        Activo = original.Activo;
    }

    public override string Titulo => _original.Id == 0 ? "Nuevo proveedor" : $"Editar proveedor Nº {_original.Id}";

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

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

    /// <summary>
    /// Retroalimentación en vivo, como en la ficha de cliente. Un proveedor factura, así que se
    /// exige RIF completo con dígito verificador (no cédula); el campo sigue siendo opcional.
    /// </summary>
    public string RifAyuda => string.IsNullOrWhiteSpace(Rif)
        ? "Opcional. RIF completo: J-12345678-9."
        : Services.Rif.Error(Rif, exigirRif: true) ?? $"✓ {Services.Rif.Normalizar(Rif)} · RIF válido";

    private string _telefono = string.Empty;
    public string Telefono
    {
        get => _telefono;
        set => SetProperty(ref _telefono, value);
    }

    private string _notas = string.Empty;
    public string Notas
    {
        get => _notas;
        set => SetProperty(ref _notas, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            error = "Indique el nombre del proveedor.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Rif))
        {
            if (Services.Rif.Error(Rif, exigirRif: true) is { } errorRif)
            {
                error = errorRif;
                return false;
            }

            // Se compara normalizado: "J123456789" y "J-12345678-9" son el mismo proveedor. Las
            // fichas viejas guardadas sin normalizar también se normalizan para comparar.
            var rif = Services.Rif.Normalizar(Rif);
            var repetido = _proveedores.GetAll()
                .Any(p => p.Id != _original.Id
                          && string.Equals(Services.Rif.Normalizar(p.Rif) ?? p.Rif.Trim(), rif,
                                           StringComparison.OrdinalIgnoreCase));

            if (repetido)
            {
                error = $"Ya existe un proveedor con el RIF {rif}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public override Proveedor ObtenerResultado()
    {
        var proveedor = _original.Clonar();
        proveedor.Nombre = Nombre.Trim();
        proveedor.Rif = Services.Rif.Normalizar(Rif) ?? Rif.Trim();
        proveedor.Telefono = Telefono.Trim();
        proveedor.Notas = Notas.Trim();
        proveedor.Activo = Activo;
        return proveedor;
    }
}
