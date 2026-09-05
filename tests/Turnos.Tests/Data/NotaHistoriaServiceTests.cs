using Turnos.Core.Entidades;
using Turnos.Data;
using Turnos.Data.Servicios;

namespace Turnos.Tests.Data;

public class NotaHistoriaServiceTests
{
    private static async Task<(TurnosDbContext, NotaHistoriaService, int)> PrepararAsync(BaseDePrueba baseDePrueba)
    {
        var contexto = await baseDePrueba.InicializadaAsync();
        var paciente = new Paciente { Nombre = "Juan", Apellido = "Perez" };
        contexto.Pacientes.Add(paciente);
        await contexto.SaveChangesAsync();
        return (contexto, new NotaHistoriaService(contexto), paciente.Id);
    }

    [Fact]
    public async Task Crear_conTextoValido_laPersiste()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var resultado = await servicio.CrearAsync(pacienteId, 1, "Llamó por teléfono, refiere mejoría.");

        Assert.True(resultado.Success);
        Assert.True(resultado.Data!.Id > 0);
        using var otro = baseDePrueba.Nuevo();
        Assert.Equal("Llamó por teléfono, refiere mejoría.", otro.NotasHistoria.Single().Texto);
    }

    [Fact]
    public async Task Crear_conTextoVacio_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;

        var resultado = await servicio.CrearAsync(pacienteId, 1, "   ");

        Assert.False(resultado.Success);
    }

    [Fact]
    public async Task EditarTexto_conNotaExistente_loActualiza()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        var creada = await servicio.CrearAsync(pacienteId, 1, "Texto original.");

        var resultado = await servicio.EditarTextoAsync(creada.Data!.Id, "Texto corregido.");

        Assert.True(resultado.Success);
        using var otro = baseDePrueba.Nuevo();
        Assert.Equal("Texto corregido.", otro.NotasHistoria.Single().Texto);
    }

    [Fact]
    public async Task EditarTexto_conTextoVacio_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        var creada = await servicio.CrearAsync(pacienteId, 1, "Texto original.");

        var resultado = await servicio.EditarTextoAsync(creada.Data!.Id, " ");

        Assert.False(resultado.Success);
        using var otro = baseDePrueba.Nuevo();
        Assert.Equal("Texto original.", otro.NotasHistoria.Single().Texto);
    }

    [Fact]
    public async Task ObtenerPorPaciente_devuelveSoloLasDelPacienteYDelProfesionalOrdenadasDeLaMasRecienteALaMasVieja()
    {
        using var baseDePrueba = new BaseDePrueba();
        var (contexto, servicio, pacienteId) = await PrepararAsync(baseDePrueba);
        using var _ = contexto;
        contexto.Profesionales.Add(new Profesional { Nombre = "Otro" });
        var otroPaciente = new Paciente { Nombre = "Ana", Apellido = "Gomez" };
        contexto.Pacientes.Add(otroPaciente);
        await contexto.SaveChangesAsync();

        var masVieja = await servicio.CrearAsync(pacienteId, 1, "Primera nota.");
        var masNueva = await servicio.CrearAsync(pacienteId, 1, "Segunda nota.");
        await servicio.CrearAsync(otroPaciente.Id, 1, "De otro paciente.");
        await servicio.CrearAsync(pacienteId, 2, "De otro profesional.");

        var resultado = await servicio.ObtenerPorPacienteAsync(pacienteId, 1);

        Assert.True(resultado.Success);
        Assert.Equal([masNueva.Data!.Id, masVieja.Data!.Id], resultado.Data!.Select(n => n.Id));
    }
}
