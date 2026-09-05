using Turnos.Core;
using Turnos.Core.Entidades;

namespace Turnos.Data.Servicios;

public interface ITurnoService
{
    Task<Result<List<Turno>>> ObtenerRangoAsync(int profesionalId, DateTime desde, DateTime hasta);
    Task<Result<List<Turno>>> ObtenerPorPacienteAsync(int pacienteId, int profesionalId);
    Task<Result<Turno>> GuardarAsync(Turno turno);
    Task<Result> CambiarEstadoAsync(int turnoId, EstadoTurno estado);
    Task<Result> GuardarNotaClinicaAsync(int turnoId, string? nota);
}
