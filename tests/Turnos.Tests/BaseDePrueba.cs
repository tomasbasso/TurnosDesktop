using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Turnos.Data;

namespace Turnos.Tests;

/// <summary>
/// Base SQLite real sobre un archivo temporal. NO se usa el provider InMemory:
/// ese no ejecuta el ValueConverter de decimal a TEXT, que es justamente donde
/// aparecen los bugs de este proyecto.
/// </summary>
public sealed class BaseDePrueba : IDisposable
{
    public string Ruta { get; }

    public BaseDePrueba()
    {
        Ruta = Path.Combine(Path.GetTempPath(), $"turnos-test-{Guid.NewGuid():N}.db");
    }

    /// <summary>Un contexto nuevo, como en producción: el DbContext es Transient.</summary>
    public TurnosDbContext Nuevo()
    {
        var opciones = new DbContextOptionsBuilder<TurnosDbContext>()
            .UseSqlite($"Data Source={Ruta}")
            .Options;
        return new TurnosDbContext(opciones);
    }

    /// <summary>Crea el esquema y siembra los datos iniciales.</summary>
    public async Task<TurnosDbContext> InicializadaAsync()
    {
        var contexto = Nuevo();
        await new DatabaseInitializer(contexto).InicializarAsync();
        return contexto;
    }

    /// <summary>Lee un valor crudo, sin pasar por EF ni sus converters.</summary>
    public string? LeerCrudo(string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={Ruta}");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteScalar()?.ToString();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(Ruta)) File.Delete(Ruta);
    }
}
