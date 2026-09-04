namespace Turnos.Core.Entidades;

public class Profesional
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Hex. Reservado para una futura vista "todas las agendas".</summary>
    public string Color { get; set; } = "#2563eb";

    public TimeOnly HoraInicioAgenda { get; set; } = new(7, 0);
    public TimeOnly HoraFinAgenda { get; set; } = new(21, 0);
    public bool Activo { get; set; } = true;

    /// <summary>Ruta relativa a wwwroot (ej. "images/profesionales/ezequiel-tosso.png"). Null = sin foto.</summary>
    public string? FotoPerfil { get; set; }
}
