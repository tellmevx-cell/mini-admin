namespace MiniAdmin.Shared;

public sealed record ApiResponse<T>(int Code, T Data, string Message)
{
    public static ApiResponse<T> Ok(T data) => new(0, data, "ok");

    public static ApiResponse<T> Fail(string message, int code = 1) => new(code, default!, message);

    public static ApiResponse<T> Fail(string message, T data, int code = 1) => new(code, data, message);
}
