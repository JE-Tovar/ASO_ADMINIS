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
y emisor en memoria (`Dobles.cs`), así que no tocan SQL Server ni `%AppData%`; cubren `Rif`,
`CalculoFiscal` y la numeración de `VentasService` (contingencia, choque entre cajas).

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
Cierre del día), **Finanzas** (Cuentas por Pagar, Gastos varios, Banco y Proveedores), **Inventario** (Almacén, Entradas y
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
  `DatosFiscalesCompletos` no se emite nada. **Caja por máquina**: `AjustesApp.CajaCodigo`
  (Configuración · Aplicación); sin caja tampoco se emite.
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
- **Contingencia:** si el emisor no responde, quien tenga `Ventas.Contingencia` registra la factura
  hecha en papel. Se cargan su n° y su n° de control, que se valida que no se repita. Queda
  `PendienteTransmitir` hasta que "Transmitir pendientes" la envía.
- **Retenciones de IVA recibidas** (`RetencionIvaRecibida`, `RetencionesService`): son las del
  cliente agente de retención, del 75 % o el 100 % del IVA.
  - **Comprobante:** número de 14 dígitos; una retención por factura; el comprobante no se
    repite.
  - **Por qué es una fila aparte:** la factura es inalterable.
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
  el % dentro y los nombres en leyenda (máx. 6 porciones + "Otros"), para que nada se salga de
  la tarjeta. No crear `PlotModel` "pelados" en un ViewModel.

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
| **Operador** | El día a día: emite facturas y mantiene clientes y servicios; crea/edita proveedores y facturas de compra; mantiene el catálogo de artículos y registra entradas y salidas de almacén; ve el cierre del día (reporte X). No mueve dinero, no anula ni borra nada |
| **Supervisor** | Todo lo de Operador, más notas de crédito/débito, contingencia, cierre Z, retenciones de IVA y libro de ventas; registrar pagos (dispara el asiento en Banco) y gastos varios, administrar cuentas bancarias, anular documentos de almacén, eliminar, y resolver peticiones de su dominio |
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
5. **Ventas fiscal, pendientes**:
   - **Emisor real:** solo existe el emisor **simulado** (sin valor fiscal). Hay que escribir el
     conector de la máquina fiscal (p. ej. HKA) o de la imprenta digital que elija el cliente,
     contra `IEmisorFiscal`.
   - **Crédito:** no hay cuentas por cobrar ni venta a crédito.
   - **IGTF en notas:** las notas de crédito no revierten IGTF.
   - **Por confirmar con el contador:** los porcentajes de IVA, el criterio del IGTF y las
     columnas exactas del libro de ventas.
   - **Documento para el cliente:** el simulador solo escribe un `.txt` en `%AppData%`; no hay
     factura imprimible ni PDF para entregar. Con máquina fiscal la imprime la máquina; con
     imprenta digital, normalmente el proveedor devuelve el PDF. Se resuelve junto con el
     conector real.
   - **Rangos de contingencia:** hoy solo se valida que el n° de control de papel no se repita.
     Falta cargar en Organización el rango de formatos autorizados por la imprenta y exigir que
     el número caiga dentro. No depende del cliente: se puede hacer ya.
   - **Providencia SNAT/2026/00093** (vigente desde abril de 2027): solo se conoce por notas de
     prensa. Hay que revisar su texto completo en la Gaceta 43.484 por si exige algo más en el
     documento.
   - **RIF:** solo se valida el dígito verificador, no que exista. No hay API oficial del SENIAT.
     Si se retoma, la opción barata es un botón "Consultar en SENIAT" en la ficha del cliente
     más una marca de "verificado por / fecha".
6. **`Solicitables` vacío**: no hay ninguna petición de cambio configurada todavía — ver
   "Peticiones de cambio" arriba. Los dos candidatos naturales ya existen:
   `EntradasInventario.Anular` y `SalidasInventario.Anular`, que hoy un Operador simplemente no
   ve.
7. **Pantalla de datos de la organización**: `Administración · Organización` solo pide nombre y
   código; si el negocio real necesita más datos de la organización (dirección, RIF, etc.),
   agregarlos ahí.

## Próximo paso sugerido

1. **Conector del emisor fiscal real** (máquina fiscal o imprenta digital) en cuanto el cliente
   elija equipo o proveedor; hasta entonces todo se emite con el simulador, sin valor fiscal.
2. **Decidir los roles reales** del negocio y reemplazar los cuatro genéricos.
3. **Elegir el color de marca real** y actualizar lo descrito en "Marca" (y regenerar el `.ico`).
4. **Llevar la comprobación de permisos a los servicios de dominio** de cada módulo nuevo desde
   el principio (no repetir el hueco que tiene hoy el CRUD genérico).
