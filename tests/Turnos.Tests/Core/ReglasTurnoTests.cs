using Turnos.Core.Reglas;

namespace Turnos.Tests.Core;

public class ReglasTurnoTests
{
    private static DateTime H(int hora, int minuto) => new(2026, 9, 8, hora, minuto, 0);

    [Theory]
    // contiguos: uno termina justo cuando arranca el otro -> NO se superponen
    [InlineData(10, 00, 10, 40, 10, 40, 11, 20, false)]
    [InlineData(10, 40, 11, 20, 10, 00, 10, 40, false)]
    // disjuntos
    [InlineData(10, 00, 10, 40, 9, 00, 9, 40, false)]
    // solapamiento parcial
    [InlineData(10, 00, 10, 40, 10, 20, 11, 00, true)]
    [InlineData(10, 20, 11, 00, 10, 00, 10, 40, true)]
    // idénticos
    [InlineData(10, 00, 10, 40, 10, 00, 10, 40, true)]
    // uno contenido en el otro
    [InlineData(10, 00, 11, 00, 10, 20, 10, 40, true)]
    [InlineData(10, 20, 10, 40, 10, 00, 11, 00, true)]
    // por un solo minuto
    [InlineData(10, 00, 10, 40, 10, 39, 11, 00, true)]
    public void SeSuperponen_cubreLosCasosDeBorde(
        int hIniA, int mIniA, int hFinA, int mFinA,
        int hIniB, int mIniB, int hFinB, int mFinB,
        bool esperado)
    {
        var resultado = ReglasTurno.SeSuperponen(
            H(hIniA, mIniA), H(hFinA, mFinA),
            H(hIniB, mIniB), H(hFinB, mFinB));

        Assert.Equal(esperado, resultado);
    }
}
