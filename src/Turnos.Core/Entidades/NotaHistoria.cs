namespace Turnos.Core.Entidades;

/// <summary>Entrada manual de la historia clínica, sin turno asociado.</summary>
public class NotaHistoria
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Misma separación de historia clínica por profesional que Turno.</summary>
    public int ProfesionalId { get; set; }
    public Profesional? Profesional { get; set; }

    public DateTime CreadoEl { get; set; } = DateTime.Now;
    public string Texto { get; set; } = string.Empty;
}
