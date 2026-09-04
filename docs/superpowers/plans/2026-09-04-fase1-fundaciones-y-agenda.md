# Fase 1 — Fundaciones y Agenda · Plan de Implementación

> **Para ejecutores agénticos:** SUB-SKILL REQUERIDA: usá `superpowers:subagent-driven-development` (recomendado) o `superpowers:executing-plans` para implementar tarea por tarea. Los pasos usan checkbox (`- [ ]`) para seguimiento.

**Objetivo:** Dejar una app de escritorio que abre, permite dar de alta pacientes y agendar turnos en un calendario semanal, con la base respaldada automáticamente.

**Arquitectura:** Cuatro proyectos. `Turnos.Core` y `Turnos.Data` son `net8.0` puro para que xUnit corra sin el workload de MAUI; toda la lógica testeable vive ahí. `Turnos.App` es MAUI Blazor Hybrid y solo contiene componentes Razor. El esquema completo de la base —incluidas las tablas que la UI todavía no usa— se crea en esta fase, así las fases siguientes agregan pantallas y no migraciones.

**Tech Stack:** .NET 8, MAUI Blazor Hybrid, EF Core 8 + SQLite, Tailwind CSS v4 (CLI), CommunityToolkit.Maui, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-04-sistema-turnos-kinesiologia-design.md`

## Restricciones globales

Aplican a todas las tareas.

- **TFMs:** `Turnos.Core`, `Turnos.Data` y `Turnos.Tests` son `net8.0`. `Turnos.App` es `net8.0-windows10.0.19041.0;net8.0-android`.
- **Sin migraciones de EF.** El esquema se crea con `EnsureCreatedAsync()` y evoluciona con `PRAGMA table_info` + `ALTER TABLE ... ADD COLUMN` condicional. Nunca ejecutar `dotnet ef migrations`.
- **`DbContext` Transient.** Registrado con `ServiceLifetime.Transient` tanto para el contexto como para sus opciones.
- **`decimal` se persiste como TEXT** con `CultureInfo.InvariantCulture` vía `ValueConverter`. **Todo monto se materializa con `.ToList()` antes de sumar u ordenar.**
- **Los servicios devuelven `Result<T>`.** Nunca excepciones como control de flujo.
- **Sin autenticación.** Ningún login, ninguna contraseña, ningún PIN.
- **Tailwind CSS v4 sin `tailwind.config.js`.** El tema va en `@theme` dentro del CSS.
- **Dominio y UI en español.** Entidades, propiedades, métodos y textos.
- **Base:** `turnos.db` en `FileSystem.AppDataDirectory`.

## Estructura de archivos

```
TurnosDesktop.sln
src/
  Turnos.Core/
    Result.cs                        Result y Result<T>
    Enums.cs                         EstadoTurno, EstadoTratamiento, FormaPago
    Entidades/
      Profesional.cs
      Paciente.cs
      Tratamiento.cs
      Turno.cs
      Pago.cs
    Reglas/
      ReglasTurno.cs                 solapamiento — función pura
  Turnos.Data/
    TurnosDbContext.cs               DbSets, converters, índices
    DatabaseInitializer.cs           EnsureCreated + evoluciones + seed
    Servicios/
      IPacienteService.cs / PacienteService.cs
      ITurnoService.cs  / TurnoService.cs
      IBackupService.cs / BackupService.cs
  Turnos.App/
    MauiProgram.cs                   DI, arranque de base, CommunityToolkit
    EstadoApp.cs                     profesional activo y preferencias
    package.json                     Tailwind CLI
    wwwroot/css/app.css              entrada de Tailwind + @theme
    Components/
      Layout/MainLayout.razor        barra superior fija
      Layout/SelectorProfesional.razor
      Layout/Tarjeta.razor           botón grande de la home
      Pages/Home.razor               tarjetas + agenda del día
      Pages/Pacientes.razor          listado y buscador
      Pages/Agenda.razor             página que hospeda el calendario
      Pages/Historia.razor           aviso de etapa futura (Fase 2)
      Pages/Caja.razor               aviso de etapa futura (Fase 3)
      Pages/Config.razor             ajustes y backup manual
      Pacientes/FormPaciente.razor   alta y edición, reusable en modal
      Agenda/CalendarioSemanal.razor la grilla
      Agenda/ModalTurno.razor        alta y edición de turno
      Agenda/BuscadorPaciente.razor  autocomplete
      Agenda/PanelTurno.razor        estado y nota clínica
      Agenda/BotonEstado.razor       botón de estado del panel
tests/
  Turnos.Tests/
    BaseDePrueba.cs                  fixture de SQLite sobre archivo temporal
    Core/ResultTests.cs
    Core/ReglasTurnoTests.cs
    Data/EsquemaTests.cs
    Data/PacienteServiceTests.cs
    Data/TurnoServiceTests.cs
    Data/BackupServiceTests.cs
```

---

### Task 1: Solución, proyectos y `Result<T>`

**Files:**
- Create: `TurnosDesktop.sln`
- Create: `src/Turnos.Core/Turnos.Core.csproj`, `src/Turnos.Core/Result.cs`
- Create: `src/Turnos.Data/Turnos.Data.csproj`
- Create: `tests/Turnos.Tests/Turnos.Tests.csproj`
- Test: `tests/Turnos.Tests/Core/ResultTests.cs`

**Interfaces:**
- Consume: nada.
- Produce: `Turnos.Core.Result` con `Success` (bool), `Message` (string), y las fábricas `Result.Ok(string message = "")` y `Result.Fail(string message)`. `Turnos.Core.Result<T>` hereda de `Result`, agrega `Data` (`T?`), y expone `Result<T>.Ok(T data, string message = "")` y `Result<T>.Fail(string message)`. Todos los servicios de las tareas siguientes devuelven estos tipos.

- [ ] **Paso 1: Crear la solución y los proyectos**

```bash
cd /c/Users/UD/Desktop/SistemaDeTurnos
dotnet new sln -n TurnosDesktop
dotnet new classlib -n Turnos.Core -o src/Turnos.Core -f net8.0
dotnet new classlib -n Turnos.Data -o src/Turnos.Data -f net8.0
dotnet new xunit  -n Turnos.Tests -o tests/Turnos.Tests -f net8.0
rm src/Turnos.Core/Class1.cs src/Turnos.Data/Class1.cs
dotnet sln add src/Turnos.Core src/Turnos.Data tests/Turnos.Tests
dotnet add src/Turnos.Data reference src/Turnos.Core
dotnet add tests/Turnos.Tests reference src/Turnos.Core src/Turnos.Data
```

- [ ] **Paso 2: Habilitar nullable e implicit usings en los tres proyectos**

En cada uno de `src/Turnos.Core/Turnos.Core.csproj`, `src/Turnos.Data/Turnos.Data.csproj` y `tests/Turnos.Tests/Turnos.Tests.csproj`, el `<PropertyGroup>` debe contener exactamente:

```xml
<TargetFramework>net8.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
```

- [ ] **Paso 3: Escribir el test que falla**

Crear `tests/Turnos.Tests/Core/ResultTests.cs`:

```csharp
using Turnos.Core;

namespace Turnos.Tests.Core;

public class ResultTests
{
    [Fact]
    public void Ok_conDatos_exponeExitoYDatos()
    {
        var resultado = Result<int>.Ok(42, "listo");

        Assert.True(resultado.Success);
        Assert.Equal(42, resultado.Data);
        Assert.Equal("listo", resultado.Message);
    }

    [Fact]
    public void Fail_noExponeDatos_yConservaElMensaje()
    {
        var resultado = Result<int>.Fail("no se pudo");

        Assert.False(resultado.Success);
        Assert.Equal(0, resultado.Data);
        Assert.Equal("no se pudo", resultado.Message);
    }

    [Fact]
    public void Ok_sinGenerico_noRequiereMensaje()
    {
        var resultado = Result.Ok();

        Assert.True(resultado.Success);
        Assert.Equal(string.Empty, resultado.Message);
    }
}
```

- [ ] **Paso 4: Correr el test y verificar que falla**

Run: `dotnet test tests/Turnos.Tests`
Expected: FALLA al compilar — `The type or namespace name 'Result' could not be found`.

- [ ] **Paso 5: Implementar `Result`**

Crear `src/Turnos.Core/Result.cs`:

```csharp
namespace Turnos.Core;

/// <summary>
/// Resultado de una operación de servicio. Los servicios nunca lanzan
/// excepciones como control de flujo: devuelven esto y la UI muestra Message.
/// </summary>
public class Result
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static Result Ok(string message = "") => new() { Success = true, Message = message };
    public static Result Fail(string message) => new() { Success = false, Message = message };
}

public class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Ok(T data, string message = "") =>
        new() { Success = true, Data = data, Message = message };

    public new static Result<T> Fail(string message) =>
        new() { Success = false, Message = message };
}
```

- [ ] **Paso 6: Correr el test y verificar que pasa**

Run: `dotnet test tests/Turnos.Tests`
Expected: PASA — 3 tests.

- [ ] **Paso 7: Commit**

```bash
git add TurnosDesktop.sln src tests
git commit -m "Crear solucion de cuatro proyectos y el tipo Result"
```

---

### Task 2: Entidades, enums y la regla de solapamiento

**Files:**
- Create: `src/Turnos.Core/Enums.cs`
- Create: `src/Turnos.Core/Entidades/Profesional.cs`, `Paciente.cs`, `Tratamiento.cs`, `Turno.cs`, `Pago.cs`
- Create: `src/Turnos.Core/Reglas/ReglasTurno.cs`
- Test: `tests/Turnos.Tests/Core/ReglasTurnoTests.cs`

**Interfaces:**
- Consume: nada de tareas anteriores.
- Produce: las cinco entidades POCO del namespace `Turnos.Core.Entidades` con las propiedades listadas abajo; los enums `EstadoTurno`, `EstadoTratamiento` y `FormaPago` en `Turnos.Core`; y `ReglasTurno.SeSuperponen(DateTime inicioA, DateTime finA, DateTime inicioB, DateTime finB) → bool`. `TurnoService` (Task 5) consume esa función.

Se crean las cinco entidades aunque las fases 2 y 3 sean las que las usen en pantalla. El esquema completo desde el arranque evita migraciones para agregar tablas.

- [ ] **Paso 1: Escribir el test que falla**

Crear `tests/Turnos.Tests/Core/ReglasTurnoTests.cs`:

```csharp
using Turnos.Core.Reglas;

namespace Turnos.Tests.Core;

public class ReglasTurnoTests
{
    private static DateTime H(int hora, int minuto) => new(2026, 9, 8, hora, minuto, 0);

    [Theory]
    // contiguos: uno termina justo cuando arranca el otro -> NO se superponen
    [InlineData(10, 00, 10, 40, 10, 40, 11, 20, false)]
    [InlineData(10, 40, 11, 20, 10, 00, 10, 40, false)]
    // disjuntos
    [InlineData(10, 00, 10, 40, 9, 00, 9, 40, false)]
    // solapamiento parcial
    [InlineData(10, 00, 10, 40, 10, 20, 11, 00, true)]
    [InlineData(10, 20, 11, 00, 10, 00, 10, 40, true)]
    // idénticos
    [InlineData(10, 00, 10, 40, 10, 00, 10, 40, true)]
    // uno contenido en el otro
    [InlineData(10, 00, 11, 00, 10, 20, 10, 40, true)]
    [InlineData(10, 20, 10, 40, 10, 00, 11, 00, true)]
    // por un solo minuto
    [InlineData(10, 00, 10, 40, 10, 39, 11, 00, true)]
    public void SeSuperponen_cubreLosCasosDeBorde(
        int hIniA, int mIniA, int hFinA, int mFinA,
        int hIniB, int mIniB, int hFinB, int mFinB,
        bool esperado)
    {
        var resultado = ReglasTurno.SeSuperponen(
            H(hIniA, mIniA), H(hFinA, mFinA),
            H(hIniB, mIniB), H(hFinB, mFinB));

        Assert.Equal(esperado, resultado);
    }
}
```

- [ ] **Paso 2: Correr el test y verificar que falla**

Run: `dotnet test tests/Turnos.Tests --filter ReglasTurnoTests`
Expected: FALLA al compilar — `The type or namespace name 'Reglas' does not exist`.

- [ ] **Paso 3: Implementar la regla**

Crear `src/Turnos.Core/Reglas/ReglasTurno.cs`:

```csharp
namespace Turnos.Core.Reglas;

public static class ReglasTurno
{
    /// <summary>
    /// Dos intervalos se superponen si cada uno empieza antes de que el otro
    /// termine. Los comparadores son estrictos a propósito: dos turnos
    /// contiguos (10:00-10:40 y 10:40-11:20) NO se superponen.
    /// </summary>
    public static bool SeSuperponen(DateTime inicioA, DateTime finA, DateTime inicioB, DateTime finB)
        => inicioA < finB && finA > inicioB;
}
```

- [ ] **Paso 4: Correr el test y verificar que pasa**

Run: `dotnet test tests/Turnos.Tests --filter ReglasTurnoTests`
Expected: PASA — 9 casos.

- [ ] **Paso 5: Crear los enums**

Crear `src/Turnos.Core/Enums.cs`:

```csharp
namespace Turnos.Core;

public enum EstadoTurno
{
    Programado = 0,
    Atendido = 1,
    Ausente = 2,
    Cancelado = 3
}

public enum EstadoTratamiento
{
    Activo = 0,
    Finalizado = 1,
    Abandonado = 2
}

public enum FormaPago
{
    Efectivo = 0,
    Transferencia = 1,
    Tarjeta = 2,
    ObraSocial = 3
}
```

Los valores van explícitos porque se persisten como enteros: cambiar el orden de los miembros corrompería datos existentes.

- [ ] **Paso 6: Crear las entidades**

Crear `src/Turnos.Core/Entidades/Profesional.cs`:

```csharp
namespace Turnos.Core.Entidades;

