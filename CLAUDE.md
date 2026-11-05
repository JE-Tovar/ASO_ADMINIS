# ASO Adminis — sistema de gestión para venta de artículos

Aplicación de gestión de escritorio: **WPF · .NET 8** (`net8.0-windows`), instalación local en
LAN, una sola organización por instalación. Es un sistema **genérico de venta de artículos**,
pensado para servir a cualquier comercio (zapatería, venta de pintura, ferretería…) sin
modificar el modelo de datos: **Ventas** y **Reportes** sobre **Inventario** y **Finanzas**.

## Origen de este proyecto (2026-09-13)

Este proyecto nace clonando **ASO_GENERIC**, la plantilla de arranque para aplicaciones de
gestión de escritorio (armazón técnico completo + Finanzas + Inventario de ejemplo), y
renombrando namespace/nombre de la app (`ASO_ADMINIS`/`ASO Adminis`) para que no arrastre nada del
cliente anterior. ASO_GENERIC a su vez viene de **ASO_RTR**, que venía de **ASO**
(`ASO_SOFTWARE_BASE`, un sistema de cosecha mecanizada de caña de azúcar): ASO_RTR redujo ASO a su
armazón técnico completo más un único módulo de ejemplo (Finanzas · Cuentas por Pagar y Banco), le
sumó un módulo de Inventario construido de cero contra ese armazón, y luego ASO_GENERIC quitó los
módulos de negocio propios de ASO_RTR (Nómina, Operaciones, Catálogo — específicos de una planta
de embotellado) para quedar como plantilla neutra, sin negocio real.

Sobre ese armazón se agregaron después **Ventas** (registro de ventas que descuenta el almacén),
**Reportes** (ventas y gastos) y la **tasa de cambio BCV**. Un primer intento lo orientó a una
zapatería (módulo Catálogo con Marcas y Modelos); el 2026-11-03 se revirtió para dejarlo
genérico: **lo que se vende es directamente el `Articulo` de Inventario · Almacén**, con su
precio de venta y una categoría de texto libre, así que sirve igual para calzado que para
pintura. No hay marcas ni categorías fijas de ningún rubro — si un cliente concreto las
necesita, se agregan para él, no en la base.

Finanzas e Inventario, además, cubren los cuatro patrones que vale la pena tener a la vista al
construir un módulo nuevo:

- **CRUD simple** — `Proveedor`.
- **Documento con líneas** — `FacturaProveedor`, `EntradaInventario`/`SalidaInventario`.
- **Documento con máquina de estados** — `CuentasPorPagarService`, `BancoService`,
  `EntradasInventarioService`/`SalidasInventarioService`.
- **Contenedor de dos padrones** — `CuentasPorPagarViewModel` (facturas + proveedores),
  `BancoViewModel` (movimientos + cuentas).
- Y, solo en Inventario: **existencia derivada** (kardex calculado, no tecleado) y **grilla de
  líneas editable** (`Controls/PuenteDeDatos.cs`), que no tenía ningún otro módulo del scaffold.

La migración de EF Core también se reinició: `Migrations/` de este proyecto empieza en una
migración `Baseline`, generada contra el modelo ya recortado — no había una migración limpia que
heredar una vez que se quitaron las tablas de los tres módulos eliminados.

## Cómo ejecutar

Es escritorio, no web (no hay dev server / puerto). `dotnet run` dentro de
`ASO_ADMINIS.Desktop`, o F5 en Visual Studio (`ASO_ADMINIS.slnx`).

## Decisiones de arquitectura (heredadas, sin cambios)

- **WPF con MVVM ligero**: `ViewModels/ViewModelBase.cs` (INotifyPropertyChanged). Lógica fuera
  del code-behind.
- **Datos detrás de interfaces** (`Services/I<X>DataSource.cs`), resueltas en
  `Configuration/DataSourceFactory.cs`. La UI y los ViewModels no conocen EF Core.
