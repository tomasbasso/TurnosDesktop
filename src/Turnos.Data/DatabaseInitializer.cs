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
