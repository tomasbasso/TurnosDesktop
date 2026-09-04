namespace Turnos.Core.Entidades;

public class Tratamiento
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Acá vive la separación de historia clínica por profesional.</summary>
    public int ProfesionalId { get; set; }
    public Profesional? Profesional { get; set; }

    public string Motivo { get; set; } = string.Empty;
    public int SesionesAutorizadas { get; set; }

    /// <summary>Persistido como TEXT invariante. Ver TurnosDbContext.</summary>
    public decimal PrecioSesion { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaAlta { get; set; }
    public EstadoTratamiento Estado { get; set; } = EstadoTratamiento.Activo;
    public string? Notas { get; set; }

    public List<Turno> Turnos { get; set; } = [];
}
