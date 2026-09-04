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