public class Profesional
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Hex. Reservado para una futura vista "todas las agendas".</summary>
    public string Color { get; set; } = "#2563eb";

    public TimeOnly HoraInicioAgenda { get; set; } = new(7, 0);
    public TimeOnly HoraFinAgenda { get; set; } = new(21, 0);
    public bool Activo { get; set; } = true;
}
```

Crear `src/Turnos.Core/Entidades/Paciente.cs`:

```csharp
namespace Turnos.Core.Entidades;

public class Paciente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateOnly? FechaNacimiento { get; set; }

    /// <summary>Texto libre, no catálogo. El formulario sugiere valores ya cargados.</summary>
    public string? ObraSocial { get; set; }
    public string? NumeroAfiliado { get; set; }

    /// <summary>Permanente: alergias, antecedentes, limitaciones.</summary>
    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime CreadoEl { get; set; } = DateTime.Now;

    public string NombreCompleto => $"{Apellido}, {Nombre}";
}
```

Crear `src/Turnos.Core/Entidades/Tratamiento.cs`:

```csharp
namespace Turnos.Core.Entidades;

public class Tratamiento
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Acá vive la separación de historia clínica por profesional.</summary>
    public int ProfesionalId { get; set; }
    public Profesional? Profesional { get; set; }

    public string Motivo { get; set; } = string.Empty;
    public int SesionesAutorizadas { get; set; }

    /// <summary>Persistido como TEXT invariante. Ver TurnosDbContext.</summary>
    public decimal PrecioSesion { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaAlta { get; set; }
    public EstadoTratamiento Estado { get; set; } = EstadoTratamiento.Activo;
    public string? Notas { get; set; }

    public List<Turno> Turnos { get; set; } = [];
}
```

Crear `src/Turnos.Core/Entidades/Turno.cs`:

```csharp
namespace Turnos.Core.Entidades;

public class Turno
{
    public int Id { get; set; }
    public int ProfesionalId { get; set; }
    public Profesional? Profesional { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Nullable: permite el turno suelto de primera consulta.</summary>
    public int? TratamientoId { get; set; }
    public Tratamiento? Tratamiento { get; set; }

    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public EstadoTurno Estado { get; set; } = EstadoTurno.Programado;

    /// <summary>Agrupa los turnos generados juntos por el generador de series.</summary>
    public Guid? SerieId { get; set; }

    /// <summary>Logístico ("viene con la orden"), no clínico.</summary>
    public string? Observaciones { get; set; }

    /// <summary>La nota libre de la sesión. Solo tiene sentido si Estado == Atendido.</summary>
    public string? NotaClinica { get; set; }

    public int DuracionMinutos => (int)(Fin - Inicio).TotalMinutes;
}
```

Crear `src/Turnos.Core/Entidades/Pago.cs`:

```csharp
namespace Turnos.Core.Entidades;

public class Pago
{
    public int Id { get; set; }
    public int ProfesionalId { get; set; }
    public int PacienteId { get; set; }

    /// <summary>Null si es un pago suelto, no asociado a un tratamiento.</summary>
    public int? TratamientoId { get; set; }

    /// <summary>Persistido como TEXT invariante. Ver TurnosDbContext.</summary>
    public decimal Monto { get; set; }

    public FormaPago FormaPago { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Nota { get; set; }
}
```

- [ ] **Paso 7: Compilar y correr todos los tests**

Run: `dotnet test tests/Turnos.Tests`
Expected: PASA — 12 tests.

- [ ] **Paso 8: Commit**

```bash
git add src/Turnos.Core tests/Turnos.Tests
git commit -m "Agregar entidades, enums y la regla de solapamiento de turnos"
```

---

### Task 3: Contexto EF, conversión de decimal, inicializador y seed

**Files:**
- Modify: `src/Turnos.Data/Turnos.Data.csproj` (paquetes)
- Create: `src/Turnos.Data/TurnosDbContext.cs`
- Create: `src/Turnos.Data/DatabaseInitializer.cs`
- Test: `tests/Turnos.Tests/BaseDePrueba.cs`, `tests/Turnos.Tests/Data/EsquemaTests.cs`

**Interfaces:**
- Consume: las entidades y enums de la Task 2.
- Produce: `Turnos.Data.TurnosDbContext(DbContextOptions<TurnosDbContext>)` con los `DbSet` `Profesionales`, `Pacientes`, `Tratamientos`, `Turnos` y `Pagos`. `Turnos.Data.DatabaseInitializer(TurnosDbContext)` con `Task InicializarAsync()` y `Task<bool> ExisteColumnaAsync(string tabla, string columna)`. Las tareas 4, 5 y 6 consumen el contexto; la 6 llama a `InicializarAsync()` al arrancar.

- [ ] **Paso 1: Agregar los paquetes de EF Core**

```bash
dotnet add src/Turnos.Data package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.11
```

`Turnos.Tests` recibe `Microsoft.Data.Sqlite` de forma transitiva por su referencia a `Turnos.Data`; no hace falta agregarlo.

- [ ] **Paso 2: Escribir el fixture de base de prueba**

Crear `tests/Turnos.Tests/BaseDePrueba.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Turnos.Data;

namespace Turnos.Tests;

/// <summary>
/// Base SQLite real sobre un archivo temporal. NO se usa el provider InMemory:
/// ese no ejecuta el ValueConverter de decimal a TEXT, que es justamente donde
/// aparecen los bugs de este proyecto.
/// </summary>
public sealed class BaseDePrueba : IDisposable
{
    public string Ruta { get; }

    public BaseDePrueba()
    {
        Ruta = Path.Combine(Path.GetTempPath(), $"turnos-test-{Guid.NewGuid():N}.db");
    }

    /// <summary>Un contexto nuevo, como en producción: el DbContext es Transient.</summary>
    public TurnosDbContext Nuevo()
    {
        var opciones = new DbContextOptionsBuilder<TurnosDbContext>()
            .UseSqlite($"Data Source={Ruta}")
            .Options;
        return new TurnosDbContext(opciones);
    }

    /// <summary>Crea el esquema y siembra los datos iniciales.</summary>
    public async Task<TurnosDbContext> InicializadaAsync()
    {
        var contexto = Nuevo();
        await new DatabaseInitializer(contexto).InicializarAsync();
        return contexto;
    }

    /// <summary>Lee un valor crudo, sin pasar por EF ni sus converters.</summary>
    public string? LeerCrudo(string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={Ruta}");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteScalar()?.ToString();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(Ruta)) File.Delete(Ruta);
    }
}
```

- [ ] **Paso 3: Escribir los tests que fallan**

Crear `tests/Turnos.Tests/Data/EsquemaTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Turnos.Core;
using Turnos.Core.Entidades;
using Turnos.Data;

namespace Turnos.Tests.Data;

public class EsquemaTests
{
    [Fact]
    public async Task Inicializar_siembraAEzequielTosso()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();

        var profesional = await contexto.Profesionales.SingleAsync();

        Assert.Equal("Ezequiel Tosso", profesional.Nombre);
        Assert.Equal(new TimeOnly(7, 0), profesional.HoraInicioAgenda);
        Assert.Equal(new TimeOnly(21, 0), profesional.HoraFinAgenda);
        Assert.True(profesional.Activo);
    }

    [Fact]
    public async Task Inicializar_esIdempotente_yNoDuplicaElSeed()
    {
        using var baseDePrueba = new BaseDePrueba();

        using (var primero = await baseDePrueba.InicializadaAsync()) { }
        using (var segundo = await baseDePrueba.InicializadaAsync()) { }

        using var contexto = baseDePrueba.Nuevo();
        Assert.Equal(1, await contexto.Profesionales.CountAsync());
    }

    [Fact]
    public async Task ExisteColumna_distingueColumnasPresentesDeAusentes()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var inicializador = new DatabaseInitializer(contexto);

        Assert.True(await inicializador.ExisteColumnaAsync("Pacientes", "ObraSocial"));
        Assert.False(await inicializador.ExisteColumnaAsync("Pacientes", "ColumnaQueNoExiste"));
    }

    [Fact]
    public async Task Decimal_seGuardaComoTextoInvariante_yVuelveIgual()
    {
        using var baseDePrueba = new BaseDePrueba();
        using (var contexto = await baseDePrueba.InicializadaAsync())
        {
            var paciente = new Paciente { Nombre = "Juan", Apellido = "Perez" };
            contexto.Pacientes.Add(paciente);
            await contexto.SaveChangesAsync();

            contexto.Tratamientos.Add(new Tratamiento
            {
                PacienteId = paciente.Id,
                ProfesionalId = 1,
                Motivo = "Lumbalgia",
                SesionesAutorizadas = 10,
                PrecioSesion = 12345.67m,
                FechaInicio = new DateOnly(2026, 9, 8)
            });
            await contexto.SaveChangesAsync();
        }

        // El valor crudo debe usar punto decimal, sin importar la cultura de la maquina.
        var crudo = baseDePrueba.LeerCrudo("SELECT PrecioSesion FROM Tratamientos LIMIT 1");
        Assert.Equal("12345.67", crudo);

        // Y debe volver identico leido con un contexto nuevo.
        using var otroContexto = baseDePrueba.Nuevo();
        var guardado = await otroContexto.Tratamientos.SingleAsync();
        Assert.Equal(12345.67m, guardado.PrecioSesion);
    }

    [Fact]
    public async Task Enum_sePersisteComoEntero()
    {
        using var baseDePrueba = new BaseDePrueba();
        using (var contexto = await baseDePrueba.InicializadaAsync())
        {
            var paciente = new Paciente { Nombre = "Ana", Apellido = "Ruiz" };
            contexto.Pacientes.Add(paciente);
            await contexto.SaveChangesAsync();

            contexto.Turnos.Add(new Turno
            {
                ProfesionalId = 1,
                PacienteId = paciente.Id,
                Inicio = new DateTime(2026, 9, 8, 10, 0, 0),
                Fin = new DateTime(2026, 9, 8, 10, 40, 0),
                Estado = EstadoTurno.Ausente
            });
            await contexto.SaveChangesAsync();
        }

        Assert.Equal("2", baseDePrueba.LeerCrudo("SELECT Estado FROM Turnos LIMIT 1"));
    }
}
```

- [ ] **Paso 4: Correr los tests y verificar que fallan**

Run: `dotnet test tests/Turnos.Tests --filter EsquemaTests`
Expected: FALLA al compilar — `TurnosDbContext` y `DatabaseInitializer` no existen.

- [ ] **Paso 5: Implementar el contexto**

Crear `src/Turnos.Data/TurnosDbContext.cs`:

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Turnos.Core.Entidades;

namespace Turnos.Data;

public class TurnosDbContext(DbContextOptions<TurnosDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Profesional> Profesionales => Set<Profesional>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Tratamiento> Tratamientos => Set<Tratamiento>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<Pago> Pagos => Set<Pago>();

    /// <summary>
    /// decimal -> TEXT con cultura invariante. SQLite no tiene tipo decimal y el
    /// REAL de punto flotante pierde centavos. Consecuencia importante: SQLite
    /// no sabe ordenar ni sumar este TEXT, asi que TODA consulta que ordene o
    /// sume montos debe materializar con .ToList() ANTES de hacerlo.
    /// </summary>
    private static readonly ValueConverter<decimal, string> DecimalATexto = new(
        valor => valor.ToString(CultureInfo.InvariantCulture),
        texto => decimal.Parse(texto, NumberStyles.Any, CultureInfo.InvariantCulture));

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Tratamiento>().Property(t => t.PrecioSesion).HasConversion(DecimalATexto);
        modelo.Entity<Pago>().Property(p => p.Monto).HasConversion(DecimalATexto);

        modelo.Entity<Paciente>().HasIndex(p => new { p.Apellido, p.Nombre });
        modelo.Entity<Paciente>().HasIndex(p => p.Dni);

        // La consulta que corre en cada cambio de semana del calendario.
        modelo.Entity<Turno>().HasIndex(t => new { t.ProfesionalId, t.Inicio });
        modelo.Entity<Turno>().HasIndex(t => t.SerieId);

        modelo.Entity<Turno>()
            .HasOne(t => t.Tratamiento)
            .WithMany(tr => tr.Turnos)
            .HasForeignKey(t => t.TratamientoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelo.Entity<Tratamiento>().HasIndex(t => new { t.PacienteId, t.ProfesionalId });
        modelo.Entity<Pago>().HasIndex(p => new { p.ProfesionalId, p.Fecha });
    }
}
```

- [ ] **Paso 6: Implementar el inicializador**

Crear `src/Turnos.Data/DatabaseInitializer.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Turnos.Core.Entidades;

namespace Turnos.Data;

/// <summary>
/// Crea y evoluciona el esquema SIN migraciones de EF. Una base ya instalada en
/// la maquina del profesional no se puede recrear: se le agregan columnas.
/// </summary>
public class DatabaseInitializer(TurnosDbContext contexto)
{
    public async Task InicializarAsync()
    {
        await contexto.Database.EnsureCreatedAsync();
        await AplicarEvolucionesAsync();
        await SembrarAsync();
    }

    /// <summary>
    /// Cada columna agregada despues de la v1 se aplica aca, en orden y de forma
    /// condicional. Ejemplo del patron a seguir:
    ///
    ///   if (!await ExisteColumnaAsync("Turnos", "MotivoCancelacion"))
    ///       await EjecutarAsync("ALTER TABLE Turnos ADD COLUMN MotivoCancelacion TEXT");
    ///
    /// La v1 no tiene evoluciones todavia porque EnsureCreatedAsync ya crea el
    /// esquema completo.
    /// </summary>
    private Task AplicarEvolucionesAsync() => Task.CompletedTask;

    public async Task<bool> ExisteColumnaAsync(string tabla, string columna)
    {
        var conexion = contexto.Database.GetDbConnection();
        if (conexion.State != System.Data.ConnectionState.Open)
            await conexion.OpenAsync();

        using var comando = conexion.CreateCommand();
        comando.CommandText = $"PRAGMA table_info({tabla})";

        using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            if (string.Equals(lector.GetString(1), columna, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task EjecutarAsync(string sql) =>
        await contexto.Database.ExecuteSqlRawAsync(sql);

    private async Task SembrarAsync()
    {
        if (await contexto.Profesionales.AnyAsync()) return;

        contexto.Profesionales.Add(new Profesional
        {
            Nombre = "Ezequiel Tosso",
            Color = "#2563eb",
            HoraInicioAgenda = new TimeOnly(7, 0),
            HoraFinAgenda = new TimeOnly(21, 0),
            Activo = true
        });
        await contexto.SaveChangesAsync();
    }
}
```

