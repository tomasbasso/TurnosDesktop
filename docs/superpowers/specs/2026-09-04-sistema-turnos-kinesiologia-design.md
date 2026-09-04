# Sistema de Turnos para Kinesiología — Diseño

Fecha: 2026-09-04
Estado: aprobado para planificación

## 1. Propósito

Aplicación de escritorio, offline, para que un kinesiólogo gestione su agenda de
turnos, el padrón de pacientes, la evolución clínica de cada tratamiento y el
cobro de las sesiones. Uso personal, sin componente web, sin servidor.

Profesional inicial: **Ezequiel Tosso**.

### Criterios de éxito

1. Cargar un turno nuevo desde el calendario toma menos de 15 segundos,
   incluyendo dar de alta a un paciente que llama por primera vez.
2. Generar las 10 sesiones de un tratamiento ("martes y jueves 10:00") es una
   sola operación, con vista previa de las fechas antes de confirmar.
3. Es imposible superponer dos turnos por accidente.
4. Al abrir la app se ve, sin navegar, a quién se atiende hoy.
5. Escribir la evolución de una sesión se hace desde la agenda, viendo la nota
   de la sesión anterior.
6. Nunca se pierden datos: el archivo de base se respalda automáticamente.

## 2. Stack

- .NET 8 MAUI Blazor Hybrid — `net8.0-windows` + `net8.0-android`
- Componentes Razor
- Tailwind CSS v4 (CLI vía npm, compilado en target MSBuild, sin `tailwind.config.js`)
- CommunityToolkit.Maui (toasts, FolderPicker)
- EF Core 8 + SQLite local (`turnos.db` en `FileSystem.AppDataDirectory`)
- xUnit para tests
- QuestPDF / ClosedXML — instalados pero sin uso en v1 (ver §10 Backlog)

## 3. Decisiones tomadas

| Tema | Decisión | Razón |
|---|---|---|
| Multiprofesional | Agendas separadas por profesional | El consultorio puede sumar colegas sin mezclar agendas |
| Autenticación | Ninguna. Selector de profesional sin contraseña | App personal en una máquina de confianza; la separación es de vista, no de seguridad |
| Pacientes | Padrón único compartido entre profesionales | Evita duplicar al mismo paciente |
| Historia clínica | Por profesional, nota libre por sesión | Sin fricción al escribir; lo estructurado no se usaría |
| Calendario | Grilla horaria libre, estilo Google Calendar | Flexibilidad para horarios que no caen en slots fijos |
| Tratamientos | Entidad con N sesiones autorizadas + generador de turnos recurrentes | Es el patrón real de kinesiología |
| Cobros | Movimientos de pago; saldo calculado, sin flag por turno | Los bonos se pagan por adelantado; un flag por sesión obligaría a mentir |
| Estructura | 4 proyectos en capas | Permite testear con xUnit sin el workload de MAUI |
| Componente calendario | Razor propio, sin librería | Tailwind v4 ya es el sistema de estilos; una librería de componentes pelearía con él |

## 4. Estructura de la solución

```
Turnos.Core     net8.0    Entidades, enums, Result<T>, interfaces, reglas puras
Turnos.Data     net8.0    DbContext, ValueConverters, DatabaseInitializer, servicios
Turnos.App      net8.0-windows;net8.0-android    MAUI Blazor Hybrid, Razor, Tailwind
Turnos.Tests    net8.0    xUnit → referencia Core y Data
```

`Turnos.Core` y `Turnos.Data` son `net8.0` puro **a propósito**: un proyecto MAUI
multi-target no expone un TFM que xUnit pueda referenciar sin fricción. Con la
lógica en proyectos limpios, `dotnet test` corre en cualquier máquina sin el
workload de MAUI instalado.

`Turnos.Core` no referencia EF Core. Las entidades son POCOs.

## 5. Modelo de datos

### Profesional

| Campo | Tipo | Notas |
|---|---|---|
| Id | int PK | |
| Nombre | string | requerido |
| Color | string | hex; reservado para una futura vista "todas las agendas" |
| HoraInicioAgenda | TimeOnly | define el rango visible de la grilla |
| HoraFinAgenda | TimeOnly | |
| Activo | bool | |

