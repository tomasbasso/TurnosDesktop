using Microsoft.Extensions.DependencyInjection;
using Turnos.Data.Servicios;

namespace Turnos.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) { Title = "Turnos.App" };

		window.Destroying += (_, _) =>
		{
			using var alcance = IPlatformApplication.Current!.Services.CreateScope();
			var backup = alcance.ServiceProvider.GetRequiredService<IBackupService>();
			var carpeta = Path.Combine(FileSystem.AppDataDirectory, "backups");

			backup.CopiarAsync(carpeta).GetAwaiter().GetResult();
			backup.PurgarAntiguosAsync(carpeta).GetAwaiter().GetResult();
		};

		return window;
	}
}