`EjecutarAsync` queda sin llamadores en la v1 a propósito: es la mitad del patrón de evolución y la primera columna nueva la usa. Si el compilador avisa por método privado sin usar, se resuelve al agregar la primera evolución.

- [ ] **Paso 7: Correr los tests y verificar que pasan**

Run: `dotnet test tests/Turnos.Tests`
Expected: PASA — 17 tests. El test de decimal es el importante: si falla con `"12345,67"` en vez de `"12345.67"`, el `ValueConverter` no está aplicado.

- [ ] **Paso 8: Commit**

```bash
git add src/Turnos.Data tests/Turnos.Tests
git commit -m "Agregar contexto EF, conversion invariante de decimal, inicializador de esquema y seed"
```

---

### Task 4: `PacienteService`

**Files:**
- Create: `src/Turnos.Data/Servicios/IPacienteService.cs`, `src/Turnos.Data/Servicios/PacienteService.cs`
- Test: `tests/Turnos.Tests/Data/PacienteServiceTests.cs`

**Interfaces:**
- Consume: `TurnosDbContext` (Task 3), `Result<T>` (Task 1), `Paciente` (Task 2).
- Produce: `Turnos.Data.Servicios.IPacienteService` con:
  - `Task<Result<List<Paciente>>> BuscarAsync(string? texto, int limite = 20)`
  - `Task<Result<Paciente>> ObtenerAsync(int id)`
  - `Task<Result<Paciente>> GuardarAsync(Paciente paciente)`
  - `Task<Result<List<string>>> ObrasSocialesUsadasAsync()`
  Las tareas 8 y 10 lo consumen.

- [ ] **Paso 1: Escribir los tests que fallan**

Crear `tests/Turnos.Tests/Data/PacienteServiceTests.cs`:

```csharp
using Turnos.Core.Entidades;
using Turnos.Data.Servicios;

namespace Turnos.Tests.Data;

public class PacienteServiceTests
{
    [Fact]
    public async Task Guardar_sinApellido_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);

        var resultado = await servicio.GuardarAsync(new Paciente { Nombre = "Juan" });

        Assert.False(resultado.Success);
        Assert.Contains("apellido", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guardar_pacienteNuevo_asignaIdYFechaDeAlta()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);

        var resultado = await servicio.GuardarAsync(
            new Paciente { Nombre = "Juan", Apellido = "Perez", ObraSocial = "OSDE" });

        Assert.True(resultado.Success);
        Assert.True(resultado.Data!.Id > 0);
        Assert.True(resultado.Data.Activo);
    }

    [Fact]
    public async Task Buscar_encuentraPorApellidoYPorDni_ignorandoMayusculas()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);
        await servicio.GuardarAsync(new Paciente { Nombre = "Juan", Apellido = "Perez", Dni = "30111222" });
        await servicio.GuardarAsync(new Paciente { Nombre = "Ana", Apellido = "Ruiz", Dni = "28999888" });

        var porApellido = await servicio.BuscarAsync("per");
        var porDni = await servicio.BuscarAsync("28999");

        Assert.Single(porApellido.Data!);
        Assert.Equal("Perez", porApellido.Data![0].Apellido);
        Assert.Single(porDni.Data!);
        Assert.Equal("Ruiz", porDni.Data![0].Apellido);
    }

    [Fact]
    public async Task Buscar_sinTexto_devuelveTodosOrdenadosPorApellido()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);
        await servicio.GuardarAsync(new Paciente { Nombre = "Ana", Apellido = "Ruiz" });
        await servicio.GuardarAsync(new Paciente { Nombre = "Juan", Apellido = "Perez" });

        var resultado = await servicio.BuscarAsync(null);

        Assert.Equal(2, resultado.Data!.Count);
        Assert.Equal("Perez", resultado.Data[0].Apellido);
    }

    [Fact]
    public async Task Buscar_noDevuelvePacientesInactivos()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);
        await servicio.GuardarAsync(new Paciente { Nombre = "Ana", Apellido = "Ruiz", Activo = false });

        var resultado = await servicio.BuscarAsync(null);

        Assert.Empty(resultado.Data!);
    }

    [Fact]
    public async Task ObrasSocialesUsadas_devuelveValoresDistintosSinVacios()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);
        await servicio.GuardarAsync(new Paciente { Nombre = "A", Apellido = "A", ObraSocial = "OSDE" });
        await servicio.GuardarAsync(new Paciente { Nombre = "B", Apellido = "B", ObraSocial = "OSDE" });
        await servicio.GuardarAsync(new Paciente { Nombre = "C", Apellido = "C", ObraSocial = "Swiss Medical" });
        await servicio.GuardarAsync(new Paciente { Nombre = "D", Apellido = "D", ObraSocial = null });

        var resultado = await servicio.ObrasSocialesUsadasAsync();

        Assert.Equal(["OSDE", "Swiss Medical"], resultado.Data!);
    }

    [Fact]
    public async Task Obtener_conIdInexistente_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);

        var resultado = await servicio.ObtenerAsync(999);

        Assert.False(resultado.Success);
    }
}
```

- [ ] **Paso 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/Turnos.Tests --filter PacienteServiceTests`
Expected: FALLA al compilar — `PacienteService` no existe.

- [ ] **Paso 3: Escribir la interfaz**

Crear `src/Turnos.Data/Servicios/IPacienteService.cs`:

```csharp
using Turnos.Core;
using Turnos.Core.Entidades;

namespace Turnos.Data.Servicios;

public interface IPacienteService
{
    Task<Result<List<Paciente>>> BuscarAsync(string? texto, int limite = 20);
    Task<Result<Paciente>> ObtenerAsync(int id);
    Task<Result<Paciente>> GuardarAsync(Paciente paciente);
    Task<Result<List<string>>> ObrasSocialesUsadasAsync();
}
```

- [ ] **Paso 4: Implementar el servicio**

Crear `src/Turnos.Data/Servicios/PacienteService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Turnos.Core;
using Turnos.Core.Entidades;

namespace Turnos.Data.Servicios;

public class PacienteService(TurnosDbContext contexto) : IPacienteService
{
    public async Task<Result<List<Paciente>>> BuscarAsync(string? texto, int limite = 20)
    {
        var consulta = contexto.Pacientes.AsNoTracking().Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            consulta = consulta.Where(p =>
                p.Apellido.ToLower().Contains(t) ||
                p.Nombre.ToLower().Contains(t) ||
                (p.Dni != null && p.Dni.Contains(t)));
        }

        var pacientes = await consulta
            .OrderBy(p => p.Apellido).ThenBy(p => p.Nombre)
            .Take(limite)
            .ToListAsync();

        return Result<List<Paciente>>.Ok(pacientes);
    }

    public async Task<Result<Paciente>> ObtenerAsync(int id)
    {
        var paciente = await contexto.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return paciente is null
            ? Result<Paciente>.Fail("No se encontró el paciente.")
            : Result<Paciente>.Ok(paciente);
    }

    public async Task<Result<Paciente>> GuardarAsync(Paciente paciente)
    {
        if (string.IsNullOrWhiteSpace(paciente.Nombre))
            return Result<Paciente>.Fail("El nombre es obligatorio.");

        if (string.IsNullOrWhiteSpace(paciente.Apellido))
            return Result<Paciente>.Fail("El apellido es obligatorio.");

        paciente.Nombre = paciente.Nombre.Trim();
        paciente.Apellido = paciente.Apellido.Trim();
        paciente.ObraSocial = string.IsNullOrWhiteSpace(paciente.ObraSocial)
            ? null : paciente.ObraSocial.Trim();

        if (paciente.Id == 0)
        {
            paciente.CreadoEl = DateTime.Now;
            contexto.Pacientes.Add(paciente);
        }
        else
        {
            contexto.Pacientes.Update(paciente);
        }

        await contexto.SaveChangesAsync();
        return Result<Paciente>.Ok(paciente, "Paciente guardado.");
    }

    public async Task<Result<List<string>>> ObrasSocialesUsadasAsync()
    {
        var obras = await contexto.Pacientes.AsNoTracking()
            .Where(p => p.ObraSocial != null && p.ObraSocial != "")
            .Select(p => p.ObraSocial!)
            .Distinct()
            .OrderBy(o => o)
            .ToListAsync();

        return Result<List<string>>.Ok(obras);
    }
}
```

- [ ] **Paso 5: Correr los tests y verificar que pasan**

Run: `dotnet test tests/Turnos.Tests --filter PacienteServiceTests`
Expected: PASA — 7 tests.

- [ ] **Paso 6: Commit**

```bash
git add src/Turnos.Data tests/Turnos.Tests
git commit -m "Agregar PacienteService con busqueda, alta y sugerencia de obras sociales"
```

---

### Task 5: `TurnoService` y la validación de solapamiento

**Files:**
- Create: `src/Turnos.Data/Servicios/ITurnoService.cs`, `src/Turnos.Data/Servicios/TurnoService.cs`
- Test: `tests/Turnos.Tests/Data/TurnoServiceTests.cs`

**Interfaces:**
- Consume: `TurnosDbContext` (Task 3), `ReglasTurno.SeSuperponen` (Task 2), `Result<T>` (Task 1).
- Produce: `Turnos.Data.Servicios.ITurnoService` con:
  - `Task<Result<List<Turno>>> ObtenerRangoAsync(int profesionalId, DateTime desde, DateTime hasta)`
  - `Task<Result<Turno>> GuardarAsync(Turno turno)`
  - `Task<Result> CambiarEstadoAsync(int turnoId, EstadoTurno estado)`
  - `Task<Result> GuardarNotaClinicaAsync(int turnoId, string? nota)`
  Las tareas 7, 9, 10 y 11 lo consumen.

- [ ] **Paso 1: Escribir los tests que fallan**

Crear `tests/Turnos.Tests/Data/TurnoServiceTests.cs`:

```csharp
using Turnos.Core;
using Turnos.Core.Entidades;
using Turnos.Data;
using Turnos.Data.Servicios;

namespace Turnos.Tests.Data;

public class TurnoServiceTests
{
    private static DateTime H(int hora, int minuto) => new(2026, 9, 8, hora, minuto, 0);

    private static async Task<(TurnosDbContext, TurnoService, int)> PrepararAsync(BaseDePrueba baseDePrueba)
    {
        var contexto = await baseDePrueba.InicializadaAsync();
        var paciente = new Paciente { Nombre = "Juan", Apellido = "Perez" };
        contexto.Pacientes.Add(paciente);
        await contexto.SaveChangesAsync();
        return (contexto, new TurnoService(contexto), paciente.Id);
    }

    private static Turno NuevoTurno(int pacienteId, DateTime inicio, DateTime fin) => new()
    {
        ProfesionalId = 1,
        PacienteId = pacienteId,
        Inicio = inicio,
        Fin = fin
    };

    [Fact]
    public async Task Guardar_turnoValido_loPersiste()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        Assert.True(resultado.Success);
        Assert.True(resultado.Data!.Id > 0);
        Assert.Equal(EstadoTurno.Programado, resultado.Data.Estado);
    }

    [Fact]
    public async Task Guardar_conFinAnteriorAlInicio_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 40), H(10, 0)));

        Assert.False(resultado.Success);
    }

    [Fact]
    public async Task Guardar_turnoSolapado_fallaYNombraAlPaciente()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 20), H(11, 0)));

        Assert.False(resultado.Success);
        Assert.Contains("Perez", resultado.Message);
        Assert.Contains("10:00", resultado.Message);
    }

    [Fact]
    public async Task Guardar_turnoContiguo_seAcepta()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 40), H(11, 20)));

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task Guardar_noChocaContraUnTurnoCancelado()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        var existente = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));
        await servicio.CambiarEstadoAsync(existente.Data!.Id, EstadoTurno.Cancelado);

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task Guardar_noChocaContraOtroProfesional()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        contexto.Profesionales.Add(new Profesional { Nombre = "Otro" });
        await contexto.SaveChangesAsync();
        await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        var deOtro = NuevoTurno(pacienteId, H(10, 0), H(10, 40));
        deOtro.ProfesionalId = 2;
        var resultado = await servicio.GuardarAsync(deOtro);

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task Guardar_alEditarUnTurno_noChocaConsigoMismo()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        var creado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        var editado = creado.Data!;
        editado.Observaciones = "viene con la orden";
        var resultado = await servicio.GuardarAsync(editado);

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task ObtenerRango_devuelveSoloLosDelProfesionalYDelRango()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));
        await servicio.GuardarAsync(NuevoTurno(pacienteId,
            new DateTime(2026, 9, 20, 10, 0, 0), new DateTime(2026, 9, 20, 10, 40, 0)));

        var resultado = await servicio.ObtenerRangoAsync(1,
            new DateTime(2026, 9, 7), new DateTime(2026, 9, 14));

        Assert.Single(resultado.Data!);
    }

    [Fact]
    public async Task GuardarNotaClinica_persisteLaNota()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        var creado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));
        await servicio.CambiarEstadoAsync(creado.Data!.Id, EstadoTurno.Atendido);

        var resultado = await servicio.GuardarNotaClinicaAsync(creado.Data.Id, "Mejor movilidad cervical.");

        Assert.True(resultado.Success);
        using var otro = baseDePrueba.Nuevo();
        Assert.Equal("Mejor movilidad cervical.", otro.Turnos.Single().NotaClinica);
    }
}
```

- [ ] **Paso 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/Turnos.Tests --filter TurnoServiceTests`
Expected: FALLA al compilar — `TurnoService` no existe.

- [ ] **Paso 3: Escribir la interfaz**

