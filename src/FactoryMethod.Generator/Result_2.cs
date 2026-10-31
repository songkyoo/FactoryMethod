using System.Diagnostics.CodeAnalysis;

namespace Macaron.FactoryMethod;

internal readonly struct Result<T, TError>
    where T : notnull
    where TError : notnull
{
    #region Conversion Operators
    public static implicit operator Result<T, TError>(T value) => new(value);

    public static implicit operator Result<T, TError>(TError error) => new(error);
    #endregion

    #region Fields
    private readonly byte _tag;
    private readonly T? _value;
    private readonly TError? _error;
    #endregion

    #region Constructors
    public Result(T value)
    {
        _tag = 1;
        _value = value;
    }

    public Result(TError error)
    {
        _tag = 2;
        _error = error;
    }
    #endregion

    #region Properties
    public bool HasValue => _tag == 1;

    public bool HasError => _tag == 2;

    public T Value
    {
        get
        {
            if (_tag != 1)
            {
                throw new InvalidOperationException("Cannot get value");
            }

            return _value!;
        }
    }

    public TError Error
    {
        get
        {
            if (_tag != 2)
            {
                throw new InvalidOperationException("Cannot get error");
            }

            return _error!;
        }
    }
    #endregion

    #region Overrides
    public override string ToString()
    {
        return _tag switch
        {
            0 => "<Uninitialized>",
            1 => $"{_value}",
            2 => $"{_error}",
            _ => throw new InvalidOperationException($"Invalid tag: {_tag}")
        };
    }
    #endregion

    #region Methods
    public bool TryGetValue([NotNullWhen(returnValue: true)] out T? value)
    {
        if (_tag != 1)
        {
            value = default;

            return false;
        }

        value = _value!;

        return true;
    }

    public bool TryGetError([NotNullWhen(returnValue: true)] out TError? error)
    {
        if (_tag != 2)
        {
            error = default;

            return false;
        }

        error = _error!;

        return true;
    }
    #endregion
}