No hay tabla de horarios por día. La grilla es libre; dos campos alcanzan para
saber qué franja dibujar.

### Paciente — padrón único, compartido

| Campo | Tipo | Notas |
|---|---|---|
| Id | int PK | |
| Nombre | string | requerido |
| Apellido | string | requerido |
| Dni | string? | |
| Telefono | string? | |
| Email | string? | |
| FechaNacimiento | DateOnly? | |
| Observaciones | string? | permanente: alergias, antecedentes, limitaciones |
| Activo | bool | |
| CreadoEl | DateTime | |

Índice en `(Apellido, Nombre)` y en `Dni` para el autocomplete.

### Tratamiento

| Campo | Tipo | Notas |
|---|---|---|
| Id | int PK | |
| PacienteId | FK Paciente | |
| ProfesionalId | FK Profesional | **acá vive la separación por profesional** |
| Motivo | string | diagnóstico / motivo de consulta |
| SesionesAutorizadas | int | |
| PrecioSesion | decimal | persistido como TEXT invariante |
| FechaInicio | DateOnly | |
| FechaAlta | DateOnly? | null mientras esté activo |
| Estado | EstadoTratamiento | Activo, Finalizado, Abandonado |
| Notas | string? | |

**Las sesiones usadas NO se almacenan.** Se calculan como
`Turnos.Count(t => t.Estado == EstadoTurno.Atendido)`. Un contador almacenado se
desincroniza en cuanto se cancela un turno ya marcado como atendido.

### Turno

| Campo | Tipo | Notas |
|---|---|---|
| Id | int PK | |
| ProfesionalId | FK Profesional | dueño de la agenda |
| PacienteId | FK Paciente | |
| TratamientoId | FK Tratamiento? | **nullable**: permite el turno suelto de primera consulta |
| Inicio | DateTime | hora local, sin offset |
| Fin | DateTime | |
| Estado | EstadoTurno | Programado, Atendido, Ausente, Cancelado |
| SerieId | Guid? | agrupa los turnos generados juntos |
| Observaciones | string? | logístico ("viene con la orden") |
| NotaClinica | string? | la nota libre de la sesión |

Índice en `(ProfesionalId, Inicio)` — es la consulta que corre en cada cambio de
semana del calendario.

La nota clínica es un campo del turno y no una tabla aparte: es exactamente
"una nota libre por sesión" y evita un join en la pantalla más usada. Si en el
futuro se necesitan varias notas o adjuntos por sesión, se migra con el esquema
de `ALTER TABLE` condicional de §8.

### Pago

| Campo | Tipo | Notas |
|---|---|---|
| Id | int PK | |
| ProfesionalId | FK Profesional | |
| PacienteId | FK Paciente | |
| TratamientoId | FK Tratamiento? | null si es un pago suelto |
| Monto | decimal | persistido como TEXT invariante |
| FormaPago | FormaPago | Efectivo, Transferencia, Tarjeta, ObraSocial |
| Fecha | DateOnly | |
| Nota | string? | |

Derivaciones, sin campos almacenados:

```
Saldo del tratamiento = (sesiones atendidas × PrecioSesion) − Σ Pagos del tratamiento
Caja del mes          = Σ Pagos del profesional con Fecha en ese mes
```

No existe un flag "cobrado" en el turno. En kinesiología se paga por bono; un
flag por sesión obligaría a mentir en ese caso y crearía dos fuentes de verdad.
Consecuencia aceptada: se responde "¿este tratamiento está al día?", no "¿esta
sesión puntual está paga?".

### Enums

```csharp
enum EstadoTurno       { Programado, Atendido, Ausente, Cancelado }
enum EstadoTratamiento { Activo, Finalizado, Abandonado }
enum FormaPago         { Efectivo, Transferencia, Tarjeta, ObraSocial }
```

### Preferencias (no van a la base)

Guardadas en `Preferences` de MAUI, no en SQLite:

| Clave | Tipo | Default |
|---|---|---|
| `ProfesionalActivoId` | int | el primer profesional activo |
| `DuracionTurnoPorDefecto` | int (minutos) | 30 |
| `MostrarDomingo` | bool | false |
| `UltimoBackupAutomatico` | DateOnly | — |

