using Microsoft.EntityFrameworkCore;
using Turnos.Core;
using Turnos.Core.Entidades;

namespace Turnos.Data.Servicios;

public class PacienteService(TurnosDbContext contexto) : IPacienteService
{
    public async Task<Result<List<Paciente>>> BuscarAsync(string? texto, int limite = 20)
    {
        var consulta = contexto.Pacientes.AsNoTracking().Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            consulta = consulta.Where(p =>
                p.Apellido.ToLower().Contains(t) ||
                p.Nombre.ToLower().Contains(t) ||
                (p.Dni != null && p.Dni.Contains(t)));
        }

        var pacientes = await consulta
            .OrderBy(p => p.Apellido).ThenBy(p => p.Nombre)
            .Take(limite)
            .ToListAsync();

        return Result<List<Paciente>>.Ok(pacientes);
    }

    public async Task<Result<Paciente>> ObtenerAsync(int id)
    {
        var paciente = await contexto.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return paciente is null
            ? Result<Paciente>.Fail("No se encontró el paciente.")
            : Result<Paciente>.Ok(paciente);
    }

    public async Task<Result<Paciente>> GuardarAsync(Paciente paciente)
    {
        if (string.IsNullOrWhiteSpace(paciente.Nombre))
            return Result<Paciente>.Fail("El nombre es obligatorio.");

        if (string.IsNullOrWhiteSpace(paciente.Apellido))
            return Result<Paciente>.Fail("El apellido es obligatorio.");

        paciente.Nombre = paciente.Nombre.Trim();
        paciente.Apellido = paciente.Apellido.Trim();
        paciente.ObraSocial = string.IsNullOrWhiteSpace(paciente.ObraSocial)
            ? null : paciente.ObraSocial.Trim();

        if (paciente.Id == 0)
        {
            paciente.CreadoEl = DateTime.Now;
            contexto.Pacientes.Add(paciente);
        }
        else
        {
            contexto.Pacientes.Update(paciente);
        }

        await contexto.SaveChangesAsync();
        return Result<Paciente>.Ok(paciente, "Paciente guardado.");
    }

    public async Task<Result<List<string>>> ObrasSocialesUsadasAsync()
    {
        var obras = await contexto.Pacientes.AsNoTracking()
            .Where(p => p.ObraSocial != null && p.ObraSocial != "")
            .Select(p => p.ObraSocial!)
            .Distinct()
            .OrderBy(o => o)
            .ToListAsync();

        return Result<List<string>>.Ok(obras);
    }
}
