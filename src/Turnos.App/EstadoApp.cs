namespace Turnos.App;

/// <summary>
/// Preferencias del usuario. Viven en Preferences de MAUI, NO en SQLite: no son
/// datos del dominio y no tienen que viajar en el backup.
/// </summary>
public class EstadoApp
{
    public event Action? Cambio;

    public int ProfesionalActivoId
    {
        get => Preferences.Get(nameof(ProfesionalActivoId), 0);
        set { Preferences.Set(nameof(ProfesionalActivoId), value); Cambio?.Invoke(); }
    }

    public int DuracionPorDefecto
    {
        get => Preferences.Get(nameof(DuracionPorDefecto), 30);
        set { Preferences.Set(nameof(DuracionPorDefecto), value); Cambio?.Invoke(); }
    }

    public bool MostrarDomingo
    {
        get => Preferences.Get(nameof(MostrarDomingo), false);
        set { Preferences.Set(nameof(MostrarDomingo), value); Cambio?.Invoke(); }
    }

    public DateTime UltimoBackupAutomatico
    {
        get => Preferences.Get(nameof(UltimoBackupAutomatico), DateTime.MinValue);
        set => Preferences.Set(nameof(UltimoBackupAutomatico), value);
    }
}
