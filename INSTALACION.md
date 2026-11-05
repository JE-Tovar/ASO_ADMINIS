# Instalar ASO Adminis en otra PC

El instalador es el de **Velopack**: `AsoAdminis-win-Setup.exe`, publicado en los Releases de
GitHub (cómo generar una versión: README, "Publicar una versión"). No pide permisos de
administrador, no necesita .NET instalado (va dentro) y después **se actualiza sola**: tras el
login busca una versión nueva y ofrece reiniciar.

Instalada, la app vive en `%LocalAppData%\AsoAdminis` y guarda lo de la máquina en
`%LocalAppData%\ASO Adminis` (la base LocalDB en `App_Data\` y, si existe,
`appsettings.local.json`), fuera de lo que reemplaza cada actualización.

El `.exe` no está firmado: la primera vez Windows SmartScreen puede avisar "Editor desconocido"
(→ *Más información* → *Ejecutar de todas formas*).

## Modo 1: una sola PC

Necesita **SQL Server LocalDB**. Si la PC no lo tiene: descargar el instalador de **SQL Server
Express**, elegir "Descargar medios" → **LocalDB** e instalar `SqlLocalDB.msi`. Luego ejecutar
`AsoAdminis-win-Setup.exe`. No hay nada que configurar.

LocalDB es **por usuario de Windows**: si dos personas usan la misma PC con cuentas distintas,
cada una vería su propia base. En ese caso, usar el modo red apuntando a esa misma PC.

## Modo 2: varias cajas en red

Los datos viven en **una** PC que hace de servidor, con **SQL Server Express** (gratuito). Las
demás se conectan a ella.

**En el servidor (una sola vez):**
1. Instalar SQL Server Express (instancia por defecto `SQLEXPRESS`).
2. *SQL Server Configuration Manager* → Protocolos de SQLEXPRESS → habilitar **TCP/IP**; activar
   y arrancar el servicio **SQL Server Browser**; reiniciar el servicio de SQL Server.
3. Firewall de Windows: permitir **TCP 1433** y **UDP 1434** (o el programa `sqlservr.exe`).
4. En una red de trabajo sin dominio lo más simple es un **usuario SQL**: en SQL Server
   Management Studio, habilitar autenticación mixta, crear un login (p. ej. `aso`) y darle el
   rol `dbcreator` (la app crea la base sola la primera vez).

**En cada puesto (también en el servidor, si se usa como caja):**
1. Ejecutar `AsoAdminis-win-Setup.exe`.
2. Crear `%LocalAppData%\ASO Adminis\appsettings.local.json` con la cadena del servidor (la
   barra invertida va doble):

   ```json
   {
     "ConnectionStrings": {
       "AsoAdminisDb": "Server=NOMBRE-DEL-SERVIDOR\\SQLEXPRESS;Database=AsoAdminis;User Id=aso;Password=LA_CLAVE;TrustServerCertificate=True;"
     }
   }
   ```

   Con dominio de Windows, en lugar de `User Id`/`Password` va `Trusted_Connection=True;`.
3. Abrir la app. Todas las cajas deben usar el mismo `Database`.

El archivo está en la carpeta de cada usuario de Windows: si varias personas usan la misma caja
con cuentas distintas, cada una necesita su copia.

> La contraseña SQL queda en texto plano en ese archivo. Usar un login dedicado solo a esta base.

## Primer arranque

Contra una base vacía, la aplicación **crea las tablas sola** (aplica las migraciones) y muestra
la pantalla de primer arranque: nombre y código de la organización y el usuario Desarrollador.
Después, en Administración · Organización, completar los datos fiscales (razón social, RIF,
dirección), y en Configuración · Aplicación el código de **caja** de cada puesto.

## Actualizaciones

Llegan solas desde GitHub Releases. Con varias cajas en red conviene que todas se actualicen el
mismo día: la primera que abra la versión nueva migra la base, y una caja vieja contra una base
nueva puede fallar.

## Respaldo

- **Una sola PC:** copiar `%LocalAppData%\ASO Adminis\App_Data\` con la aplicación cerrada.
- **Red:** en el servidor, SQL Server Management Studio → base `AsoAdminis` → Tareas → Copia de
  seguridad (Express no trae el Agente: programarla con el Programador de tareas y `sqlcmd`).

Además, respaldar `%AppData%\ASO Adminis\diario-fiscal.jsonl` de cada caja si existe: anota
documentos fiscales emitidos que no se pudieron guardar en la base.