Crear `src/Turnos.Data/Servicios/ITurnoService.cs`:

```csharp
using Turnos.Core;
using Turnos.Core.Entidades;

namespace Turnos.Data.Servicios;

public interface ITurnoService
{
    Task<Result<List<Turno>>> ObtenerRangoAsync(int profesionalId, DateTime desde, DateTime hasta);
    Task<Result<Turno>> GuardarAsync(Turno turno);
    Task<Result> CambiarEstadoAsync(int turnoId, EstadoTurno estado);
    Task<Result> GuardarNotaClinicaAsync(int turnoId, string? nota);
}
```

- [ ] **Paso 4: Implementar el servicio**

Crear `src/Turnos.Data/Servicios/TurnoService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Turnos.Core;
using Turnos.Core.Entidades;
using Turnos.Core.Reglas;

namespace Turnos.Data.Servicios;

public class TurnoService(TurnosDbContext contexto) : ITurnoService
{
    public async Task<Result<List<Turno>>> ObtenerRangoAsync(int profesionalId, DateTime desde, DateTime hasta)
    {
        var turnos = await contexto.Turnos.AsNoTracking()
            .Include(t => t.Paciente)
            .Where(t => t.ProfesionalId == profesionalId && t.Inicio >= desde && t.Inicio < hasta)
            .OrderBy(t => t.Inicio)
            .ToListAsync();

        return Result<List<Turno>>.Ok(turnos);
    }

    public async Task<Result<Turno>> GuardarAsync(Turno turno)
    {
        if (turno.Fin <= turno.Inicio)
            return Result<Turno>.Fail("El turno tiene que terminar después de empezar.");

        if (turno.PacienteId == 0)
            return Result<Turno>.Fail("Hay que elegir un paciente.");

        var conflicto = await BuscarConflictoAsync(turno);
        if (conflicto is not null)
        {
            var quien = conflicto.Paciente is null
                ? "otro turno"
                : $"{conflicto.Paciente.Apellido}, {conflicto.Paciente.Nombre}";

            return Result<Turno>.Fail(
                $"Se superpone con {quien}, {conflicto.Inicio:HH:mm}–{conflicto.Fin:HH:mm}.");
        }

        if (turno.Id == 0) contexto.Turnos.Add(turno);
        else contexto.Turnos.Update(turno);

        await contexto.SaveChangesAsync();
        return Result<Turno>.Ok(turno, "Turno guardado.");
    }

    /// <summary>
    /// Busca el primer turno del mismo profesional que se superponga. Excluye los
    /// cancelados y al propio turno cuando se está editando.
    /// </summary>
    private async Task<Turno?> BuscarConflictoAsync(Turno turno)
    {
        // Se traen los candidatos del dia y se evalua el solapamiento en memoria
        // con la misma funcion pura que testea ReglasTurnoTests, para que la regla
        // viva en un solo lugar.
        var dia = turno.Inicio.Date;

        var candidatos = await contexto.Turnos.AsNoTracking()
            .Include(t => t.Paciente)
            .Where(t => t.ProfesionalId == turno.ProfesionalId
                     && t.Id != turno.Id
                     && t.Estado != EstadoTurno.Cancelado
                     && t.Inicio >= dia && t.Inicio < dia.AddDays(1))
            .ToListAsync();

        return candidatos.FirstOrDefault(t =>
            ReglasTurno.SeSuperponen(turno.Inicio, turno.Fin, t.Inicio, t.Fin));
    }

    public async Task<Result> CambiarEstadoAsync(int turnoId, EstadoTurno estado)
    {
        var turno = await contexto.Turnos.FirstOrDefaultAsync(t => t.Id == turnoId);
        if (turno is null) return Result.Fail("No se encontró el turno.");

        turno.Estado = estado;
        await contexto.SaveChangesAsync();
        return Result.Ok("Estado actualizado.");
    }

    public async Task<Result> GuardarNotaClinicaAsync(int turnoId, string? nota)
    {
        var turno = await contexto.Turnos.FirstOrDefaultAsync(t => t.Id == turnoId);
        if (turno is null) return Result.Fail("No se encontró el turno.");

        turno.NotaClinica = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        await contexto.SaveChangesAsync();
        return Result.Ok("Nota guardada.");
    }
}
```

- [ ] **Paso 5: Correr todos los tests y verificar que pasan**

Run: `dotnet test tests/Turnos.Tests`
Expected: PASA — 33 tests.

- [ ] **Paso 6: Commit**

```bash
git add src/Turnos.Data tests/Turnos.Tests
git commit -m "Agregar TurnoService con validacion de solapamiento y cambio de estado"
```

---

### Task 6: Proyecto MAUI, Tailwind v4 y arranque

**Files:**
- Create: `src/Turnos.App/` (proyecto MAUI Blazor completo)
- Create: `src/Turnos.App/package.json`, `src/Turnos.App/wwwroot/css/app.css`
- Create: `src/Turnos.App/EstadoApp.cs`
- Modify: `src/Turnos.App/Turnos.App.csproj`, `src/Turnos.App/MauiProgram.cs`, `src/Turnos.App/wwwroot/index.html`

**Interfaces:**
- Consume: `TurnosDbContext`, `DatabaseInitializer` (Task 3), `IPacienteService`, `ITurnoService` (Tasks 4-5).
- Produce: `Turnos.App.EstadoApp` (singleton) con `int ProfesionalActivoId { get; set; }`, `int DuracionPorDefecto { get; set; }`, `bool MostrarDomingo { get; set; }` y `event Action? Cambio`. Las tareas 7 a 12 lo consumen.

Esta tarea no tiene test automatizado: entrega el shell de la aplicación, y su verificación es abrirla. Los pasos de verificación manual son explícitos y obligatorios.

- [ ] **Paso 1: Crear el proyecto MAUI Blazor**

```bash
cd /c/Users/UD/Desktop/SistemaDeTurnos
dotnet new maui-blazor -n Turnos.App -o src/Turnos.App
dotnet sln add src/Turnos.App
dotnet add src/Turnos.App reference src/Turnos.Core src/Turnos.Data
dotnet add src/Turnos.App package CommunityToolkit.Maui --version 9.1.0
```

Si `dotnet new maui-blazor` falla, falta el workload: `dotnet workload install maui`.

- [ ] **Paso 2: Fijar los TFMs**

En `src/Turnos.App/Turnos.App.csproj`, dejar solo Windows y Android:

```xml
<TargetFrameworks>net8.0-android;net8.0-windows10.0.19041.0</TargetFrameworks>
```

Borrar cualquier TFM de iOS, MacCatalyst o Tizen que haya generado la plantilla, junto con las carpetas `Platforms/iOS`, `Platforms/MacCatalyst` y `Platforms/Tizen`.

- [ ] **Paso 3: Instalar Tailwind v4**

```bash
cd src/Turnos.App
npm init -y
npm install -D tailwindcss @tailwindcss/cli
```

No se crea `tailwind.config.js`: en v4 la configuración vive en el CSS.

- [ ] **Paso 4: Escribir la entrada de Tailwind**

Crear `src/Turnos.App/wwwroot/css/app.css`:

```css
@import "tailwindcss";

@source "../../Components/**/*.razor";
@source "../../*.razor";

@theme {
  --color-marca-50:  #eff6ff;
  --color-marca-100: #dbeafe;
  --color-marca-500: #3b82f6;
  --color-marca-600: #2563eb;
  --color-marca-700: #1d4ed8;

  --color-estado-programado: #3b82f6;
  --color-estado-atendido:   #22c55e;
  --color-estado-ausente:    #f59e0b;
  --color-estado-cancelado:  #9ca3af;
}

html, body {
  height: 100%;
  overscroll-behavior: none;
}
```

Borrar el `bootstrap` y el `app.css` viejo que trae la plantilla en `wwwroot/`.

- [ ] **Paso 5: Compilar Tailwind desde MSBuild**

En `src/Turnos.App/Turnos.App.csproj`, agregar antes del `</Project>` final:

```xml
<Target Name="TailwindInstall" BeforeTargets="TailwindBuild"
        Condition="!Exists('$(MSBuildProjectDirectory)/node_modules')">
  <Exec Command="npm install" WorkingDirectory="$(MSBuildProjectDirectory)" />
</Target>

<Target Name="TailwindBuild" BeforeTargets="BeforeBuild">
  <Exec Command="npx @tailwindcss/cli -i wwwroot/css/app.css -o wwwroot/css/app.min.css --minify"
        WorkingDirectory="$(MSBuildProjectDirectory)" />
</Target>
```

- [ ] **Paso 6: Enlazar el CSS compilado**

En `src/Turnos.App/wwwroot/index.html`, reemplazar todos los `<link>` de CSS del `<head>` por uno solo:

```html
<link rel="stylesheet" href="css/app.min.css" />
```

- [ ] **Paso 7: Implementar `EstadoApp`**

Crear `src/Turnos.App/EstadoApp.cs`:

```csharp
namespace Turnos.App;

/// <summary>
/// Preferencias del usuario. Viven en Preferences de MAUI, NO en SQLite: no son
/// datos del dominio y no tienen que viajar en el backup.
/// </summary>
public class EstadoApp
{
    public event Action? Cambio;

    public int ProfesionalActivoId
    {
        get => Preferences.Get(nameof(ProfesionalActivoId), 0);
        set { Preferences.Set(nameof(ProfesionalActivoId), value); Cambio?.Invoke(); }
    }

    public int DuracionPorDefecto
    {
        get => Preferences.Get(nameof(DuracionPorDefecto), 30);
        set { Preferences.Set(nameof(DuracionPorDefecto), value); Cambio?.Invoke(); }
    }

    public bool MostrarDomingo
    {
        get => Preferences.Get(nameof(MostrarDomingo), false);
        set { Preferences.Set(nameof(MostrarDomingo), value); Cambio?.Invoke(); }
    }

    public DateTime UltimoBackupAutomatico
    {
        get => Preferences.Get(nameof(UltimoBackupAutomatico), DateTime.MinValue);
        set => Preferences.Set(nameof(UltimoBackupAutomatico), value);
    }
}
```

- [ ] **Paso 8: Configurar la inyección de dependencias y el arranque**

Reemplazar el contenido de `src/Turnos.App/MauiProgram.cs`:

```csharp
using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turnos.Data;
using Turnos.Data.Servicios;

namespace Turnos.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        var rutaBase = Path.Combine(FileSystem.AppDataDirectory, "turnos.db");

        // Transient a proposito, contexto y opciones: cada pantalla arranca con un
        // change tracker limpio y no arrastra entidades viejas entre navegaciones.
        builder.Services.AddDbContext<TurnosDbContext>(
            opciones => opciones.UseSqlite($"Data Source={rutaBase}"),
            ServiceLifetime.Transient,
            ServiceLifetime.Transient);

        builder.Services.AddTransient<DatabaseInitializer>();
        builder.Services.AddTransient<IPacienteService, PacienteService>();
        builder.Services.AddTransient<ITurnoService, TurnoService>();
        builder.Services.AddSingleton<EstadoApp>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using (var alcance = app.Services.CreateScope())
        {
            var inicializador = alcance.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            inicializador.InicializarAsync().GetAwaiter().GetResult();
        }

        return app;
    }
}
```

- [ ] **Paso 9: Verificación manual**

Run: `dotnet build src/Turnos.App -f net8.0-windows10.0.19041.0`
Expected: compila, y existe `src/Turnos.App/wwwroot/css/app.min.css` con contenido.

Correr la app en Windows desde Visual Studio o `dotnet run --project src/Turnos.App -f net8.0-windows10.0.19041.0`.

Verificar los tres puntos:
1. La ventana abre sin excepción.
2. Existe el archivo `turnos.db` en `%LOCALAPPDATA%\Packages\...\LocalState\` (la ruta exacta la imprime `FileSystem.AppDataDirectory`).
3. Las clases de Tailwind aplican: agregar temporalmente `<h1 class="text-3xl font-bold text-marca-600">Prueba</h1>` en `Components/Pages/Home.razor` y confirmar que se ve grande, en negrita y azul. Quitarlo después.

- [ ] **Paso 10: Commit**

```bash
git add src/Turnos.App TurnosDesktop.sln
git commit -m "Agregar proyecto MAUI Blazor con Tailwind v4, DI y arranque de la base"
```

---

### Task 7: `MainLayout` y pantalla de inicio

**Files:**
- Modify: `src/Turnos.App/Components/Layout/MainLayout.razor`
- Modify: `src/Turnos.App/Components/Pages/Home.razor`
- Create: `src/Turnos.App/Components/Layout/SelectorProfesional.razor`
- Delete: `src/Turnos.App/Components/Layout/NavMenu.razor`

**Interfaces:**
- Consume: `EstadoApp` (Task 6), `ITurnoService.ObtenerRangoAsync` (Task 5), `TurnosDbContext` para leer profesionales.
- Produce: el layout con barra superior y la home con las cinco tarjetas. Nada que consuman otras tareas más allá de las rutas `/agenda` y `/pacientes`, que las tareas 8 y 9 implementan.

- [ ] **Paso 1: Borrar la navegación de la plantilla**

```bash
rm src/Turnos.App/Components/Layout/NavMenu.razor
rm -f src/Turnos.App/Components/Layout/NavMenu.razor.css
rm -f src/Turnos.App/Components/Layout/MainLayout.razor.css
rm -f src/Turnos.App/Components/Pages/Counter.razor
rm -f src/Turnos.App/Components/Pages/Weather.razor
```

- [ ] **Paso 2: Escribir el selector de profesional**

Crear `src/Turnos.App/Components/Layout/SelectorProfesional.razor`:

```razor
@using Microsoft.EntityFrameworkCore
@using Turnos.Core.Entidades
@using Turnos.Data
@inject TurnosDbContext Contexto
@inject EstadoApp Estado

<select class="rounded-lg border border-marca-100 bg-white px-3 py-1.5 text-sm font-medium text-slate-700"
        value="@Estado.ProfesionalActivoId"
        @onchange="AlCambiar">
    @foreach (var profesional in profesionales)
    {
        <option value="@profesional.Id">@profesional.Nombre</option>
    }
