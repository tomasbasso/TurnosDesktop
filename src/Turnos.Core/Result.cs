namespace Turnos.Core;

/// <summary>
/// Resultado de una operación de servicio. Los servicios nunca lanzan
/// excepciones como control de flujo: devuelven esto y la UI muestra Message.
/// </summary>
public class Result
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static Result Ok(string message = "") => new() { Success = true, Message = message };
    public static Result Fail(string message) => new() { Success = false, Message = message };
}

public class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Ok(T data, string message = "") =>
        new() { Success = true, Data = data, Message = message };

    public new static Result<T> Fail(string message) =>
        new() { Success = false, Message = message };
}
