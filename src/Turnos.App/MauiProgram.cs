using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turnos.Data;
using Turnos.Data.Servicios;

namespace Turnos.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        var rutaBase = Path.Combine(FileSystem.AppDataDirectory, "turnos.db");

        // Transient a proposito, contexto y opciones: cada pantalla arranca con un
        // change tracker limpio y no arrastra entidades viejas entre navegaciones.
        builder.Services.AddDbContext<TurnosDbContext>(
            opciones => opciones.UseSqlite($"Data Source={rutaBase}"),
            ServiceLifetime.Transient,
            ServiceLifetime.Transient);

        builder.Services.AddTransient<DatabaseInitializer>();
        builder.Services.AddTransient<IPacienteService, PacienteService>();
        builder.Services.AddTransient<ITurnoService, TurnoService>();
        builder.Services.AddTransient<IBackupService, BackupService>();
        builder.Services.AddSingleton<EstadoApp>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using (var alcance = app.Services.CreateScope())
        {
            var inicializador = alcance.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            inicializador.InicializarAsync().GetAwaiter().GetResult();
        }

        using (var alcance = app.Services.CreateScope())
        {
            var estado = alcance.ServiceProvider.GetRequiredService<EstadoApp>();
            if (estado.UltimoBackupAutomatico.Date < DateTime.Today)
            {
                var backup = alcance.ServiceProvider.GetRequiredService<IBackupService>();
                var carpeta = Path.Combine(FileSystem.AppDataDirectory, "backups");

                var resultado = backup.CopiarAsync(carpeta).GetAwaiter().GetResult();
                if (resultado.Success)
                {
                    backup.PurgarAntiguosAsync(carpeta).GetAwaiter().GetResult();
                    estado.UltimoBackupAutomatico = DateTime.Now;
                }
            }
        }

        return app;
    }
}