No hay tabla de sesión ni login: el profesional activo es solo la última
elección recordada.

## 6. Pantallas

### Inicio (`/`)

Barra superior con el selector de profesional. Debajo, tarjetas grandes:
**Turnos · Pacientes · Historia Clínica · Caja · Ajustes**.

Debajo de las tarjetas, la agenda del día en curso: hora, paciente y
"sesión N de M" por cada turno, incluyendo los huecos libres.

### Rutas

| Ruta | Pantalla |
|---|---|
| `/` | Inicio: tarjetas + agenda del día |
| `/agenda` | Calendario semanal |
| `/pacientes` | Listado con buscador |
| `/pacientes/{id}` | Ficha: datos, tratamientos, saldo |
| `/pacientes/{id}/historia` | Historia clínica: sesiones en orden cronológico |
| `/historia` | Buscador que desemboca en la ruta anterior |
| `/caja` | Movimientos y total del mes |
| `/config` | Profesionales, duración por defecto, backup |

### Layout

`MainLayout` con barra superior fija: botón volver, título de sección y el
profesional activo siempre visible, para no cargar un turno en la agenda
equivocada. Sin sidebar; la home hace de menú.

## 7. El calendario

### Geometría

Vista semanal. Se muestran seis columnas —lunes a sábado— salvo que la
preferencia `MostrarDomingo` esté activa, en cuyo caso son siete. Eje vertical
entre `HoraInicioAgenda` y `HoraFinAgenda` del profesional activo.

Escala fija: `1 minuto = 1.2 px` (30 min = 36 px). Cada turno es un `div`
absoluto:

```
top    = (Inicio − HoraInicioAgenda).TotalMinutes × escala
height = (Fin − Inicio).TotalMinutes × escala
```

Líneas guía cada 30 min, más tenues cada 15. Columna de hoy resaltada. Línea
roja fina en la hora actual. Header con `‹ semana ›`, botón **Hoy** y el rango
de fechas.

### Crear un turno

Click en zona vacía → la hora se calcula desde la posición vertical del click y
se **redondea a 15 minutos**; se abre el modal precargado con esa fecha y hora.

Campos del modal:

- **Paciente**: autocomplete por apellido o DNI, con "＋ paciente nuevo" que da
  de alta sin salir del modal.
- **Tratamiento**: autoselecciona el tratamiento `Activo` de ese paciente con
  ese profesional. Si hay varios, se elige. Si no hay ninguno, ofrece crear uno
  o dejar el turno suelto.
- **Duración**: 30 / 40 / 45 / 60 / otro. Default configurable en Ajustes.
- **Observaciones**: logístico, no clínico.

Al elegir tratamiento, el modal muestra **"Sesión 5 de 10 · quedan 6"**.

### Solapamiento

Antes de guardar, el servicio verifica si existe otro turno del mismo
profesional, en estado distinto de `Cancelado`, tal que:

```
existente.Inicio < nuevo.Fin && existente.Fin > nuevo.Inicio
```

Si lo hay, devuelve `Result.Fail("Se superpone con Juan Pérez, 10:00–10:40")`.
Bloqueo duro, sin opción de forzar.

### Series recurrentes

Botón **Repetir**, accesible desde el tratamiento y desde el modal de turno.

Entrada: chips de días de la semana (`L M M J V S`), hora, duración, y cantidad
de sesiones (precargada con las autorizadas restantes) o fecha fin.

Antes de confirmar se muestra la **vista previa de las fechas generadas**,
marcando las que chocan con turnos existentes y con quién chocan. El usuario
puede omitir las conflictivas o cancelar. Nunca se crea una serie a ciegas.

Todos los turnos generados comparten un `SerieId`, lo que habilita "cancelar el
resto de la serie" de un click.

Si el total supera las sesiones autorizadas, se advierte pero no se bloquea.

### Estados

```
              ┌──► Atendido    consume sesión · habilita nota clínica
Programado ───┼──► Ausente     no vino · NO consume sesión
              └──► Cancelado   anulado · NO consume sesión
```

Solo `Atendido` descuenta del tratamiento. Se marca con un click desde el turno
en la grilla, sin abrir el detalle.

