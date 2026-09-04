using Turnos.Core;

namespace Turnos.Tests.Core;

public class ResultTests
{
    [Fact]
    public void Ok_conDatos_exponeExitoYDatos()
    {
        var resultado = Result<int>.Ok(42, "listo");

        Assert.True(resultado.Success);
        Assert.Equal(42, resultado.Data);
        Assert.Equal("listo", resultado.Message);
    }

    [Fact]
    public void Fail_noExponeDatos_yConservaElMensaje()
    {
        var resultado = Result<int>.Fail("no se pudo");

        Assert.False(resultado.Success);
        Assert.Equal(0, resultado.Data);
        Assert.Equal("no se pudo", resultado.Message);
    }

    [Fact]
    public void Ok_sinGenerico_noRequiereMensaje()
    {
        var resultado = Result.Ok();

        Assert.True(resultado.Success);
        Assert.Equal(string.Empty, resultado.Message);
    }
}
