namespace CafePOS.Desktop.Services;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public Dictionary<string, string[]> Errors { get; }

    public ApiException(string message, int statusCode = 0, Dictionary<string, string[]>? errors = null) : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}
