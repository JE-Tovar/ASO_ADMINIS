namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Catalogo de los permisos que existen, en formato "Modulo.Accion".
///
/// Los comandos los piden por cadena, y <c>CrudViewModelBase</c> arma tres por submodulo
/// interpolando <c>ModuloPermiso</c>. Tener el catalogo escrito en un solo sitio es lo que
/// permite revisar la matriz de un vistazo y detectar un permiso huerfano.
///
/// Los permisos de navegacion llevan el prefijo <c>Ver.</c> y su sufijo es la clave del
/// submodulo en <c>Navigation/ModuloCatalogo.cs</c>, para que no puedan desincronizarse.
///
/// PROVISIONAL: este es el scaffold genérico de ASO. Solo trae los permisos del armazón
/// (usuarios, peticiones, organización) y los de los dos módulos de ejemplo (Finanzas ·
/// Cuentas por Pagar y Banco, e Inventario). Cada módulo de negocio nuevo agrega su propia
/// clase aquí siguiendo el mismo patrón — ver la receta en CLAUDE.md.
/// </summary>
public static class Permisos
{
    public const string PrefijoVer = "Ver.";

    /// <summary>Permiso de navegacion de una clave de modulo o submodulo.</summary>
    public static string Ver(string clave) => PrefijoVer + clave;

    public static class Proveedores
    {
        public const string Crear = "Proveedores.Crear";
        public const string Editar = "Proveedores.Editar";
        public const string Eliminar = "Proveedores.Eliminar";
    }

    public static class FacturasProveedor
    {
        public const string Crear = "FacturasProveedor.Crear";
        public const string Editar = "FacturasProveedor.Editar";
        public const string Eliminar = "FacturasProveedor.Eliminar";
    }

    public static class Finanzas
    {
        public const string Pagar = "Finanzas.Pagar";
        public const string Anular = "Finanzas.Anular";
    }

    /// <summary>
    /// El libro de banco: los movimientos que se teclean a mano. Los que nacen de un pago no
    /// piden permiso propio — ya lo guarda <see cref="Finanzas.Pagar"/>, y exigir otro más
    /// dejaría pagar la factura sin que el dinero apareciera en el libro.
    /// </summary>
    public static class Banco
    {
        public const string Crear = "Banco.Crear";
        public const string Editar = "Banco.Editar";
        public const string Eliminar = "Banco.Eliminar";
        public const string Conciliar = "Banco.Conciliar";
        public const string Anular = "Banco.Anular";
        public const string Transferir = "Banco.Transferir";
    }

    /// <summary>
    /// El catálogo de cuentas de la organización. Se llama "CuentasBancarias" y no "Cuentas"
    /// para no chocar con el vocabulario de otros módulos financieros que se agreguen después
    /// (Cuentas por Cobrar, por ejemplo).
    /// </summary>
    public static class CuentasBancarias
    {
        public const string Crear = "CuentasBancarias.Crear";
        public const string Editar = "CuentasBancarias.Editar";
        public const string Eliminar = "CuentasBancarias.Eliminar";
    }

    /// <summary>Catálogo de artículos del almacén (Inventario · Almacén).</summary>
    public static class Articulos
    {
        public const string Crear = "Articulos.Crear";
        public const string Editar = "Articulos.Editar";
        public const string Eliminar = "Articulos.Eliminar";
    }

    /// <summary>
    /// Entradas al almacén. No hay "Editar" ni "Eliminar": una entrada es un documento, y un
    /// documento no se corrige ni se borra, se anula (ver <c>EntradasInventarioService</c>).
    /// </summary>
    public static class EntradasInventario
    {
        public const string Crear = "EntradasInventario.Crear";
        public const string Anular = "EntradasInventario.Anular";
    }

    /// <summary>Boletos de salida del almacén. Mismo criterio que las entradas.</summary>
    public static class SalidasInventario
    {
        public const string Crear = "SalidasInventario.Crear";
        public const string Anular = "SalidasInventario.Anular";
    }

    public static class Peticiones
    {
        public const string Solicitar = "Peticiones.Solicitar";
        public const string Resolver = "Peticiones.Resolver";
    }

    /// <summary>
    /// Los datos de la propia organización (nombre y código interno). No hay padrón ni alta:
    /// la organización nace en el primer arranque y aquí solo se corrigen sus datos.
    /// </summary>
    public static class Organizacion
    {
        public const string Editar = "Organizacion.Editar";
    }

    public static class Usuarios
    {
        public const string Crear = "Usuarios.Crear";
        public const string Editar = "Usuarios.Editar";
        public const string Eliminar = "Usuarios.Eliminar";

        /// <summary>
        /// Repartir el rol Desarrollador, que es el que lo puede todo. Va aparte de
        /// <see cref="Crear"/> para que un administrador de organización no se fabrique un
        /// usuario con más alcance del que él mismo tiene.
        /// </summary>
        public const string CrearDesarrollador = "Usuarios.CrearDesarrollador";
    }
}
