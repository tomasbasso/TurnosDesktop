using Microsoft.Data.Sqlite;
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
        Assert.Equal("images/profesionales/ezequiel-tosso.png", profesional.FotoPerfil);
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

    /// <summary>
    /// Simula una base ya instalada (esquema previo a este cambio: PacienteId
    /// NOT NULL y sin NombreLibre) y verifica que InicializarAsync la reconstruya
    /// sin perder datos, permitiendo turnos sin paciente de ahi en mas.
    /// </summary>
    [Fact]
    public async Task Inicializar_migraElEsquemaViejoDeTurnos_permitePacienteNuloYPreservaDatos()
    {
        using var baseDePrueba = new BaseDePrueba();

        using (var conexion = new SqliteConnection($"Data Source={baseDePrueba.Ruta}"))
        {
            conexion.Open();

            void Ejecutar(string sql)
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = sql;
                comando.ExecuteNonQuery();
            }

            Ejecutar("""
                CREATE TABLE "Pacientes" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Pacientes" PRIMARY KEY AUTOINCREMENT,
                    "Nombre" TEXT NOT NULL,
                    "Apellido" TEXT NOT NULL,
                    "Dni" TEXT NULL,
                    "Telefono" TEXT NULL,
                    "Email" TEXT NULL,
                    "FechaNacimiento" TEXT NULL,
                    "ObraSocial" TEXT NULL,
                    "NumeroAfiliado" TEXT NULL,
                    "Observaciones" TEXT NULL,
                    "Activo" INTEGER NOT NULL,
                    "CreadoEl" TEXT NOT NULL
                )
                """);
            Ejecutar("""
                CREATE TABLE "Profesionales" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Profesionales" PRIMARY KEY AUTOINCREMENT,
                    "Nombre" TEXT NOT NULL,
                    "Color" TEXT NOT NULL,
                    "HoraInicioAgenda" TEXT NOT NULL,
                    "HoraFinAgenda" TEXT NOT NULL,
                    "Activo" INTEGER NOT NULL,
                    "FotoPerfil" TEXT NULL
                )
                """);
            Ejecutar("""
                CREATE TABLE "Tratamientos" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Tratamientos" PRIMARY KEY AUTOINCREMENT,
                    "PacienteId" INTEGER NOT NULL,
                    "ProfesionalId" INTEGER NOT NULL,
                    "Motivo" TEXT NOT NULL,
                    "SesionesAutorizadas" INTEGER NOT NULL,
                    "PrecioSesion" TEXT NOT NULL,
                    "FechaInicio" TEXT NOT NULL,
                    "FechaAlta" TEXT NULL,
                    "Estado" INTEGER NOT NULL,
                    "Notas" TEXT NULL
                )
                """);
            Ejecutar("""
                CREATE TABLE "Pagos" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Pagos" PRIMARY KEY AUTOINCREMENT,
                    "ProfesionalId" INTEGER NOT NULL,
                    "PacienteId" INTEGER NOT NULL,
                    "TratamientoId" INTEGER NULL,
                    "Monto" TEXT NOT NULL,
                    "FormaPago" INTEGER NOT NULL,
                    "Fecha" TEXT NOT NULL,
                    "Nota" TEXT NULL
                )
                """);
            Ejecutar("""
                CREATE TABLE "Turnos" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Turnos" PRIMARY KEY AUTOINCREMENT,
                    "ProfesionalId" INTEGER NOT NULL,
                    "PacienteId" INTEGER NOT NULL,
                    "TratamientoId" INTEGER NULL,
                    "Inicio" TEXT NOT NULL,
                    "Fin" TEXT NOT NULL,
                    "Estado" INTEGER NOT NULL,
                    "SerieId" TEXT NULL,
                    "Observaciones" TEXT NULL,
                    "NotaClinica" TEXT NULL,
                    CONSTRAINT "FK_Turnos_Pacientes_PacienteId" FOREIGN KEY ("PacienteId") REFERENCES "Pacientes" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Turnos_Profesionales_ProfesionalId" FOREIGN KEY ("ProfesionalId") REFERENCES "Profesionales" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Turnos_Tratamientos_TratamientoId" FOREIGN KEY ("TratamientoId") REFERENCES "Tratamientos" ("Id") ON DELETE SET NULL
                )
                """);
            Ejecutar("CREATE INDEX \"IX_Turnos_ProfesionalId_Inicio\" ON \"Turnos\" (\"ProfesionalId\", \"Inicio\")");
            Ejecutar("CREATE INDEX \"IX_Turnos_SerieId\" ON \"Turnos\" (\"SerieId\")");
            Ejecutar("CREATE INDEX \"IX_Turnos_PacienteId\" ON \"Turnos\" (\"PacienteId\")");
            Ejecutar("CREATE INDEX \"IX_Turnos_TratamientoId\" ON \"Turnos\" (\"TratamientoId\")");

            Ejecutar("""
                INSERT INTO "Profesionales" ("Nombre","Color","HoraInicioAgenda","HoraFinAgenda","Activo","FotoPerfil")
                VALUES ('Ezequiel Tosso','#2563eb','07:00:00','21:00:00',1,'images/profesionales/ezequiel-tosso.png')
                """);
            Ejecutar("""
                INSERT INTO "Pacientes" ("Nombre","Apellido","Activo","CreadoEl")
                VALUES ('Juan','Perez',1,'2026-09-01 10:00:00')
                """);
            Ejecutar("""
                INSERT INTO "Turnos" ("ProfesionalId","PacienteId","Inicio","Fin","Estado")
                VALUES (1,1,'2026-09-08 10:00:00','2026-09-08 10:40:00',0)
                """);
        }

        using var contexto = await baseDePrueba.InicializadaAsync();

        Assert.True(await new DatabaseInitializer(contexto).ExisteColumnaAsync("Turnos", "NombreLibre"));

        var turnoExistente = await contexto.Turnos.SingleAsync();
        Assert.Equal(1, turnoExistente.PacienteId);

        var turnoLibre = new Turno
        {
            ProfesionalId = 1,
            NombreLibre = "Consulta particular",
            Inicio = new DateTime(2026, 9, 8, 11, 0, 0),
            Fin = new DateTime(2026, 9, 8, 11, 40, 0)
        };
        contexto.Turnos.Add(turnoLibre);
        await contexto.SaveChangesAsync();

        Assert.Null(turnoLibre.PacienteId);
        Assert.Equal(2, await contexto.Turnos.CountAsync());
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
