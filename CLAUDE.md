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

Pruebas: `dotnet test ASO_ADMINIS.slnx` (proyecto `ASO_ADMINIS.Tests`, xUnit). Usan fuentes de datos
y emisor en memoria (`Dobles.cs`), así que no tocan SQL Server ni `%AppData%` (el diario fiscal va
a una carpeta temporal: `InicioDePruebas`); cubren `Rif`, `CalculoFiscal`, la numeración de
`VentasService`, ventas a crédito y cuentas por cobrar, cajas y turnos, y la bitácora. **Integración con SQL** (`IntegracionSqlTests`):
solo con `ASO_PRUEBAS_SQL=1` (PowerShell: `$env:ASO_PRUEBAS_SQL=1; dotnet test ASO_ADMINIS.slnx`);
crean una base LocalDB temporal con todas las migraciones y la borran al terminar. Prueban
rollback, candado, dos cajas vendiendo la última unidad, venta a crédito con cobro bancario e
índices de CxC, conflicto de edición, bitácora y su trigger. Sin la variable salen como omitidas. Ojo: si la compilación falla, no correr con
`--no-build` (correrían binarios viejos).

Instalación en otra PC: instalador y actualizaciones automáticas con **Velopack** (`Program.cs`,
`Services/Actualizaciones.cs`; cómo publicar una versión en el README). Instalada, `AppConfig`
guarda la base y `appsettings.local.json` en `%LocalAppData%\ASO Adminis`, fuera de lo que
reemplaza cada actualización. Guía para el que instala (una PC o varias cajas contra un SQL
Server Express): `INSTALACION.md`.

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

Hoy hay **cuatro módulos de negocio**: **Ventas** (Registro de ventas, Servicios, Clientes y
Cierre del día), **Finanzas** (Cuentas por Pagar, Cuentas por Cobrar, Gastos varios, Banco y Proveedores), **Inventario** (Almacén, Entradas y
Salidas) y **Reportes** (Ventas, Gastos y Libro de ventas, de solo lectura), más **cuatro módulos fijados** sin
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
  - **Tamaño de la ventana de edición:** cada editor elige uno de los tres anchos de `Ancho`, y
    el alto se ajusta al contenido.
  - **`AbreMaximizado`:** un editor puede pedir ventana maximizada y redimensionable. Lo hacen
    los editores de mucho uso: **Emitir factura** (distribución de punto de venta: renglones a
    todo el alto a la izquierda y un panel de cobro de 400 px a la derecha), **Registrar entrada**,
    **Boleto de salida** y la **nota de crédito/débito** (cabecera arriba, grilla de líneas en
    la fila `*`, total y observaciones abajo). La ventana principal también abre maximizada (`MainWindow.xaml`).
  - **Qué cambia en ese modo:** `CrudEditorWindow` apaga su scroll vertical para que las filas
    `*` del formulario llenen la ventana, y cada grilla lleva su propio scroll. Los mínimos
    (1000×640) los fija `ServicioDialogo`.
  - **Doble clic en una fila** (2026-11-13): `controls:DobleClicFila.Comando="{Binding
    AbrirCommand}"` en el `DataGrid` del listado (también responde a Enter). `AbrirCommand` de
    `CrudViewModelBase` hace lo mismo que Editar si la fila se puede editar; si no, muestra la
    ficha de solo lectura que devuelva `CrearDetalle(item)` (por defecto ninguna).
    - **Fichas:** `ViewModels/DetalleViewModels.cs` (`DetalleViewModelBase<T>`: botón
      "Cerrar", sin Cancelar, Esc cierra) y `Views/<X>DetalleView.xaml`, en la misma
      `CrudEditorWindow`. Hoy: venta (factura y notas), entrada, salida, gasto vario y factura
      de proveedor pagada o anulada, y cuenta por cobrar con su estado de cuenta y recibos. Los datos opcionales van con `controls:Dato`, que se oculta
      si el valor está vacío.
    - **En Reportes** (2026-11-15), que no son CRUD, cada pantalla tiene su `Seleccionada` y
      su `AbrirCommand`. Reportes · Ventas abre la ficha de la venta. Reportes · Gastos abre la
      del documento de origen por `FilaGasto.DocumentoId`: un pago a proveedor abre su factura,
      con lo comprado (los renglones copiados de la entrada), y un gasto vario abre su gasto.
      El Libro de ventas no la tiene: sus filas pueden estar consolidadas.
    - **Permiso:** ver la ficha solo pide acceso a la pantalla, no `…Editar`.

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
`Styles/EditorTemplates.xaml` (un `DataTemplate` por editor o ficha, o la ventana sale vacía) y
`Styles/Theme.xaml` (un `Chip…Style` por enum de estado nuevo, con
`BasedOn="{StaticResource ChipBaseStyle}"`). Después, `dotnet ef migrations add`. Si es un
documento que no se edita, darle su ficha (`CrearDetalle`) y poner `DobleClicFila` en su grilla.

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
- **Solo unidad base, sin presentaciones** (2026-11-13). `UnidadMedida` (enum, ordinal) es la
  unidad en que se cuenta la existencia, se compra y se vende, y **no se alarga por rubro**. La
  medida comercial (galón, cuñete, caja de 12) va escrita en la descripción, y si tiene
  existencia propia es otro artículo ("Pintura blanca galón" ≠ "Pintura blanca cuñete").
  - **Antes había presentaciones** (`Articulo.Presentaciones`, tabla `PresentacionArticulo`:
    nombre, factor y precio). La migración `QuitarPresentacionesArticulo` borró la tabla.
  - **Lo que queda en los renglones:** `PresentacionNombre`, `Factor` y `CantidadPresentacion`
    (entradas) y `PresentacionNombre`/`Factor` (ventas) se conservan porque los documentos
    viejos los usan y las ventas son inalterables. Los nuevos se escriben con la unidad corta y
    factor 1. No quitarlos: `DisponibleParaDevolver` agrupa por `(ArticuloId,
    PresentacionNombre)`, y la devolución de una factura vieja reingresa con su factor.
  - **El kardex** suma `Cantidad`, que siempre estuvo en unidad base (`decimal(18,4)`).
    `EntradasInventarioService` sigue comprobando `Cantidad = CantidadPresentacion × Factor`.
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
- **Borrar un artículo** (2026-11-13, `InventarioService.PuedeEliminar`): sin movimientos,
  siempre; con movimientos, solo si está inactivo y con existencia cero. El historial no se
  toca: los renglones de entradas, salidas y facturas guardan su snapshot de código y
  descripción. Se pierde solo lo que se lee del catálogo vivo: Reportes · Ventas agrupa esas
  ventas en "Sin categoría". Si no se puede, Almacén explica el motivo en vez de dejar el botón
  gris. Los servicios siguen la misma regla (`PuedeEliminarServicio`): uno ya facturado se
  borra si está inactivo; no hay existencia que comprobar.
- **Los correlativos** (`ENT-000123`, `SAL-000123`) los asigna el servicio al registrar, con
  "el último + 1", y el índice único `(OrganizacionId, Numero)` es la red por si dos puestos
  coincidieran. Se persisten, no se derivan del `Id`, porque la descripción de la cuenta por pagar
  cita el número de la entrada y hace falta antes de insertar.
- **Quién autoriza un boleto no es un campo del formulario**: lo estampa el servicio con el usuario
  de la sesión, para que no se pueda escribir otro nombre en el papel.
- **`Agregar()` de `CrudViewModelBase` es `protected virtual`** (igual que `Editar` y
  `Eliminar`): Entradas y Salidas lo redefinen para que el alta pase por el servicio de dominio y
  no escriba directo contra la fuente de datos.
