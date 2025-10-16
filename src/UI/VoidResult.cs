namespace UI;

/// <summary>
/// Represents the result of an operation that can either succeed or fail with an error
/// </summary>
public readonly struct VoidResult
{
    private readonly Exception _error;
    private readonly bool _isSuccess;

    private VoidResult(bool isSuccess, Exception error = null!)
    {
        _isSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the result represents a successful operation
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the result represents a failed operation
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the error. Only valid when IsFailure is true.
    /// </summary>
    public Exception Error => _isSuccess
        ? throw new InvalidOperationException("Cannot access Error when result is a success")
        : _error;

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static VoidResult Success() => new(isSuccess: true);

    /// <summary>
    /// Creates a failed result with the given error
    /// </summary>
    public static VoidResult Failure(Exception error) => new(isSuccess: false, error);

    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    public static VoidResult Failure(string errorMessage) => new(isSuccess: false, new InvalidOperationException(errorMessage));

    /// <summary>
    /// Executes one of two functions based on the result state
    /// </summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Exception, TResult> onFailure) =>
        _isSuccess ? onSuccess() : onFailure(_error);

    /// <summary>
    /// Executes one of two actions based on the result state
    /// </summary>
    public void Match(Action onSuccess, Action<Exception> onFailure)
    {
        if (_isSuccess)
        {
            onSuccess();
        }
        else
        {
            onFailure(_error);
        }
    }

    /// <summary>
    /// Implicit conversion from Exception to VoidResult
    /// </summary>
    public static implicit operator VoidResult(Exception error) => Failure(error);
}