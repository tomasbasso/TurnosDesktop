namespace Turnos.Core.Entidades;

public class Pago
{
    public int Id { get; set; }
    public int ProfesionalId { get; set; }
    public int PacienteId { get; set; }

    /// <summary>Null si es un pago suelto, no asociado a un tratamiento.</summary>
    public int? TratamientoId { get; set; }

    /// <summary>Persistido como TEXT invariante. Ver TurnosDbContext.</summary>
    public decimal Monto { get; set; }

    public FormaPago FormaPago { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Nota { get; set; }
}
