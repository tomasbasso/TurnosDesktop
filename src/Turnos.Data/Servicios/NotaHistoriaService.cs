using Microsoft.EntityFrameworkCore;
using Turnos.Core;
using Turnos.Core.Entidades;

namespace Turnos.Data.Servicios;

public class NotaHistoriaService(TurnosDbContext contexto) : INotaHistoriaService
{
    public async Task<Result<List<NotaHistoria>>> ObtenerPorPacienteAsync(int pacienteId, int profesionalId)
    {
        try
        {
            var notas = await contexto.NotasHistoria.AsNoTracking()
                .Where(n => n.PacienteId == pacienteId && n.ProfesionalId == profesionalId)
                .OrderByDescending(n => n.Id)
                .ToListAsync();

            return Result<List<NotaHistoria>>.Ok(notas);
        }
        catch (Exception ex)
        {
            return Result<List<NotaHistoria>>.Fail($"No se pudieron obtener las notas: {ex.Message}");
        }
    }

    public async Task<Result<NotaHistoria>> CrearAsync(int pacienteId, int profesionalId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return Result<NotaHistoria>.Fail("Hay que escribir algo para guardar.");

        try
        {
            var nota = new NotaHistoria
            {
                PacienteId = pacienteId,
                ProfesionalId = profesionalId,
                Texto = texto.Trim()
            };

            contexto.NotasHistoria.Add(nota);
            await contexto.SaveChangesAsync();
            return Result<NotaHistoria>.Ok(nota, "Nota agregada.");
        }
        catch (Exception ex)
        {
            return Result<NotaHistoria>.Fail($"No se pudo guardar la nota: {ex.Message}");
        }
    }

    public async Task<Result> EditarTextoAsync(int notaId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return Result.Fail("El texto no puede quedar vacío.");

        try
        {
            var nota = await contexto.NotasHistoria.FirstOrDefaultAsync(n => n.Id == notaId);
            if (nota is null) return Result.Fail("No se encontró la nota.");

            nota.Texto = texto.Trim();
            await contexto.SaveChangesAsync();
            return Result.Ok("Nota actualizada.");
        }
        catch (Exception ex)
        {
            return Result.Fail($"No se pudo actualizar la nota: {ex.Message}");
        }
    }
}
