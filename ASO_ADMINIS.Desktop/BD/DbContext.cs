using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class AsoAdminisDbContext : DbContext
{
    // ---- Armazón: organización y seguridad ----
    public DbSet<Organizacion> Organizaciones { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<PermisoUsuario> PermisosUsuario { get; set; }
    public DbSet<PeticionCambio> PeticionesCambio { get; set; }

    // ---- Plantilla de ejemplo: Finanzas · Cuentas por Pagar y Banco ----
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<FacturaProveedor> FacturasProveedor { get; set; }
    public DbSet<CuentaBancaria> CuentasBancarias { get; set; }
    public DbSet<MovimientoBanco> MovimientosBanco { get; set; }

    // ---- Inventario: almacén, entradas y salidas ----
    public DbSet<Articulo> Articulos { get; set; }
    public DbSet<EntradaInventario> EntradasInventario { get; set; }
    public DbSet<SalidaInventario> SalidasInventario { get; set; }

    /// <summary>
    /// Organización sobre la que trabaja ESTE contexto. Se toma del ámbito al construirlo y no
    /// cambia después: cada método de las fuentes Sql abre su propio contexto, así que un cambio
    /// de organización se refleja en la siguiente consulta sin invalidar nada.
    ///
    /// Es un campo de instancia y no una lectura directa de <see cref="Ambito"/> dentro del filtro
    /// porque EF parametriza el acceso al campo — el modelo se compila una sola vez y el valor
    /// viaja como parámetro de la consulta.
    /// </summary>
    private readonly int? _organizacionId;

    public AsoAdminisDbContext() => _organizacionId = Ambito.OrganizacionId;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // La cadena de conexión vive en appsettings.json / appsettings.local.json,
            // no en el código. Cada máquina configura la suya sin tocar el repo.
            optionsBuilder.UseSqlServer(AppConfig.ConnectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---- Organización y seguridad ----

        modelBuilder.Entity<Organizacion>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Codigo).IsRequired().HasMaxLength(20);
            entity.Property(o => o.Nombre).IsRequired().HasMaxLength(150);
            entity.HasIndex(o => o.Codigo).IsUnique();

            entity.Ignore(o => o.Etiqueta);
            entity.Ignore(o => o.EstadoTexto);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.NombreUsuario).IsRequired().HasMaxLength(60);
            entity.Property(u => u.NombreCompleto).IsRequired().HasMaxLength(150);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(120);
            entity.Property(u => u.PasswordSalt).IsRequired().HasMaxLength(60);

            // Único en todo el sistema, no por organización: al autenticar todavía no se sabe
            // a qué organización pertenece quien escribe, así que un nombre repetido sería ambiguo.
            entity.HasIndex(u => u.NombreUsuario).IsUnique();

            entity.Ignore(u => u.RolTexto);
            entity.Ignore(u => u.EstadoTexto);
        });

        modelBuilder.Entity<PermisoUsuario>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Permiso).IsRequired().HasMaxLength(80);
            entity.Property(p => p.UsuarioNombre).HasMaxLength(60);
            entity.HasIndex(p => new { p.UsuarioId, p.Permiso }).IsUnique();

            entity.Ignore(p => p.EfectoTexto);
        });

        modelBuilder.Entity<PeticionCambio>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Permiso).IsRequired().HasMaxLength(80);
            entity.Property(p => p.Accion).IsRequired().HasMaxLength(120);
            entity.Property(p => p.TipoEntidad).HasMaxLength(60);
            entity.Property(p => p.EntidadId).HasMaxLength(60);
            entity.Property(p => p.EntidadDescripcion).HasMaxLength(300);
            entity.Property(p => p.Motivo).IsRequired().HasMaxLength(500);
            entity.Property(p => p.SolicitadoPorNombre).HasMaxLength(150);
            entity.Property(p => p.ResueltoPorNombre).HasMaxLength(150);
            entity.Property(p => p.ComentarioResolucion).HasMaxLength(500);

            entity.Ignore(p => p.EstaPendiente);
            entity.Ignore(p => p.EstadoTexto);
            entity.Ignore(p => p.Resumen);
        });

        // ---- Plantilla de ejemplo: Finanzas · Cuentas por Pagar y Banco ----
        //
        // Documentos planos, sin colecciones anidadas salvo FacturaProveedor.Lineas (OwnsMany):
        // no hace falta SqlAgregadoDataSource para CuentaBancaria/MovimientoBanco. Como en el
        // resto de las tablas planas, las relaciones son int sueltos (ProveedorId, CuentaId,
        // OrigenId) sin clave foránea real: la integridad es de la aplicación y cada documento
        // lleva su snapshot de texto (ProveedorNombre, CuentaNombre).

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Nombre).IsRequired().HasMaxLength(150);
            entity.Property(p => p.Rif).HasMaxLength(30);
            entity.Property(p => p.Telefono).HasMaxLength(30);
            entity.Property(p => p.Notas).HasMaxLength(500);

            entity.Ignore(p => p.EstadoTexto);
            entity.Ignore(p => p.Etiqueta);
        });

        modelBuilder.Entity<FacturaProveedor>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.NumeroDocumento).HasMaxLength(50);
            entity.Property(f => f.ProveedorNombre).HasMaxLength(150);
            entity.Property(f => f.Descripcion).HasMaxLength(500);
            entity.Property(f => f.MotivoAnulacion).HasMaxLength(500);
            entity.Property(f => f.Monto).HasColumnType("decimal(18,2)").IsRequired();

            entity.Ignore(f => f.EstaVencida);
            entity.Ignore(f => f.DiasParaVencer);
            entity.Ignore(f => f.EstadoTexto);
            entity.Ignore(f => f.MontoTexto);
            entity.Ignore(f => f.VencimientoTexto);
            entity.Ignore(f => f.PlazoTexto);

            entity.OwnsMany(f => f.Lineas, linea =>
            {
                linea.WithOwner().HasForeignKey("FacturaProveedorId");
                linea.Property<int>("Id");
                linea.HasKey("Id");
                linea.Property(x => x.DestinoTexto).HasMaxLength(150);
                linea.Property(x => x.CantidadTexto).HasMaxLength(50);
                linea.Property(x => x.PrecioUnitario).HasColumnType("decimal(18,2)");
                linea.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");

                linea.Ignore(x => x.PrecioUnitarioTexto);
                linea.Ignore(x => x.SubtotalTexto);
            });
        });

        modelBuilder.Entity<CuentaBancaria>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Nombre).IsRequired().HasMaxLength(120);
            entity.Property(c => c.Banco).HasMaxLength(120);
            entity.Property(c => c.NumeroCuenta).HasMaxLength(40);
            entity.Property(c => c.Moneda).IsRequired().HasMaxLength(10);
            entity.Property(c => c.Notas).HasMaxLength(500);
            entity.Property(c => c.SaldoInicial).HasColumnType("decimal(18,2)").IsRequired();

            entity.Ignore(c => c.TipoTexto);
            entity.Ignore(c => c.EstadoTexto);
            entity.Ignore(c => c.NombreConMoneda);
            entity.Ignore(c => c.SaldoActual);
            entity.Ignore(c => c.SaldoActualTexto);
            entity.Ignore(c => c.SaldoInicialTexto);
            entity.Ignore(c => c.AperturaTexto);
            entity.Ignore(c => c.DetalleTexto);
        });

        modelBuilder.Entity<MovimientoBanco>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.CuentaNombre).HasMaxLength(120);
            entity.Property(m => m.Concepto).IsRequired().HasMaxLength(300);
            entity.Property(m => m.Referencia).HasMaxLength(60);
            entity.Property(m => m.MotivoAnulacion).HasMaxLength(500);
            entity.Property(m => m.Monto).HasColumnType("decimal(18,2)").IsRequired();

            // El extracto siempre se pide por cuenta y fecha; sin esto cada apertura de la
            // pantalla haría un recorrido completo de la tabla, que es la que más crece.
            entity.HasIndex(m => new { m.CuentaId, m.Fecha });

            // La consulta del anti-doble-asiento: antes de escribir un pago se comprueba que
            // ese documento no tenga ya el suyo.
            entity.HasIndex(m => new { m.Origen, m.OrigenId });

            entity.Ignore(m => m.Efecto);
            entity.Ignore(m => m.EsDerivado);
            entity.Ignore(m => m.TipoTexto);
            entity.Ignore(m => m.MontoTexto);
            entity.Ignore(m => m.EntradaTexto);
            entity.Ignore(m => m.SalidaTexto);
            entity.Ignore(m => m.FechaTexto);
            entity.Ignore(m => m.SaldoCorrido);
            entity.Ignore(m => m.SaldoCorridoTexto);
            entity.Ignore(m => m.EstadoTexto);
            entity.Ignore(m => m.CategoriaTexto);
            entity.Ignore(m => m.OrigenTexto);
            entity.Ignore(m => m.DocumentoTexto);
            entity.Ignore(m => m.ConciliacionTexto);
        });

        // ---- Inventario ----

        modelBuilder.Entity<Articulo>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Codigo).IsRequired().HasMaxLength(20);
            entity.Property(a => a.Nombre).IsRequired().HasMaxLength(150);
            entity.Property(a => a.Categoria).HasMaxLength(60);
            entity.Property(a => a.Ubicacion).HasMaxLength(60);
            entity.Property(a => a.Notas).HasMaxLength(500);
            entity.Property(a => a.Minimo).HasColumnType("decimal(18,2)").IsRequired();

            // Respalda el código aleatorio: si dos altas simultáneas generan el mismo candidato,
            // la segunda choca contra el índice en vez de duplicar el artículo.
            entity.HasIndex(a => new { a.OrganizacionId, a.Codigo }).IsUnique();

            // Existencia NO se persiste: es la suma del kardex, la rellena InventarioService.
            entity.Ignore(a => a.Existencia);
            entity.Ignore(a => a.BajoMinimo);
            entity.Ignore(a => a.SinExistencia);
            entity.Ignore(a => a.EstadoTexto);
            entity.Ignore(a => a.UnidadTexto);
            entity.Ignore(a => a.UnidadCorta);
            entity.Ignore(a => a.ExistenciaTexto);
            entity.Ignore(a => a.MinimoTexto);
            entity.Ignore(a => a.Etiqueta);
        });

        modelBuilder.Entity<EntradaInventario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Numero).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ProveedorNombre).HasMaxLength(150);
            entity.Property(e => e.NumeroDocumento).HasMaxLength(50);
            entity.Property(e => e.CompradoPor).HasMaxLength(150);
            entity.Property(e => e.Observaciones).HasMaxLength(500);
            entity.Property(e => e.FacturaProveedorNumero).HasMaxLength(50);
            entity.Property(e => e.MotivoAnulacion).HasMaxLength(500);
            entity.Property(e => e.CreadoPorNombre).HasMaxLength(150);
            entity.Property(e => e.Total).HasColumnType("decimal(18,2)").IsRequired();

            // El correlativo se calcula como "el último + 1" antes de escribir; este índice es
            // la red que convierte una carrera entre dos puestos en un error, no en un duplicado.
            entity.HasIndex(e => new { e.OrganizacionId, e.Numero }).IsUnique();

            // El camino inverso del enlace a Finanzas: qué entrada originó una cuenta por pagar.
            entity.HasIndex(e => e.FacturaProveedorId);

            entity.Ignore(e => e.GeneraCuentaPorPagar);
            entity.Ignore(e => e.CuentaEnKardex);
            entity.Ignore(e => e.TipoTexto);
            entity.Ignore(e => e.EstadoTexto);
            entity.Ignore(e => e.OrigenTexto);
            entity.Ignore(e => e.TotalTexto);
            entity.Ignore(e => e.FechaTexto);
            entity.Ignore(e => e.VencimientoTexto);
            entity.Ignore(e => e.CuentaPorPagarTexto);
            entity.Ignore(e => e.CantidadLineas);
            entity.Ignore(e => e.TotalUnidades);

            entity.OwnsMany(e => e.Lineas, linea =>
            {
                linea.WithOwner().HasForeignKey("EntradaInventarioId");
                linea.Property<int>("Id");
                linea.HasKey("Id");
                linea.Property(x => x.ArticuloCodigo).HasMaxLength(20);
                linea.Property(x => x.ArticuloNombre).HasMaxLength(150);
                linea.Property(x => x.UnidadTexto).HasMaxLength(30);
                linea.Property(x => x.Cantidad).HasColumnType("decimal(18,2)");
                linea.Property(x => x.PrecioUnitario).HasColumnType("decimal(18,2)");
                linea.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");

                linea.Ignore(x => x.CantidadTexto);
                linea.Ignore(x => x.PrecioUnitarioTexto);
                linea.Ignore(x => x.SubtotalTexto);
                linea.Ignore(x => x.ArticuloTexto);
            });
        });

        modelBuilder.Entity<SalidaInventario>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Numero).IsRequired().HasMaxLength(20);
            entity.Property(s => s.DestinoDetalle).HasMaxLength(150);
            entity.Property(s => s.RetiradoPor).IsRequired().HasMaxLength(150);
            entity.Property(s => s.AutorizadoPorNombre).HasMaxLength(150);
            entity.Property(s => s.Observaciones).HasMaxLength(500);
            entity.Property(s => s.MotivoAnulacion).HasMaxLength(500);

            entity.HasIndex(s => new { s.OrganizacionId, s.Numero }).IsUnique();

            entity.Ignore(s => s.CuentaEnKardex);
            entity.Ignore(s => s.DestinoTexto);
            entity.Ignore(s => s.MotivoTexto);
            entity.Ignore(s => s.EstadoTexto);
            entity.Ignore(s => s.FechaTexto);
            entity.Ignore(s => s.CantidadLineas);
            entity.Ignore(s => s.TotalUnidades);

            entity.OwnsMany(s => s.Lineas, linea =>
            {
                linea.WithOwner().HasForeignKey("SalidaInventarioId");
                linea.Property<int>("Id");
                linea.HasKey("Id");
                linea.Property(x => x.ArticuloCodigo).HasMaxLength(20);
                linea.Property(x => x.ArticuloNombre).HasMaxLength(150);
                linea.Property(x => x.UnidadTexto).HasMaxLength(30);
                linea.Property(x => x.Cantidad).HasColumnType("decimal(18,2)");

                linea.Ignore(x => x.CantidadTexto);
                linea.Ignore(x => x.ArticuloTexto);
            });
        });

        AplicarFiltroDeOrganizacion(modelBuilder);
    }

    /// <summary>
    /// Aísla las organizaciones: ninguna consulta devuelve filas de otra, sin que las fuentes
    /// de datos ni los ViewModels tengan que acordarse de filtrar.
    ///
    /// Recorre el modelo en vez de listar las entidades una por una, así una entidad nueva que
    /// implemente <see cref="IDeOrganizacion"/> queda aislada por el solo hecho de existir.
    /// Es fail-closed: sin ámbito fijado no se ve nada, en vez de verse todo.
    ///
    /// Para saltarlo hay que pedirlo explícitamente con <c>IgnoreQueryFilters()</c>, y hoy eso
    /// solo pasa en el login.
    /// </summary>
    private void AplicarFiltroDeOrganizacion(ModelBuilder modelBuilder)
    {
        var plantilla = typeof(AsoAdminisDbContext).GetMethod(
            nameof(FiltrarPorOrganizacion),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IDeOrganizacion).IsAssignableFrom(tipo.ClrType))
                plantilla.MakeGenericMethod(tipo.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    /// <summary>
    /// El filtro en sí. Se escribe como lambda normal — y no armando el árbol de expresión a
    /// mano — porque así el compilador genera la captura de <c>_organizacionId</c> con la forma
    /// exacta que EF sabe convertir en parámetro de consulta. El modelo se compila una sola vez
    /// y el mismo modelo sirve para todas las organizaciones.
    /// </summary>
    private void FiltrarPorOrganizacion<T>(ModelBuilder modelBuilder) where T : class, IDeOrganizacion
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => e.OrganizacionId == _organizacionId);
    }

    /// <summary>
    /// Estampa la organización activa en toda fila nueva que le pertenezca. Va aquí, en un solo
    /// sitio, en vez de en cada <c>Sql…DataSource</c>: olvidarlo en uno solo crearía filas
    /// huérfanas que después ninguna consulta devolvería.
    /// </summary>
    public override int SaveChanges()
    {
        EstamparOrganizacion();

        var filas = base.SaveChanges();
        AnunciarSiEscribio(filas);
        return filas;
    }

    /// <summary>
    /// La variante asíncrona tiene que estampar igual. Se sobrescribe aunque hoy la capa de datos
    /// sea toda síncrona: el día que se agregue el primer <c>SaveChangesAsync</c>, sus filas
    /// nacerían con <c>OrganizacionId = 0</c> y el filtro global las haría invisibles al instante.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
                                                     CancellationToken cancellationToken = default)
    {
        EstamparOrganizacion();

        var filas = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        AnunciarSiEscribio(filas);
        return filas;
    }

    /// <summary>
    /// Avisa de la escritura para que la pantalla abierta se recargue sola (ver
    /// <see cref="CambiosDeDatos"/>). Va aquí por el mismo motivo que el estampado de arriba: es
    /// el único punto por el que pasan TODAS las escrituras de la aplicación —fuera de
    /// <c>BD/</c> nadie construye un <c>AsoAdminisDbContext</c>—, así que ninguna acción puede
    /// olvidarse de avisar, ni siquiera las que hace un servicio de dominio por su cuenta.
    ///
    /// Solo cuando la escritura llegó a la base: se llama DESPUÉS del <c>base.SaveChanges</c>,
    /// de modo que una excepción se propaga sin haber disparado ninguna recarga, y se comprueba
    /// que haya filas afectadas para no mover nada cuando no cambió nada.
    /// </summary>
    private static void AnunciarSiEscribio(int filasAfectadas)
    {
        if (filasAfectadas > 0)
            CambiosDeDatos.Publicar();
    }

    private void EstamparOrganizacion()
    {
        foreach (var entrada in ChangeTracker.Entries<IDeOrganizacion>())
        {
            if (entrada.Entity.OrganizacionId != 0)
                continue;

            if (entrada.State == EntityState.Added)
            {
                entrada.Entity.OrganizacionId = Ambito.Exigir();
                continue;
            }

            // Una MODIFICACIÓN que llega sin organización es el caso que dejaba filas huérfanas.
            //
            // Ninguno de los editores conserva OrganizacionId: todos construyen su resultado con
            // un `new` que copia los campos del formulario y nada más. Esa copia llega con 0, y
            // como el estampado solo miraba las filas nuevas, el UPDATE escribía 0 encima de la
            // organización buena. La fila seguía en la tabla, pero el filtro global la dejaba
            // fuera de TODA consulta: editar un registro equivalía a borrarlo de la vista.
            //
            // Se recupera el valor que tenía la fila antes del cambio, y solo si no hay (la
            // entidad se adjuntó desprendida, sin pasar por la base) se cae a la organización
            // activa. Así un update jamás mueve una fila de organización, ni siquiera por
            // accidente.
            if (entrada.State == EntityState.Modified)
            {
                var original = entrada.OriginalValues.GetValue<int>(nameof(IDeOrganizacion.OrganizacionId));
                entrada.Entity.OrganizacionId = original != 0 ? original : Ambito.Exigir();
            }
        }
    }
}
