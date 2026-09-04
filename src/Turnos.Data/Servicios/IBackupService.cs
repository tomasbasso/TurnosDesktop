using Turnos.Core;

namespace Turnos.Data.Servicios;

public interface IBackupService
{
    /// <summary>Copia el archivo .db a la carpeta destino. Devuelve la ruta creada.</summary>
    Task<Result<string>> CopiarAsync(string carpetaDestino);

    Task<Result> PurgarAntiguosAsync(string carpetaDestino, int conservar = 7);
}
