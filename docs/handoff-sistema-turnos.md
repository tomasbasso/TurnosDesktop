# Handoff — Sistema de turnos de kinesiología

Última actualización: 2026-09-04 (Fase 1 completa y pusheada). Repo: https://github.com/tomasbasso/TurnosDesktop (branch `main`).

## Objetivo

App de escritorio para que Ezequiel Tosso, kinesiólogo, deje de manejar sus turnos
en papel/WhatsApp. Reemplaza tres cosas a la vez: la agenda, el padrón de pacientes
y el seguimiento de sesiones autorizadas por bono.

El caso que define el producto: el paciente llega con un bono de 10 sesiones y hay
que saber en qué número va sin contar a mano.

Alcance v1: agenda, pacientes, tratamientos con series recurrentes, historia clínica
como nota por sesión, y caja simple. Un profesional hoy, varios mañana, sin login.

## Decisiones tomadas y por qué

**Sin autenticación.** Es una PC de consultorio, no un servidor. El profesional se
elige de un desplegable y queda recordado. Un login sería fricción diaria sin
amenaza real que justifique el costo.

**Padrón de pacientes compartido, historia clínica separada por profesional.**
Un paciente atendido por dos kinesiólogos es una persona, no dos fichas. Pero la
historia de cada uno es suya: la separación vive en `Tratamiento.ProfesionalId`,
no en `Paciente`.

**Agendas separadas por profesional, no una vista combinada.** El color del turno
codifica el estado (programado/atendido/ausente/cancelado), no quién atiende.

**Historia clínica = nota libre por sesión, campo `NotaClinica` en `Turno`.**
No hay tabla aparte ni escalas medibles. La sesión y su nota son la misma cosa;
separarlas obligaría a mantener dos entidades sincronizadas sin ganar nada.

**Componente de calendario propio, no MudBlazor / Radzen / FullCalendar.**
Todos traen su propio sistema de estilos y pelean con Tailwind v4. La grilla es
divs absolutos con escala 1 min = 1.2 px. Es menos código que domar una librería.

**Grilla horaria libre, no slots fijos.** Estilo Google Calendar: se hace click en
cualquier punto del día y se elige la duración. Los slots fijos obligan a decidir
la duración de la sesión antes de conocer al paciente.

**Sesiones usadas se calculan, no se almacenan.** Contar `Turnos` con
`Estado == Atendido` no puede desincronizarse; un contador incrementado sí.

**Cobros como entidad `Pago` (movimientos), sin flag "cobrado" en `Turno`.**
Los bonos se pagan por adelantado: el dinero no tiene relación 1:1 con la sesión.
El saldo es `sesiones atendidas × PrecioSesion − Σ Pagos`. El usuario aclaró que
los pagos son un plus — lo importante del producto es el turno.

**Obra social como texto libre en `Paciente`, no tabla catálogo.** Un catálogo
obliga a mantener un ABM antes de poder cargar el primer paciente. El formulario
sugiere valores ya usados con un `datalist`. Normalizable después si hace falta.

**Solapamiento: bloqueo duro.** No hay "forzar igual". Dos turnos contiguos
(10:00–10:40 y 10:40–11:20) NO se superponen: los comparadores son estrictos.

**Cuatro proyectos, con `Core`/`Data`/`Tests` en `net8.0` puro.** Así xUnit corre
sin el workload de MAUI. Toda la lógica testeable vive fuera del proyecto MAUI.

**Esquema completo de base desde la Fase 1**, aunque la UI de tratamientos y pagos
llegue después. `EnsureCreatedAsync` crea todas las tablas de una; las fases
siguientes agregan pantallas, no migraciones.

**Entrega en tres planes**, no uno. La spec era demasiado grande para un solo plan
de implementación; cada fase entrega software usable por su cuenta.

## Archivos tocados

La Fase 1 completa está implementada: solución de cuatro proyectos, backend
completo (entidades, EF Core, `PacienteService`, `TurnoService`) y la app MAUI
Blazor (agenda, pacientes, backup, ajustes). Ver `src/` y `tests/`.

```
.gitignore                                                    .NET/MAUI + *.db + backups/ + node_modules/ + .superpowers/
docs/superpowers/specs/2026-09-04-sistema-turnos-kinesiologia-design.md
docs/superpowers/plans/2026-09-04-fase1-fundaciones-y-agenda.md
docs/handoff-sistema-turnos.md                                este archivo
src/Turnos.Core/, src/Turnos.Data/, src/Turnos.App/            implementados (Fase 1)
tests/Turnos.Tests/                                            36/36 tests en verde
```

Resuelto: `ezequiel_tosso_foto_perfil.png` en la raíz era la foto de perfil de
Ezequiel. Se copió a `src/Turnos.App/wwwroot/images/profesionales/ezequiel-tosso.png`
y se agregó `Profesional.FotoPerfil` (columna nueva vía evolución de esquema) para
mostrarla como avatar junto al selector de profesional. El archivo original en la
raíz sigue sin trackear — queda ahí por si se quiere conservar la fuente; se puede
borrar sin perder nada, ya está copiado adentro del proyecto.

## Estado actual

```
dec1afe  Corregir perdida de nota clinica, error UI sin estilo, excepciones sin
         capturar, edicion de pacientes sin copia y refresco de agenda al cambiar
         de profesional
...      (15 commits de la Fase 1, ver git log 536822b..dec1afe)
```

