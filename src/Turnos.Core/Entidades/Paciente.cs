namespace Turnos.Core.Entidades;

public class Paciente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateOnly? FechaNacimiento { get; set; }

    /// <summary>Texto libre, no catálogo. El formulario sugiere valores ya cargados.</summary>
    public string? ObraSocial { get; set; }
    public string? NumeroAfiliado { get; set; }

    /// <summary>Permanente: alergias, antecedentes, limitaciones.</summary>
    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime CreadoEl { get; set; } = DateTime.Now;

    public string NombreCompleto => $"{Apellido}, {Nombre}";
}