</select>

@code {
    private List<Profesional> profesionales = [];

    protected override async Task OnInitializedAsync()
    {
        profesionales = await Contexto.Profesionales
            .AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync();

        var activoSigueValido = profesionales.Any(p => p.Id == Estado.ProfesionalActivoId);
        if (!activoSigueValido && profesionales.Count > 0)
            Estado.ProfesionalActivoId = profesionales[0].Id;
    }

    private void AlCambiar(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var id))
            Estado.ProfesionalActivoId = id;
    }
}
```

- [ ] **Paso 3: Escribir el layout**

Reemplazar `src/Turnos.App/Components/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase
@inject NavigationManager Navegacion

<div class="flex h-screen flex-col bg-slate-50">
    <header class="flex shrink-0 items-center gap-3 border-b border-slate-200 bg-white px-4 py-2.5">
        @if (Navegacion.ToBaseRelativePath(Navegacion.Uri) != "")
        {
            <button class="rounded-lg px-2 py-1 text-slate-500 hover:bg-slate-100"
                    @onclick="@(() => Navegacion.NavigateTo("/"))">
                ← Inicio
            </button>
        }
        <span class="text-sm font-semibold tracking-tight text-slate-800">Kinesiología</span>
        <div class="ml-auto">
            <SelectorProfesional />
        </div>
    </header>

    <main class="flex min-h-0 flex-1 flex-col overflow-hidden">
        @Body
    </main>
</div>
```

- [ ] **Paso 4: Escribir la pantalla de inicio**

Reemplazar `src/Turnos.App/Components/Pages/Home.razor`:

```razor
@page "/"
@using Turnos.Core
@using Turnos.Core.Entidades
@using Turnos.Data.Servicios
@inject ITurnoService TurnoServicio
@inject EstadoApp Estado
@inject NavigationManager Navegacion

<div class="flex-1 overflow-auto p-6">
    <div class="mx-auto max-w-3xl">

        <div class="grid grid-cols-2 gap-4 sm:grid-cols-3">
            <Tarjeta Titulo="Turnos"  Icono="📅" Ruta="/agenda" />
            <Tarjeta Titulo="Pacientes" Icono="👥" Ruta="/pacientes" />
            <Tarjeta Titulo="Historia clínica" Icono="📋" Ruta="/historia" />
            <Tarjeta Titulo="Caja" Icono="💵" Ruta="/caja" />
            <Tarjeta Titulo="Ajustes" Icono="⚙️" Ruta="/config" />
        </div>

        <h2 class="mt-8 mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
            Hoy · @DateTime.Today.ToString("dddd d 'de' MMMM")
        </h2>

        @if (turnosDeHoy.Count == 0)
        {
            <p class="rounded-xl border border-dashed border-slate-300 p-6 text-center text-sm text-slate-400">
                No hay turnos cargados para hoy.
            </p>
        }
        else
        {
            <ul class="divide-y divide-slate-200 overflow-hidden rounded-xl border border-slate-200 bg-white">
                @foreach (var turno in turnosDeHoy)
                {
                    <li class="flex items-center gap-4 px-4 py-3">
                        <span class="w-14 text-sm font-semibold tabular-nums text-slate-700">
                            @turno.Inicio.ToString("HH:mm")
                        </span>
                        <span class="flex-1 text-sm text-slate-800">
                            @(turno.Paciente?.NombreCompleto ?? "—")
                        </span>
                        <span class="text-xs text-slate-400">@turno.DuracionMinutos min</span>
                    </li>
                }
            </ul>
        }
    </div>
</div>

@code {
    private List<Turno> turnosDeHoy = [];

    protected override async Task OnInitializedAsync()
    {
        var hoy = DateTime.Today;
        var resultado = await TurnoServicio.ObtenerRangoAsync(
            Estado.ProfesionalActivoId, hoy, hoy.AddDays(1));

        if (resultado.Success)
            turnosDeHoy = resultado.Data!.Where(t => t.Estado != EstadoTurno.Cancelado).ToList();
    }
}
```

- [ ] **Paso 5: Escribir el componente de tarjeta**

Crear `src/Turnos.App/Components/Layout/Tarjeta.razor`:

```razor
@inject NavigationManager Navegacion

<button class="flex aspect-square flex-col items-center justify-center gap-2 rounded-2xl border border-slate-200 bg-white shadow-sm transition hover:border-marca-500 hover:shadow-md"
        @onclick="@(() => Navegacion.NavigateTo(Ruta))">
    <span class="text-3xl">@Icono</span>
    <span class="px-2 text-center text-sm font-medium text-slate-700">@Titulo</span>
</button>

