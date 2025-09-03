namespace PluginLauncher.Models;

/// <summary>
/// Result of an operation with status and message
/// </summary>
public class OperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }

    public static OperationResult CreateSuccess(string message = "Operation completed successfully")
    {
        return new OperationResult { Success = true, Message = message };
    }

    public static OperationResult CreateFailure(string message, Exception? exception = null)
    {
        return new OperationResult { Success = false, Message = message, Exception = exception };
    }

    public override string ToString()
    {
        return $"{(Success ? "SUCCESS" : "FAILURE")}: {Message}";
    }
}

/// <summary>
/// Generic result with data
/// </summary>
public class OperationResult<T> : OperationResult
{
    public T? Data { get; set; }

    public static OperationResult<T> CreateSuccess(T data, string message = "Operation completed successfully")
    {
        return new OperationResult<T> { Success = true, Message = message, Data = data };
    }

    public static new OperationResult<T> CreateFailure(string message, Exception? exception = null)
    {
        return new OperationResult<T> { Success = false, Message = message, Exception = exception };
    }
}