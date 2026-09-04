# Handoff — Sistema de turnos de kinesiología

Última actualización: 2026-09-04. Repo: https://github.com/tomasbasso/TurnosDesktop (branch `main`).

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

Todo lo que existe hoy es documentación y configuración. **No hay una sola línea
de C# escrita.**

```
.gitignore                                                    .NET/MAUI + *.db + backups/ + node_modules/
docs/superpowers/specs/2026-09-04-sistema-turnos-kinesiologia-design.md
docs/superpowers/plans/2026-09-04-fase1-fundaciones-y-agenda.md
docs/handoff-sistema-turnos.md                                este archivo
```

Sin trackear: `ezequiel_tosso_foto_perfil.png` en la raíz. No se decidió qué es —
puede ser el avatar del profesional o haberse colado. Preguntar antes de moverlo o
borrarlo.

## Estado actual

```
06815fa  Agregar plan de implementacion de la Fase 1 y obra social en Paciente
cf7216e  Agregar spec de diseno del sistema de turnos
```

- Spec: **aprobada por el usuario** ("el spect esta bien"), auto-revisada, sin TBDs.
- Plan Fase 1: escrito, auto-revisado, 12 tareas con código real en cada paso.
- Planes de Fases 2 y 3: **no escritos**. Se escriben cuando la fase anterior cierre.
- Código: no empezado.

Estamos parados justo antes de la Task 1 del Plan 1.

## Próximos pasos

1. **Decisión pendiente del usuario**: cómo ejecutar el Plan 1 —
   `superpowers:subagent-driven-development` (un subagente fresco por tarea, revisión
   entre tareas) o `superpowers:executing-plans` (en la sesión, por lotes con
   checkpoints). Se le ofreció y todavía no eligió.
2. Ejecutar las 12 tareas del Plan 1 en orden. Cada una termina en commit propio.
3. Cerrar la Fase 1 con el recorrido manual completo que está al final del plan.
4. Escribir el Plan 2 (tratamientos, series recurrentes, historia clínica) con
   `superpowers:writing-plans`, usando la misma spec.

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
