namespace Turnos.Core.Entidades;

public class Turno
{
    public int Id { get; set; }
    public int ProfesionalId { get; set; }
    public Profesional? Profesional { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Nullable: permite el turno suelto de primera consulta.</summary>
    public int? TratamientoId { get; set; }
    public Tratamiento? Tratamiento { get; set; }

    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public EstadoTurno Estado { get; set; } = EstadoTurno.Programado;

    /// <summary>Agrupa los turnos generados juntos por el generador de series.</summary>
    public Guid? SerieId { get; set; }

    /// <summary>Logístico ("viene con la orden"), no clínico.</summary>
    public string? Observaciones { get; set; }

    /// <summary>La nota libre de la sesión. Solo tiene sentido si Estado == Atendido.</summary>
    public string? NotaClinica { get; set; }

    public int DuracionMinutos => (int)(Fin - Inicio).TotalMinutes;
}