- **Sin mocks**: el único camino es SQL Server vía EF Core. Un modo "sin BD" no sobrevive al
  aislamiento por organización, porque los mocks no pasan por el filtro global de EF.
- **Regla de oro**: toda regla de negocio vive en servicios de dominio, nunca en eventos de
  botones.
- **Autorización en capas**: hoy solo la capa 1 (RBAC por permiso de comando) y la 3
  (segregación de funciones: aprobador ≠ solicitante, en `PeticionService`) están completas. La
  autorización se comprueba en el `CanExecute` de los comandos y, para las transiciones propias
  de un documento, dentro del servicio de dominio (`CuentasPorPagarService`, `BancoService`
  exigen `ISesionActual` por constructor). El CRUD genérico (`CrudViewModelBase`) todavía escribe
  directo contra el `IDataSource` sin repetir la comprobación — hueco puntual heredado, ver
  "Próximo paso sugerido".

## Estructura de módulos

Hoy hay **cuatro módulos de negocio**: **Ventas** (Registro de ventas), **Finanzas** (Cuentas por
Pagar, Banco y Proveedores), **Inventario** (Almacén, Entradas y Salidas) y **Reportes** (Ventas y
Gastos, de solo lectura, con gráficos OxyPlot), más **cuatro módulos fijados** sin
submódulos: **Inicio**, **Peticiones** (bandeja de solicitudes de cambio), **Administración**
(usuarios con sus permisos, y los datos de la propia organización) y **Configuración**
(apariencia, cuenta propia, preferencias de la máquina — anclada al pie del sidebar, fuera de su
`ScrollViewer`, porque no es trabajo del día).

- `Navigation/ModuloCatalogo.cs` — **fuente única** de la estructura (clave, nombre, descripción,
  icono, submódulos). Sidebar, Inicio, dashboard y enrutado leen de aquí.
- `Controls/Sidebar.xaml(.cs)` + `ViewModels/SidebarViewModel.cs` — menú de dos niveles; solo
  emite `NavegacionSolicitada`. `MainWindow.Navegar(modulo, submodulo)` decide la vista.
- `Views/InicioView` — lanzador con una tarjeta por módulo.
- `Views/ModuloDashboardView` — resumen del módulo: indicadores + tarjeta por submódulo. Los
  valores los calcula `ModuloDashboardViewModel.CalcularIndicadores` (un `switch` por clave de
  módulo) — hoy con los casos `"Ventas"`, `"Finanzas"` e `"Inventario"`.
- `Views/SubmoduloView` — submódulo en construcción, para cuando se agregue uno nuevo al catálogo
  antes de tener su pantalla real.
- Framework CRUD reutilizable (`CrudViewModelBase`, `CrudEditorViewModelBase`, `CrudEditorWindow`,
  `IServicioDialogo`), login/sesión y capa de datos (`Models`, `Services`, `BD`,
  `DataSourceFactory`).

## Cómo se agrega un submódulo (receta)

Archivos a **crear**: `Models/<X>.cs` (con `Clonar()`, y **`: IDeOrganizacion` con su
`OrganizacionId`** si la entidad pertenece a una organización; los modelos NO implementan
INotifyPropertyChanged) · `Services/I<X>DataSource.cs` + `BD/Sql<X>DataSource.cs` ·
`Services/<X>Service.cs` si es documento (métodos `PuedeX` puros para el `CanExecute` +
transiciones que revalidan y lanzan `InvalidOperationException` en español) ·
`ViewModels/<Submodulo>ViewModel.cs` · editores · vistas XAML.

La fuente de datos es una línea, no una clase: hereda de `SqlCrudDataSource<T, TId>` y solo
declara las consultas que sean suyas. Si la entidad es una raíz con hijos que se guardan juntos,
hereda de `SqlAgregadoDataSource<T, TId>` en su lugar (ver `FacturaProveedor`/`FacturaProveedorLinea`
como ejemplo de documento con líneas). El ViewModel de la pantalla hereda de
`PantallaViewModelBase`, o de `PantallaCrudViewModel<T, TId>` si además es el listado CRUD de un
maestro; las dos ramas cumplen `IPantalla`, que es lo que el shell enruta.