- **Los renglones se agregan buscando** (Factura, Entradas y Salidas, 2026-11-12):
  `Controls/BuscadorArticulos` + `ViewModels/BuscadorArticulosViewModel`, encima de la grilla.
  Reemplaza el combo de artículo por renglón, que no escalaba con un catálogo grande.
  - **Cómo busca:** cada palabra escrita tiene que aparecer en código, descripción o categoría,
    sin acentos ni mayúsculas (`Services/Texto.Normalizar`). Se puede filtrar por categoría.
  - **Orden:** primero el código exacto, luego los códigos que empiezan igual, luego el resto
    por código. Muestra hasta 50 resultados.
  - **Al elegir:** Enter (código exacto, si no el resaltado) o clic llaman a
    `AgregarArticulo` del editor. Si el artículo ya está en su unidad base, le suma 1, pensado
    para lector de código de barras.
  - **Teclado:** el control marca Enter como manejado para que no dispare el botón
    `IsDefault` de la ventana (Emitir / Registrar). Con la lista abierta, Esc solo la cierra.
  - **Los renglones** ya no tienen combo de artículo: el código y la descripción son de solo
    lectura. Para cambiar un artículo se quita el renglón.
- **Venta a granel** (2026-11-12): es a granel lo que tiene unidad base kilogramo, litro o
  metro (`Articulo.EsGranel`, calculado, sin columna).
  - **Cantidades:** solo lo de granel lleva decimales en la factura; pieza, caja, saco y los
    servicios van en enteros (`VentasService.Validar`). `VentaLineas.Cantidad` es
    `decimal(18,3)`: antes tenía 2 decimales, y 0,125 kg se guardaba como 0,13, así que no
    cuadraba con el monto ni con la salida. Más de 3 decimales se rechaza, no se redondea.
  - **Cómo se agrega:**
    - Sin cantidad, un artículo a granel abre un renglón vacío (`PideCantidad`) y el cursor
      salta a la cantidad (`Controls/Enfoque`).
    - Enter ahí devuelve el foco al buscador y no emite.
    - "1,25*código" agrega esa cantidad del código exacto.
  - **Etiquetas de balanza** (`Services/EtiquetaBalanza`): EAN-13 de peso variable con
    prefijo (por defecto 20), PLU (5 dígitos), peso (5 dígitos, 3 decimales) y verificador.
    - **Configuración:** el formato es por máquina (`AjustesApp`, Configuración · Aplicación).
    - **Cómo encuentra el artículo:** el PLU se compara con `Articulo.CodigoBalanza`, sin
      ceros a la izquierda; ese campo solo existe para granel y no se repite.
    - **Etiqueta inválida:** si el verificador no cuadra, se busca como texto normal.
    - **PLU sin asignar:** el buscador avisa y no agrega nada.
    - **Pendiente:** etiquetas con precio en vez de peso, y balanza conectada por USB o serie.
- **`Controls/PuenteDeDatos.cs`** es genérico: las columnas de un `DataGrid` no están en el árbol
  visual y no heredan `DataContext`, así que un `Binding` puesto en una columna falla en
  silencio. Lo usan las dos grillas de líneas (Entradas, Salidas) para ocultar la columna de
  precios en un ajuste y para llegar al comando de quitar línea.

## Ventas: documentos fiscales (SENIAT), Reportes y tasa de cambio

Desde el 2026-11-04 Ventas cumple lo que el SENIAT exige a un software de ventas. El marco que se
tomó (revisarlo con el contador del cliente): la Providencia **SNAT/2026/00084** derogó la
homologación de software (la 2024/000121), y la **SNAT/2026/00093** (GO 43.484, vigente desde
abril de 2027) fija como medios de emisión el portal o la app del SENIAT, las **imprentas
digitales** y las **máquinas fiscales** con autorización previa. El papel queda solo para
**contingencias**. **El software nunca se asigna el número de control: lo da el emisor.**

- **El artículo ES el producto, y se trabaja por código primero.** `Articulo` se identifica por
  `Codigo`; no tiene "nombre" sino **`Descripcion`** (hasta 200 caracteres, clara y detallada),
  que es lo que se imprime en el renglón de la factura.
  - **Dónde se ve:** en combos y títulos aparece como `Codigo · Descripcion` (`Etiqueta`), y las
    listas de activos se ordenan por código.
  - **Genéricos:** `InventarioService.DescripcionGenerica` rechaza descripciones como "Artículo"
    o "Varios".
  - **Migración `ArticuloDescripcion`:** renombra la columna `Nombre` → `Descripcion` (editada a
    mano para no perder datos).
  - **Snapshots:** los de las líneas de Entradas y Salidas se siguen llamando `ArticuloNombre`,
    pero guardan la descripción.
- **El precio es fijo (2026-11-12).** Quien factura no puede cambiarlo: la columna de precio
  del editor es de solo lectura y `VentasService.FijarDesdeCatalogo` (en `EmitirFactura` y
  `RegistrarEnContingencia`, antes de `Preparar`) vuelve a leer de la base el precio del
  artículo (en su unidad base, factor 1), la alícuota, el código, la descripción y `EsServicio`, ignorando lo que
  mande la pantalla. Un artículo con precio 0 no se factura, y un renglón de factura sin
  `ArticuloId` se rechaza (los conceptos libres son solo de la nota de débito). No hay
  descuentos ni permiso para saltarse esto: el precio se cambia en el catálogo.
- **Servicios (Ventas · Servicios, 2026-11-12).** Un servicio (cambio de aceite, recarga de
  batería) es un `Articulo` con `Tipo = TipoArticulo.Servicio`, no una entidad aparte, para
  que `VentaLinea.ArticuloId`, las notas de crédito, Reportes · Ventas por categoría y el código
  único sirvan igual.
  - **Sin existencia:** `UnidadCorta` es "serv" y no entra en
    Almacén (`PasaFiltroExtra`), Entradas/Salidas (`InventarioService.ActivosConExistencia`
    solo devuelve bienes) ni en los contadores del panel de Inventario. El editor de facturas usa
    `InventarioService.ParaVender()` (bienes + servicios).
  - **En la factura:** `VentaLinea.EsServicio` es snapshot. Esos renglones no exigen existencia,
    no generan `SalidaInventario` y, en una nota de crédito, no reingresan nada.
  - **Catálogo:** `ServiciosViewModel`/`ServicioEditorViewModel`, permisos `Servicios.*` (mismo
    nivel que `Articulos.*`). El precio es obligatorio (> 0). Un servicio ya facturado solo
    se borra si antes se desactivó; sus facturas conservan el snapshot.
  - **SENIAT:** mismo documento fiscal que los bienes: descripción no genérica (se sumaron
    "Servicio", "Mano de obra", "Trabajo"…), cantidad, precio y alícuota (General salvo los
    exentos del art. 19 LIVA). Bienes y servicios pueden ir en la misma factura.
  - **Pendiente:** la retención de ISLR que un cliente agente de retención hace sobre
    servicios (Decreto 1808). No toca la factura ni el libro de ventas. Se haría como las
    retenciones de IVA recibidas.
- **Precio, categoría y alícuota.** El artículo lleva además `PrecioVenta` (US$, **sin IVA**),
  `Categoria` (texto libre) y `Alicuota` (`TipoAlicuota`: Exento/General/Reducida/Adicional,
  ordinal).
  - **Porcentajes:** no van en el artículo sino en `Organizacion` (`IvaGeneral` 16, `IvaReducida`
    8, `IvaAdicional` 31 = 16 + 15 suntuario, `IgtfPorcentaje` 3), porque los decreta el
    Ejecutivo.
- **Cliente de la factura** (2026-11-12): la factura pide RIF/C.I., nombre, dirección,
  teléfono y correo.
  - **Obligatorio** (2026-11-13): RIF o cédula, nombre y dirección (`VentasService.ValidarCliente`,
    también en contingencia); teléfono y correo son opcionales. Ya no se factura a consumidor
    final sin datos; el botón del editor es "Limpiar". Las notas no se validan así porque
    copian el cliente de su factura, que puede ser anterior. Consecuencia en el libro de
    ventas: solo las facturas viejas sin RIF se consolidan por Z; las nuevas van una por fila.
  - **Autocompletado:** el campo RIF/C.I. sugiere de la lista por identificación (sin guiones ni
    puntos) o por nombre (`ClientesService.Sugerencias`). Con la identificación exacta se
    autocompleta solo (`PorIdentificacion`: "v12345678" = "V-12.345.678").
    - Si luego deja de coincidir, se borra lo autocompletado que nadie tocó: al teclear una
      cédula, "V-1234567" puede ser otra persona a medio camino.
  - **Al emitir** (`ClientesService.RegistrarDesdeFactura`, llamado por `VentasService`):
    - un cliente nuevo se da de alta (`Clientes.Crear`);
    - uno existente se pone al día y se reactiva (`Clientes.Editar`); el teléfono y el correo
      vacíos no borran los de la ficha.
  - **Cuándo se registra:** después de validar y antes de emitir, porque la factura es
    inalterable y el `ClienteId` tiene que ir puesto al guardarla.
  - **Nunca impide facturar:** sin permiso, o si falla, la factura sale con su snapshot igual.
  - **Snapshots nuevos:** `Venta.ClienteTelefono` y `ClienteCorreo`. El correo es por donde una
    imprenta digital enviará la factura. Si se escribe un correo, tiene que tener forma de
    correo.
  - **Validación del padrón:** está en `ClientesService.Validar` (antes `ClienteValidacion`,
    en el ViewModel).
