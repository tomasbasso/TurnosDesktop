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
    /// condicional.
    /// </summary>
    private async Task AplicarEvolucionesAsync()
    {
        if (!await ExisteColumnaAsync("Profesionales", "FotoPerfil"))
            await EjecutarAsync("ALTER TABLE Profesionales ADD COLUMN FotoPerfil TEXT");

        // Una base ya instalada tiene filas creadas antes de esta columna: la
        // siembra no las toca (solo corre en tabla vacia), asi que se completan aca.
        // Idempotente: una vez fijado el valor, el WHERE ya no matchea.
        await EjecutarAsync("""
            UPDATE Profesionales SET FotoPerfil = 'images/profesionales/ezequiel-tosso.png'
            WHERE Nombre = 'Ezequiel Tosso' AND FotoPerfil IS NULL
            """);

        if (!await ExisteColumnaAsync("Turnos", "NombreLibre"))
            await PermitirTurnoSinPacienteAsync();
    }

    /// <summary>
    /// Vuelve opcional Turnos.PacienteId y agrega NombreLibre (el nombre suelto de
    /// un turno sin ficha de paciente). SQLite no permite aflojar un NOT NULL con
    /// ALTER TABLE, asi que se reconstruye la tabla: se la copia con el esquema
    /// nuevo, se borra la vieja y se renombra la nueva en su lugar.
    /// </summary>
    private async Task PermitirTurnoSinPacienteAsync()
    {
        await EjecutarAsync("PRAGMA foreign_keys=OFF");

        await using var transaccion = await contexto.Database.BeginTransactionAsync();
        try
        {
            await EjecutarAsync("""
                CREATE TABLE "Turnos_new" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Turnos" PRIMARY KEY AUTOINCREMENT,
                    "ProfesionalId" INTEGER NOT NULL,
                    "PacienteId" INTEGER NULL,
                    "TratamientoId" INTEGER NULL,
                    "Inicio" TEXT NOT NULL,
                    "Fin" TEXT NOT NULL,
                    "Estado" INTEGER NOT NULL,
                    "SerieId" TEXT NULL,
                    "Observaciones" TEXT NULL,
                    "NotaClinica" TEXT NULL,
                    "NombreLibre" TEXT NULL,
                    CONSTRAINT "FK_Turnos_Pacientes_PacienteId" FOREIGN KEY ("PacienteId") REFERENCES "Pacientes" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Turnos_Profesionales_ProfesionalId" FOREIGN KEY ("ProfesionalId") REFERENCES "Profesionales" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Turnos_Tratamientos_TratamientoId" FOREIGN KEY ("TratamientoId") REFERENCES "Tratamientos" ("Id") ON DELETE SET NULL
                )
                """);

            await EjecutarAsync("""
                INSERT INTO "Turnos_new" ("Id","ProfesionalId","PacienteId","TratamientoId","Inicio","Fin","Estado","SerieId","Observaciones","NotaClinica")
                SELECT "Id","ProfesionalId","PacienteId","TratamientoId","Inicio","Fin","Estado","SerieId","Observaciones","NotaClinica"
                FROM "Turnos"
                """);

            await EjecutarAsync("DROP TABLE \"Turnos\"");
            await EjecutarAsync("ALTER TABLE \"Turnos_new\" RENAME TO \"Turnos\"");

            await EjecutarAsync("CREATE INDEX \"IX_Turnos_PacienteId\" ON \"Turnos\" (\"PacienteId\")");
            await EjecutarAsync("CREATE INDEX \"IX_Turnos_ProfesionalId_Inicio\" ON \"Turnos\" (\"ProfesionalId\", \"Inicio\")");
            await EjecutarAsync("CREATE INDEX \"IX_Turnos_SerieId\" ON \"Turnos\" (\"SerieId\")");
            await EjecutarAsync("CREATE INDEX \"IX_Turnos_TratamientoId\" ON \"Turnos\" (\"TratamientoId\")");

            await transaccion.CommitAsync();
        }
        finally
        {
            await EjecutarAsync("PRAGMA foreign_keys=ON");
        }
    }

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
            Activo = true,
            FotoPerfil = "images/profesionales/ezequiel-tosso.png"
        });
        await contexto.SaveChangesAsync();
    }
}