- Spec: aprobada por el usuario, sin TBDs.
- Plan Fase 1: **ejecutado completo y pusheado a `origin/main`.**
- Planes de Fases 2 y 3: **no escritos**. Se escriben cuando toque.
- Código: Fase 1 completa. 36/36 tests pasando, build de `Turnos.App` limpio.
- Ejecución: `superpowers:subagent-driven-development` — 12 tareas, cada una con
  implementador + revisión propia, más una revisión final de toda la rama que
  encontró y corrigió un bug crítico (ver "Trampas conocidas").

## Dos cosas pendientes de decisión/trabajo antes de la Fase 2

1. **Decisión de producto sin resolver**: el spec pedía que los turnos cancelados
   se vean tachados en la grilla; el plan (autoridad de esta fase) los oculta por
   completo. Se implementó fiel al plan — significa que **cancelar un turno es
   irreversible desde la UI hoy**. Preguntarle a Tomás/Ezequiel si eso es lo que
   quieren antes de que la Fase 2 toque esta zona.
2. **Gap conocido, hoy inalcanzable**: al cambiar el profesional activo,
   `Agenda.razor` recarga los turnos pero no el horario de agenda
   (`HoraInicioAgenda`/`HoraFinAgenda`) del profesional nuevo. No se puede
   disparar todavía porque solo existe un profesional sembrado (Ezequiel Tosso) y
   la Fase 1 no tiene alta de profesionales. Arreglarlo cuando se agregue
   soporte multi-profesional: el handler de `Estado.Cambio` en `Agenda.razor`
   debe releer también esas dos horas, no solo los turnos.

## Próximos pasos

1. Confirmar con el usuario la decisión de producto pendiente (arriba).
2. Escribir el Plan 2 (tratamientos, series recurrentes, historia clínica) con
   `superpowers:writing-plans`, usando la misma spec.
3. Considerar, para la Fase 2, migrar `TurnosDbContext` de inyección directa a
   `IDbContextFactory<TurnosDbContext>` — el registro Transient actual es
   correcto para el change tracker, pero en Blazor Hybrid (un solo scope de DI
   para toda la vida de la app) los contextos nunca se disponen. Techo práctico
   bajo (unos MB), no es urgente, pero conviene resolverlo antes de que la Fase 2
   sume más pantallas que inyecten el contexto directo.

## Trampas conocidas

**`decimal` se guarda como TEXT.** SQLite no tiene decimal y su REAL pierde
centavos. Hay un `ValueConverter` a TEXT invariante. Consecuencia: **SQLite no sabe
ordenar ni sumar ese TEXT**. Toda consulta que ordene o sume montos debe
materializar con `.ToList()` ANTES de hacerlo. Es el bug más probable del proyecto
y no da error: da un número equivocado.

**Nunca correr `dotnet ef migrations`.** El esquema se crea con `EnsureCreatedAsync`
y evoluciona con `PRAGMA table_info` + `ALTER TABLE ADD COLUMN` condicional en
`DatabaseInitializer`. La base del profesional no se puede recrear.

**Los tests usan SQLite real sobre archivo temporal, NO el provider InMemory.**
InMemory no ejecuta el `ValueConverter` de decimal, así que los tests pasarían y
producción fallaría. Está en `BaseDePrueba`.

**`DbContext` va Transient, no Scoped.** En Blazor Hybrid el scope dura toda la vida
de la app: un contexto Scoped acumula entidades trackeadas para siempre. Se registra
con `ServiceLifetime.Transient` en contexto Y opciones.

**No agregar MudBlazor, Radzen ni FullCalendar.** Ya se descartaron por conflicto
con Tailwind v4. Si aparece la tentación de usarlos para el calendario, la decisión
ya se tomó en contra.

**Tailwind v4 no lleva `tailwind.config.js`.** El tema va en `@theme` dentro del CSS.
Si alguien crea el config file, no lo lee nadie.

**`dotnet new maui-blazor` falla sin el workload**: `dotnet workload install maui`.

**Los turnos cancelados no bloquean horario ni se dibujan en la grilla.** Es
intencional: cancelar libera el hueco. Si un test espera verlos, el test está mal.

**Antes de copiar la base hay que hacer `PRAGMA wal_checkpoint(TRUNCATE)`.** Sin eso
el backup puede quedar sin los últimos cambios, que están en el write-ahead log.

**`gh` CLI no está instalado en esta máquina.** Usar `git` plano para todo lo de
GitHub, o instalarlo primero.

**Identidad de git**: commitea como `tomasbasso <tomas.basso@hotmail.com>`, que no es
el mail de la cuenta de Claude Code. Si se quiere el de GitHub, cambiarlo antes de
la primera tanda de commits de código.

**Cuidado al pasar un `Turno` (u otra entidad) entre componentes Razor sin copiar.**
La Fase 1 tuvo un bug crítico (corregido en `dec1afe`) donde `PanelTurno` guardaba
la nota clínica en la base pero no actualizaba el objeto `Turno` en memoria; al
recargar la grilla y editar ese mismo turno después, se pisaba la nota real con el
valor viejo. Pasó porque tres componentes de tareas distintas (`TurnoService`,
`CalendarioSemanal`/`Agenda`, `PanelTurno`) compartían la misma instancia sin que
ninguna revisión de tarea individual pudiera verlo — solo apareció en la revisión
final de toda la rama, trazando el flujo real de un usuario. Regla: cualquier
componente que edita una entidad debe copiarla al empezar a editar, y cualquier
código que guarda cambios debe sincronizar el objeto en memoria o forzar un
re-fetch antes de que otro componente lo vuelva a leer. `Agenda.EditarSeleccionado`
y `Pacientes.razor` (tras el fix) hacen esto bien; es el patrón a copiar.
