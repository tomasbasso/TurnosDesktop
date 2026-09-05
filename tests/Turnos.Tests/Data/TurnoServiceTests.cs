using Turnos.Core;
using Turnos.Core.Entidades;
using Turnos.Data;
using Turnos.Data.Servicios;

namespace Turnos.Tests.Data;

public class TurnoServiceTests
{
    private static DateTime H(int hora, int minuto) => new(2026, 9, 8, hora, minuto, 0);

    private static async Task<(TurnosDbContext, TurnoService, int)> PrepararAsync(BaseDePrueba baseDePrueba)
    {
        var contexto = await baseDePrueba.InicializadaAsync();
        var paciente = new Paciente { Nombre = "Juan", Apellido = "Perez" };
        contexto.Pacientes.Add(paciente);
        await contexto.SaveChangesAsync();
        return (contexto, new TurnoService(contexto), paciente.Id);
    }

    private static Turno NuevoTurno(int pacienteId, DateTime inicio, DateTime fin) => new()
    {
        ProfesionalId = 1,
        PacienteId = pacienteId,
        Inicio = inicio,
        Fin = fin
    };

    [Fact]
    public async Task Guardar_turnoValido_loPersiste()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        Assert.True(resultado.Success);
        Assert.True(resultado.Data!.Id > 0);
        Assert.Equal(EstadoTurno.Programado, resultado.Data.Estado);
    }

    [Fact]
    public async Task Guardar_sinPacienteNiNombreLibre_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, _) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var turno = new Turno { ProfesionalId = 1, Inicio = H(10, 0), Fin = H(10, 40) };
        var resultado = await servicio.GuardarAsync(turno);

        Assert.False(resultado.Success);
        Assert.Equal("Hay que elegir un paciente o escribir un nombre.", resultado.Message);
    }

    [Fact]
    public async Task Guardar_conNombreLibreSinPaciente_loPersiste()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, _) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var turno = new Turno
        {
            ProfesionalId = 1,
            NombreLibre = "  Consulta particular  ",
            Inicio = H(10, 0),
            Fin = H(10, 40)
        };
        var resultado = await servicio.GuardarAsync(turno);

        Assert.True(resultado.Success);
        Assert.Null(resultado.Data!.PacienteId);
        Assert.Equal("Consulta particular", resultado.Data.NombreLibre);
    }

    [Fact]
    public async Task Guardar_conPacienteYNombreLibre_descartaElNombreLibre()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var turno = NuevoTurno(pacienteId, H(10, 0), H(10, 40));
        turno.NombreLibre = "no debería quedar";

        var resultado = await servicio.GuardarAsync(turno);

        Assert.True(resultado.Success);
        Assert.Null(resultado.Data!.NombreLibre);
    }

    [Fact]
    public async Task Guardar_turnoSolapadoConNombreLibre_fallaYNombraElTurno()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, _) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        await servicio.GuardarAsync(new Turno
        {
            ProfesionalId = 1,
            NombreLibre = "Consulta particular",
            Inicio = H(10, 0),
            Fin = H(10, 40)
        });

        var resultado = await servicio.GuardarAsync(new Turno
        {
            ProfesionalId = 1,
            NombreLibre = "Otro",
            Inicio = H(10, 20),
            Fin = H(11, 0)
        });

        Assert.False(resultado.Success);
        Assert.Contains("Consulta particular", resultado.Message);
    }

    [Fact]
    public async Task Guardar_conFinAnteriorAlInicio_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 40), H(10, 0)));

        Assert.False(resultado.Success);
    }

    [Fact]
    public async Task Guardar_turnoSolapado_fallaYNombraAlPaciente()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 20), H(11, 0)));

        Assert.False(resultado.Success);
        Assert.Contains("Perez", resultado.Message);
        Assert.Contains("10:00", resultado.Message);
    }

    [Fact]
    public async Task Guardar_turnoContiguo_seAcepta()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 40), H(11, 20)));

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task Guardar_noChocaContraUnTurnoCancelado()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        var existente = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));
        await servicio.CambiarEstadoAsync(existente.Data!.Id, EstadoTurno.Cancelado);

        var resultado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task Guardar_noChocaContraOtroProfesional()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        contexto.Profesionales.Add(new Profesional { Nombre = "Otro" });
        await contexto.SaveChangesAsync();
        await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        var deOtro = NuevoTurno(pacienteId, H(10, 0), H(10, 40));
        deOtro.ProfesionalId = 2;
        var resultado = await servicio.GuardarAsync(deOtro);

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task Guardar_alEditarUnTurno_noChocaConsigoMismo()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        var creado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));

        var editado = creado.Data!;
        editado.Observaciones = "viene con la orden";
        var resultado = await servicio.GuardarAsync(editado);

        Assert.True(resultado.Success);
    }

    [Fact]
    public async Task ObtenerRango_devuelveSoloLosDelProfesionalYDelRango()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));
        await servicio.GuardarAsync(NuevoTurno(pacienteId,
            new DateTime(2026, 9, 20, 10, 0, 0), new DateTime(2026, 9, 20, 10, 40, 0)));

        var resultado = await servicio.ObtenerRangoAsync(1,
            new DateTime(2026, 9, 7), new DateTime(2026, 9, 14));

        Assert.Single(resultado.Data!);
    }

    [Fact]
    public async Task GuardarNotaClinica_persisteLaNota()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        var creado = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(10, 0), H(10, 40)));
        await servicio.CambiarEstadoAsync(creado.Data!.Id, EstadoTurno.Atendido);

        var resultado = await servicio.GuardarNotaClinicaAsync(creado.Data.Id, "Mejor movilidad cervical.");

        Assert.True(resultado.Success);
        using var otro = baseDePrueba.Nuevo();
        Assert.Equal("Mejor movilidad cervical.", otro.Turnos.Single().NotaClinica);
    }

    [Fact]
    public async Task ObtenerPorPaciente_devuelveSoloLosDelPacienteYDelProfesionalOrdenadosDelMasRecienteAlMasViejo()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        contexto.Profesionales.Add(new Profesional { Nombre = "Otro" });
        var otroPaciente = new Paciente { Nombre = "Ana", Apellido = "Gomez" };
        contexto.Pacientes.Add(otroPaciente);
        await contexto.SaveChangesAsync();

        var masViejo = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(9, 0), H(9, 40)));
        var masNuevo = await servicio.GuardarAsync(NuevoTurno(pacienteId, H(11, 0), H(11, 40)));
        await servicio.GuardarAsync(NuevoTurno(otroPaciente.Id, H(10, 0), H(10, 40)));
        var deOtroProfesional = NuevoTurno(pacienteId, H(12, 0), H(12, 40));
        deOtroProfesional.ProfesionalId = 2;
        await servicio.GuardarAsync(deOtroProfesional);

        var resultado = await servicio.ObtenerPorPacienteAsync(pacienteId, 1);

        Assert.True(resultado.Success);
        Assert.Equal([masNuevo.Data!.Id, masViejo.Data!.Id], resultado.Data!.Select(t => t.Id));
    }
}
