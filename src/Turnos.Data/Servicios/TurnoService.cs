using Microsoft.EntityFrameworkCore;
using Turnos.Core;
using Turnos.Core.Entidades;
using Turnos.Core.Reglas;

namespace Turnos.Data.Servicios;

public class TurnoService(TurnosDbContext contexto) : ITurnoService
{
    public async Task<Result<List<Turno>>> ObtenerRangoAsync(int profesionalId, DateTime desde, DateTime hasta)
    {
        try
        {
            var turnos = await contexto.Turnos.AsNoTracking()
                .Include(t => t.Paciente)
                .Where(t => t.ProfesionalId == profesionalId && t.Inicio >= desde && t.Inicio < hasta)
                .OrderBy(t => t.Inicio)
                .ToListAsync();

            return Result<List<Turno>>.Ok(turnos);
        }
        catch (Exception ex)
        {
            return Result<List<Turno>>.Fail($"No se pudieron obtener los turnos: {ex.Message}");
        }
    }

    public async Task<Result<List<Turno>>> ObtenerPorPacienteAsync(int pacienteId, int profesionalId)
    {
        try
        {
            var turnos = await contexto.Turnos.AsNoTracking()
                .Where(t => t.PacienteId == pacienteId && t.ProfesionalId == profesionalId)
                .OrderByDescending(t => t.Inicio)
                .ToListAsync();

            return Result<List<Turno>>.Ok(turnos);
        }
        catch (Exception ex)
        {
            return Result<List<Turno>>.Fail($"No se pudo obtener la historia del paciente: {ex.Message}");
        }
    }

    public async Task<Result<Turno>> GuardarAsync(Turno turno)
    {
        if (turno.Fin <= turno.Inicio)
            return Result<Turno>.Fail("El turno tiene que terminar después de empezar.");

        turno.NombreLibre = string.IsNullOrWhiteSpace(turno.NombreLibre) ? null : turno.NombreLibre.Trim();

        if (turno.PacienteId is null or 0 && turno.NombreLibre is null)
            return Result<Turno>.Fail("Hay que elegir un paciente o escribir un nombre.");

        if (turno.PacienteId is not null and not 0)
            turno.NombreLibre = null;

        try
        {
            var conflicto = await BuscarConflictoAsync(turno);
            if (conflicto is not null)
            {
                var quien = conflicto.NombreMostrado == "—" ? "otro turno" : conflicto.NombreMostrado;

                return Result<Turno>.Fail(
                    $"Se superpone con {quien}, {conflicto.Inicio:HH:mm}–{conflicto.Fin:HH:mm}.");
            }

            if (turno.Id == 0) contexto.Turnos.Add(turno);
            else contexto.Turnos.Update(turno);

            await contexto.SaveChangesAsync();
            return Result<Turno>.Ok(turno, "Turno guardado.");
        }
        catch (Exception ex)
        {
            return Result<Turno>.Fail($"No se pudo guardar el turno: {ex.Message}");
        }
    }

    /// <summary>
    /// Busca el primer turno del mismo profesional que se superponga. Excluye los
    /// cancelados y al propio turno cuando se está editando.
    /// </summary>
    private async Task<Turno?> BuscarConflictoAsync(Turno turno)
    {
        // Se traen los candidatos del dia y se evalua el solapamiento en memoria
        // con la misma funcion pura que testea ReglasTurnoTests, para que la regla
        // viva en un solo lugar.
        var dia = turno.Inicio.Date;

        var candidatos = await contexto.Turnos.AsNoTracking()
            .Include(t => t.Paciente)
            .Where(t => t.ProfesionalId == turno.ProfesionalId
                     && t.Id != turno.Id
                     && t.Estado != EstadoTurno.Cancelado
                     && t.Inicio >= dia && t.Inicio < dia.AddDays(1))
            .ToListAsync();

        return candidatos.FirstOrDefault(t =>
            ReglasTurno.SeSuperponen(turno.Inicio, turno.Fin, t.Inicio, t.Fin));
    }

    public async Task<Result> CambiarEstadoAsync(int turnoId, EstadoTurno estado)
    {
        try
        {
            var turno = await contexto.Turnos.FirstOrDefaultAsync(t => t.Id == turnoId);
            if (turno is null) return Result.Fail("No se encontró el turno.");

            turno.Estado = estado;
            await contexto.SaveChangesAsync();
            return Result.Ok("Estado actualizado.");
        }
        catch (Exception ex)
        {
            return Result.Fail($"No se pudo cambiar el estado del turno: {ex.Message}");
        }
    }

    public async Task<Result> GuardarNotaClinicaAsync(int turnoId, string? nota)
    {
        try
        {
            var turno = await contexto.Turnos.FirstOrDefaultAsync(t => t.Id == turnoId);
            if (turno is null) return Result.Fail("No se encontró el turno.");

            turno.NotaClinica = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
            await contexto.SaveChangesAsync();
            return Result.Ok("Nota guardada.");
        }
        catch (Exception ex)
        {
            return Result.Fail($"No se pudo guardar la nota clínica: {ex.Message}");
        }
    }
}