Color por estado, no por profesional: azul programado, verde atendido, ámbar
ausente, gris tachado cancelado.

### Nota clínica desde la agenda

Click en un turno `Atendido` → panel lateral con textarea. Arriba, en gris, la
nota de la sesión anterior del mismo tratamiento, como contexto.

### Entrega en dos etapas

- **Etapa 1**: click para crear, duración por desplegable, editar fecha y hora
  en el modal. Funcionalidad completa y usable.
- **Etapa 2**: drag & drop para mover un turno y arrastre del borde inferior
  para ajustar la duración.

La etapa 2 es un refinamiento sobre algo que ya funciona. Es la parte más
delicada del proyecto y no bloquea la utilidad del sistema.

## 8. Persistencia

### Base

`turnos.db` en `FileSystem.AppDataDirectory`.

`DbContext` registrado como **Transient**: cada pantalla arranca con un change
tracker limpio y no arrastra entidades obsoletas entre navegaciones.

### Evolución de esquema — sin migraciones de EF

Un `DatabaseInitializer` que corre al arrancar la app:

1. `EnsureCreatedAsync()` crea el esquema si la base no existe.
2. Para cada columna agregada después de la v1: `PRAGMA table_info(Tabla)` y, si
   la columna no está, `ALTER TABLE ... ADD COLUMN`.

Cada evolución queda como un método numerado y explícito, ejecutado en orden.
Una base ya instalada evoluciona sin perder datos.

### Conversiones

- `decimal` → TEXT con `CultureInfo.InvariantCulture` vía `ValueConverter`.
- `DateOnly` / `TimeOnly` → TEXT ISO.
- `DateTime` → TEXT ISO, hora local sin offset.

**Regla no negociable**: todo monto se materializa con `.ToList()` antes de
sumar u ordenar. SQLite no ordena correctamente un decimal guardado como texto,
y sumarlo en SQL produce resultados silenciosamente incorrectos. Esta regla va
comentada en el código, no solo en esta spec.

### Seed

Al crear la base: profesional **Ezequiel Tosso**, agenda 07:00–21:00, activo.

### Backup

Copia del archivo `.db`. Tres disparadores:

| Cuándo | Comportamiento |
|---|---|
| Manual | Botón en Ajustes, con selector de carpeta (FolderPicker del CommunityToolkit) |
| Automático | Al abrir la app, una vez por día |
| Al cerrar | En el evento de cierre de la ventana |

Destino: `AppDataDirectory/backups/turnos-yyyyMMdd-HHmmss.db`. Retención: los
últimos 7. Antes de copiar se ejecuta `PRAGMA wal_checkpoint` para que el
archivo quede completo.

### Servicios

Todos devuelven `Result<T>` con `Success` / `Message` / `Data`. Sin excepciones
como control de flujo. La UI muestra el `Message` en un toast y sigue viva.

## 9. Testing

`Turnos.Tests`, xUnit sobre `net8.0`.

**Reglas puras en `Core`** — sin base de datos:

- Detección de solapamiento de turnos
- Generación de las fechas de una serie recurrente
- Conteo de sesiones consumidas de un tratamiento
- Cálculo del saldo de un tratamiento

**`Data`, con SQLite real sobre archivo temporal** — no el provider InMemory.
InMemory no reproduce el `ValueConverter` de decimal a TEXT, que es justamente
donde aparecen los bugs. Un archivo temporal por test es fiel y sigue siendo
rápido.

## 10. Fuera del alcance de la v1 (backlog)

Ninguno de estos requiere cambios al modelo de datos de §5 más allá de columnas
nuevas:

- Reportes: historia clínica a PDF (QuestPDF), agenda y caja a Excel (ClosedXML)
- Obras sociales: catálogo, número de afiliado, sesiones autorizadas por la obra
- Recordatorio de turno por WhatsApp, disparando la app instalada
- Escalas clínicas medibles por sesión (dolor EVA, rango articular) y sus gráficos
- Adjuntos por sesión (estudios, fotos)
- Vista "todas las agendas" con color por profesional
- PIN por profesional, si la separación de historias debiera ser una barrera real