Archivos a **modificar** siempre: `Configuration/DataSourceFactory.cs` (campo cacheado `??=`),
`BD/DbContext.cs` (`DbSet` + configuración en `OnModelCreating`), la tabla `Pantallas` de
`MainWindow.xaml.cs` (una línea por clave de submódulo), `Styles/PantallaTemplates.xaml` (un
`DataTemplate` por pantalla, o el área de contenido muestra el nombre del tipo del ViewModel),
`Styles/EditorTemplates.xaml` (un `DataTemplate` por editor, o la ventana sale vacía) y
`Styles/Theme.xaml` (un `Chip…Style` por enum de estado nuevo, con
`BasedOn="{StaticResource ChipBaseStyle}"`). Después, `dotnet ef migrations add`.

El permiso de navegación **no se declara**: `Submodulo.Permiso` lo deriva de la clave
(`Ver.<clave>`), así que basta con dar de alta el submódulo en `ModuloCatalogo`. Lo que sí hay
que hacer es sumarlo al rol que corresponda en `Services/MatrizPermisos.cs`, o no lo verá nadie
salvo el Desarrollador.

Arquetipos a copiar:

- **Finanzas · Cuentas por Pagar** — `Models/Proveedor.cs` (CRUD simple),
  `Models/FacturaProveedor.cs` (documento con líneas), `Services/CuentasPorPagarService.cs`
  (documento con máquina de estados), `ViewModels/CuentasPorPagarViewModel.cs` (contenedor de dos
  padrones).
- **Inventario** — kardex derivado (`InventarioService.ExistenciasPorArticulo`), documento con
  líneas editables en vivo (`ViewModels/EntradasViewModel.cs` + `Controls/PuenteDeDatos.cs`), y
  acoplamiento entre módulos (`EntradasInventarioService` exige `CuentasPorPagarService`).

## Inventario

Tres submódulos: **Almacén** (catálogo de artículos con su existencia), **Entradas** (lo que
llega) y **Salidas** (boletos de salida). Las decisiones que no se deducen del código:

- **La existencia NO se guarda: se deriva.** `Articulo` no tiene columna de existencia;
  `InventarioService.ExistenciasPorArticulo()` la calcula como Σ líneas de entradas − Σ líneas de
  salidas, sin contar documentos anulados. `Articulo.Existencia` es una propiedad `Ignore()`-ada
  que rellena el servicio antes de pintar, igual que `CuentaBancaria.SaldoActual`. Se eligió así
  porque un número editable a mano se desincroniza del historial y no deja rastro de por qué
  cambió; el precio a pagar es que **hay que refrescar la vista a mano** tras rellenarla, porque
  los modelos no notifican cambios (ver `AlmacenViewModel.Recargar`). El cálculo es una pasada por
  cada tabla sobre un diccionario, no una consulta por artículo.
