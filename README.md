# ASO Adminis

Sistema de gestión de escritorio para un negocio de **venta de artículos**. Trae el armazón
técnico y estético de **ASO** (autenticación, roles y permisos, aislamiento multi-organización,
framework CRUD/MVVM, tema claro/oscuro, peticiones de cambio) más dos módulos de ejemplo —
**Finanzas** (Cuentas por Pagar y Banco) e **Inventario** — como plantilla viva de los patrones a
seguir: CRUD simple, documento con líneas, documento con máquina de estados, contenedor de dos
padrones, kardex derivado. Ver `CLAUDE.md` para el detalle completo de qué trae, cómo está
organizado y cómo se agrega un módulo nuevo.

## Estado

Este proyecto nace clonando la plantilla `ASO_GENERIC` y renombrándola. El módulo real de venta de
artículos (catálogo, punto de venta, clientes) todavía no está construido — hoy solo están los dos
módulos de ejemplo del armazón. Se agrega siguiendo la receta de `CLAUDE.md`.

## Requisitos

- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11

## Ejecutar

```bash
cd ASO_ADMINIS.Desktop
dotnet run
```

O abrir `ASO_ADMINIS.slnx` en Visual Studio 2022.

La base de datos es un archivo **SQL Server LocalDB** (`ASO_ADMINIS.Desktop/App_Data/AsoAdminis.mdf`),
no un servidor aparte: no hace falta configurar nada para arrancar. Hace falta tener **LocalDB**
instalado (viene con Visual Studio; si no, se instala aparte con el instalador liviano
"SqlLocalDB.msi" de SQL Server Express). La primera vez, aplica el esquema:

```bash
cd ASO_ADMINIS.Desktop
dotnet ef database update
```

Esto crea `App_Data/AsoAdminis.mdf` (no se sube al repo — cada máquina tiene el suyo, ver
`.gitignore`). En el primer arranque contra una base sin usuarios, la aplicación pide el nombre
de la organización y crea el usuario desarrollador. No hay usuarios ni contraseñas por defecto.

Si en cambio quieres apuntar a un SQL Server real (compartido o remoto) en vez de tu LocalDB
local, copia `ASO_ADMINIS.Desktop/appsettings.local.example.json` como `appsettings.local.json` y
pon ahí tu cadena de conexión — no se sube al repo, así que cada quien puede tener la suya.

## Estructura

```
ASO_ADMINIS/
├── ASO_ADMINIS.slnx
└── ASO_ADMINIS.Desktop/      # Aplicación WPF (MVVM ligero)
    ├── Models/           # Entidades del dominio
    ├── Services/         # Servicios de dominio, sesión/permisos y contratos de datos
    ├── ViewModels/       # Lógica de presentación
    ├── Views/            # Pantallas y editores
    ├── Navigation/       # Catálogo de módulos y submódulos (fuente única)
    ├── Configuration/    # Configuración y composición de fuentes de datos
    ├── BD/               # EF Core / SQL Server (DbContext + fuentes Sql…)
    ├── Migrations/       # Migraciones EF Core (una sola: Baseline)
    ├── Controls/         # Sidebar y componentes reutilizables
    └── Styles/           # Paleta y estilos
```

## Módulos

| Módulo | Submódulos | Estado |
|---|---|---|
| Finanzas | Cuentas por Pagar · Banco · Proveedores | módulo de ejemplo, funcional |
| Inventario | Almacén · Entradas · Salidas | módulo de ejemplo, funcional |
| Ventas | — | pendiente de construir (negocio real) |

Además hay **cuatro secciones fijas** según el rol: **Inicio**, **Peticiones** (bandeja de
solicitudes de cambio), **Administración** (usuarios y permisos, y los datos de la organización)
y **Configuración**, esta última anclada al pie del menú lateral: tema claro/oscuro, escala de la
interfaz, cambio de la propia contraseña y las preferencias de la máquina. Se guardan en
`%AppData%\ASO Adminis\ajustes.json`, no en la base de datos.

**Inventario** lleva el almacén de insumos: un catálogo de artículos cuya existencia **no se
teclea**, se calcula de lo que entró menos lo que salió. Registrar una entrada por compra deja
sola su cuenta por pagar en Finanzas, y las salidas se emiten como boleto con destino, quién
retira y quién autoriza.

El módulo real de Ventas se agrega siguiendo la receta de "Cómo se agrega un submódulo" en
`CLAUDE.md`, usando Finanzas e Inventario como arquetipo.

## Roles

Cuatro roles genéricos, sin atar a ningún puesto real todavía — ver "PROVISIONAL" en `CLAUDE.md`.

| Rol | Qué puede |
|---|---|
| **Operador** | El día a día: registra facturas de proveedor y da de alta proveedores; mantiene el catálogo del almacén y registra entradas y salidas. No mueve dinero, no anula ni borra nada: para eso levanta una petición |
| **Supervisor** | Todo lo de Operador, más registrar pagos (que asientan el movimiento en el libro de banco), administrar cuentas bancarias, y anular entradas y salidas de almacén. Resuelve peticiones de su dominio |
| **Administrador de organización** | Todo dentro de la organización. Lo único que no puede es crear otros usuarios Desarrollador |
| **Desarrollador** | Todo, y es el único que puede crear otros usuarios Desarrollador |

**Una sola organización trabaja por instalación.** Ningún formulario pregunta a qué organización
pertenece algo: se estampa la de la instalación.

## Marca

No trae logo ni color de marca reales todavía — `Assets/Logo/` está vacía y el sidebar/login
muestran un monograma de texto ("A") como placeholder. `Styles/Colors.xaml`/`ColorsOscuro.xaml`
traen una paleta ámbar/azul ya calculada con los contrastes en regla, también como placeholder.
Ver la sección "Marca" de `CLAUDE.md` para qué tocar al definir la marca real.

## Estado del proyecto

El armazón técnico está completo y probado (compila, migra y arranca) sobre los dos módulos de
ejemplo. Lo que sigue pendiente de definición —roles reales, color de marca, razón social, y el
módulo real de Ventas— está en la sección "PROVISIONAL" de `CLAUDE.md`.
