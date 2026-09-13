using System;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Alta/edición de una marca. El nombre no se repite: dos fichas de la misma marca partirían
/// sus modelos en dos catálogos.
/// </summary>
public sealed class MarcaEditorViewModel : CrudEditorViewModelBase<Marca>
{
    private readonly Marca _original;
    private readonly IMarcaDataSource _marcas;

    public MarcaEditorViewModel(Marca original, IMarcaDataSource marcas)
    {
        _original = original;
        _marcas = marcas;

        Nombre = original.Nombre;
        Notas = original.Notas;
        Activo = original.Activo;
    }

    public override string Titulo => _original.Id == 0 ? "Nueva marca" : $"Editar marca {_original.Nombre}";

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
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
            error = "Indique el nombre de la marca.";
            return false;
        }

        var repetida = _marcas.GetAll()
            .Any(m => m.Id != _original.Id
                      && string.Equals(m.Nombre.Trim(), Nombre.Trim(), StringComparison.OrdinalIgnoreCase));

        if (repetida)
        {
            error = $"Ya existe una marca llamada {Nombre.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    public override Marca ObtenerResultado()
    {
        var marca = _original.Clonar();
        marca.Nombre = Nombre.Trim();
        marca.Notas = Notas.Trim();
        marca.Activo = Activo;
        return marca;
    }
}