- **Datos del emisor** en `Organizacion` (`RazonSocial`, `Rif`, `DireccionFiscal`, `Telefono`,
  `ContribuyenteEspecial`), editables en Administración · Organización. Sin
  `DatosFiscalesCompletos` no se emite nada. **Caja del usuario** (`SesionActual.CajaActual`,
  ver "Cajas, cajeros y turnos"); sin caja tampoco se emite, y la factura además exige turno.
- **RIF** (`Services/Rif.cs`): normaliza a `J-12345678-9` y valida el dígito verificador con
  módulo 11. Letras: V=1, E=2, J=3, P=4, G=5. La letra se multiplica por 4 y los 8 dígitos por
  3,2,7,6,5,4,3,2; luego DV = 11 − (suma mod 11), con ≥10 → 0. Se verificó con `G-20000303-0`
  (SENIAT) y `J-00002961-0`. Una cédula (`V-12345678`, sin DV) se acepta para consumidor final.
  Se aplica en Organización (RIF obligatorio), Clientes y el cliente de cada documento (RIF o
  cédula) y **Proveedores** (opcional, pero si se escribe tiene que ser RIF con DV; se guarda
  normalizado y el duplicado se busca normalizado). La letra C (consejos comunales) no está
  soportada.
- **`Venta` es un documento fiscal** (`TipoDocumentoVenta`: Factura, NotaCredito, NotaDebito).
  - **Renglones y pagos:** `VentaLinea` (tabla `VentaLineas`) y `VentaPago` (tabla
    `VentaPagos`) son `OwnsMany`. Hay pago mixto: los métodos en divisa son `Venta.EsDivisa`
    (efectivo divisa, Zelle), y para ellos se guarda también el monto en US$.
  - **Snapshots:** se guardan el cliente (`ClienteRif`/`ClienteNombre`/`ClienteDireccion`;
    vacío = consumidor final, solo en documentos anteriores al 2026-11-13), la tasa, el precio en Bs y US$ y la alícuota y su % de cada
    renglón, los totales por alícuota, el IGTF, el total y el vuelto.
  - **Del emisor:** `NumeroFiscal`, `NumeroControl`, `SerialEmisor` y `NumeroZ`.
  - **Moneda:** los montos van en **Bs**; el US$ es solo referencia.
- **Venta a crédito y Finanzas · Cuentas por Cobrar** (`Models/CuentaPorCobrar.cs`,
  `Services/CuentasPorCobrarService.cs`): `Venta.CondicionPago` distingue Contado/Credito; no se
  reutiliza `MetodoPago.Credito`, que significa tarjeta. El crédito exige el permiso
  `Ventas.VenderACredito`, cliente persistido y vencimiento elegido al emitir. Puede llevar pagos
  iniciales: la deuda inicial es total − anticipos.
  - **Factura inalterable:** sus renglones y `Venta.Pagos` no cambian después. Cada abono posterior
    crea un recibo `REC-000001` y un `MovimientoCuentaPorCobrar`; no es un documento fiscal del
    SENIAT. Las cuentas nacen de facturas a crédito: no hay alta, edición ni borrado manual.
  - **Moneda:** total, anticipos, abonos y saldo se mantienen en **Bs**. `SaldoUsdReferencia` usa
    siempre `TasaCambioEmision`, no la tasa del cobro. Un recibo en divisa guarda además US$ y la
    tasa BCV usada para convertirlo.
  - **Cobranzas:** parciales o totales, sin sobrepago. Efectivo exige turno abierto propio y entra
    en su arqueo; los métodos bancarios crean una entrada en Banco por recibo. Anular exige motivo,
    revierte saldo y asiento, y se bloquea si el turno está cerrado o el asiento conciliado.
  - **Ajustes:** notas de crédito/débito y retenciones de IVA mueven el saldo en la misma
    transacción de su documento. Una nota posterior al cobro total puede dejar `SaldoAFavor`; no
    hay cuotas, intereses, mora ni límites automáticos.
  - **IGTF temporal:** para contribuyente especial se bloquean los cobros posteriores en divisas
    hasta acordar su tratamiento con el contador/proveedor fiscal. Los anticipos en divisas de la
    factura conservan el cálculo de IGTF ya existente.
  - **UI y permisos:** filtros, totales, vencimiento derivado, estado de cuenta, recibos y
    comprobante no fiscal en `CuentasPorCobrarViewModel` y sus vistas. Supervisor puede vender a
    crédito, cobrar y anular; Cajero no recibe esos permisos.
- **Cálculo** en `Services/CalculoFiscal.cs`, puro.
  - **Renglones:** se redondea cada uno a 2 decimales (precio Bs = US$ × tasa; monto; IVA).
  - **Bases e IVA:** son la suma de sus renglones.
  - **IGTF:** solo si `ContribuyenteEspecial`. Es el 3 % de lo cobrado en divisas (en Bs), con
    tope en el total antes del IGTF.
  - **En vivo:** el editor llama a `VentasService.Preparar`, que es lo mismo que se repite al
    emitir.
- **`VentasService`.** El orden es: validar → **emitir en `IEmisorFiscal`** → guardar → mover el
  almacén.
  - **Si falla el emisor,** no se guarda nada.
  - **Si falla el guardado después de emitir,** el documento ya existe para el fisco: se anota
    en `%AppData%\ASO Adminis\diario-fiscal.jsonl` y se avisa.
  - **Factura:** genera una `SalidaInventario` de N renglones (`VentaId`).
  - **Nota de crédito:** devuelve renglones de la factura a sus mismos precios y alícuotas, hasta
    lo que queda por devolver (`DisponibleParaDevolver`, por artículo + presentación). Reingresa
    la mercancía con una `EntradaInventario` de tipo **`DevolucionVenta`**, que no genera
    cuenta por pagar y no se anula.
  - **Nota de débito:** conceptos libres con alícuota; no mueve el almacén.
  - **Inmutable:** no hay `Anular`.
- **Inalterabilidad**: `AsoAdminisDbContext.ProtegerDocumentosFiscales` (en `SaveChanges`) rechaza
  borrar un `Venta`, o modificar cualquier columna suya, sus renglones o sus pagos.
  - **Únicas dos excepciones:** `Estado` PendienteTransmitir → Registrada (más
    `FechaTransmision`), y `NumeroZ` de nulo a un valor.
  - **Cómo se escriben esas dos:** con `IVentaDataSource.MarcarTransmitido` y `EstamparZ`, que
    cargan la fila rastreada. `Update` y `Delete` de `SqlVentaDataSource` lanzan, porque un
    `Update` desprendido marca todas las columnas y la guarda lo rechazaría igual.
