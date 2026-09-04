using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Turnos.Core.Entidades;

namespace Turnos.Data;

public class TurnosDbContext(DbContextOptions<TurnosDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Profesional> Profesionales => Set<Profesional>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Tratamiento> Tratamientos => Set<Tratamiento>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<Pago> Pagos => Set<Pago>();

    /// <summary>
    /// decimal -> TEXT con cultura invariante. SQLite no tiene tipo decimal y el
    /// REAL de punto flotante pierde centavos. Consecuencia importante: SQLite
    /// no sabe ordenar ni sumar este TEXT, asi que TODA consulta que ordene o
    /// sume montos debe materializar con .ToList() ANTES de hacerlo.
    /// </summary>
    private static readonly ValueConverter<decimal, string> DecimalATexto = new(
        valor => valor.ToString(CultureInfo.InvariantCulture),
        texto => decimal.Parse(texto, NumberStyles.Any, CultureInfo.InvariantCulture));

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Tratamiento>().Property(t => t.PrecioSesion).HasConversion(DecimalATexto);
        modelo.Entity<Pago>().Property(p => p.Monto).HasConversion(DecimalATexto);

        modelo.Entity<Paciente>().HasIndex(p => new { p.Apellido, p.Nombre });
        modelo.Entity<Paciente>().HasIndex(p => p.Dni);

        // La consulta que corre en cada cambio de semana del calendario.
        modelo.Entity<Turno>().HasIndex(t => new { t.ProfesionalId, t.Inicio });
        modelo.Entity<Turno>().HasIndex(t => t.SerieId);

        modelo.Entity<Turno>()
            .HasOne(t => t.Tratamiento)
            .WithMany(tr => tr.Turnos)
            .HasForeignKey(t => t.TratamientoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelo.Entity<Tratamiento>().HasIndex(t => new { t.PacienteId, t.ProfesionalId });
        modelo.Entity<Pago>().HasIndex(p => new { p.ProfesionalId, p.Fecha });
    }
}
