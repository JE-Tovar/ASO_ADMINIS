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

    // ---- Ventas ----
    public DbSet<TasaCambio> TasasCambio { get; set; }
    public DbSet<Venta> Ventas { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<RetencionIvaRecibida> RetencionesIva { get; set; }
    public DbSet<CierreZ> CierresZ { get; set; }

    // ---- Plantilla de ejemplo: Finanzas · Cuentas por Pagar y Banco ----
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<FacturaProveedor> FacturasProveedor { get; set; }
    public DbSet<CuentaBancaria> CuentasBancarias { get; set; }
    public DbSet<MovimientoBanco> MovimientosBanco { get; set; }
    public DbSet<GastoVario> GastosVarios { get; set; }

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

            // Los valores por defecto de la columna (16/8/31/3) los pone la migración para las
            // filas existentes; no van con HasDefaultValue porque EF tomaría un 0 tecleado como
            // "sin valor" y lo cambiaría por el defecto.
            entity.Property(o => o.RazonSocial).HasMaxLength(200);
            entity.Property(o => o.Rif).HasMaxLength(15);
            entity.Property(o => o.DireccionFiscal).HasMaxLength(300);
            entity.Property(o => o.Telefono).HasMaxLength(40);
            entity.Property(o => o.IvaGeneral).HasColumnType("decimal(5,2)");
            entity.Property(o => o.IvaReducida).HasColumnType("decimal(5,2)");
            entity.Property(o => o.IvaAdicional).HasColumnType("decimal(5,2)");
            entity.Property(o => o.IgtfPorcentaje).HasColumnType("decimal(5,2)");

            entity.Ignore(o => o.Etiqueta);
            entity.Ignore(o => o.EstadoTexto);
            entity.Ignore(o => o.DatosFiscalesCompletos);
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

        // ---- Ventas: tasa de cambio y registro de ventas ----

        modelBuilder.Entity<TasaCambio>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Valor).HasColumnType("decimal(18,4)").IsRequired();

            // Una fila por día: respalda el "buscar o crear" de TasaCambioService.
            entity.HasIndex(t => new { t.OrganizacionId, t.Fecha }).IsUnique();

            entity.Ignore(t => t.FechaTexto);
            entity.Ignore(t => t.ValorTexto);
        });

        modelBuilder.Entity<Venta>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Numero).IsRequired().HasMaxLength(20);
            entity.Property(v => v.CajaCodigo).HasMaxLength(20);
            entity.Property(v => v.DocumentoAfectadoNumero).HasMaxLength(30);
            entity.Property(v => v.Concepto).HasMaxLength(300);
            entity.Property(v => v.ClienteRif).HasMaxLength(15);
            entity.Property(v => v.ClienteNombre).HasMaxLength(200);
            entity.Property(v => v.ClienteDireccion).HasMaxLength(300);
            // Contacto del cliente al emitir (snapshot, como la dirección). Mismas longitudes que Clientes.
            entity.Property(v => v.ClienteTelefono).HasMaxLength(40);
            entity.Property(v => v.ClienteCorreo).HasMaxLength(120);
            entity.Property(v => v.MotivoAnulacion).HasMaxLength(500);
            entity.Property(v => v.NumeroFiscal).HasMaxLength(30);
            entity.Property(v => v.NumeroControl).HasMaxLength(30);
            entity.Property(v => v.SerialEmisor).HasMaxLength(40);
            entity.Property(v => v.TasaCambioUsada).HasColumnType("decimal(18,4)").IsRequired();

            foreach (var monto in new[]
                     {
                         nameof(Venta.MontoExento), nameof(Venta.BaseGeneral), nameof(Venta.IvaGeneral),
                         nameof(Venta.BaseReducida), nameof(Venta.IvaReducida), nameof(Venta.BaseAdicional),
                         nameof(Venta.IvaAdicional), nameof(Venta.BaseIgtf), nameof(Venta.Igtf),
                         nameof(Venta.Total), nameof(Venta.Vuelto)
                     })
                entity.Property(monto).HasColumnType("decimal(18,2)");

            foreach (var porcentaje in new[]
                     {
                         nameof(Venta.PorcentajeGeneral), nameof(Venta.PorcentajeReducida),
                         nameof(Venta.PorcentajeAdicional), nameof(Venta.PorcentajeIgtf)
                     })
                entity.Property(porcentaje).HasColumnType("decimal(5,2)");

            // El correlativo se calcula como "el último + 1" antes de escribir; este índice es
            // la red por si dos puestos coincidieran — mismo criterio que Entradas/Salidas.
            entity.HasIndex(v => new { v.OrganizacionId, v.Numero }).IsUnique();
            entity.HasIndex(v => v.DocumentoAfectadoId);
            entity.HasIndex(v => new { v.OrganizacionId, v.Fecha });

            entity.OwnsMany(v => v.Lineas, linea =>
            {
                linea.WithOwner().HasForeignKey("VentaId");
                linea.Property<int>("Id");
                linea.HasKey("Id");
                linea.ToTable("VentaLineas");
                linea.Property(x => x.Codigo).HasMaxLength(20);
                linea.Property(x => x.Descripcion).IsRequired().HasMaxLength(200);
                linea.Property(x => x.PresentacionNombre).HasMaxLength(40);
                linea.Property(x => x.Factor).HasColumnType("decimal(18,4)");
                // 3 decimales: a granel se vende por gramos (0,125 kg). Con 2 se guardaba 0,13 y
                // la factura no cuadraba con su monto ni con la salida del almacén.
                linea.Property(x => x.Cantidad).HasColumnType("decimal(18,3)");
                linea.Property(x => x.PrecioUnitarioUsd).HasColumnType("decimal(18,2)");
                linea.Property(x => x.PrecioUnitario).HasColumnType("decimal(18,2)");
                linea.Property(x => x.PorcentajeIva).HasColumnType("decimal(5,2)");
                linea.Property(x => x.Monto).HasColumnType("decimal(18,2)");
                linea.Property(x => x.Iva).HasColumnType("decimal(18,2)");

                linea.Ignore(x => x.CantidadBase);
                linea.Ignore(x => x.CantidadTexto);
                linea.Ignore(x => x.PrecioUnitarioTexto);
                linea.Ignore(x => x.MontoTexto);
                linea.Ignore(x => x.AlicuotaTexto);
            });

            entity.OwnsMany(v => v.Pagos, pago =>
            {
                pago.WithOwner().HasForeignKey("VentaId");
                pago.Property<int>("Id");
                pago.HasKey("Id");
                pago.ToTable("VentaPagos");
                pago.Property(x => x.MontoBs).HasColumnType("decimal(18,2)");
                pago.Property(x => x.MontoDivisa).HasColumnType("decimal(18,2)");
                pago.Property(x => x.Referencia).HasMaxLength(60);

                pago.Ignore(x => x.EsDivisa);
                pago.Ignore(x => x.MetodoTexto);
            });

            foreach (var derivada in new[]
                     {
                         nameof(Venta.Vigente), nameof(Venta.Signo), nameof(Venta.BaseImponible),
                         nameof(Venta.TotalIva), nameof(Venta.TotalConIva), nameof(Venta.TotalUsd),
                         nameof(Venta.EsConsumidorFinal), nameof(Venta.TipoTexto), nameof(Venta.TipoCorto),
                         nameof(Venta.EstadoTexto), nameof(Venta.FechaTexto), nameof(Venta.FechaHoraTexto),
                         nameof(Venta.ClienteTexto), nameof(Venta.ArticuloTexto), nameof(Venta.MetodoPagoTexto),
                         nameof(Venta.NumeroFiscalTexto), nameof(Venta.TotalTexto), nameof(Venta.TotalBsTexto),
                         nameof(Venta.TotalUsdTexto)
                     })
                entity.Ignore(derivada);
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

        modelBuilder.Entity<RetencionIvaRecibida>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.ToTable("RetencionesIvaRecibidas");
            entity.Property(r => r.VentaNumeroFiscal).HasMaxLength(30);
            entity.Property(r => r.ClienteRif).IsRequired().HasMaxLength(15);
            entity.Property(r => r.ClienteNombre).HasMaxLength(200);
            entity.Property(r => r.NumeroComprobante).IsRequired().HasMaxLength(20);
            entity.Property(r => r.Porcentaje).HasColumnType("decimal(5,2)");
            entity.Property(r => r.IvaFactura).HasColumnType("decimal(18,2)");
            entity.Property(r => r.MontoRetenido).HasColumnType("decimal(18,2)");

            // Una retención por factura, y un comprobante no se registra dos veces.
            entity.HasIndex(r => new { r.OrganizacionId, r.VentaId }).IsUnique();
            entity.HasIndex(r => new { r.OrganizacionId, r.ClienteRif, r.NumeroComprobante }).IsUnique();

            entity.Ignore(r => r.FechaTexto);
            entity.Ignore(r => r.MontoTexto);
        });

        modelBuilder.Entity<CierreZ>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.ToTable("CierresZ");
            entity.Property(c => c.CajaCodigo).HasMaxLength(20);
            entity.Property(c => c.SerialEmisor).HasMaxLength(40);
            entity.Property(c => c.CreadoPorNombre).HasMaxLength(150);
            foreach (var monto in new[]
                     {
                         nameof(CierreZ.Exento), nameof(CierreZ.BaseGeneral), nameof(CierreZ.IvaGeneral),
                         nameof(CierreZ.BaseReducida), nameof(CierreZ.IvaReducida), nameof(CierreZ.BaseAdicional),
                         nameof(CierreZ.IvaAdicional), nameof(CierreZ.Igtf), nameof(CierreZ.Total),
                         nameof(CierreZ.TotalSistema)
                     })
                entity.Property(monto).HasColumnType("decimal(18,2)");

            entity.HasIndex(c => new { c.OrganizacionId, c.CajaCodigo, c.NumeroZ }).IsUnique();

            foreach (var derivada in new[]
                     {
                         nameof(CierreZ.Diferencia), nameof(CierreZ.Cuadra), nameof(CierreZ.FechaTexto),
                         nameof(CierreZ.TotalTexto), nameof(CierreZ.TotalSistemaTexto), nameof(CierreZ.CuadraTexto)
                     })
                entity.Ignore(derivada);
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Rif).IsRequired().HasMaxLength(15);
            entity.Property(c => c.Nombre).IsRequired().HasMaxLength(200);
            entity.Property(c => c.DireccionFiscal).HasMaxLength(300);
            entity.Property(c => c.Telefono).HasMaxLength(40);
            entity.Property(c => c.Correo).HasMaxLength(120);

            // Respaldo del "no repetir RIF" de ClientesService.Validar.
            entity.HasIndex(c => new { c.OrganizacionId, c.Rif }).IsUnique();

            entity.Ignore(c => c.EstadoTexto);
            entity.Ignore(c => c.Etiqueta);
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

        modelBuilder.Entity<GastoVario>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Numero).IsRequired().HasMaxLength(20);
            entity.Property(g => g.Categoria).IsRequired().HasMaxLength(60);
            entity.Property(g => g.Concepto).IsRequired().HasMaxLength(200);
            entity.Property(g => g.Beneficiario).HasMaxLength(120);
            entity.Property(g => g.Referencia).HasMaxLength(60);
            entity.Property(g => g.CuentaNombre).HasMaxLength(120);
            entity.Property(g => g.CreadoPorNombre).HasMaxLength(120);
            entity.Property(g => g.MotivoAnulacion).HasMaxLength(500);
            entity.Property(g => g.Monto).HasColumnType("decimal(18,2)");
            entity.Property(g => g.TasaCambio).HasColumnType("decimal(18,4)");
            entity.Property(g => g.MontoBs).HasColumnType("decimal(18,2)");

            // Mismo criterio que Entradas/Salidas: el servicio numera "el último + 1" y el
            // índice único es la red si dos puestos coinciden.
            entity.HasIndex(g => new { g.OrganizacionId, g.Numero }).IsUnique();
            entity.HasIndex(g => new { g.OrganizacionId, g.Fecha });

            entity.Ignore(g => g.Vigente);
            entity.Ignore(g => g.FechaTexto);
            entity.Ignore(g => g.MonedaTexto);
            entity.Ignore(g => g.MontoTexto);
            entity.Ignore(g => g.MontoBsTexto);
            entity.Ignore(g => g.PagoTexto);
            entity.Ignore(g => g.EstadoTexto);
        });

        // ---- Inventario ----

        modelBuilder.Entity<Articulo>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Codigo).IsRequired().HasMaxLength(20);
            entity.Property(a => a.Descripcion).IsRequired().HasMaxLength(200);
            entity.Property(a => a.Categoria).HasMaxLength(60);
            entity.Property(a => a.Ubicacion).HasMaxLength(60);
            entity.Property(a => a.Notas).HasMaxLength(500);
            entity.Property(a => a.Minimo).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(a => a.PrecioVenta).HasColumnType("decimal(18,2)").IsRequired();

            // Los artículos que ya existían quedan en la general (defaultValue de la migración);
            // el exento se marca a mano.
            entity.Ignore(a => a.AlicuotaTexto);

            // Bien o servicio (ordinal). Lo que ya existía queda como Bien (0) por la migración.
            entity.Ignore(a => a.EsServicio);
            entity.Ignore(a => a.EsGranel);
            entity.Property(a => a.CodigoBalanza).HasMaxLength(13);

            // Respalda el código aleatorio: si dos altas simultáneas generan el mismo candidato,
            // la segunda choca contra el índice en vez de duplicar el artículo.
            entity.HasIndex(a => new { a.OrganizacionId, a.Codigo }).IsUnique();

            // Equivalente en bolívares: NO se persiste, lo calcula AlmacenViewModel con la tasa
            // vigente antes de pintar la grilla — mismo criterio que Existencia.
            entity.Ignore(a => a.PrecioVentaBs);
            entity.Ignore(a => a.PrecioVentaTexto);
            entity.Ignore(a => a.PrecioVentaBsTexto);

            // Existencia NO se persiste: es la suma del kardex, la rellena InventarioService.
            entity.Ignore(a => a.Existencia);
            entity.Ignore(a => a.BajoMinimo);
            entity.Ignore(a => a.SinExistencia);
            entity.Ignore(a => a.EstadoTexto);
            entity.Ignore(a => a.UnidadTexto);
            entity.Ignore(a => a.UnidadCorta);
            entity.Ignore(a => a.ExistenciaTexto);
            entity.Ignore(a => a.DisponibleTexto);
            entity.Ignore(a => a.CategoriaTexto);
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
            entity.HasIndex(e => e.VentaId);

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
                linea.Property(x => x.PresentacionNombre).HasMaxLength(40);
                linea.Property(x => x.Factor).HasColumnType("decimal(18,4)");
                linea.Property(x => x.CantidadPresentacion).HasColumnType("decimal(18,2)");
                // 4 decimales en la cantidad base: 3 × 0,946 L = 2,838 L no debe redondearse.
                linea.Property(x => x.Cantidad).HasColumnType("decimal(18,4)");
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

            // El camino inverso del enlace a Ventas: qué salida generó una venta.
            entity.HasIndex(s => s.VentaId);

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
                // 4 decimales: la salida de una venta por presentación (3 × 0,946 L) es fraccionaria.
                linea.Property(x => x.Cantidad).HasColumnType("decimal(18,4)");

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
        ProtegerDocumentosFiscales();

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
        ProtegerDocumentosFiscales();

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

    /// <summary>
    /// Inalterabilidad de los documentos fiscales (exigencia del SENIAT): un documento de venta
    /// emitido no se borra ni se edita. Va aquí, y no solo en <c>VentasService</c>, porque es el
    /// único punto por el que pasan TODAS las escrituras: ni un servicio nuevo ni un CRUD
    /// genérico puede saltárselo.
    ///
    /// Solo hay dos cambios legítimos sobre un documento ya guardado, y son los únicos que pasan:
    /// - transmitir uno de contingencia: <c>Estado</c> PendienteTransmitir → Registrada y su
    ///   <c>FechaTransmision</c>;
    /// - estamparle el reporte Z que lo cerró: <c>NumeroZ</c> de nulo a un valor.
    ///
    /// Por eso las fuentes de datos los escriben cargando la fila rastreada y cambiando solo ese
    /// campo (ver <c>SqlVentaDataSource</c>): un <c>Update</c> de entidad desprendida marca todas
    /// las columnas como modificadas y aquí se rechaza.
    /// </summary>
    private void ProtegerDocumentosFiscales()
    {
        foreach (var e in ChangeTracker.Entries<Venta>())
        {
            if (e.State == EntityState.Deleted)
                throw new InvalidOperationException(
                    $"El documento {e.Entity.NumeroFiscalTexto} no se puede eliminar: un documento fiscal es inalterable.");

            if (e.State != EntityState.Modified)
                continue;

            var cambiadas = e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToHashSet();
            cambiadas.Remove(nameof(IDeOrganizacion.OrganizacionId));

            var permitidas = new[] { nameof(Venta.Estado), nameof(Venta.FechaTransmision), nameof(Venta.NumeroZ) };
            var prohibidas = cambiadas.Except(permitidas).ToList();

            var transicionValida =
                !cambiadas.Contains(nameof(Venta.Estado))
                || (e.OriginalValues.GetValue<EstadoVenta>(nameof(Venta.Estado)) == EstadoVenta.PendienteTransmitir
                    && e.Entity.Estado == EstadoVenta.Registrada);

            var zValido =
                !cambiadas.Contains(nameof(Venta.NumeroZ))
                || e.OriginalValues.GetValue<int?>(nameof(Venta.NumeroZ)) is null;

            if (prohibidas.Count > 0 || !transicionValida || !zValido)
                throw new InvalidOperationException(
                    $"El documento {e.Entity.NumeroFiscalTexto} no se puede modificar: un documento fiscal es inalterable. " +
                    "Para corregirlo, emita una nota de crédito o de débito.");
        }

        var hijos = ChangeTracker.Entries<VentaLinea>()
            .Cast<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>()
            .Concat(ChangeTracker.Entries<VentaPago>());

        foreach (var e in hijos)
        {
            if (e.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException(
                    "Los renglones y pagos de un documento fiscal no se pueden modificar ni eliminar.");
        }
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
