using System;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Padrón de usuarios del núcleo activo. El listado ya viene acotado por el filtro global de
/// organización, así que un administrador no ve ni puede tocar los usuarios de otro núcleo.
/// </summary>
public sealed class UsuariosCrudViewModel : CrudViewModelBase<Usuario, int>
{
    private readonly ISesionActual _sesion;

    public UsuariosCrudViewModel(IUsuarioDataSource usuarios, IServicioDialogo dialogos, ISesionActual sesion)
        : base(usuarios, dialogos, sesion)
    {
        _sesion = sesion;
    }

    protected override string ModuloPermiso => "Usuarios";

    protected override bool CoincideBusqueda(Usuario item, string texto) =>
        item.NombreUsuario.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NombreCompleto.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.RolTexto.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override Usuario CrearNuevo() => new();

    protected override CrudEditorViewModelBase<Usuario> CrearEditor(Usuario item) =>
        new UsuarioEditorViewModel(item, _sesion);

    /// <summary>
    /// Nadie se borra a sí mismo: dejaría el núcleo sin quien lo administre y cerraría la
    /// sesión en curso sobre una fila que ya no existe. Desactivarlo sí se puede desde el editor.
    /// </summary>
    protected override bool PuedeEliminar(Usuario item) => item.Id != _sesion.UsuarioActual?.Id;
}
