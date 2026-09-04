using Microsoft.EntityFrameworkCore;
using Turnos.Core;
using Turnos.Core.Entidades;
using Turnos.Data;

namespace Turnos.Tests.Data;

public class EsquemaTests
{
    [Fact]
    public async Task Inicializar_siembraAEzequielTosso()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();

        var profesional = await contexto.Profesionales.SingleAsync();

        Assert.Equal("Ezequiel Tosso", profesional.Nombre);
        Assert.Equal(new TimeOnly(7, 0), profesional.HoraInicioAgenda);
        Assert.Equal(new TimeOnly(21, 0), profesional.HoraFinAgenda);
        Assert.True(profesional.Activo);
        Assert.Equal("images/profesionales/ezequiel-tosso.png", profesional.FotoPerfil);
    }

    [Fact]
    public async Task Inicializar_esIdempotente_yNoDuplicaElSeed()
    {
        using var baseDePrueba = new BaseDePrueba();

        using (var primero = await baseDePrueba.InicializadaAsync()) { }
        using (var segundo = await baseDePrueba.InicializadaAsync()) { }

        using var contexto = baseDePrueba.Nuevo();
        Assert.Equal(1, await contexto.Profesionales.CountAsync());
    }

    [Fact]
    public async Task ExisteColumna_distingueColumnasPresentesDeAusentes()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var inicializador = new DatabaseInitializer(contexto);

        Assert.True(await inicializador.ExisteColumnaAsync("Pacientes", "ObraSocial"));
        Assert.False(await inicializador.ExisteColumnaAsync("Pacientes", "ColumnaQueNoExiste"));
    }

    [Fact]
    public async Task Decimal_seGuardaComoTextoInvariante_yVuelveIgual()
    {
        using var baseDePrueba = new BaseDePrueba();
        using (var contexto = await baseDePrueba.InicializadaAsync())
        {
            var paciente = new Paciente { Nombre = "Juan", Apellido = "Perez" };
            contexto.Pacientes.Add(paciente);
            await contexto.SaveChangesAsync();

            contexto.Tratamientos.Add(new Tratamiento
            {
                PacienteId = paciente.Id,
                ProfesionalId = 1,
                Motivo = "Lumbalgia",
                SesionesAutorizadas = 10,
                PrecioSesion = 12345.67m,
                FechaInicio = new DateOnly(2026, 9, 8)
            });
            await contexto.SaveChangesAsync();
        }

        // El valor crudo debe usar punto decimal, sin importar la cultura de la maquina.
        var crudo = baseDePrueba.LeerCrudo("SELECT PrecioSesion FROM Tratamientos LIMIT 1");
        Assert.Equal("12345.67", crudo);

        // Y debe volver identico leido con un contexto nuevo.
        using var otroContexto = baseDePrueba.Nuevo();
        var guardado = await otroContexto.Tratamientos.SingleAsync();
        Assert.Equal(12345.67m, guardado.PrecioSesion);
    }

    [Fact]
    public async Task Enum_sePersisteComoEntero()
    {
        using var baseDePrueba = new BaseDePrueba();
        using (var contexto = await baseDePrueba.InicializadaAsync())
        {
            var paciente = new Paciente { Nombre = "Ana", Apellido = "Ruiz" };
            contexto.Pacientes.Add(paciente);
            await contexto.SaveChangesAsync();

            contexto.Turnos.Add(new Turno
            {
                ProfesionalId = 1,
                PacienteId = paciente.Id,
                Inicio = new DateTime(2026, 9, 8, 10, 0, 0),
                Fin = new DateTime(2026, 9, 8, 10, 40, 0),
                Estado = EstadoTurno.Ausente
            });
            await contexto.SaveChangesAsync();
        }

        Assert.Equal("2", baseDePrueba.LeerCrudo("SELECT Estado FROM Turnos LIMIT 1"));
    }
}
