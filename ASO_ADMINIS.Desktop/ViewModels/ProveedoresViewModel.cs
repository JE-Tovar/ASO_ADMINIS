using System;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Finanzas · Proveedores: maestro de proveedores, promovido a submódulo propio (antes era la
/// pestaña "Proveedores" dentro de Cuentas por Pagar). Mismo patrón que Nómina · Empleados:
/// listado único, sin conmutador de padrones.
/// </summary>
public sealed class ProveedoresViewModel : PantallaCrudViewModel<Proveedor, int>
{
    private readonly IProveedorDataSource _proveedores;

    public ProveedoresViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearProveedores(), new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ProveedoresViewModel(Modulo modulo,
                                 Submodulo submodulo,
                                 IProveedorDataSource proveedores,
                                 IServicioDialogo dialogos,
                                 ISesionActual sesion)
        : base(modulo, submodulo, proveedores, dialogos, sesion)
    {
        _proveedores = proveedores;
    }

    protected override string ModuloPermiso => "Proveedores";

    protected override bool CoincideBusqueda(Proveedor item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Rif.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Telefono.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override Proveedor CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Proveedor> CrearEditor(Proveedor item) =>
        new ProveedorEditorViewModel(item, _proveedores);
}