@code {
    [Parameter, EditorRequired] public string Titulo { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Icono { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Ruta { get; set; } = string.Empty;
}
```

- [ ] **Paso 6: Crear las páginas pendientes de fases futuras**

Sin esto, dos de las cinco tarjetas de la home llevan a una ruta inexistente y
Blazor muestra su cartel genérico de "nothing at this address".

Crear `src/Turnos.App/Components/Pages/Historia.razor`:

```razor
@page "/historia"

<div class="flex flex-1 items-center justify-center p-6">
    <p class="max-w-sm text-center text-sm text-slate-400">
        La historia clínica se habilita junto con los tratamientos, en la próxima etapa.
    </p>
</div>
```

Crear `src/Turnos.App/Components/Pages/Caja.razor`:

```razor
@page "/caja"

<div class="flex flex-1 items-center justify-center p-6">
    <p class="max-w-sm text-center text-sm text-slate-400">
        La caja se habilita junto con los cobros, en la última etapa.
    </p>
</div>
```

- [ ] **Paso 7: Registrar los using globales**

En `src/Turnos.App/Components/_Imports.razor`, agregar al final:

```razor
@using Turnos.App
@using Turnos.App.Components.Layout
```

- [ ] **Paso 8: Verificación manual**

Run: `dotnet run --project src/Turnos.App -f net8.0-windows10.0.19041.0`

Verificar:
1. La home muestra cinco tarjetas y el nombre **Ezequiel Tosso** en el selector.
2. La barra superior no muestra "← Inicio" en la home.
3. Al hacer click en Turnos, la app navega a `/agenda` (todavía vacía) y aparece el botón "← Inicio".
4. La sección "Hoy" dice que no hay turnos.
5. Las tarjetas de Historia clínica y Caja abren sus páginas con el aviso de etapa futura, no el cartel de error de Blazor.

La tarjeta de Ajustes queda sin destino hasta la Task 12, que crea `/config`.

- [ ] **Paso 9: Commit**

```bash
git add src/Turnos.App
git commit -m "Agregar layout con barra superior, selector de profesional y pantalla de inicio"
```

---

### Task 8: Pantalla de pacientes

**Files:**
- Create: `src/Turnos.App/Components/Pages/Pacientes.razor`
- Create: `src/Turnos.App/Components/Pacientes/FormPaciente.razor`

**Interfaces:**
- Consume: `IPacienteService` (Task 4).
- Produce: `FormPaciente` con los parámetros `[Parameter] Paciente Paciente`, `[Parameter] EventCallback<Paciente> AlGuardar` y `[Parameter] EventCallback AlCancelar`. La Task 10 lo reutiliza dentro del modal de turno para dar de alta un paciente sin salir del alta de turno.

- [ ] **Paso 1: Escribir el formulario reutilizable**

Crear `src/Turnos.App/Components/Pacientes/FormPaciente.razor`:

```razor
@using Turnos.Core.Entidades
@using Turnos.Data.Servicios
@inject IPacienteService PacienteServicio

<div class="space-y-3">
    <div class="grid grid-cols-2 gap-3">
        <label class="block">
            <span class="mb-1 block text-xs font-medium text-slate-600">Apellido *</span>
            <input class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                   @bind="Paciente.Apellido" @bind:event="oninput" />
        </label>
        <label class="block">
            <span class="mb-1 block text-xs font-medium text-slate-600">Nombre *</span>
            <input class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                   @bind="Paciente.Nombre" @bind:event="oninput" />
        </label>
        <label class="block">
            <span class="mb-1 block text-xs font-medium text-slate-600">DNI</span>
            <input class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                   @bind="Paciente.Dni" />
        </label>
        <label class="block">
            <span class="mb-1 block text-xs font-medium text-slate-600">Teléfono</span>
            <input class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                   @bind="Paciente.Telefono" />
        </label>
        <label class="block">
            <span class="mb-1 block text-xs font-medium text-slate-600">Obra social</span>
            <input class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                   list="obras-sociales" @bind="Paciente.ObraSocial" />
            <datalist id="obras-sociales">
                @foreach (var obra in obrasSociales)
                {
                    <option value="@obra" />
                }
            </datalist>
        </label>
        <label class="block">
            <span class="mb-1 block text-xs font-medium text-slate-600">N° de afiliado</span>
            <input class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                   @bind="Paciente.NumeroAfiliado" />
        </label>
    </div>

    <label class="block">
        <span class="mb-1 block text-xs font-medium text-slate-600">
            Observaciones <span class="font-normal text-slate-400">— alergias, antecedentes, limitaciones</span>
        </span>
        <textarea rows="3" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                  @bind="Paciente.Observaciones"></textarea>
    </label>

    @if (mensaje is not null)
    {
        <p class="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">@mensaje</p>
    }

    <div class="flex justify-end gap-2 pt-1">
        <button class="rounded-lg px-4 py-2 text-sm text-slate-600 hover:bg-slate-100"
                @onclick="AlCancelar">Cancelar</button>
        <button class="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700"
                @onclick="Guardar" disabled="@guardando">Guardar</button>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public Paciente Paciente { get; set; } = new();
    [Parameter] public EventCallback<Paciente> AlGuardar { get; set; }
    [Parameter] public EventCallback AlCancelar { get; set; }

    private List<string> obrasSociales = [];
    private string? mensaje;
    private bool guardando;

    protected override async Task OnInitializedAsync()
    {
        var resultado = await PacienteServicio.ObrasSocialesUsadasAsync();
        if (resultado.Success) obrasSociales = resultado.Data!;
    }

    private async Task Guardar()
    {
        guardando = true;
        mensaje = null;

        var resultado = await PacienteServicio.GuardarAsync(Paciente);

        guardando = false;

        if (!resultado.Success)
        {
            mensaje = resultado.Message;
            return;
        }

        await AlGuardar.InvokeAsync(resultado.Data!);
    }
}
```

- [ ] **Paso 2: Escribir la pantalla de listado**

Crear `src/Turnos.App/Components/Pages/Pacientes.razor`:

```razor
@page "/pacientes"
@using Turnos.Core.Entidades
@using Turnos.Data.Servicios
@inject IPacienteService PacienteServicio

<div class="flex min-h-0 flex-1 flex-col p-6">
    <div class="mx-auto flex w-full max-w-3xl min-h-0 flex-1 flex-col">

        <div class="mb-4 flex gap-2">
            <input class="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm"
                   placeholder="Buscar por apellido, nombre o DNI"
                   @bind="texto" @bind:event="oninput" @bind:after="Buscar" />
            <button class="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700"
                    @onclick="NuevoPaciente">＋ Nuevo</button>
        </div>

        @if (enEdicion is not null)
        {
            <div class="mb-4 rounded-xl border border-marca-100 bg-white p-4 shadow-sm">
                <h2 class="mb-3 text-sm font-semibold text-slate-700">
                    @(enEdicion.Id == 0 ? "Nuevo paciente" : "Editar paciente")
                </h2>
                <FormPaciente Paciente="enEdicion"
                              AlGuardar="AlGuardarPaciente"
                              AlCancelar="@(() => enEdicion = null)" />
            </div>
        }

        <ul class="min-h-0 flex-1 divide-y divide-slate-200 overflow-auto rounded-xl border border-slate-200 bg-white">
            @foreach (var paciente in pacientes)
            {
                <li class="flex cursor-pointer items-center gap-3 px-4 py-3 hover:bg-slate-50"
                    @onclick="@(() => enEdicion = paciente)">
                    <span class="flex-1 text-sm font-medium text-slate-800">@paciente.NombreCompleto</span>
                    @if (!string.IsNullOrWhiteSpace(paciente.ObraSocial))
                    {
                        <span class="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">
                            @paciente.ObraSocial
                        </span>
                    }
                    <span class="w-24 text-right text-xs text-slate-400">@paciente.Telefono</span>
                </li>
            }
        </ul>
    </div>
</div>

@code {
    private List<Paciente> pacientes = [];
    private string? texto;
    private Paciente? enEdicion;

    protected override Task OnInitializedAsync() => Buscar();

    private async Task Buscar()
    {
        var resultado = await PacienteServicio.BuscarAsync(texto, limite: 200);
        if (resultado.Success) pacientes = resultado.Data!;
    }

    private void NuevoPaciente() => enEdicion = new Paciente();

    private async Task AlGuardarPaciente(Paciente _)
    {
        enEdicion = null;
        await Buscar();
    }
}
```

- [ ] **Paso 3: Agregar el using del formulario**

En `src/Turnos.App/Components/_Imports.razor`, agregar:

```razor
@using Turnos.App.Components.Pacientes
```

- [ ] **Paso 4: Verificación manual**

Run: `dotnet run --project src/Turnos.App -f net8.0-windows10.0.19041.0`

Verificar:
1. Dar de alta un paciente con apellido "Perez", nombre "Juan", obra social "OSDE". Aparece en la lista.
2. Intentar guardar uno sin apellido: muestra "El apellido es obligatorio." y no lo guarda.
3. Dar de alta un segundo paciente y escribir "OS" en obra social: el `datalist` sugiere "OSDE".
4. Escribir "per" en el buscador: filtra a Perez.
5. Cerrar y volver a abrir la app: los pacientes siguen ahí.

- [ ] **Paso 5: Commit**

```bash
git add src/Turnos.App
git commit -m "Agregar pantalla de pacientes con alta, edicion y sugerencia de obra social"
```

---

### Task 9: Componente `CalendarioSemanal`

**Files:**
- Create: `src/Turnos.App/Components/Agenda/CalendarioSemanal.razor`
- Create: `src/Turnos.App/Components/Pages/Agenda.razor`

**Interfaces:**
- Consume: `ITurnoService.ObtenerRangoAsync` (Task 5), `EstadoApp` (Task 6), `TurnosDbContext` para leer el rango horario del profesional.
- Produce: `CalendarioSemanal` con `[Parameter] DateOnly Lunes`, `[Parameter] List<Turno> Turnos`, `[Parameter] TimeOnly HoraInicio`, `[Parameter] TimeOnly HoraFin`, `[Parameter] bool MostrarDomingo`, `[Parameter] EventCallback<DateTime> AlClickEnVacio` y `[Parameter] EventCallback<Turno> AlClickEnTurno`. Las tareas 10 y 11 se enganchan a esos dos callbacks.

- [ ] **Paso 1: Escribir la grilla**

Crear `src/Turnos.App/Components/Agenda/CalendarioSemanal.razor`:

```razor
@using Turnos.Core
@using Turnos.Core.Entidades

<div class="flex min-h-0 flex-1 overflow-auto bg-white">

    <div class="sticky left-0 z-10 w-14 shrink-0 border-r border-slate-200 bg-white">
        <div style="height:@(CabeceraAlto)px"></div>
        @foreach (var hora in Horas())
        {
            <div class="relative border-t border-slate-100" style="height:@(60 * Escala)px">
                <span class="absolute -top-2 right-1 text-[10px] tabular-nums text-slate-400">
                    @hora.ToString("HH:mm")
                </span>
            </div>
        }
    </div>

    @foreach (var dia in Dias())
    {
        <div class="min-w-[110px] flex-1 border-r border-slate-200 last:border-r-0">

            <div class="sticky top-0 z-10 flex flex-col items-center justify-center border-b border-slate-200 bg-white"
                 style="height:@(CabeceraAlto)px">
                <span class="text-[10px] uppercase tracking-wide text-slate-400">
                    @dia.ToString("ddd")
                </span>
                <span class="text-sm font-semibold @(dia == DateTime.Today ? "text-marca-600" : "text-slate-700")">
                    @dia.Day
                </span>
            </div>

            <div class="relative" style="height:@(AltoTotal)px"
                 @onclick="@(e => ClickEnVacio(dia, e))">

                @foreach (var hora in Horas())
                {
                    <div class="pointer-events-none absolute inset-x-0 border-t border-slate-100"
                         style="top:@(Desplazamiento(hora))px"></div>
                }

                @if (dia == DateTime.Today && AhoraVisible())
                {
                    <div class="pointer-events-none absolute inset-x-0 z-20 border-t-2 border-red-500"
                         style="top:@(Desplazamiento(TimeOnly.FromDateTime(DateTime.Now)))px"></div>
                }

                @foreach (var turno in TurnosDe(dia))
                {
                    <div class="absolute inset-x-1 z-10 overflow-hidden rounded-md border-l-4 px-1.5 py-0.5 text-[11px] leading-tight shadow-sm @Clases(turno.Estado)"
                         style="top:@(Desplazamiento(TimeOnly.FromDateTime(turno.Inicio)))px; height:@(Alto(turno))px"
                         @onclick:stopPropagation="true"
                         @onclick="@(() => AlClickEnTurno.InvokeAsync(turno))">
                        <div class="font-semibold tabular-nums">@turno.Inicio.ToString("HH:mm")</div>
                        <div class="truncate">@(turno.Paciente?.NombreCompleto ?? "—")</div>
                    </div>
                }
            </div>
        </div>
    }
</div>

@code {
    [Parameter, EditorRequired] public DateOnly Lunes { get; set; }
    [Parameter] public List<Turno> Turnos { get; set; } = [];
    [Parameter] public TimeOnly HoraInicio { get; set; } = new(7, 0);
    [Parameter] public TimeOnly HoraFin { get; set; } = new(21, 0);
    [Parameter] public bool MostrarDomingo { get; set; }
    [Parameter] public EventCallback<DateTime> AlClickEnVacio { get; set; }
    [Parameter] public EventCallback<Turno> AlClickEnTurno { get; set; }

    /// <summary>Pixeles por minuto. 30 min = 36 px.</summary>
    private const double Escala = 1.2;
    private const int CabeceraAlto = 48;

    private double AltoTotal => (HoraFin - HoraInicio).TotalMinutes * Escala;

    private IEnumerable<DateTime> Dias()
    {
        var cantidad = MostrarDomingo ? 7 : 6;
        for (var i = 0; i < cantidad; i++)
            yield return Lunes.ToDateTime(TimeOnly.MinValue).AddDays(i);
    }

    private IEnumerable<TimeOnly> Horas()
    {
        for (var h = HoraInicio.Hour; h <= HoraFin.Hour; h++)
            yield return new TimeOnly(h, 0);
    }

    private double Desplazamiento(TimeOnly momento) =>
        (momento - HoraInicio).TotalMinutes * Escala;

    private double Alto(Turno turno) =>
        Math.Max(turno.DuracionMinutos * Escala, 18);

    private bool AhoraVisible()
    {
        var ahora = TimeOnly.FromDateTime(DateTime.Now);
        return ahora >= HoraInicio && ahora <= HoraFin;
    }

    private IEnumerable<Turno> TurnosDe(DateTime dia) =>
        Turnos.Where(t => t.Inicio.Date == dia.Date && t.Estado != EstadoTurno.Cancelado);

    private static string Clases(EstadoTurno estado) => estado switch
    {
        EstadoTurno.Atendido  => "border-l-estado-atendido bg-green-50 text-green-900",
        EstadoTurno.Ausente   => "border-l-estado-ausente bg-amber-50 text-amber-900",
        EstadoTurno.Cancelado => "border-l-estado-cancelado bg-slate-100 text-slate-400 line-through",
        _                     => "border-l-estado-programado bg-marca-50 text-marca-700"
    };

    /// <summary>
    /// Convierte la posicion vertical del click en una hora, redondeada a 15 min.
    /// OffsetY es relativo al div del dia, que arranca justo debajo de la cabecera.
    /// </summary>
    private async Task ClickEnVacio(DateTime dia, MouseEventArgs e)
    {
        var minutosCrudos = e.OffsetY / Escala;
        var minutos = Math.Round(minutosCrudos / 15) * 15;

        var momento = dia.Date
            .Add(HoraInicio.ToTimeSpan())
            .AddMinutes(minutos);

        if (momento.TimeOfDay >= HoraFin.ToTimeSpan()) return;

        await AlClickEnVacio.InvokeAsync(momento);
    }
}
```

- [ ] **Paso 2: Escribir la página que lo hospeda**

Crear `src/Turnos.App/Components/Pages/Agenda.razor`:

```razor
@page "/agenda"
@using Microsoft.EntityFrameworkCore
@using Turnos.Core.Entidades
@using Turnos.Data
@using Turnos.Data.Servicios
@inject ITurnoService TurnoServicio
@inject TurnosDbContext Contexto
@inject EstadoApp Estado

<div class="flex min-h-0 flex-1 flex-col">

    <div class="flex shrink-0 items-center gap-2 border-b border-slate-200 bg-white px-4 py-2">
        <button class="rounded-lg px-2 py-1 text-slate-500 hover:bg-slate-100" @onclick="SemanaAnterior">‹</button>
        <button class="rounded-lg px-3 py-1 text-sm font-medium text-slate-600 hover:bg-slate-100" @onclick="IrAHoy">Hoy</button>
        <button class="rounded-lg px-2 py-1 text-slate-500 hover:bg-slate-100" @onclick="SemanaSiguiente">›</button>
        <span class="ml-2 text-sm font-semibold text-slate-700">@Rotulo()</span>
    </div>

    <CalendarioSemanal Lunes="lunes"
                       Turnos="turnos"
                       HoraInicio="horaInicio"
                       HoraFin="horaFin"
                       MostrarDomingo="Estado.MostrarDomingo"
                       AlClickEnVacio="AlClickEnVacio"
                       AlClickEnTurno="AlClickEnTurno" />
</div>

@code {
    private DateOnly lunes;
    private List<Turno> turnos = [];
    private TimeOnly horaInicio = new(7, 0);
    private TimeOnly horaFin = new(21, 0);

    protected override async Task OnInitializedAsync()
    {
        lunes = LunesDe(DateOnly.FromDateTime(DateTime.Today));

        var profesional = await Contexto.Profesionales.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == Estado.ProfesionalActivoId);

        if (profesional is not null)
        {
            horaInicio = profesional.HoraInicioAgenda;
            horaFin = profesional.HoraFinAgenda;
        }

        await CargarSemana();
    }

    private static DateOnly LunesDe(DateOnly fecha)
    {
        var desplazamiento = ((int)fecha.DayOfWeek + 6) % 7;
        return fecha.AddDays(-desplazamiento);
    }

    private async Task CargarSemana()
    {
        var desde = lunes.ToDateTime(TimeOnly.MinValue);
        var resultado = await TurnoServicio.ObtenerRangoAsync(
            Estado.ProfesionalActivoId, desde, desde.AddDays(7));

        if (resultado.Success) turnos = resultado.Data!;
    }

    private async Task SemanaAnterior()  { lunes = lunes.AddDays(-7); await CargarSemana(); }
    private async Task SemanaSiguiente() { lunes = lunes.AddDays(7);  await CargarSemana(); }
    private async Task IrAHoy()          { lunes = LunesDe(DateOnly.FromDateTime(DateTime.Today)); await CargarSemana(); }

    private string Rotulo()
    {
        var fin = lunes.AddDays(Estado.MostrarDomingo ? 6 : 5);
        return $"{lunes:dd/MM} – {fin:dd/MM/yyyy}";
    }

    // La Task 10 reemplaza estos dos metodos para abrir el modal.
    private Task AlClickEnVacio(DateTime momento) => Task.CompletedTask;
    private Task AlClickEnTurno(Turno turno) => Task.CompletedTask;
}
```

- [ ] **Paso 3: Agregar el using de agenda**

En `src/Turnos.App/Components/_Imports.razor`, agregar:

```razor
@using Turnos.App.Components.Agenda
@using Microsoft.AspNetCore.Components.Web
```

- [ ] **Paso 4: Sembrar datos para verificar a ojo**

Agregar temporalmente al final de `OnInitializedAsync` de `Agenda.razor`, correr una vez, y **borrarlo después**:

```csharp
if (!turnos.Any())
{
    var paciente = await Contexto.Pacientes.FirstOrDefaultAsync();
    if (paciente is not null)
    {
        var hoy = DateTime.Today;
        await TurnoServicio.GuardarAsync(new Turno
        {
            ProfesionalId = Estado.ProfesionalActivoId,
            PacienteId = paciente.Id,
            Inicio = hoy.AddHours(10),
            Fin = hoy.AddHours(10).AddMinutes(40)
        });
        await CargarSemana();
    }
}
```

- [ ] **Paso 5: Verificación manual**

Run: `dotnet run --project src/Turnos.App -f net8.0-windows10.0.19041.0`

Verificar:
1. La grilla muestra seis columnas, de lunes a sábado, con las horas 07:00 a 21:00 en el eje izquierdo.
2. El turno sembrado aparece a la altura de las 10:00, con 48 px de alto (40 min × 1.2), en azul.
3. La columna de hoy tiene el número del día en azul y una línea roja a la hora actual.
4. `‹` y `›` cambian de semana y el rótulo de fechas acompaña; `Hoy` vuelve a la semana actual.
5. Borrar el bloque del Paso 4 y confirmar que el turno sigue apareciendo (quedó persistido).

- [ ] **Paso 6: Commit**

```bash
git add src/Turnos.App
git commit -m "Agregar componente de calendario semanal y pagina de agenda"
```

---

### Task 10: Modal de turno y buscador de paciente

**Files:**
- Create: `src/Turnos.App/Components/Agenda/BuscadorPaciente.razor`
- Create: `src/Turnos.App/Components/Agenda/ModalTurno.razor`
- Modify: `src/Turnos.App/Components/Pages/Agenda.razor`

**Interfaces:**
- Consume: `IPacienteService` (Task 4), `ITurnoService.GuardarAsync` (Task 5), `FormPaciente` (Task 8), los callbacks de `CalendarioSemanal` (Task 9), `EstadoApp.DuracionPorDefecto` (Task 6).
- Produce: `ModalTurno` con `[Parameter] Turno Turno`, `[Parameter] EventCallback AlGuardar`, `[Parameter] EventCallback AlCerrar`. `BuscadorPaciente` con `[Parameter] int PacienteId` y `[Parameter] EventCallback<Paciente> PacienteSeleccionado`.

- [ ] **Paso 1: Escribir el buscador de paciente**

Crear `src/Turnos.App/Components/Agenda/BuscadorPaciente.razor`:

```razor
@using Turnos.Core.Entidades
@using Turnos.Data.Servicios
@inject IPacienteService PacienteServicio

<div class="relative">
    @if (seleccionado is not null)
    {
        <div class="flex items-center gap-2 rounded-lg border border-marca-500 bg-marca-50 px-3 py-2">
            <span class="flex-1 text-sm font-medium text-marca-700">@seleccionado.NombreCompleto</span>
            <button class="text-xs text-slate-500 hover:text-slate-700" @onclick="Limpiar">cambiar</button>
        </div>
    }
    else
    {
        <input class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
               placeholder="Buscar paciente por apellido o DNI"
               @bind="texto" @bind:event="oninput" @bind:after="Buscar" />

        @if (candidatos.Count > 0)
        {
            <ul class="absolute z-30 mt-1 max-h-56 w-full overflow-auto rounded-lg border border-slate-200 bg-white shadow-lg">
                @foreach (var candidato in candidatos)
                {
                    <li class="cursor-pointer px-3 py-2 text-sm hover:bg-marca-50"
                        @onclick="@(() => Seleccionar(candidato))">
                        @candidato.NombreCompleto
                        @if (!string.IsNullOrWhiteSpace(candidato.Dni))
                        {
                            <span class="ml-2 text-xs text-slate-400">@candidato.Dni</span>
                        }
                    </li>
                }
            </ul>
        }

        @if (!string.IsNullOrWhiteSpace(texto) && candidatos.Count == 0)
        {
            <button class="mt-1 w-full rounded-lg border border-dashed border-marca-500 px-3 py-2 text-sm text-marca-600 hover:bg-marca-50"
                    @onclick="@(() => mostrandoAlta = true)">
                ＋ Dar de alta a "@texto"
            </button>
        }
    }
