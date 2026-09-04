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
