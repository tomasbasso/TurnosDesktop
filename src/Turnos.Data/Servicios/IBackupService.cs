using Turnos.Core;

namespace Turnos.Data.Servicios;

/// <summary>Lo que trae una copia, para mostrarlo antes de restaurarla.</summary>
public record ResumenCopia(DateTime Fecha, int Profesionales, int Pacientes, int Turnos);

public interface IBackupService
{
    /// <summary>Copia el archivo .db a la carpeta destino. Devuelve la ruta creada.</summary>
    Task<Result<string>> CopiarAsync(string carpetaDestino);

    Task<Result> PurgarAntiguosAsync(string carpetaDestino, int conservar = 7);

    /// <summary>Verifica que el archivo sea una copia de esta app, sin modificarlo, y cuenta lo que trae.</summary>
    Task<Result<ResumenCopia>> InspeccionarAsync(string archivo);

    /// <summary>
    /// Reemplaza todos los datos por los de la copia. Antes guarda lo que había en
    /// <paramref name="carpetaRespaldo"/> y devuelve la ruta de ese respaldo.
    /// </summary>
    Task<Result<string>> RestaurarAsync(string archivo, string carpetaRespaldo);
}