</div>

@if (mostrandoAlta)
{
    <div class="mt-3 rounded-lg border border-marca-100 bg-slate-50 p-3">
        <h3 class="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">Paciente nuevo</h3>
        <FormPaciente Paciente="pacienteNuevo"
                      AlGuardar="AlAltaDePaciente"
                      AlCancelar="@(() => mostrandoAlta = false)" />
    </div>
}

@code {
    [Parameter] public int PacienteId { get; set; }
    [Parameter] public EventCallback<Paciente> PacienteSeleccionado { get; set; }

    private string? texto;
    private List<Paciente> candidatos = [];
    private Paciente? seleccionado;
    private bool mostrandoAlta;
    private Paciente pacienteNuevo = new();

    protected override async Task OnParametersSetAsync()
    {
        if (PacienteId != 0 && seleccionado?.Id != PacienteId)
        {
            var resultado = await PacienteServicio.ObtenerAsync(PacienteId);
            if (resultado.Success) seleccionado = resultado.Data;
        }
        else if (PacienteId == 0)
        {
            seleccionado = null;
        }
    }

    private async Task Buscar()
    {
        if (string.IsNullOrWhiteSpace(texto)) { candidatos = []; return; }

        var resultado = await PacienteServicio.BuscarAsync(texto, limite: 8);
        if (resultado.Success) candidatos = resultado.Data!;
    }

    private async Task Seleccionar(Paciente paciente)
    {
        seleccionado = paciente;
        candidatos = [];
        texto = null;
        await PacienteSeleccionado.InvokeAsync(paciente);
    }

    private void Limpiar()
    {
        seleccionado = null;
        texto = null;
        candidatos = [];
    }

    private async Task AlAltaDePaciente(Paciente paciente)
    {
        mostrandoAlta = false;
        pacienteNuevo = new Paciente();
        await Seleccionar(paciente);
    }
}
```

- [ ] **Paso 2: Escribir el modal de turno**

Crear `src/Turnos.App/Components/Agenda/ModalTurno.razor`:

```razor
@using Turnos.Core
@using Turnos.Core.Entidades
@using Turnos.Data.Servicios
@inject ITurnoService TurnoServicio

<div class="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4"
     @onclick="AlCerrar.InvokeAsync">

    <div class="w-full max-w-md rounded-2xl bg-white p-5 shadow-xl"
         @onclick:stopPropagation="true">

        <h2 class="mb-4 text-base font-semibold text-slate-800">
            @(Turno.Id == 0 ? "Nuevo turno" : "Editar turno")
        </h2>

        <div class="space-y-3">
            <div>
                <span class="mb-1 block text-xs font-medium text-slate-600">Paciente *</span>
                <BuscadorPaciente PacienteId="Turno.PacienteId"
                                  PacienteSeleccionado="AlElegirPaciente" />
            </div>

            <div class="grid grid-cols-3 gap-3">
                <label class="col-span-2 block">
                    <span class="mb-1 block text-xs font-medium text-slate-600">Fecha y hora</span>
                    <input type="datetime-local"
                           class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                           value="@Turno.Inicio.ToString("yyyy-MM-ddTHH:mm")"
                           @onchange="AlCambiarInicio" />
                </label>
                <label class="block">
                    <span class="mb-1 block text-xs font-medium text-slate-600">Duración</span>
                    <select class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                            value="@duracion" @onchange="AlCambiarDuracion">
                        <option value="30">30 min</option>
                        <option value="40">40 min</option>
                        <option value="45">45 min</option>
                        <option value="60">60 min</option>
                    </select>
                </label>
            </div>

            <label class="block">
                <span class="mb-1 block text-xs font-medium text-slate-600">Observaciones</span>
                <input class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                       placeholder="viene con la orden, primera vez, etc."
                       @bind="Turno.Observaciones" />
            </label>

            @if (mensaje is not null)
            {
                <p class="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">@mensaje</p>
            }

            <div class="flex justify-end gap-2 pt-1">
                <button class="rounded-lg px-4 py-2 text-sm text-slate-600 hover:bg-slate-100"
                        @onclick="AlCerrar.InvokeAsync">Cancelar</button>
                <button class="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700"
                        @onclick="Guardar" disabled="@guardando">Guardar</button>
            </div>
        </div>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public Turno Turno { get; set; } = new();
    [Parameter] public EventCallback AlGuardar { get; set; }
    [Parameter] public EventCallback AlCerrar { get; set; }

    private int duracion = 30;
    private string? mensaje;
    private bool guardando;

    protected override void OnParametersSet()
    {
        if (Turno.Fin > Turno.Inicio) duracion = Turno.DuracionMinutos;
    }

    private void AlElegirPaciente(Paciente paciente) => Turno.PacienteId = paciente.Id;

    private void AlCambiarInicio(ChangeEventArgs e)
    {
        if (DateTime.TryParse(e.Value?.ToString(), out var nuevo))
        {
            Turno.Inicio = nuevo;
            Turno.Fin = nuevo.AddMinutes(duracion);
        }
    }

    private void AlCambiarDuracion(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var minutos))
        {
            duracion = minutos;
            Turno.Fin = Turno.Inicio.AddMinutes(minutos);
        }
    }

    private async Task Guardar()
    {
        guardando = true;
        mensaje = null;

        Turno.Fin = Turno.Inicio.AddMinutes(duracion);
        var resultado = await TurnoServicio.GuardarAsync(Turno);

        guardando = false;

        if (!resultado.Success)
        {
            mensaje = resultado.Message;
            return;
        }

        await AlGuardar.InvokeAsync();
    }
}
```

- [ ] **Paso 3: Enganchar el modal a la agenda**

En `src/Turnos.App/Components/Pages/Agenda.razor`, reemplazar los dos métodos placeholder del bloque `@code` por:

```csharp
private Turno? turnoEnEdicion;

private Task AlClickEnVacio(DateTime momento)
{
    turnoEnEdicion = new Turno
    {
        ProfesionalId = Estado.ProfesionalActivoId,
        Inicio = momento,
        Fin = momento.AddMinutes(Estado.DuracionPorDefecto)
    };
    return Task.CompletedTask;
}

private Task AlClickEnTurno(Turno turno)
{
    turnoEnEdicion = new Turno
    {
        Id = turno.Id,
        ProfesionalId = turno.ProfesionalId,
        PacienteId = turno.PacienteId,
        TratamientoId = turno.TratamientoId,
        Inicio = turno.Inicio,
        Fin = turno.Fin,
        Estado = turno.Estado,
        SerieId = turno.SerieId,
        Observaciones = turno.Observaciones,
        NotaClinica = turno.NotaClinica
    };
    return Task.CompletedTask;
}

private async Task AlGuardarTurno()
{
    turnoEnEdicion = null;
    await CargarSemana();
}
```

Se copia el turno en vez de pasar la instancia que vino de `ObtenerRangoAsync`: esa viene con `AsNoTracking` y, si el usuario cancela, no queremos que la grilla ya muestre los cambios.

Y agregar, después del `<CalendarioSemanal ... />`:

```razor
@if (turnoEnEdicion is not null)
{
    <ModalTurno Turno="turnoEnEdicion"
                AlGuardar="AlGuardarTurno"
                AlCerrar="@(() => turnoEnEdicion = null)" />
}
```

- [ ] **Paso 4: Verificación manual**

Run: `dotnet run --project src/Turnos.App -f net8.0-windows10.0.19041.0`

Verificar:
1. Click en un hueco del martes a la altura de las 10:00 → abre el modal con "mar 10:00" ya cargado, redondeado a 15 min.
2. Buscar "per" → aparece Perez en la lista → seleccionarlo → queda fijado con botón "cambiar".
3. Buscar "zzz" (no existe) → aparece "＋ Dar de alta a zzz" → da de alta sin cerrar el modal y queda seleccionado.
4. Guardar con duración 40 → el turno aparece en la grilla al instante, con la altura correcta.
5. Cargar otro turno que pise al anterior → muestra "Se superpone con Perez, Juan, 10:00–10:40." y no lo guarda.
6. Cargar uno contiguo (10:40) → lo acepta.
7. Click en un turno existente → abre el modal en modo edición con los datos cargados.

- [ ] **Paso 5: Commit**

```bash
git add src/Turnos.App
git commit -m "Agregar modal de turno con buscador de paciente y alta inline"
```

---

### Task 11: Acciones de estado y nota clínica desde la grilla

**Files:**
- Create: `src/Turnos.App/Components/Agenda/PanelTurno.razor`
- Modify: `src/Turnos.App/Components/Pages/Agenda.razor`

**Interfaces:**
- Consume: `ITurnoService.CambiarEstadoAsync` y `GuardarNotaClinicaAsync` (Task 5).
- Produce: `PanelTurno` con `[Parameter] Turno Turno`, `[Parameter] EventCallback AlCambiar`, `[Parameter] EventCallback AlCerrar`, `[Parameter] EventCallback AlEditar`.

El click en un turno abre este panel lateral, no el modal de edición: marcar atendido es la acción frecuente, editar es la rara. Editar queda a un botón de distancia.

- [ ] **Paso 1: Escribir el panel lateral**

Crear `src/Turnos.App/Components/Agenda/PanelTurno.razor`:

```razor
@using Turnos.Core
@using Turnos.Core.Entidades
@using Turnos.Data.Servicios
@inject ITurnoService TurnoServicio

<div class="absolute inset-y-0 right-0 z-30 flex w-80 flex-col border-l border-slate-200 bg-white shadow-xl">

    <div class="flex items-start gap-2 border-b border-slate-200 p-4">
        <div class="flex-1">
            <p class="text-sm font-semibold text-slate-800">@(Turno.Paciente?.NombreCompleto ?? "—")</p>
            <p class="text-xs text-slate-500">
                @Turno.Inicio.ToString("dddd d/MM · HH:mm") – @Turno.Fin.ToString("HH:mm")
            </p>
        </div>
        <button class="rounded-lg px-2 text-slate-400 hover:bg-slate-100" @onclick="AlCerrar.InvokeAsync">✕</button>
    </div>

    <div class="grid grid-cols-3 gap-2 border-b border-slate-200 p-3">
        <BotonEstado Etiqueta="Atendido" Activo="@(Turno.Estado == EstadoTurno.Atendido)"
                     Clase="bg-green-600" AlHacerClick="@(() => Cambiar(EstadoTurno.Atendido))" />
        <BotonEstado Etiqueta="Ausente" Activo="@(Turno.Estado == EstadoTurno.Ausente)"
                     Clase="bg-amber-500" AlHacerClick="@(() => Cambiar(EstadoTurno.Ausente))" />
        <BotonEstado Etiqueta="Cancelar" Activo="@(Turno.Estado == EstadoTurno.Cancelado)"
                     Clase="bg-slate-500" AlHacerClick="@(() => Cambiar(EstadoTurno.Cancelado))" />
    </div>

    @if (Turno.Estado == EstadoTurno.Atendido)
    {
        <div class="flex min-h-0 flex-1 flex-col p-4">
            <span class="mb-1 text-xs font-medium text-slate-600">Nota de la sesión</span>
            <textarea class="min-h-0 flex-1 resize-none rounded-lg border border-slate-300 p-2 text-sm"
                      placeholder="Qué se trabajó hoy, cómo respondió, qué sigue."
                      @bind="nota" @bind:event="oninput"></textarea>

            <button class="mt-2 rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700"
                    @onclick="GuardarNota">Guardar nota</button>

            @if (mensaje is not null)
            {
                <p class="mt-2 text-xs text-green-700">@mensaje</p>
            }
        </div>
    }
    else
    {
        <p class="flex-1 p-4 text-xs text-slate-400">
            La nota de la sesión se habilita al marcar el turno como atendido.
        </p>
    }

    <div class="border-t border-slate-200 p-3">
        <button class="w-full rounded-lg px-4 py-2 text-sm text-slate-600 hover:bg-slate-100"
                @onclick="AlEditar.InvokeAsync">Editar fecha, hora o paciente</button>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public Turno Turno { get; set; } = new();
    [Parameter] public EventCallback AlCambiar { get; set; }
    [Parameter] public EventCallback AlCerrar { get; set; }
    [Parameter] public EventCallback AlEditar { get; set; }

    private string? nota;
    private string? mensaje;

    protected override void OnParametersSet() => nota = Turno.NotaClinica;

    private async Task Cambiar(EstadoTurno estado)
    {
        await TurnoServicio.CambiarEstadoAsync(Turno.Id, estado);
        Turno.Estado = estado;
        mensaje = null;
        await AlCambiar.InvokeAsync();
    }

    private async Task GuardarNota()
    {
        var resultado = await TurnoServicio.GuardarNotaClinicaAsync(Turno.Id, nota);
        mensaje = resultado.Success ? "Guardada." : resultado.Message;
        await AlCambiar.InvokeAsync();
    }
}
```

- [ ] **Paso 2: Escribir el botón de estado**

Crear `src/Turnos.App/Components/Agenda/BotonEstado.razor`:

```razor
<button class="rounded-lg px-2 py-2 text-xs font-medium transition @(Activo ? $"{Clase} text-white" : "bg-slate-100 text-slate-600 hover:bg-slate-200")"
        @onclick="AlHacerClick">
    @Etiqueta
</button>