- **Emisor intercambiable** (`Services/Fiscal/`): `IEmisorFiscal` (`Emitir`,
  `TransmitirContingencia`, `ReporteX`/`ReporteZ`, `TieneReportesXZ`, `EsFiscal`).
  - **Elección:** se hace en `appsettings(.local).json`, en la sección `EmisorFiscal` (`Tipo`,
    `Modo`, `Fallar`), a través de `EmisoresFiscales.Crear`.
  - **Implementación disponible:** hoy solo **`EmisorFiscalSimulado`**.
    - Puede portarse como máquina (con X/Z) o como imprenta digital.
    - Lleva sus correlativos en `%AppData%\ASO Adminis\emisor-simulado.json` y escribe cada
      documento en `...\documentos\`, marcado **SIN VALOR FISCAL**.
    - Con `Fallar: true` simula un emisor caído.
  - **PENDIENTE:** escribir el conector real (máquina HKA o API de una imprenta digital) cuando
    el cliente elija.
- **Contingencia:** si el emisor no responde (falla SEGURA, `EmisorFiscalException`), quien tenga
  `Ventas.Contingencia` registra la factura hecha en papel. Se cargan su n° y su n° de control.
  Queda `PendienteTransmitir` hasta que "Transmitir pendientes" la envía.
  - **Talonarios** (`Models/RangoContingencia.cs`, reglas puras en `Services/RangosContingencia.cs`,
    Administración · Contingencia, permisos `RangosContingencia.Crear/Editar`): el n° de control
    TIENE que caer en un talonario activo (prefijo + correlativo con N cifras) y no estar usado.
    **Fuera de rango se bloquea siempre**, y sin talonarios cargados no se registra ninguna
    contingencia. Se guarda escrito como en el formato ("1" → "00-000001"). El formulario propone
    el siguiente libre y avisa con ≤ 10 % libre. Los rangos no se solapan (mismo prefijo) y no se
    pueden achicar dejando afuera un número usado.
- **Comprobante no fiscal** (2026-11-18): desde la ficha de cualquier venta (doble clic en
  Registro), "Imprimir comprobante" y "Guardar PDF", en hoja carta. Lleva en grande "DOCUMENTO NO
  FISCAL" y en el pie cuál es el documento fiscal (n° y control del emisor). No se ofrece al emitir:
  el documento fiscal ya lo da el emisor. `Services/ComprobanteNoFiscal.cs` arma los datos una vez
  (los textos compartidos con la ficha están en `TextosDeVenta`); los dibujan `ComprobantePdf`
  (PDFsharp-MigraDoc, MIT; las fuentes de Windows con `FuentesWindows`, porque PDFsharp 6 no las
  busca solo) y `ComprobanteImpreso` (`FlowDocument` + `PrintDialog`). Colores fijos (el papel es
  blanco aunque el tema sea oscuro). Los datos del emisor salen de la organización ACTUAL: la venta no
  guarda copia. Sin permiso propio: quien ve la ficha puede sacar la copia.
- **Retenciones de IVA recibidas** (`RetencionIvaRecibida`, `RetencionesService`): son las del
  cliente agente de retención, del 75 % o el 100 % del IVA.
  - **Comprobante:** número de 14 dígitos; una retención por factura; el comprobante no se
    repite.
  - **Por qué es una fila aparte:** la factura es inalterable. Si tiene CxC con saldo, la
    retención lo reduce sin crear recibo, caja ni asiento bancario; no puede exceder ese saldo.
- **Cierre del día** (Ventas · Cierre, `CierreService`):
  - **Resumen diario:** por alícuota y medio de pago, neto de notas de crédito. Existe siempre.
  - **Con máquina fiscal:** hay reporte X y **Cierre Z** (`Ventas.CierreZ`). El Z se guarda en
    `CierreZ`, con lo que dice el sistema al lado ("Cuadra" o "Diferencia"), y se estampa su
    número en los documentos de la caja que no tenían Z.
- **Libro de ventas** (Reportes · Libro de ventas, `LibroVentasService`): se lleva por mes, o por
  quincena si la organización es contribuyente especial.
  - **Columnas:** las 16 del documento de requisitos, más la alícuota adicional, el tipo de
    documento y de transacción (01-REG, 02-COMP, 03-ANU), el serial/Z y las retenciones.
  - **Filas:** los documentos a contribuyentes van una por fila; el consumidor final con Z se
    consolida en una fila por Z ("RZ"); la contingencia va siempre en su fila. Las notas de
    crédito salen en negativo.
  - **Retenciones:** van en la fila de su factura, o en una fila "RET" si el comprobante es de
    otro período.
  - **Exportación:** a CSV (`;`, coma decimal, UTF-8 con BOM). Las columnas exactas se confirman
    con el contador.
- **Ventas históricas**: las ventas de una sola línea anteriores a `VentasDocumentoFiscal` se
  migraron como `EsHistorico` (un renglón, un pago si tenían método). No entran al libro.
- **Tasa BCV**: `TasaCambioService` la trae de DolarAPI al iniciar sesión (sin internet sigue con
  la última guardada) y se puede forzar en Administración · Organización.
  - **Pie del sidebar:** la tasa vigente se ve allí para todos los roles. Se corrige sola porque
    `ActualizarDesdeApi` dispara el evento estático `TasaCambioService.TasaActualizada`. El
    sidebar se desuscribe en `Unloaded`, porque la ventana se reconstruye en cada sesión.
  - **Equivalentes en Bs** (`Articulo.PrecioVentaBs`): son propiedades `Ignore()`-adas que
    rellena el ViewModel, igual que `Existencia`.
- **Reportes · Ventas** es de solo lectura y va en Bs, neto de notas. Agrupa por día y por
  `Articulo.Categoria` (por renglón) y suma por medio de pago desde `VentaPago`.

## Gastos varios y Reportes · Gastos (2026-11-05)

- **Finanzas · Gastos varios** (`GastoVario`, `GastosVariosService`): lo que se paga y no es una
  factura de proveedor (luz, alquiler, fletes…). Documento plano `GAS-000123`; no se edita ni se
  borra, se **anula** con motivo (`Finanzas.Anular`). Registrar pide `GastosVarios.Crear`
  (Supervisor en adelante: mueve dinero).
  - **Moneda Bs o US$.** Si es US$ se congelan `TasaCambio` (la BCV vigente en la fecha del
    gasto, `GastosVariosService.TasaEn`) y `MontoBs`; el reporte suma `MontoBs` y no se mueve
    cuando cambia la tasa.
  - **Categoría** es texto libre con sugerencias (`CategoriasBase` ∪ las ya usadas), combo
    editable (`EditableComboBoxStyle`).
  - **Cuenta opcional.** Sin cuenta = efectivo por fuera de Banco. Con cuenta (de la misma
    moneda, se valida) se escribe su asiento `OrigenMovimiento.GastoVario` con
    `BancoService.RegistrarGastoVario`; si el asiento falla se retira el gasto. Anular el gasto
    anula su asiento con `BancoService.AnularDerivados` (composición: no pide `Banco.Anular`).
- **Reportes · Gastos** (`ReporteGastosService`) junta, en Bs, las facturas de proveedor
  **pagadas** (por fecha de pago; su moneda es la de la cuenta de su asiento, y si es US$ se
  convierte con la tasa de ese día) y los gastos varios vigentes. Filtro por origen, totales por
  origen, barras apiladas por mes y torta por categoría.
- **Gráficos** (`ViewModels/Graficos.cs`): toda gráfica de Reportes se construye aquí. Toma los
  colores del tema activo, deja margen para la etiqueta de la barra más larga y la torta lleva
  el % dentro (máx. 6 porciones + "Otros"), para que nada se salga de la tarjeta. No crear
  `PlotModel` "pelados" en un ViewModel.
  - **Leyenda de la torta** (2026-11-15): `Graficos.Torta` devuelve un `GraficoTorta` (modelo +
    porciones con nombre, %, monto y color), y la pinta `Controls/TortaConLeyenda` con la lista
    WPF al lado, en la misma tarjeta. No es una leyenda de OxyPlot porque la `PieSeries` no pinta
    sus porciones en `Legends`: salía vacía y los colores quedaban sin nombre.

## Persistencia

Las entidades de dominio persisten en **SQL Server vía EF Core Migrations**.

- **Migraciones** en `ASO_ADMINIS.Desktop/Migrations/`: `Baseline` (modelo recortado:
  `Organizacion`, `Usuario`, `PermisoUsuario`, `PeticionCambio`, `Proveedor`,
  `FacturaProveedor`+`FacturaProveedorLinea`, `CuentaBancaria`, `MovimientoBanco`, `Articulo`,
  `EntradaInventario`+`EntradaInventarioLinea`, `SalidaInventario`+`SalidaInventarioLinea`),
  `AgregarCatalogoMarcasModelos`, `AgregarTasaCambio`, `AgregarVentas`,
  `QuitarCatalogoMarcasModelos`, `AgregarMetodoPagoVenta`, `AgregarPresentacionesArticulo`,
  `AgregarDatosFiscales`, `VentasDocumentoFiscal` (editada a mano: pasa cada venta vieja a un
  renglón + pago antes de borrar las columnas viejas; su `Down` lanza, no es reversible) y
  `AgregarCierreZRetenciones`
  (esta última rellena los documentos previos como cargados en unidad base, factor 1).
  Después vinieron `ArticuloDescripcion`, `AgregarGastosVarios` (tabla nueva, sin datos que
  migrar), `AgregarServicios` (`Articulos.Tipo` = 0/Bien y `VentaLineas.EsServicio` = false
  para lo existente) `VentaGranel` (`VentaLineas.Cantidad` a `decimal(18,3)`, agrandar sin
  pérdida; `Articulos.CodigoBalanza`), `VentaContactoCliente` (`Ventas.ClienteTelefono` y
  `ClienteCorreo`, vacíos para lo existente) y `QuitarPresentacionesArticulo` (borra la tabla
  `PresentacionArticulo`; los snapshots de los renglones se quedan). `QuitarCatalogoMarcasModelos` deshace el enfoque zapatería **sin perder datos**:
  su `Up` está reordenado a mano para pasar el precio de cada modelo a su artículo y repuntar
  `Ventas.ModeloId` → `ArticuloId` antes de borrar `Marcas`/`Modelos`. Si se edita una
  migración generada, revisar el script con `dotnet ef migrations script`.
- **Al abrir, `BD/Esquema.Actualizar` aplica lo pendiente con un candado** (`sp_getapplock`
  'ASO:Migraciones' de sesión, espera 3 min): EF Core 8 no serializa `Migrate` (eso es de EF 9), y
  con la 0.5.0 dos instancias abiertas a la vez chocaron ("Column name 'CajaId' … specified more
  than once"). **Un candado de sesión se suelta con `sp_releaseapplock`, nunca solo cerrando**: la
  conexión vuelve al pool con la sesión viva y el candado sigue puesto (minutos). Lo prueba
  `EsquemaSqlTests` (cuatro cajas a la vez sobre una base de la 0.4.0).
- **La cadena de conexión vive solo en `appsettings.local.json`** (por máquina, en `.gitignore`);
  la de `appsettings.json` (clave `ConnectionStrings:AsoAdminisDb`) apunta a LocalDB con un
  `.mdf` en `App_Data`.
- **Casi no hay claves foráneas reales** en las tablas planas: las relaciones son `int` sueltos y la
  integridad es de la aplicación, con snapshots de texto (`…Nombre`) en cada documento. Las
  únicas FK son las de cajas y turnos (`Usuarios.CajaId`, `Turnos.CajaId`, `Ventas.CajaId`,
  `Ventas.TurnoId`, todas `Restrict`) y las de los renglones `OwnsMany`. Las viejas se dejaron
  sin FK a propósito: una base real con huérfanos haría fallar la migración.

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

Cuatro roles (`Models/Rol.cs`): Cajero, Supervisor, AdministradorOrganizacion y Desarrollador,
cada uno con un conjunto base en
`Services/MatrizPermisos.cs` que el administrador ajusta por usuario con `PermisoUsuario`
(concede o revoca; **revocar gana**).

| Rol | Alcance |
|---|---|
| **Supervisor** | La oficina y su supervisión: emite facturas y mantiene clientes y servicios; proveedores y facturas de compra; catálogo de artículos, entradas y salidas de almacén; más notas de crédito/débito, contingencia, cierre Z, retenciones de IVA y libro de ventas; vender a crédito, registrar y anular cobros de clientes, registrar pagos (dispara el asiento en Banco) y gastos varios, administrar cuentas bancarias, anular documentos de almacén, eliminar, resolver peticiones de su dominio y cerrar el turno de otro cajero |
| **AdministradorOrganizacion** | Todo dentro de la organización, salvo crear usuarios Desarrollador |
| **Desarrollador** | Todos los permisos, y es el único que reparte su propio rol |
| **Cajero** | Solo vender en SU caja y dentro de su turno: Registro de ventas, Clientes y Cierre del día (abrir/cerrar su turno). No ve Finanzas, Inventario, Reportes, Peticiones ni las ventas de otras cajas. Obligatorio tener caja asignada |

- **`Services/Permisos.cs`** es el catálogo de cadenas, formato `"Modulo.Accion"`. Los de
  navegación llevan prefijo `Ver.` y se **derivan de la clave del submódulo**.
- **`Rol` se persiste como ORDINAL** (`Usuarios.Rol int`, sin `HasConversion`): los miembros se
  añaden **siempre al final**, y **no se borran**. El rol **Operador** (ordinal 0) se retiró el
  2026-11-18: el miembro quedó como `Rol.OperadorRetirado`, no se asigna y no da ningún permiso, y
  la migración `QuitarRolOperador` pasó a sus usuarios a Supervisor. Lo que hacía vive en
  `MatrizPermisos._oficina`, la base del Supervisor. Añadir un rol es enum + conjunto en `MatrizPermisos` + el array
  `asignables` de `UsuarioEditorViewModel` (escrito a mano, no `Enum.GetValues`).
- **`SesionActual`** calcula el conjunto efectivo al entrar y lo cachea (`Puede` es una búsqueda en
  tabla hash). **Se refresca en caliente** cada minuto (ver "Seguridad de las cuentas").
- **Contraseñas**: PBKDF2-SHA256 con salt por usuario y 210 000 iteraciones
  (`Services/Passwords.cs`). No hay usuarios sembrados ni contraseñas por defecto: en la primera
  ejecución contra una base sin usuarios, `Views/PrimerArranqueView` pide el nombre de la
  organización, su código, y crea el usuario Desarrollador.

## Cajas, cajeros y turnos (2026-11-18)

Antes la caja era un texto libre por PC (`AjustesApp.CajaCodigo`, hoy `[Obsolete]` y sin leer) y
no había diferencia entre una cuenta de caja y una de oficina. Ahora:

- **`Models/Caja.cs`**: padrón en Administración · Cajas (`CajasCrudViewModel`). Código único por
  organización, **fijo después de crearla** (ventas, turnos y Z guardan copia). No se borra, se
  desactiva (no existe `Cajas.Eliminar`).
- **La caja es del usuario, no de la PC**: `Usuario.CajaId`, que se elige en el editor de usuario.
  `SesionActual.IniciarSesion` la resuelve en `CajaActual` (una inactiva cuenta como ninguna). Un
  **Cajero** sin caja activa no entra: `SesionRechazadaException`, que el login muestra tal cual.
  Otros roles pueden tener caja (un supervisor que despacha) o no.
- **`Models/Turno.cs` + `Services/TurnosService.cs`**: se abre con fondo (Bs y US$) y se cierra con
  arqueo. **Un solo turno abierto por caja**, respaldado por el índice único filtrado
  `IX_Turnos_CajaId_Abierto`. Esperado = fondo + `EfectivoBs`/`EfectivoDivisa` de las facturas
  vigentes del turno − `Vuelto` (en Bs) + cobros de clientes en efectivo vigentes del turno. Las notas de crédito no llevan pagos, así que un
  reintegro en efectivo se anota en las observaciones. Si el arqueo no cuadra, el editor exige
  observaciones. Cerrar el turno de otro exige `Turnos.CerrarAjeno`, que **solo tienen Supervisor,
  Administrador y Desarrollador**: está en `MatrizPermisos.SoloSupervision`, así que
  `SesionActual` se lo quita a un Cajero aunque se lo concedan a mano, y la pantalla de permisos no
  deja marcarlo. El
  botón "Cerrar turno de otro cajero" de Cierre del día solo lo ve quien puede.
- **`VentasService.PuedeFacturar`**: caja + turno abierto **propio** en esa caja. Solo para la
  factura; notas y contingencia (Supervisor) solo piden caja. La factura guarda `CajaId` y
  `TurnoId`. Sin proveedor de turno (`turno:` nulo en el constructor) no se factura: fail-closed.
- **`Ventas.VerTodasLasCajas`**: sin él (el Cajero), Registro, resumen diario, Z y turnos se
  filtran a la caja propia. Se filtra en la vista, no en la fuente.
- **Dónde se ve**: chip ámbar/azul en la cabecera de `MainWindow` ("CAJA-01 · Turno desde 08:12" /
  "Sin turno"), que se repinta con `CambiosDeDatos`; tarjeta "Mi turno" y grilla "Turnos" en
  Ventas · Cierre del día; Configuración · Aplicación muestra la caja de solo lectura.
- **Migración `AgregarCajasYTurnos`** (editada a mano): crea una `Caja` por cada código distinto
  (mayúsculas, sin espacios) que haya en `Ventas` o `CierresZ`, por organización, y rellena
  `Ventas.CajaId`. Tras actualizar, **hay que asignar caja a cada usuario que venda**.

## Transacciones y concurrencia (2026-11-18)

Cada fuente `Sql…DataSource` abre su propio contexto por método, así que lo que toca dos tablas
no era atómico (había compensaciones a mano con `Delete` en los `catch`). Ahora:

- **`Services/UnidadDeTrabajo.cs`**: `UnidadDeTrabajo.Ejecutar(bloqueo, () => …)`. Estática como
  `Ajustes`; `App.OnStartup` pone `BD/UnidadDeTrabajoSql` (un `TransactionScope` ReadCommitted, 60 s).
  Las pruebas en memoria se quedan con `Directa`; para probar el rollback sin SQL está
  `TransaccionEnMemoria` (`Dobles.cs`) con `UnidadDeTrabajo.Usar(…)`, que es por flujo (AsyncLocal)
  porque las pruebas corren en paralelo.
- **No escala a MSDTC** (que .NET 8 no soporta) porque las fuentes nunca abren dos conexiones a la
  vez. **Regla nueva: una fuente de `BD/` no abre un contexto dentro de otro**, ni llama a otra
  fuente con su contexto abierto.
- **Candado `UnidadDeTrabajo.Documentos`** (`sp_getapplock` exclusivo por organización, dueño la
  transacción, espera máx. 20 s): ventas (factura, notas, contingencia), salidas, entradas y su
  anulación, gastos. Ventas lo toma desde la validación de existencia hasta el guardado: dos cajas
  no venden la misma última unidad, y los correlativos "último + 1" no chocan.
- **Envueltos en transacción**: los de arriba más pago de factura (asiento + factura), anular
  gasto, transferencia y anulación con contraparte en Banco, cierre Z + estampado, y el `Update` de
  `SqlAgregadoDataSource` (borrar hijos + escribir nuevos).
- **Ventas y el diario fiscal**: `VentasService.EmisionAtomica` corre validar → emitir → guardar →
  mover almacén en una transacción. Si algo falla DESPUÉS de emitir (incluida la confirmación), el
  documento va a `DiarioFiscal` y se lanza `EmitidoSinGuardarException`. El emisor fiscal se llama
  con el candado tomado: si la impresora tarda, las otras cajas esperan (hasta 20 s y avisan).
- **Concurrencia optimista**: `Models/IConVersion` (`byte[]? Version`, `rowversion`, la marca
  `AplicarVersiones` en `DbContext` para toda entidad que la implemente). La tienen los maestros y
  los documentos con estado; `Venta` no (es inalterable). `CrudViewModelBase.Editar` copia la versión
  del registro abierto si el editor armó el resultado con `new`; `SqlAgregadoDataSource.Update` la
  compara antes de tocar nada. El choque sale como `ConflictoDeEdicionException` (en
  `ISesionActual.cs`), que `CrudViewModelBase.Intentar` muestra y recarga. **Al reutilizar una
  entidad ya guardada para un segundo `Update`, tomar la `Version` nueva** (ver `CuentaViewModel`).

## Registro y bitácora (2026-11-18)

- **Registro técnico** (`Services/Registro.cs`, Serilog): `%LocalAppData%\ASO Adminis\logs\aso-AAAAMMDD.log`,
  30 días, con versión, equipo, usuario y caja en cada línea. Se inicia en `Program.Main`. `App`
  instala los manejadores de errores no controlados: el de la interfaz registra, avisa y deja seguir
  (antes cerraba la app). Se registran arranque, migraciones, login (solo el nombre), conflictos,
  candados vencidos, el diario fiscal y los `catch` que antes se tragaban el error. **Nunca**
  contraseñas ni cadenas de conexión. Configuración · Aplicación tiene "Abrir carpeta de registros".
- **Bitácora** (`Models/RegistroAuditoria.cs`, tabla `RegistrosAuditoria`): la escribe sola
  `AsoAdminisDbContext.SaveChanges` por cada alta, modificación o baja de una entidad raíz (no
  owned), con quién, caja, equipo y el detalle campo por campo (JSON). En una modificación lo de
  antes se lee con `GetDatabaseValues()` (las entidades llegan desprendidas). Un `Update` sin
  cambios reales no se anota. El cambio y su fila van en la misma transacción. La parte pura
  (diferencias, enmascarado de `PasswordHash`/`PasswordSalt`, formato) está en `Services/Auditoria.cs`.
- **Solo se agrega**: `ProtegerBitacora` rechaza modificarla o borrarla, y el trigger
  `TR_RegistrosAuditoria_SoloAgregar` (migración `IntegridadYBitacora`) lo impide por fuera.
- **Administración · Bitácora**: permiso `Bitacora.Ver` (por defecto solo el administrador).
  Filtra en el servidor (`SqlBitacoraDataSource`) por fechas, tipo y texto; máx. 1000 filas.

## Seguridad de las cuentas (2026-11-18)

- **`Services/PoliticaSeguridad.cs`**: todas las reglas en un sitio (antes el largo mínimo estaba en
  cuatro pantallas). `ErrorPassword`: 8+, letras y números, sin el nombre de usuario.
- **Bloqueo** (`Usuario.IntentosFallidos`, `BloqueadoHasta`): `AuthService` cuenta los fallos con
  `IUsuarioDataSource.RegistrarIntentoFallido` (sin ámbito, como el login) y bloquea 15 min a los 5.
  Estando bloqueada, **solo quien escribe la contraseña correcta** recibe el aviso
  (`SesionRechazadaException`); a quien prueba claves se le contesta como siempre, sin alargar el
  bloqueo. Se levanta en el editor de usuario (*Desbloquear*) o poniendo contraseña nueva. Queda en
  la bitácora: `CapturarAuditoria` usa los originales rastreados cuando el filtro de organización
  (sin sesión) oculta la fila a `GetDatabaseValues`.
- **Cambio obligatorio** (`Usuario.DebeCambiarPassword`): lo marca el editor de usuario cuando un
  administrador pone la contraseña de OTRO. `LoginView` abre `CambioPasswordObligatorioView`, que
  reutiliza `CuentaViewModel.CambiarPassword`; sin cambiarla no se entra.
- **Inactividad** (`Organizacion.MinutosInactividad`, 15 por defecto, 0 apaga; Administración ·
  Organización): `MainWindow.Guardia.cs` mira la entrada con `InputManager.PreProcessInput` y tapa
  todo con `SesionBloqueadaView` (no se cierra con la X; la contraseña pasa por `AuthService`, así que
  cuenta para el bloqueo).
- **Permisos en caliente**: cada minuto `SesionActual.Leer` (en segundo plano) + `Aplicar` (en el
  hilo de interfaz). Cambio de rol/permisos/caja → se aplica y se redibuja `MainWindow` cuando no hay
  un editor modal abierto; desactivado o cajero sin caja → se cierran las ventanas y la sesión.
  También refresca los datos de la organización (`Ambito.Actualizar`).

## Respaldos (2026-11-18)

- **`Services/RespaldoService.cs`** + **`BD/SqlRespaldoDataSource.cs`**: `.bak` nativo
  (`BACKUP DATABASE … WITH CHECKSUM` + `RESTORE VERIFYONLY`; el nombre de la base se escapa a mano
  porque con LocalDB es la ruta del .mdf y `QUOTENAME` devuelve NULL pasados 128) y exportación
  portable (zip: `manifiesto.json` + `tablas/<Tabla>.json`, recorriendo el modelo de EF con
  `IgnoreQueryFilters`; los owned van anidados). **El .bak no se restaura en PostgreSQL**: la
  exportación portable es la que servirá para migrar de motor (falta escribir el importador, junto
  con la API).
- **Cuándo**: primer login del día de cualquier PC (`LoginViewModel`, en segundo plano, si falta un
  exitoso de alguno de los dos tipos en 20 h), manual (Administración · Respaldos, permiso
  `Respaldos.Ejecutar`) o `ASO_ADMINIS.Desktop.exe --respaldar` (`Program.RespaldoProgramado`, sin
  ventanas, para el Programador de tareas). Un candado de sesión (`sp_getapplock` 'ASO:Respaldo',
  sin espera) evita dos respaldos a la vez.
- **Dónde**: `Respaldos:Carpeta` (local; por defecto `%LocalAppData%\ASO Adminis\Respaldos`) y
  `Respaldos:CarpetaServidor` (ruta del servidor de SQL para el .bak; por defecto la de la instancia,
  o la local con LocalDB). Se conservan 14 de cada tipo; los .bak solo se limpian si la base está en
  esta PC, y nunca se toca un archivo que no tenga nuestro nombre.
- **Historial**: tabla `Respaldos` (`RegistroRespaldo`, sin organización, excluida de la bitácora).

## Cómo escribir un conector fiscal (2026-11-18)

La base común está hecha; el conector concreto se escribe cuando el cliente tenga equipo o
proveedor **y** su documentación (manual del protocolo / SDK, o la API) con un equipo o ambiente de
PRUEBAS. No escribir comandos ni campos de memoria: un conector que "parece" andar y falla con el
equipo real es peor que el simulador.

1. Una clase en `Services/Fiscal/` que implemente `IEmisorFiscal`, y su `Tipo` en
   `EmisoresFiscales.Crear` (leer su configuración de `EmisorFiscal:*` en `appsettings.local.json`,
   que es POR PC: cada caja puede tener su máquina o el mismo proveedor).
2. **Distinguir las dos fallas** de `Emitir`: `EmisorFiscalException` solo si es SEGURO que no se
   emitió (permite reintentar o ir a contingencia); `EmisionInciertaException` si se envió y no se
   sabe (corte, tiempo vencido): `VentasService` no guarda, no ofrece contingencia y lo anota en el
   diario fiscal para verificarlo. **Ante la duda, la incierta.** No reintentar por dentro un envío
   que pudo haber llegado.
3. `ProbarConexion` sin emitir nada (estado de la máquina, o un ping a la API). Lo usa el botón
   "Probar emisor" de Ventas · Cierre del día.
4. Devolver en `ResultadoFiscal.RutaDocumento` el comprobante o PDF si el emisor lo da: queda en
   `Venta.DocumentoRuta` y se abre desde la ficha de la venta ("Ver documento del emisor").
5. `TieneReportesXZ` = máquina fiscal; `EsFiscal` = true (solo el simulador es false).
6. **Contrato**: heredar `ContratoEmisorFiscal` (`ASO_ADMINIS.Tests/FiscalTests.cs`) indicando cómo
   crearlo (y caído). Contra el equipo o ambiente DE PRUEBAS; si alguna falla, no está listo.
   El simulador ya lo cumple en sus dos modos. `EmisorFiscal:Incierto = true` en el simulador
   prueba el camino de la emisión incierta.

## Peticiones de cambio

Cuando a un rol le falta un permiso **de los sensibles** (`MatrizPermisos.Solicitables`), el botón
puede pedir el motivo con el `MotivoEditorViewModel` de siempre y dejar una `PeticionCambio` en la
bandeja del administrador (módulo fijado **Peticiones**). El mecanismo es genérico y está
completo (`PeticionService`, segregación de funciones, auditoría de la decisión); lo que está
vacío es la lista `Solicitables` — hoy no hay ninguna acción sensible gateada en las pantallas
que ve un Cajero. Al agregar un módulo real con acciones sensibles (anular, aprobar, etc.),
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
2. **Roles genéricos**: `Cajero` / `Supervisor` / `AdministradorOrganizacion` / `Desarrollador`
   no están atados a ningún puesto real. Ajustar los nombres y los conjuntos base en
   `Models/Rol.cs` / `Services/MatrizPermisos.cs` en cuanto el negocio real esté definido — son
   *el único* rediseño pendiente, porque los cuatro roles conservan intacto el mecanismo
   (ordinal append-only, permisos por deltas, revocar gana).
3. **Jerarquía física**: hoy es "una organización, sin nivel intermedio". Si el negocio real
   tiene varias plantas o líneas de producción bajo la misma organización, diseñar ese nivel
   cuando se conozca la necesidad real — no está prefigurado en el modelo de datos actual.
4. **Nombre de la empresa / razón social**: el `.csproj` usa "ASO Adminis" como placeholder en
   `Product`/`Company`, a falta de la razón social real del cliente.
5. **Ventas fiscal, pendientes**:
   - **Emisor real:** solo existe el emisor **simulado** (sin valor fiscal). Depende de lo que
     tenga cada cliente (máquina fiscal como HKA o imprenta digital), y hace falta su manual o
     API y un equipo o ambiente de pruebas. La base común ya está: ver "Cómo escribir un conector
     fiscal".
   - **Crédito:** ya hay venta a crédito y CxC. Queda bloqueado el cobro posterior en divisas de
     contribuyentes especiales hasta definir el IGTF; no hay cuotas, intereses ni mora.
   - **IGTF en notas:** las notas de crédito no revierten IGTF.
   - **Por confirmar con el contador:** los porcentajes de IVA, el criterio del IGTF y las
     columnas exactas del libro de ventas.
   - **Documento para el cliente:** ya hay impresión y PDF de una copia claramente marcada como
     NO FISCAL. El documento fiscal válido lo imprime la máquina o lo devuelve la imprenta digital;
     falta recibirlo, conservarlo y entregarlo mediante el conector real. El simulador solo escribe
     un `.txt` en `%AppData%`.
   - **Providencia SNAT/2026/00093:** revisión registrada en la sección siguiente. El texto completo
     consultado es una reproducción; antes de cerrar el conector hay que cotejarlo con la Gaceta
     Oficial 43.484 y los manuales técnicos que publique el SENIAT.
   - **RIF:** solo se valida el dígito verificador, no que exista. No hay API oficial del SENIAT.
     Si se retoma, la opción barata es un botón "Consultar en SENIAT" en la ficha del cliente
     más una marca de "verificado por / fecha". También falta admitir pasaporte y revisar la letra
     C para los RIF que la utilicen.
6. **`Solicitables` vacío**: no hay ninguna petición de cambio configurada todavía — ver
   "Peticiones de cambio" arriba. Los dos candidatos naturales ya existen:
   `EntradasInventario.Anular` y `SalidasInventario.Anular`, que hoy un Cajero simplemente no
   ve.
7. **Datos de la organización**: ya se editan razón social, RIF, dirección, teléfono, condición de
   contribuyente especial y alícuotas. Falta modelar la condición frente al IVA (ordinario, formal o
   no sujeto) cuando se conozca la del negocio, porque cambia las leyendas obligatorias.

## Revisión SENIAT y faltantes diferidos (2026-11-26)

Revisión estática; no certifica cumplimiento legal. Fuentes consultadas:
[PA SNAT/2011/00071](https://tributos.ivecofi.net/informacion/legislacion/providencias/pa-2011-71),
[PA SNAT/2024/000102](https://tributos.ivecofi.net/informacion/legislacion/providencias/pa-2024-102)
y [reproducción de la PA SNAT/2026/00093](https://ecomunik2.com/biblioteca-tributaria/snat-2026-00093).
La 00093 se publicó en la Gaceta 43.484 del 21/10/2026 y entra en vigencia el **19/04/2027**;
hasta el 18/04/2027 siguen la 00071, 0141 y 000102. La PA SNAT/2026/00084 eliminó la homologación
de proveedores de software de la 000121, no las obligaciones de emisión ni la autorización de los
medios. Validar el diseño definitivo con contador/asesor tributario y con el proveedor fiscal.

### Consumidor final

- El alta anónima se deshabilitó para facturas nuevas en el commit `7dea9b2` (13/11/2026);
  `VentasService.ValidarCliente` ahora exige identificación, nombre y dirección. El botón actual es
  "Limpiar", no un atajo para vender sin datos.
- `Venta.EsConsumidorFinal`, los textos y la consolidación por Z se conservan para documentos
  históricos. El comentario de `Models/Cliente.cs` que todavía describe ventas sin cliente está
  desactualizado.
- La 00071, art. 13.7, y la 00093, art. 8.6, permiten omitir el RIF de una persona natural que no
  usa la factura tributariamente, pero exigen nombre y cédula o pasaporte. La 000102, art. 7.7,
  exige además domicilio fiscal en factura digital. En máquina fiscal, los datos del comprador son
  necesarios para crédito fiscal/desembolso; no extender esa excepción a todos los medios.
- Si se recupera el atajo, llamarlo **"Persona natural sin RIF"**, pedir nombre y cédula/pasaporte,
  y decidir la dirección según el medio vigente. Separar la condición comercial de consumidor final
  de `string.IsNullOrWhiteSpace(ClienteRif)`: hoy una persona identificada con cédula no se considera
  consumidor final ni se consolida por Z.

### Brechas fiscales para resolver con el conector real

1. **Medio válido:** `EmisoresFiscales` solo crea `EmisorFiscalSimulado`, con `EsFiscal = false`.
   Sus números, controles, documentos y X/Z no tienen valor fiscal.
2. **Datos digitales:** `ResultadoFiscal` solo devuelve número, control, serial, fecha y ruta. Hay
   que contemplar rango de controles asignados, fecha de asignación, razón social/RIF de la imprenta,
   providencia y fecha de autorización, mención de la providencia aplicable, PDF fiscal y consulta /
   resguardo. La hora fiscal debe llevar segundos; que el simulador o la copia no fiscal usen `HH:mm`
   no valida ese formato.
3. **Notas:** la nota solo guarda Id, número y fecha de la factura afectada. La 00071, arts. 23-24,
   exige monto y, según el medio, serial/control original; la 00093, art. 12, exigirá siempre fecha,
   número correlativo, control y monto. Agregar snapshots o un contrato equivalente que llegue al
   emisor. Desde la 00093 la nota debe salir por el **mismo medio** de la factura: hoy se usa el
   emisor configurado al momento de la nota, sin enrutar según el documento original.
4. **Contingencia:** solo se registran facturas en papel, no NC/ND. Existe talonario y transmisión
   manual, pero falta el expediente completo: la 000102 exige registro, resguardo y notificaciones;
   la 00093, art. 7, exigirá registrar todo y remitirlo al día siguiente en PDF a la imprenta digital
   o por la API del SENIAT. Prever alerta de plazo, relación descargable y constancia de notificación.
5. **Condición IVA:** modelar las leyendas de formal/no sujeto y, desde la 00093, "Contribuyentes
   Formales del IVA" en las operaciones exentas/exoneradas que correspondan.
6. **Resguardo histórico:** la venta no guarda snapshot del emisor; la copia no fiscal usa la
   organización actual. `DocumentoRuta` puede ser una ruta local de la caja. Conservar el original
   fiscal inalterable y asegurar acceso, respaldo y retención según el medio; una copia regenerada no
   lo sustituye.
7. **Operación diaria:** X/Z, inalterabilidad, talonarios y libro de ventas ya tienen base, pero no
   certifican cumplimiento sin equipo/proveedor real. Confirmar con contador columnas del libro,
   tratamiento de notas y retenciones, alícuotas e IGTF. Las guías, series, descuentos, operaciones
   por cuenta de terceros, exportaciones y reglas sectoriales se implementan solo si aplican al
   negocio; cuando haya descuentos/cargos, el documento debe describirlos y valorarlos.

### Mejoras generales diferidas (no todas son obligaciones SENIAT)

- **Devolución de dinero:** una NC reingresa inventario y ajusta CxC, pero no genera por sí sola un
  egreso de caja/banco si la venta ya estaba cobrada. Definir reembolso, permiso, turno y arqueo.
- **Compras/CxP:** el pago de proveedor es solo total. La factura de compra no guarda desglose de
  base/IVA ni control fiscal. Faltan pagos parciales, notas del proveedor y, si se quiere cubrir todo
  el ciclo, libro de compras y comprobantes propios de retención cuando la organización sea agente.
- **ISLR:** registrar retenciones de clientes sobre servicios; no tocan la factura ni el libro de
  ventas. Revisar también los supuestos de IVA distintos de 75/100 y más de un comprobante por
  factura antes de generalizar el modelo actual.
- **Crédito:** cuotas, intereses, mora, límites, recordatorios/antigüedad y aplicación del saldo a
  favor son mejoras comerciales, no requisitos universales. El esquema actual en Bs con referencia
  US$ a tasa de emisión y recibos administrativos posteriores es coherente con lo revisado.
- **Autorización:** llevar las escrituras CRUD detrás de servicios que repitan el permiso; hoy la
  base genérica limita los comandos pero llama directo al `IDataSource`.
- **Documentación auxiliar:** actualizar README y guías cuando se agreguen cotizaciones, pedidos,
  guías o contabilidad; no son necesarios para todo comercio ni se deben presentar como mandato legal.

## Próximo paso sugerido

1. **Conector del emisor fiscal real** (máquina fiscal o imprenta digital) en cuanto el cliente
   elija equipo o proveedor; hasta entonces todo se emite con el simulador, sin valor fiscal.
2. **Decidir los roles reales** del negocio y reemplazar los cuatro genéricos.
3. **Elegir el color de marca real** y actualizar lo descrito en "Marca" (y regenerar el `.ico`).
4. **Llevar la comprobación de permisos a los servicios de dominio** de cada módulo nuevo desde
   el principio (no repetir el hueco que tiene hoy el CRUD genérico).

### Plan empresarial: pasos pendientes (anotado 2026-11-18)

Hechos: cajas/cajeros/turnos, paso 3 (transacciones, concurrencia, FK nuevas), paso 4 (seguridad
de cuentas), paso 6 (registro y bitácora) y paso 7 (respaldos). Quedan, en el orden sugerido:

1. **Paso 5 · Versiones y migraciones.** Que una caja con versión vieja no abra contra una base
   más nueva (comparar la última migración aplicada con la que conoce el código y pedir
   actualizar). Sacar la migración del arranque de "la primera caja que abre": paso controlado,
   con respaldo previo (`RespaldoService`) y, en red, solo desde el servidor o con un permiso.
   Más adelante: instalación por equipo (MSI), configuración central y canales piloto/producción.
2. **Paso 8 · CI.** GitHub Actions: compilar, `dotnet test` (también con `ASO_PRUEBAS_SQL=1`
   contra LocalDB del runner de Windows) en cada push, y publicar la versión con Velopack.
3. **Paso 2 · Async.** Pasar fuentes de datos y servicios a async, con indicador de carga y
   paginación en las listas grandes. Prepara el terreno de la API.
4. **Paso 1 · API + PostgreSQL** (juntos, decidido). Servidor ASP.NET Core entre las cajas y la
   base; implementaciones `Http…DataSource` de las interfaces de `Services/`. Al pasar a
   PostgreSQL: migración inicial nueva; `rowversion` → `xmin`; `sp_getapplock` →
   `pg_advisory_xact_lock` (`UnidadDeTrabajoSql`, `SqlRespaldoDataSource`); índice filtrado →
   parcial; trigger de la bitácora en PL/pgSQL; importador del `.zip` portable de respaldos.
   **Decisión pendiente del negocio:** ¿servidor administrado por nosotros para todas las
   organizaciones, o uno por cliente?
5. **Pruebas manuales:** en una instalación real, recorrer Cajas, Mi turno, Bitácora, Respaldos y
   bloqueo por inactividad, además del ciclo de crédito: factura con o sin anticipo, abonos parciales
   y totales, anulación de cobros, notas y retenciones. Las pruebas automatizadas no sustituyen este
   recorrido de las pantallas.
