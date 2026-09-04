using Turnos.Data.Servicios;

namespace Turnos.Tests.Data;

public class BackupServiceTests
{
    [Fact]
    public async Task Copiar_generaUnArchivoConDatos()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var destino = Directory.CreateTempSubdirectory().FullName;
        var servicio = new BackupService(contexto);

        var resultado = await servicio.CopiarAsync(destino);

        Assert.True(resultado.Success, resultado.Message);
        Assert.True(File.Exists(resultado.Data!));
        Assert.True(new FileInfo(resultado.Data!).Length > 0);

        Directory.Delete(destino, recursive: true);
    }

    [Fact]
    public async Task Copiar_aCarpetaInexistente_laCrea()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var destino = Path.Combine(Path.GetTempPath(), $"backup-{Guid.NewGuid():N}");
        var servicio = new BackupService(contexto);

        var resultado = await servicio.CopiarAsync(destino);

        Assert.True(resultado.Success, resultado.Message);
        Assert.True(Directory.Exists(destino));

        Directory.Delete(destino, recursive: true);
    }

    [Fact]
    public async Task PurgarAntiguos_dejaSoloLosMasRecientes()
    {
        using var baseDePrueba = new BaseDePrueba();
        using var contexto = await baseDePrueba.InicializadaAsync();
        var destino = Directory.CreateTempSubdirectory().FullName;
        var servicio = new BackupService(contexto);

        for (var i = 0; i < 5; i++)
        {
            var archivo = Path.Combine(destino, $"turnos-2026090{i}-100000.db");
            await File.WriteAllTextAsync(archivo, "x");
            File.SetLastWriteTime(archivo, DateTime.Now.AddDays(-5 + i));
        }

        var resultado = await servicio.PurgarAntiguosAsync(destino, conservar: 2);

        Assert.True(resultado.Success);
        Assert.Equal(2, Directory.GetFiles(destino, "turnos-*.db").Length);

        Directory.Delete(destino, recursive: true);
    }
}
