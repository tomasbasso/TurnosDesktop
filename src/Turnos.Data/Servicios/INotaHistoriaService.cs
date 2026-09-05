using Turnos.Core;
using Turnos.Core.Entidades;

namespace Turnos.Data.Servicios;

public interface INotaHistoriaService
{
    Task<Result<List<NotaHistoria>>> ObtenerPorPacienteAsync(int pacienteId, int profesionalId);
    Task<Result<NotaHistoria>> CrearAsync(int pacienteId, int profesionalId, string texto);
    Task<Result> EditarTextoAsync(int notaId, string texto);
}
