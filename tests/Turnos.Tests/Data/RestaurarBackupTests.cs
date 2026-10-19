using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Turnos.Core;
using Turnos.Core.Entidades;
using Turnos.Data.Servicios;

namespace Turnos.Tests.Data;

/// <summary>
/// Restaurar una copia: el caso real es pasar los datos de un consultorio a una
/// instalación nueva. Lo que importa es que nunca se pierda lo que había antes.
/// </summary>
public class RestaurarBackupTests
{
    private static async Task AgregarPacienteAsync(BaseDePrueba baseDePrueba, string apellido)
    {
        using var contexto = baseDePrueba.Nuevo();
        contexto.Pacientes.Add(new Paciente { Nombre = "Ana", Apellido = apellido });
        await contexto.SaveChangesAsync();
    }

    private static async Task<List<string>> ApellidosAsync(BaseDePrueba baseDePrueba)
    {
        using var contexto = baseDePrueba.Nuevo();
        return await contexto.Pacientes.Select(p => p.Apellido).OrderBy(a => a).ToListAsync();
    }

    /// <summary>Una copia hecha con el propio servicio, como la de Ajustes.</summary>
    private static async Task<(BaseDePrueba Origen, string Archivo)> CopiaConPacientesAsync(params string[] apellidos)
    {
        var origen = new BaseDePrueba();
        using (await origen.InicializadaAsync()) { }
        foreach (var apellido in apellidos) await AgregarPacienteAsync(origen, apellido);

        using var contexto = origen.Nuevo();
        var copia = await new BackupService(contexto).CopiarAsync(Directory.CreateTempSubdirectory().FullName);
        Assert.True(copia.Success, copia.Message);
        return (origen, copia.Data!);
    }

    [Fact]
    public async Task Inspeccionar_copiaValida_cuentaPacientesYTurnos()
    {
        var (origen, archivo) = await CopiaConPacientesAsync("Lopez", "Garcia");
        using var _ = origen;
        using var destino = new BaseDePrueba();
        using var contexto = await destino.InicializadaAsync();

        var resultado = await new BackupService(contexto).InspeccionarAsync(archivo);

        Assert.True(resultado.Success, resultado.Message);
        Assert.Equal(2, resultado.Data!.Pacientes);
        Assert.Equal(0, resultado.Data.Turnos);
        Assert.Equal(1, resultado.Data.Profesionales);
    }

    [Fact]
    public async Task Inspeccionar_archivoQueNoEsUnaBase_falla()
    {
        using var destino = new BaseDePrueba();
        using var contexto = await destino.InicializadaAsync();
        var archivo = Path.Combine(Path.GetTempPath(), $"no-es-base-{Guid.NewGuid():N}.db");
        await File.WriteAllTextAsync(archivo, "esto no es una base de datos");

        var resultado = await new BackupService(contexto).InspeccionarAsync(archivo);

        Assert.False(resultado.Success);
        Assert.Contains("no es una copia", resultado.Message);
        File.Delete(archivo);
    }

    [Fact]
    public async Task Inspeccionar_baseDeOtroPrograma_falla()
    {
        using var destino = new BaseDePrueba();
        using var contexto = await destino.InicializadaAsync();
        var archivo = Path.Combine(Path.GetTempPath(), $"otra-base-{Guid.NewGuid():N}.db");
        using (var conexion = new SqliteConnection($"Data Source={archivo};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "CREATE TABLE Clientes (Id INTEGER PRIMARY KEY)";
            comando.ExecuteNonQuery();
        }

        var resultado = await new BackupService(contexto).InspeccionarAsync(archivo);

        Assert.False(resultado.Success);
        Assert.Contains("no es una copia", resultado.Message);
        File.Delete(archivo);
    }

    [Fact]
    public async Task Restaurar_reemplazaLosDatos_yGuardaUnaCopiaDeLoQueHabia()
    {
        var (origen, archivo) = await CopiaConPacientesAsync("Lopez", "Garcia");
        using var _ = origen;
        using var destino = new BaseDePrueba();
        using (await destino.InicializadaAsync()) { }
        await AgregarPacienteAsync(destino, "Ramirez");
        var respaldo = Directory.CreateTempSubdirectory().FullName;

        Result<string> resultado;
        using (var contexto = destino.Nuevo())
            resultado = await new BackupService(contexto).RestaurarAsync(archivo, respaldo);

        Assert.True(resultado.Success, resultado.Message);
        Assert.Equal(["Garcia", "Lopez"], await ApellidosAsync(destino));

        // Lo que había antes de restaurar quedó en una copia aparte.
        Assert.True(File.Exists(resultado.Data!));
        using var conexion = new SqliteConnection($"Data Source={resultado.Data};Mode=ReadOnly;Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Apellido FROM Pacientes";
        Assert.Equal("Ramirez", comando.ExecuteScalar());
    }

    [Fact]
    public async Task Restaurar_archivoInvalido_noTocaLosDatos()
    {
        using var destino = new BaseDePrueba();
        using (await destino.InicializadaAsync()) { }
        await AgregarPacienteAsync(destino, "Ramirez");
        var archivo = Path.Combine(Path.GetTempPath(), $"no-es-base-{Guid.NewGuid():N}.db");
        await File.WriteAllTextAsync(archivo, "basura");

        using (var contexto = destino.Nuevo())
        {
            var resultado = await new BackupService(contexto).RestaurarAsync(archivo, Directory.CreateTempSubdirectory().FullName);
            Assert.False(resultado.Success);
        }

        Assert.Equal(["Ramirez"], await ApellidosAsync(destino));
        File.Delete(archivo);
    }

    [Fact]
    public async Task Restaurar_copiaDeUnaVersionAnterior_quedaConElEsquemaActual()
    {
        var (origen, archivo) = await CopiaConPacientesAsync("Lopez");
        using var _ = origen;

        // Una copia hecha antes de que existiera la historia clínica manual.
        using (var conexion = new SqliteConnection($"Data Source={archivo};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "DROP TABLE NotasHistoria";
            comando.ExecuteNonQuery();
        }

        using var destino = new BaseDePrueba();
        using (await destino.InicializadaAsync()) { }

        using (var contexto = destino.Nuevo())
        {
            var resultado = await new BackupService(contexto).RestaurarAsync(archivo, Directory.CreateTempSubdirectory().FullName);
            Assert.True(resultado.Success, resultado.Message);
        }

        Assert.Equal("NotasHistoria", destino.LeerCrudo("SELECT name FROM sqlite_master WHERE name = 'NotasHistoria'"));
        Assert.Equal(["Lopez"], await ApellidosAsync(destino));
    }
}
