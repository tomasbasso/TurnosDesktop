using Microsoft.Data.Sqlite;
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

    private static readonly string[] TablasDeLaApp = ["Profesionales", "Pacientes", "Turnos"];

    private const string NoEsCopia = "El archivo elegido no es una copia de seguridad de esta app.";

    public async Task<Result<ResumenCopia>> InspeccionarAsync(string archivo)
    {
        if (!File.Exists(archivo))
            return Result<ResumenCopia>.Fail("No se encontró el archivo elegido.");

        try
        {
            // Solo lectura y sin pool: inspeccionar no puede modificar la copia ni
            // dejarla abierta.
            await using var conexion = new SqliteConnection($"Data Source={archivo};Mode=ReadOnly;Pooling=False");
            await conexion.OpenAsync();

            if (await EscalarAsync(conexion, "PRAGMA quick_check") is not "ok")
                return Result<ResumenCopia>.Fail("La copia está dañada y no se puede usar.");

            foreach (var tabla in TablasDeLaApp)
            {
                var existe = await EscalarAsync(conexion,
                    $"SELECT name FROM sqlite_master WHERE type = 'table' AND name = '{tabla}'");
                if (existe is null) return Result<ResumenCopia>.Fail(NoEsCopia);
            }

            var resumen = new ResumenCopia(
                File.GetLastWriteTime(archivo),
                Convert.ToInt32(await EscalarAsync(conexion, "SELECT COUNT(*) FROM Profesionales")),
                Convert.ToInt32(await EscalarAsync(conexion, "SELECT COUNT(*) FROM Pacientes")),
                Convert.ToInt32(await EscalarAsync(conexion, "SELECT COUNT(*) FROM Turnos")));

            return Result<ResumenCopia>.Ok(resumen);
        }
        catch (SqliteException)
        {
            // SQLite rechaza el archivo (no es una base): para quien restaura es lo mismo.
            return Result<ResumenCopia>.Fail(NoEsCopia);
        }
    }

    public async Task<Result<string>> RestaurarAsync(string archivo, string carpetaRespaldo)
    {
        var inspeccion = await InspeccionarAsync(archivo);
        if (!inspeccion.Success) return Result<string>.Fail(inspeccion.Message);

        var actual = contexto.Database.GetDbConnection().DataSource;
        if (string.Equals(Path.GetFullPath(archivo), Path.GetFullPath(actual), StringComparison.OrdinalIgnoreCase))
            return Result<string>.Fail("Ese archivo es la base que la app está usando ahora: elegí una copia.");

        // Sin un respaldo de lo que hay ahora no se toca nada.
        var respaldo = await CopiarAsync(carpetaRespaldo);
        if (!respaldo.Success)
            return Result<string>.Fail($"No se restauró nada: no se pudo guardar una copia de los datos actuales. {respaldo.Message}");

        try
        {
            // La API de backup de SQLite copia la base página por página sobre la
            // conexión abierta: no hace falta cerrar la app ni pisar el archivo, que
            // en Windows está bloqueado mientras se usa.
            await using (var origen = new SqliteConnection($"Data Source={archivo};Mode=ReadOnly;Pooling=False"))
            {
                await origen.OpenAsync();

                var destino = (SqliteConnection)contexto.Database.GetDbConnection();
                if (destino.State != System.Data.ConnectionState.Open) await destino.OpenAsync();

                origen.BackupDatabase(destino);
            }

            // Una copia de una versión anterior puede no tener tablas o columnas que
            // se agregaron después: se las completa igual que al abrir la app.
            await new DatabaseInitializer(contexto).InicializarAsync();
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(
                $"No se pudo restaurar la copia: {ex.Message}. Los datos anteriores están en {respaldo.Data}");
        }

        SqliteConnection.ClearAllPools();

        return Result<string>.Ok(respaldo.Data!,
            $"Datos restaurados. Lo que había antes quedó guardado en {respaldo.Data}");
    }

    private static async Task<object?> EscalarAsync(SqliteConnection conexion, string sql)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return await comando.ExecuteScalarAsync();
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