@code {
    [Parameter, EditorRequired] public string Etiqueta { get; set; } = string.Empty;
    [Parameter] public bool Activo { get; set; }
    [Parameter, EditorRequired] public string Clase { get; set; } = string.Empty;
    [Parameter] public EventCallback AlHacerClick { get; set; }
}
```

- [ ] **Paso 3: Conectar el panel a la agenda**

En `src/Turnos.App/Components/Pages/Agenda.razor`:

Envolver el contenido en un contenedor posicionado — cambiar el `div` raíz a:

```razor
<div class="relative flex min-h-0 flex-1 flex-col">
```

Reemplazar `AlClickEnTurno` por:

```csharp
private Turno? turnoSeleccionado;

private Task AlClickEnTurno(Turno turno)
{
    turnoSeleccionado = turno;
    return Task.CompletedTask;
}

private async Task AlCambiarTurno() => await CargarSemana();

private void EditarSeleccionado()
{
    var turno = turnoSeleccionado!;
    turnoSeleccionado = null;
    turnoEnEdicion = new Turno
    {
        Id = turno.Id,
        ProfesionalId = turno.ProfesionalId,
        PacienteId = turno.PacienteId,
        TratamientoId = turno.TratamientoId,
        Inicio = turno.Inicio,
        Fin = turno.Fin,
        Estado = turno.Estado,
        SerieId = turno.SerieId,
        Observaciones = turno.Observaciones,
        NotaClinica = turno.NotaClinica
    };
}
```

Y agregar, junto al bloque del `ModalTurno`:

```razor
@if (turnoSeleccionado is not null)
{
    <PanelTurno Turno="turnoSeleccionado"
                AlCambiar="AlCambiarTurno"
                AlCerrar="@(() => turnoSeleccionado = null)"
                AlEditar="EditarSeleccionado" />
}
```

- [ ] **Paso 4: Verificación manual**

Run: `dotnet run --project src/Turnos.App -f net8.0-windows10.0.19041.0`

Verificar:
1. Click en un turno → abre el panel a la derecha con el nombre y el horario.
2. "Atendido" → el turno se pone verde en la grilla y aparece el textarea de la nota.
3. Escribir una nota, "Guardar nota", cerrar el panel, volver a abrir el turno → la nota sigue ahí.
4. "Cancelar" → el turno desaparece de la grilla (los cancelados no se dibujan).
5. Con el turno cancelado, cargar otro en el mismo horario → lo acepta.
6. "Editar fecha, hora o paciente" → cierra el panel y abre el modal.

- [ ] **Paso 5: Commit**

```bash
git add src/Turnos.App
git commit -m "Agregar panel de turno con cambio de estado y nota clinica"
```

---

### Task 12: Backup de la base

**Files:**
- Create: `src/Turnos.Data/Servicios/IBackupService.cs`, `src/Turnos.Data/Servicios/BackupService.cs`
- Create: `src/Turnos.App/Components/Pages/Config.razor`
- Modify: `src/Turnos.App/MauiProgram.cs`, `src/Turnos.App/App.xaml.cs`
- Test: `tests/Turnos.Tests/Data/BackupServiceTests.cs`

**Interfaces:**
- Consume: `TurnosDbContext` (Task 3), `Result` (Task 1), `EstadoApp.UltimoBackupAutomatico` (Task 6).
- Produce: `Turnos.Data.Servicios.IBackupService` con:
  - `Task<Result<string>> CopiarAsync(string carpetaDestino)` — devuelve la ruta del archivo creado
  - `Task<Result> PurgarAntiguosAsync(string carpetaDestino, int conservar = 7)`

- [ ] **Paso 1: Escribir los tests que fallan**

Crear `tests/Turnos.Tests/Data/BackupServiceTests.cs`:

```csharp
using Turnos.Data.Servicios;

namespace Turnos.Tests.Data;

public class BackupServiceTests
{
    [Fact]
    public async Task Copiar_generaUnArchivoConDatos()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var destino = Directory.CreateTempSubdirectory().FullName;
        var servicio = new BackupService(contexto);

        var resultado = await servicio.CopiarAsync(destino);

        Assert.True(resultado.Success, resultado.Message);
        Assert.True(File.Exists(resultado.Data!));
        Assert.True(new FileInfo(resultado.Data!).Length > 0);

        Directory.Delete(destino, recursive: true);
    }

    [Fact]
    public async Task Copiar_aCarpetaInexistente_laCrea()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var destino = Path.Combine(Path.GetTempPath(), $"backup-{Guid.NewGuid():N}");
        var servicio = new BackupService(contexto);

        var resultado = await servicio.CopiarAsync(destino);

        Assert.True(resultado.Success, resultado.Message);
        Assert.True(Directory.Exists(destino));

        Directory.Delete(destino, recursive: true);
    }

    [Fact]
    public async Task PurgarAntiguos_dejaSoloLosMasRecientes()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var destino = Directory.CreateTempSubdirectory().FullName;
        var servicio = new BackupService(contexto);

        for (var i = 0; i < 5; i++)
        {
            var archivo = Path.Combine(destino, $"turnos-2026090{i}-100000.db");
            await File.WriteAllTextAsync(archivo, "x");
            File.SetLastWriteTime(archivo, DateTime.Now.AddDays(-5 + i));
        }

        var resultado = await servicio.PurgarAntiguosAsync(destino, conservar: 2);

        Assert.True(resultado.Success);
        Assert.Equal(2, Directory.GetFiles(destino, "turnos-*.db").Length);

        Directory.Delete(destino, recursive: true);
    }
}
```

- [ ] **Paso 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/Turnos.Tests --filter BackupServiceTests`
Expected: FALLA al compilar — `BackupService` no existe.

- [ ] **Paso 3: Escribir la interfaz**

Crear `src/Turnos.Data/Servicios/IBackupService.cs`:

```csharp
using Turnos.Core;

namespace Turnos.Data.Servicios;

public interface IBackupService
{
    /// <summary>Copia el archivo .db a la carpeta destino. Devuelve la ruta creada.</summary>
    Task<Result<string>> CopiarAsync(string carpetaDestino);

    Task<Result> PurgarAntiguosAsync(string carpetaDestino, int conservar = 7);
}
```

- [ ] **Paso 4: Implementar el servicio**

Crear `src/Turnos.Data/Servicios/BackupService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Turnos.Core;

namespace Turnos.Data.Servicios;

public class BackupService(TurnosDbContext contexto) : IBackupService
{
    public async Task<Result<string>> CopiarAsync(string carpetaDestino)
    {
        var origen = contexto.Database.GetDbConnection().DataSource;

        if (string.IsNullOrWhiteSpace(origen) || !File.Exists(origen))
            return Result<string>.Fail("No se encontró el archivo de la base de datos.");

        try
        {
            // Vuelca el write-ahead log al archivo principal: sin esto, la copia
            // puede quedar sin los ultimos cambios.
            await contexto.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)");

            Directory.CreateDirectory(carpetaDestino);

            var nombre = $"turnos-{DateTime.Now:yyyyMMdd-HHmmss}.db";
            var destino = Path.Combine(carpetaDestino, nombre);

            File.Copy(origen, destino, overwrite: true);

            return Result<string>.Ok(destino, $"Copia guardada en {destino}");
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"No se pudo copiar la base: {ex.Message}");
        }
    }

    public Task<Result> PurgarAntiguosAsync(string carpetaDestino, int conservar = 7)
    {
        try
        {
            if (!Directory.Exists(carpetaDestino)) return Task.FromResult(Result.Ok());

            var sobrantes = new DirectoryInfo(carpetaDestino)
                .GetFiles("turnos-*.db")
                .OrderByDescending(a => a.LastWriteTime)
                .Skip(conservar);

            foreach (var archivo in sobrantes) archivo.Delete();

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Fail($"No se pudieron borrar copias viejas: {ex.Message}"));
        }
    }
}
```

- [ ] **Paso 5: Correr los tests y verificar que pasan**

Run: `dotnet test tests/Turnos.Tests`
Expected: PASA — 36 tests.

- [ ] **Paso 6: Registrar el servicio y el backup automático**

En `src/Turnos.App/MauiProgram.cs`, agregar junto a los otros servicios:

```csharp
builder.Services.AddTransient<IBackupService, BackupService>();
```

Y después del bloque que llama a `InicializarAsync()`, agregar:

```csharp
using (var alcance = app.Services.CreateScope())
{
    var estado = alcance.ServiceProvider.GetRequiredService<EstadoApp>();
    if (estado.UltimoBackupAutomatico.Date < DateTime.Today)
    {
        var backup = alcance.ServiceProvider.GetRequiredService<IBackupService>();
        var carpeta = Path.Combine(FileSystem.AppDataDirectory, "backups");

        var resultado = backup.CopiarAsync(carpeta).GetAwaiter().GetResult();
        if (resultado.Success)
        {
            backup.PurgarAntiguosAsync(carpeta).GetAwaiter().GetResult();
            estado.UltimoBackupAutomatico = DateTime.Now;
        }
    }
}
```

Agregar el `using Turnos.Data.Servicios;` si no está.

- [ ] **Paso 7: Backup al cerrar la ventana**

En `src/Turnos.App/App.xaml.cs`, agregar el override:

```csharp
protected override Window CreateWindow(IActivationState? activationState)
{
    var window = base.CreateWindow(activationState);

    window.Destroying += (_, _) =>
    {
        using var alcance = IPlatformApplication.Current!.Services.CreateScope();
        var backup = alcance.ServiceProvider.GetRequiredService<IBackupService>();
        var carpeta = Path.Combine(FileSystem.AppDataDirectory, "backups");

        backup.CopiarAsync(carpeta).GetAwaiter().GetResult();
        backup.PurgarAntiguosAsync(carpeta).GetAwaiter().GetResult();
    };

    return window;
}
```

Con los `using Microsoft.Extensions.DependencyInjection;` y `using Turnos.Data.Servicios;` correspondientes. Si la plantilla ya define `CreateWindow`, agregar el handler dentro del método existente en vez de duplicarlo.

- [ ] **Paso 8: Escribir la pantalla de ajustes**

Crear `src/Turnos.App/Components/Pages/Config.razor`:

```razor
@page "/config"
@using CommunityToolkit.Maui.Storage
@using Turnos.Data.Servicios
@inject IBackupService BackupServicio
@inject EstadoApp Estado

<div class="flex-1 overflow-auto p-6">
    <div class="mx-auto max-w-lg space-y-6">

        <section class="rounded-xl border border-slate-200 bg-white p-4">
            <h2 class="mb-3 text-sm font-semibold text-slate-700">Agenda</h2>

            <label class="mb-3 block">
                <span class="mb-1 block text-xs font-medium text-slate-600">Duración por defecto</span>
                <select class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                        value="@Estado.DuracionPorDefecto"
                        @onchange="@(e => Estado.DuracionPorDefecto = int.Parse(e.Value!.ToString()!))">
                    <option value="30">30 min</option>
                    <option value="40">40 min</option>
                    <option value="45">45 min</option>
                    <option value="60">60 min</option>
                </select>
            </label>

            <label class="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" checked="@Estado.MostrarDomingo"
                       @onchange="@(e => Estado.MostrarDomingo = (bool)e.Value!)" />
                Mostrar domingo en el calendario
            </label>
        </section>

        <section class="rounded-xl border border-slate-200 bg-white p-4">
            <h2 class="mb-1 text-sm font-semibold text-slate-700">Copia de seguridad</h2>
            <p class="mb-3 text-xs text-slate-500">
                La app hace una copia automática al abrir, una vez por día, y otra al cerrar.
                Conserva las últimas 7.
            </p>

            <button class="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700"
                    @onclick="CopiarAhora">Guardar una copia ahora</button>

            @if (mensaje is not null)
            {
                <p class="mt-3 break-all rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600">@mensaje</p>
            }
        </section>
    </div>
</div>

@code {
    private string? mensaje;

    private async Task CopiarAhora()
    {
        var carpeta = await FolderPicker.Default.PickAsync(CancellationToken.None);

        if (!carpeta.IsSuccessful)
        {
            mensaje = "No se eligió ninguna carpeta.";
            return;
        }

        var resultado = await BackupServicio.CopiarAsync(carpeta.Folder!.Path);
        mensaje = resultado.Message;
    }
}
```

- [ ] **Paso 9: Verificación manual**

Run: `dotnet run --project src/Turnos.App -f net8.0-windows10.0.19041.0`

Verificar:
1. Existe `%LOCALAPPDATA%\...\LocalState\backups\turnos-<fecha>-<hora>.db` después del primer arranque del día.
2. Cerrar y volver a abrir la app en el mismo día → hay dos archivos (el del cierre y no un segundo automático de arranque).
3. En Ajustes, "Guardar una copia ahora" → abre el selector de carpeta y deja el archivo donde se elija.
4. Cambiar la duración por defecto a 45, ir a la agenda, hacer click en un hueco → el modal abre con 45 min.
5. Marcar "Mostrar domingo" → la grilla pasa a siete columnas.

- [ ] **Paso 10: Commit final de la fase**

```bash
git add src tests
git commit -m "Agregar backup manual, automatico y al cerrar, mas pantalla de ajustes"
git push
```

---

## Verificación de la fase completa

Antes de dar la Fase 1 por terminada:

- [ ] `dotnet test tests/Turnos.Tests` — 36 tests en verde
- [ ] `dotnet build src/Turnos.App -f net8.0-windows10.0.19041.0` — sin errores ni warnings nuevos
- [ ] Recorrido completo a mano: abrir la app → dar de alta un paciente con obra social → cargar tres turnos en la semana → intentar uno solapado y ver que se rechaza → marcar uno como atendido y escribirle la nota → cerrar y reabrir, y confirmar que todo sobrevivió
- [ ] La carpeta `backups/` tiene al menos un archivo y ninguno se subió al repo (`git status` limpio)

## Qué queda para la Fase 2

Entidad `Tratamiento` en pantalla, generador de series recurrentes con vista previa de fechas y detección de choques, contador "sesión N de M" en el modal de turno, historia clínica del paciente en orden cronológico con la nota de la sesión anterior como contexto. Las tablas ya existen; la Fase 2 agrega pantallas y servicios, no esquema.
