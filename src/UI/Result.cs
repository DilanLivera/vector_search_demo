namespace UI;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public readonly struct Result<T>
{
    private readonly T _value;
    private readonly Exception _error;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _error = null!;
        _isSuccess = true;
    }

    private Result(Exception error)
    {
        _value = default!;
        _error = error;
        _isSuccess = false;
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
    /// Gets the success value. Only valid when IsSuccess is true.
    /// </summary>
    public T Value => !_isSuccess
        ? throw new InvalidOperationException("Cannot access Value when result is a failure")
        : _value;

    /// <summary>
    /// Gets the error. Only valid when IsFailure is true.
    /// </summary>
    public Exception Error => _isSuccess
        ? throw new InvalidOperationException("Cannot access Error when result is a success")
        : _error;

    /// <summary>
    /// Creates a successful result with the given value
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the given error
    /// </summary>
    public static Result<T> Failure(Exception error) => new(error);

    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    public static Result<T> Failure(string errorMessage) => new(new InvalidOperationException(errorMessage));

    /// <summary>
    /// Executes one of two functions based on the result state
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Exception, TResult> onFailure) => _isSuccess ? onSuccess(_value) : onFailure(_error);

    /// <summary>
    /// Transforms the success value using the provided function
    /// </summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapper) => _isSuccess ? Result<TResult>.Success(mapper(_value)) : Result<TResult>.Failure(_error);

    /// <summary>
    /// Binds the result to another operation that returns a Result
    /// </summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binder) => _isSuccess ? binder(_value) : Result<TResult>.Failure(_error);

    /// <summary>
    /// Implicit conversion from T to Result<T>
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicit conversion from Exception to Result<T>
    /// </summary>
    public static implicit operator Result<T>(Exception error) => Failure(error);
}