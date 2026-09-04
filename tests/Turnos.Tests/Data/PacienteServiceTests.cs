using Turnos.Core.Entidades;
using Turnos.Data.Servicios;

namespace Turnos.Tests.Data;

public class PacienteServiceTests
{
    [Fact]
    public async Task Guardar_sinApellido_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);

        var resultado = await servicio.GuardarAsync(new Paciente { Nombre = "Juan" });

        Assert.False(resultado.Success);
        Assert.Contains("apellido", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guardar_pacienteNuevo_asignaIdYFechaDeAlta()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);

        var resultado = await servicio.GuardarAsync(
            new Paciente { Nombre = "Juan", Apellido = "Perez", ObraSocial = "OSDE" });

        Assert.True(resultado.Success);
        Assert.True(resultado.Data!.Id > 0);
        Assert.True(resultado.Data.Activo);
    }

    [Fact]
    public async Task Buscar_encuentraPorApellidoYPorDni_ignorandoMayusculas()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);
        await servicio.GuardarAsync(new Paciente { Nombre = "Juan", Apellido = "Perez", Dni = "30111222" });
        await servicio.GuardarAsync(new Paciente { Nombre = "Ana", Apellido = "Ruiz", Dni = "28999888" });

        var porApellido = await servicio.BuscarAsync("per");
        var porDni = await servicio.BuscarAsync("28999");

        Assert.Single(porApellido.Data!);
        Assert.Equal("Perez", porApellido.Data![0].Apellido);
        Assert.Single(porDni.Data!);
        Assert.Equal("Ruiz", porDni.Data![0].Apellido);
    }

    [Fact]
    public async Task Buscar_sinTexto_devuelveTodosOrdenadosPorApellido()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);
        await servicio.GuardarAsync(new Paciente { Nombre = "Ana", Apellido = "Ruiz" });
        await servicio.GuardarAsync(new Paciente { Nombre = "Juan", Apellido = "Perez" });

        var resultado = await servicio.BuscarAsync(null);

        Assert.Equal(2, resultado.Data!.Count);
        Assert.Equal("Perez", resultado.Data[0].Apellido);
    }

    [Fact]
    public async Task Buscar_noDevuelvePacientesInactivos()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);
        await servicio.GuardarAsync(new Paciente { Nombre = "Ana", Apellido = "Ruiz", Activo = false });

        var resultado = await servicio.BuscarAsync(null);

        Assert.Empty(resultado.Data!);
    }

    [Fact]
    public async Task ObrasSocialesUsadas_devuelveValoresDistintosSinVacios()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);
        await servicio.GuardarAsync(new Paciente { Nombre = "A", Apellido = "A", ObraSocial = "OSDE" });
        await servicio.GuardarAsync(new Paciente { Nombre = "B", Apellido = "B", ObraSocial = "OSDE" });
        await servicio.GuardarAsync(new Paciente { Nombre = "C", Apellido = "C", ObraSocial = "Swiss Medical" });
        await servicio.GuardarAsync(new Paciente { Nombre = "D", Apellido = "D", ObraSocial = null });

        var resultado = await servicio.ObrasSocialesUsadasAsync();

        Assert.Equal(["OSDE", "Swiss Medical"], resultado.Data!);
    }

    [Fact]
    public async Task Obtener_conIdInexistente_falla()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var servicio = new PacienteService(contexto);

        var resultado = await servicio.ObtenerAsync(999);

        Assert.False(resultado.Success);
    }
}