- **Unidad base vs presentación.** `UnidadMedida` (enum, ordinal) es la unidad en que se cuenta
  la existencia y **no se alarga por rubro**. Las medidas propias de cada negocio (galón,
  cuñete, saco de 42,5 kg, caja de 12) son `Articulo.Presentaciones`: filas hijas del artículo
  (`OwnsMany`, tabla `PresentacionArticulo`) con nombre, factor (cuánto de la unidad base
  contiene) y precio opcional (0 = factor × precio base). `Articulo.OpcionesDePresentacion()`
  ofrece siempre primero la unidad base, que no se da de alta.
  - **El kardex no sabe nada de presentaciones**: `EntradaInventarioLinea.Cantidad` y
    `SalidaInventarioLinea.Cantidad` siguen en unidad base (`decimal(18,4)` para no redondear
    3 × 0,946 L). La línea de entrada guarda además `PresentacionNombre`, `Factor` y
    `CantidadPresentacion` como snapshot; precio y subtotal van por presentación ("2 cuñetes a
    40 $"), así la factura de Finanzas cuadra con la del proveedor. `EntradasInventarioService`
    comprueba que `Cantidad = CantidadPresentacion × Factor`.
  - **`Venta.Cantidad` y el precio van en la presentación vendida**; su salida vinculada
    descuenta `Venta.CantidadBase` (cantidad × factor).
  - Las **latas cerradas con existencia propia** (galón de blanco ≠ cuñete de blanco) no usan
    presentaciones: son artículos distintos en `Pieza`. Las presentaciones son para granel o
    para "compro por caja, vendo por pieza".
  - Las salidas manuales (boletos) siguen solo en unidad base.
- **El stock inicial se carga con una entrada de tipo `Ajuste`**, que es el único tipo que no
  genera cuenta por pagar. Un ajuste negativo es una salida con motivo `Merma` o `Traslado`; no
  hay entradas con cantidad negativa.
- **Registrar una entrada de compra crea su cuenta por pagar en Finanzas**, y la dependencia no es
  opcional: `EntradasInventarioService` exige `CuentasPorPagarService` por constructor, igual que
  éste exige `BancoService`. La factura se escribe **antes** que la entrada, por el mismo motivo
  que el asiento de banco va antes de marcar una factura pagada: es la operación que puede
  rechazar. Si aun así la entrada fallara al guardarse, se retira la factura recién creada — no
  hay transacción que abarque las dos tablas, porque cada fuente de datos abre su propio contexto.
- **El anti-duplicado ya existía**: `CuentasPorPagarService.Validar` rechaza un número de documento
  repetido para el mismo proveedor, que es exactamente el caso "la misma factura se cargó dos
  veces". No hizo falta un `GetByOrigen` como el de Banco.
- **El comercio de una compra externa entra al padrón de proveedores** (se busca por nombre, se da
  de alta solo si no estaba), porque la deuda tiene que quedar a nombre de alguien y así se paga
  por el camino de siempre. Riesgo conocido: un nombre mal escrito crea un proveedor duplicado.
- **Anular una entrada** exige dos cosas, comprobadas antes de escribir nada: que el almacén no
  quede en negativo, y que su factura no esté ya pagada (eso se deshace con una nota de crédito).
  Anular una salida no exige nada: la existencia vuelve sola porque el kardex ignora lo anulado.
- **Los correlativos** (`ENT-000123`, `SAL-000123`) los asigna el servicio al registrar, con
  "el último + 1", y el índice único `(OrganizacionId, Numero)` es la red por si dos puestos
  coincidieran. Se persisten, no se derivan del `Id`, porque la descripción de la cuenta por pagar
  cita el número de la entrada y hace falta antes de insertar.
- **Quién autoriza un boleto no es un campo del formulario**: lo estampa el servicio con el usuario
  de la sesión, para que no se pueda escribir otro nombre en el papel.
- **`Agregar()` de `CrudViewModelBase` es `protected virtual`** (igual que `Editar` y
  `Eliminar`): Entradas y Salidas lo redefinen para que el alta pase por el servicio de dominio y
  no escriba directo contra la fuente de datos.
- **`Controls/PuenteDeDatos.cs`** es genérico: las columnas de un `DataGrid` no están en el árbol
  visual y no heredan `DataContext`, así que un `Binding` puesto en una columna falla en
  silencio. Lo usan las dos grillas de líneas (Entradas, Salidas) para ocultar la columna de
  precios en un ajuste y para llegar al comando de quitar línea.

## Ventas, Reportes y tasa de cambio

- **El artículo ES el producto.** No hay un catálogo aparte de Inventario: `Articulo` lleva
  `PrecioVenta` (US$, de referencia) y `Categoria` (texto libre, sin enum de rubro). Así el mismo
  sistema sirve a una zapatería o a una pinturería sin tocar el modelo.
- **`Venta` es de una sola línea** (un artículo, cantidad, precio, cliente opcional), no un
  carrito. Guarda snapshots (`ArticuloCodigo`, `ArticuloNombre`, `PrecioUnitarioUsd`,
  `TasaCambioUsada`) para que un cambio posterior de precio, tasa o nombre no altere ventas ya
  hechas. El editor propone `Articulo.PrecioVenta`, pero el precio es editable en cada venta.
- **Método de pago** (`Models/Venta.cs`, enum `MetodoPago`, ORDINAL append-only): obligatorio en
  altas nuevas (`VentasService.Validar`), sin valor preseleccionado en el editor para que nadie
  lo deje puesto por inercia. La columna es nullable solo por las ventas anteriores a
  `AgregarMetodoPagoVenta`, que se muestran como "No indicado". Reportes · Ventas lo resume en una
  tabla por método (montos exactos, para cuadrar caja).
- **Registrar una venta genera una `SalidaInventario` vinculada** (`SalidaInventario.VentaId`),
  y así el kardex la cuenta sin que Inventario sepa nada de Ventas; anular la venta anula su
  salida. `VentasService` escribe la salida directo contra su fuente de datos, no por
  `SalidasInventarioService`, porque las reglas de un boleto (quién retira, a qué área) no aplican.
- **Tasa BCV**: `TasaCambioService` la trae de DolarAPI al iniciar sesión (sin internet sigue con
  la última guardada) y se puede forzar en Administración · Organización. La tasa vigente se ve
  para todos los roles al pie del sidebar; se corrige sola porque `ActualizarDesdeApi` dispara
  el evento estático `TasaCambioService.TasaActualizada` (el sidebar se desuscribe en
  `Unloaded`, porque la ventana se reconstruye en cada sesión). Los equivalentes en Bs
  (`Articulo.PrecioVentaBs`) son propiedades `Ignore()`-adas que rellena el ViewModel, igual que
  `Existencia`.
- **Reportes** es de solo lectura: Ventas agrupa por día y por `Articulo.Categoria` (vacía →
  "Sin categoría"); Gastos son los pagos reales a proveedores.

## Persistencia

Las entidades de dominio persisten en **SQL Server vía EF Core Migrations**.

- **Migraciones** en `ASO_ADMINIS.Desktop/Migrations/`: `Baseline` (modelo recortado:
  `Organizacion`, `Usuario`, `PermisoUsuario`, `PeticionCambio`, `Proveedor`,
  `FacturaProveedor`+`FacturaProveedorLinea`, `CuentaBancaria`, `MovimientoBanco`, `Articulo`,
  `EntradaInventario`+`EntradaInventarioLinea`, `SalidaInventario`+`SalidaInventarioLinea`),
  `AgregarCatalogoMarcasModelos`, `AgregarTasaCambio`, `AgregarVentas`,
  `QuitarCatalogoMarcasModelos`, `AgregarMetodoPagoVenta` y `AgregarPresentacionesArticulo`
  (esta última rellena los documentos previos como cargados en unidad base, factor 1). Esta última deshace el enfoque zapatería **sin perder datos**:
  su `Up` está reordenado a mano para pasar el precio de cada modelo a su artículo y repuntar
  `Ventas.ModeloId` → `ArticuloId` antes de borrar `Marcas`/`Modelos`. Si se edita una
  migración generada, revisar el script con `dotnet ef migrations script`.
- **La cadena de conexión vive solo en `appsettings.local.json`** (por máquina, en `.gitignore`);
  la de `appsettings.json` (clave `ConnectionStrings:AsoAdminisDb`) apunta a LocalDB con un
  `.mdf` en `App_Data`.
- **No hay claves foráneas reales** en las tablas planas: las relaciones son `int` sueltos y la
  integridad es de la aplicación, con snapshots de texto (`…Nombre`) en cada documento.

## Una sola organización por instalación

**Una instalación atiende a una sola organización.** Ningún formulario pregunta a qué
organización pertenece algo: se estampa la de la organización instalada. **No hay ningún nivel
intermedio** — si el negocio real necesita varias plantas o líneas bajo la misma organización,
ese nivel hay que diseñarlo cuando se conozca la necesidad real (ver PROVISIONAL más abajo).

- **`Models/IDeOrganizacion.cs`** marca las entidades sujetas al ámbito.
- **`BD/DbContext.cs` hace todo el trabajo, en dos sitios:**
  - `AplicarFiltroDeOrganizacion` recorre el modelo y pone un `HasQueryFilter` a cada
    `IDeOrganizacion`. Es **fail-closed**: sin ámbito fijado no se ve nada, en vez de verse todo.
  - `SaveChanges()` estampa `OrganizacionId` en toda fila nueva, y en una fila modificada
    conserva el valor original (no el que traiga el formulario) para no dejar filas huérfanas.
- **`Services/Ambito.cs`** guarda la organización activa. La fija `SesionActual.IniciarSesion` a
  partir del usuario y **no cambia mientras dure la sesión**.
- **`IgnoreQueryFilters()`** se usa solo en el login (todavía no hay ámbito fijado), en
  `BD/SqlUsuarioDataSource.cs`.
- **`Organizaciones` no lleva filtro** — es la tabla que define el ámbito. La fila nace en el
  primer arranque (`Views/PrimerArranqueView`) y se corrige después en Administración ·
  Organización.

## Roles y permisos

Cuatro roles genéricos (`Models/Rol.cs`), cada uno con un conjunto base en
`Services/MatrizPermisos.cs` que el administrador ajusta por usuario con `PermisoUsuario`
(concede o revoca; **revocar gana**).

| Rol | Alcance |
|---|---|
| **Operador** | El día a día: crea/edita proveedores y facturas; mantiene el catálogo de artículos y registra entradas y salidas de almacén. No mueve dinero, no anula ni borra nada |
| **Supervisor** | Todo lo de Operador, más registrar pagos (dispara el asiento en Banco), administrar cuentas bancarias, anular documentos de almacén, eliminar, y resolver peticiones de su dominio |
| **AdministradorOrganizacion** | Todo dentro de la organización, salvo crear usuarios Desarrollador |
| **Desarrollador** | Todos los permisos, y es el único que reparte su propio rol |

- **`Services/Permisos.cs`** es el catálogo de cadenas, formato `"Modulo.Accion"`. Los de
  navegación llevan prefijo `Ver.` y se **derivan de la clave del submódulo**.
- **`Rol` se persiste como ORDINAL** (`Usuarios.Rol int`, sin `HasConversion`): los miembros se
  añaden **siempre al final**. Añadir un rol es enum + conjunto en `MatrizPermisos` + el array
  `asignables` de `UsuarioEditorViewModel` (escrito a mano, no `Enum.GetValues`).
- **`SesionActual`** calcula el conjunto efectivo **una vez al entrar** y lo cachea: un cambio de
  rol o de permisos se aplica al volver a iniciar sesión, no en caliente.
- **Contraseñas**: PBKDF2-SHA256 con salt por usuario y 210 000 iteraciones
  (`Services/Passwords.cs`). No hay usuarios sembrados ni contraseñas por defecto: en la primera
  ejecución contra una base sin usuarios, `Views/PrimerArranqueView` pide el nombre de la
  organización, su código, y crea el usuario Desarrollador.

## Peticiones de cambio

Cuando a un rol le falta un permiso **de los sensibles** (`MatrizPermisos.Solicitables`), el botón
puede pedir el motivo con el `MotivoEditorViewModel` de siempre y dejar una `PeticionCambio` en la
bandeja del administrador (módulo fijado **Peticiones**). El mecanismo es genérico y está
completo (`PeticionService`, segregación de funciones, auditoría de la decisión); lo que está
vacío es la lista `Solicitables` — hoy no hay ninguna acción sensible gateada en las pantallas
que ve un Operador. Al agregar un módulo real con acciones sensibles (anular, aprobar, etc.),
sumarlas ahí y cablear el comando con `Services/SolicitudesDeCambio.cs`.

## El sistema visual

Tokens en `Styles/Tokens.xaml`, dos paletas en `Colors.xaml` / `ColorsOscuro.xaml` superpuestas en
caliente por `Services/Tema.cs`, controles de WPF re-estilados en `Styles/Controles.xaml` y
`Componentes.xaml` para que el tema oscuro no deje cajas blancas. Las cuatro reglas de siempre
siguen aplicando al escribir XAML nuevo:

1. Color por `DynamicResource` a una clave de `Colors.xaml`, nunca un hex a mano.
2. Una clave nueva de color va en **las dos** paletas (`Colors.xaml` y `ColorsOscuro.xaml`).
3. No se anima un brush: se anima la opacidad de un velo o borde superpuesto.
4. Sombra solo en lo que flota (`Effect="{DynamicResource SombraFlotante}"`), nunca en una
   tarjeta de datos.
5. **Todo relleno de acento usa `PrimaryButtonBrush` con `OnAccentBrush` encima; `PrimaryBrush`
   es solo color de texto/icono, nunca fondo.** El acento de este scaffold (ámbar) es CLARO, así
   que `PrimaryBrush` (un ámbar oscurecido, solo texto) y `PrimaryButtonBrush` (el ámbar literal,
   solo relleno) son dos claves distintas a propósito — pintar un fondo con `PrimaryBrush` no da
   el color que se espera. Ver el comentario de cabecera de `Colors.xaml` para el detalle de
   contraste de cada escalón de la familia.

## Marca

`Colors.xaml`/`ColorsOscuro.xaml` traen una paleta ámbar/azul **placeholder**, con los contrastes
ya calculados (ver su comentario de cabecera).

**Logo (2026-11-04): una caja registradora**, glifo plano de una sola tinta en una cuadrícula
24×24 (visor del cliente, ticket, cuerpo con teclado, gaveta con tirador). Vive en dos sitios
que **hay que mantener iguales**:

- **En la app, vectorial**: `LogoCajaGeometry` (`Styles/Componentes.xaml`, `FillRule="EvenOdd"`
  para recortar teclas y tirador), pintado con un `Path` `Fill=OnAccentBrush` sobre el tile
  `PrimaryButtonBrush` en `Controls/Sidebar.xaml` (36 px) y `Views/LoginView.xaml` (56 px). Al ser
  vector sigue al tema sin mapa de bits.
- **En Windows, rasterizado**: `Assets/Logo/aso-adminis.ico` (16–256 px; a ≤24 px va sin ticket y
  con teclado 2×2), generado por `python Assets/Logo/generar_icono.py` (Pillow), que dibuja la
  misma geometría con los hex de `PrimaryButtonColor`/`OnAccentColor` escritos en el script.
  Es `ApplicationIcon` (el `.exe`) y `<Resource>` para el `Icon=` de `MainWindow`, `LoginView`,
  `PrimerArranqueView` y `CrudEditorWindow` (eso es lo que pinta la barra de tareas y Alt+Tab).
  **Si cambia el color de marca, regenerar el `.ico`.**

Al adoptar este scaffold para un negocio real:

1. Sustituir la familia de marca en las dos paletas (`PrimaryButtonColor`, `PrimaryColor`,
   `AccentBlueColor`, `InfoColor`, `NavIndicatorColor`, `FocusRingColor`, y los neutros de
   navegación con tinte de marca) y **recalcular los contrastes** descritos en el comentario de
   `Colors.xaml` — no basta con cambiar los hex, hay que verificar que sigan pasando AA.
2. Si el cliente trae su propio logo: reemplazar `LogoCajaGeometry` (si es vectorizable a un
   `Path`) o el `Path` por una `<Image>` rasterizada (no hay lector de SVG en el proyecto), y
   sustituir `Assets/Logo/aso-adminis.ico` conservando el nombre.
3. Cambiar el texto "ASO Adminis" en `Sidebar.xaml`, `LoginView.xaml`, `MainWindow.xaml`
   (`Title`) y `PrimerArranqueView.xaml` (`Title`), y `Product`/`Company`/`Description` en el
   `.csproj`.

## Configuración y preferencias

Tres pestañas en `Views/ConfiguracionView`: **Apariencia** (tema, escala), **Mi cuenta** (ficha +
cambiar contraseña) y **Aplicación** (hoy solo "abrir en la última sección"; no lleva permiso
propio porque no decide nada del negocio). Las preferencias NO van a la base de datos: viven en
`%AppData%\ASO Adminis\ajustes.json` (`Models/AjustesApp.cs` + `Configuration/AjustesStoreJson.cs`).

## PROVISIONAL — decisiones pendientes, marcadas a propósito

El sistema es genérico a propósito (sirve a cualquier comercio). Lo siguiente son placeholders
deliberados, fáciles de revisar y cambiar al adoptarlo para un cliente concreto; no son bugs:

1. **Marca**: la paleta ámbar/azul sigue siendo placeholder (el logo de caja registradora ya
   existe) — ver "Marca" arriba.
2. **Roles genéricos**: `Operador` / `Supervisor` / `AdministradorOrganizacion` / `Desarrollador`
   no están atados a ningún puesto real. Ajustar los nombres y los conjuntos base en
   `Models/Rol.cs` / `Services/MatrizPermisos.cs` en cuanto el negocio real esté definido — son
   *el único* rediseño pendiente, porque los cuatro roles conservan intacto el mecanismo
   (ordinal append-only, permisos por deltas, revocar gana).
3. **Jerarquía física**: hoy es "una organización, sin nivel intermedio". Si el negocio real
   tiene varias plantas o líneas de producción bajo la misma organización, diseñar ese nivel
   cuando se conozca la necesidad real — no está prefigurado en el modelo de datos actual.
4. **Nombre de la empresa / razón social**: el `.csproj` usa "ASO Adminis" como placeholder en
   `Product`/`Company`, a falta de la razón social real del cliente.
5. **Ventas mínimo**: una venta es de una sola línea, con un único método de pago (no pagos
   mixtos), el cliente es texto libre y no hay cuentas por cobrar. Es deliberadamente genérico; crecerlo (carrito multi-artículo,
   padrón de clientes, cobro) con la receta de "Cómo se agrega un submódulo", usando
   `EntradaInventario` como modelo de documento con líneas editables.
6. **`Solicitables` vacío**: no hay ninguna petición de cambio configurada todavía — ver
   "Peticiones de cambio" arriba. Los dos candidatos naturales ya existen:
   `EntradasInventario.Anular` y `SalidasInventario.Anular`, que hoy un Operador simplemente no
   ve.
7. **Pantalla de datos de la organización**: `Administración · Organización` solo pide nombre y
   código; si el negocio real necesita más datos de la organización (dirección, RIF, etc.),
   agregarlos ahí.

## Próximo paso sugerido

1. **Ampliar Ventas sin atarlo a un rubro** (varias líneas por venta, clientes, pagos mixtos);
   lo específico de un cliente (marcas, tallas, colores…) se agrega para ese cliente, no aquí.
2. **Decidir los roles reales** del negocio y reemplazar los cuatro genéricos.
3. **Elegir el color de marca real** y actualizar lo descrito en "Marca" (y regenerar el `.ico`).
4. **Llevar la comprobación de permisos a los servicios de dominio** de cada módulo nuevo desde
   el principio (no repetir el hueco que tiene hoy el CRUD genérico).
