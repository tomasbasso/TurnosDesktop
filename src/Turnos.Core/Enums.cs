namespace Turnos.Core;

public enum EstadoTurno
{
    Programado = 0,
    Atendido = 1,
    Ausente = 2,
    Cancelado = 3
}

public enum EstadoTratamiento
{
    Activo = 0,
    Finalizado = 1,
    Abandonado = 2
}

public enum FormaPago
{
    Efectivo = 0,
    Transferencia = 1,
    Tarjeta = 2,
    ObraSocial = 3
}
