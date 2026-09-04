namespace Turnos.Core.Reglas;

public static class ReglasTurno
{
    /// <summary>
    /// Dos intervalos se superponen si cada uno empieza antes de que el otro
    /// termine. Los comparadores son estrictos a propósito: dos turnos
    /// contiguos (10:00-10:40 y 10:40-11:20) NO se superponen.
    /// </summary>
    public static bool SeSuperponen(DateTime inicioA, DateTime finA, DateTime inicioB, DateTime finB)
        => inicioA < finB && finA > inicioB;
}
