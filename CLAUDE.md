# ASO Adminis — sistema de gestión para venta de artículos

Aplicación de gestión de escritorio: **WPF · .NET 8** (`net8.0-windows`), instalación local en
LAN, una sola organización por instalación. El negocio real será **venta de artículos**, todavía
por construir — hoy trae el armazón técnico completo más dos módulos de ejemplo (Finanzas e
Inventario) que documentan los patrones a seguir para construirlo.

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

**Este clon todavía no tiene el módulo de negocio real** (venta de artículos) — eso queda para una
sesión siguiente, una vez definido con el cliente cómo funciona (catálogo, punto de venta, clientes,
etc.). Por ahora se conservan los dos módulos de ejemplo que trajo ASO_GENERIC porque, entre los
dos, cubren los cuatro patrones que vale la pena tener a la vista al construir el primer módulo
real:

- **CRUD simple** — `Proveedor`.
- **Documento con líneas** — `FacturaProveedor`, `EntradaInventario`/`SalidaInventario`.
- **Documento con máquina de estados** — `CuentasPorPagarService`, `BancoService`,
  `EntradasInventarioService`/`SalidasInventarioService`.
- **Contenedor de dos padrones** — `CuentasPorPagarViewModel` (facturas + proveedores),
  `BancoViewModel` (movimientos + cuentas).
- Y, solo en Inventario: **existencia derivada** (kardex calculado, no tecleado) y **grilla de
  líneas editable** (`Controls/PuenteDeDatos.cs`), que no tenía ningún otro módulo del scaffold.

La migración de EF Core también se reinició: `Migrations/` de este proyecto empieza (y por ahora
termina) en una única migración `Baseline`, generada contra el modelo ya recortado — no había
una migración limpia que heredar una vez que se quitaron las tablas de los tres módulos
eliminados.

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

Hoy hay **dos módulos de negocio de ejemplo**: **Finanzas** (Cuentas por Pagar, Banco y
Proveedores) e **Inventario** (Almacén, Entradas y Salidas), más **cuatro módulos fijados** sin
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
  módulo) — hoy con los casos `"Finanzas"` e `"Inventario"`.
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

## Persistencia

Las entidades de dominio persisten en **SQL Server vía EF Core Migrations**.

- **Migraciones** en `ASO_ADMINIS.Desktop/Migrations/`, una sola: `Baseline`, generada contra el
  modelo recortado (`Organizacion`, `Usuario`, `PermisoUsuario`, `PeticionCambio`, `Proveedor`,
  `FacturaProveedor`+`FacturaProveedorLinea`, `CuentaBancaria`, `MovimientoBanco`, `Articulo`,
  `EntradaInventario`+`EntradaInventarioLinea`, `SalidaInventario`+`SalidaInventarioLinea`).
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
ya calculados (ver su comentario de cabecera). `Assets/Logo/` está vacía a propósito: el sidebar
(`Controls/Sidebar.xaml`) y el login (`Views/LoginView.xaml`) muestran un monograma de texto ("A"
sobre `PrimaryButtonBrush`) en vez de una imagen, para no arrastrar el arte de ningún cliente
anterior.

Al adoptar este scaffold para un negocio real:

1. Sustituir la familia de marca en las dos paletas (`PrimaryButtonColor`, `PrimaryColor`,
   `AccentBlueColor`, `InfoColor`, `NavIndicatorColor`, `FocusRingColor`, y los neutros de
   navegación con tinte de marca) y **recalcular los contrastes** descritos en el comentario de
   `Colors.xaml` — no basta con cambiar los hex, hay que verificar que sigan pasando AA.
2. Si hay logo real: ponerlo en `Assets/Logo/` (rasterizado — no hay lector de SVG en el
   proyecto), declararlo `<Resource>` en el `.csproj`, volver a poner `ApplicationIcon` en el
   `.csproj`, y reemplazar el monograma de texto por la `<Image>` en `Sidebar.xaml`/`LoginView.xaml`
   más el `Icon=` de `MainWindow.xaml`/`LoginView.xaml`/`PrimerArranqueView.xaml`/
   `CrudEditorWindow.xaml` (esto último es lo que Windows pinta de verdad en la barra de tareas,
   Alt+Tab y el Administrador de tareas — no el ícono del `.exe`).
3. Cambiar el texto "ASO Adminis" en `Sidebar.xaml`, `LoginView.xaml`, `MainWindow.xaml`
   (`Title`) y `PrimerArranqueView.xaml` (`Title`), y `Product`/`Company`/`Description` en el
   `.csproj`.

## Configuración y preferencias

Tres pestañas en `Views/ConfiguracionView`: **Apariencia** (tema, escala), **Mi cuenta** (ficha +
cambiar contraseña) y **Aplicación** (hoy solo "abrir en la última sección"; no lleva permiso
propio porque no decide nada del negocio). Las preferencias NO van a la base de datos: viven en
`%AppData%\ASO Adminis\ajustes.json` (`Models/AjustesApp.cs` + `Configuration/AjustesStoreJson.cs`).

## PROVISIONAL — decisiones pendientes, marcadas a propósito

El rubro ya está definido (venta de artículos) pero el módulo todavía no se construyó. Lo
siguiente son placeholders deliberados, fáciles de revisar y cambiar; no son bugs:

1. **Marca**: paleta ámbar/azul placeholder y monograma de texto sin logo — ver "Marca" arriba.
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
5. **Módulo de negocio real (venta de artículos)**: hoy solo están los dos de ejemplo (Finanzas,
   Inventario). El módulo de ventas se agrega con la receta de "Cómo se agrega un submódulo",
   usando Finanzas para los cuatro patrones del framework e Inventario para un módulo construido
   de cero contra este armazón (kardex derivado, documento con líneas editables, acoplamiento
   entre módulos) — probablemente el más cercano a lo que necesita un punto de venta.
6. **`Solicitables` vacío**: no hay ninguna petición de cambio configurada todavía — ver
   "Peticiones de cambio" arriba. Los dos candidatos naturales ya existen:
   `EntradasInventario.Anular` y `SalidasInventario.Anular`, que hoy un Operador simplemente no
   ve.
7. **Pantalla de datos de la organización**: `Administración · Organización` solo pide nombre y
   código; si el negocio real necesita más datos de la organización (dirección, RIF, etc.),
   agregarlos ahí.

## Próximo paso sugerido

1. **Definir con el cliente el detalle del módulo de venta de artículos** (catálogo, punto de
   venta, clientes, medios de pago) y construirlo; Finanzas e Inventario ya dejan los cuatro
   patrones y el almacén del que tirará.
2. **Decidir los roles reales** del negocio y reemplazar los cuatro genéricos.
3. **Elegir el color de marca y el logo real** y actualizar lo descrito en "Marca".
4. **Llevar la comprobación de permisos a los servicios de dominio** de cada módulo nuevo desde
   el principio (no repetir el hueco que tiene hoy el CRUD genérico).
