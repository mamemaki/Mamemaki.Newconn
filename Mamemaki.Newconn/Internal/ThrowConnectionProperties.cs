using System.Collections;

namespace Mamemaki.Newconn.Internal;

internal class ThrowConnectionProperties : IConnectionProperties
{
    public static readonly ThrowConnectionProperties Instance = new ThrowConnectionProperties();

    private const string ErrorMessage = "Properties is unavailable until it connects.";

    public object? this[Type key]
    {
        get => throw new InvalidOperationException(ErrorMessage);
        set => throw new InvalidOperationException(ErrorMessage);
    }

    public bool IsReadOnly => throw new InvalidOperationException(ErrorMessage);

    public int Revision => throw new InvalidOperationException(ErrorMessage);

    public TProperty Get<TProperty>()
    {
        throw new InvalidOperationException(ErrorMessage);
    }

    public TProperty? TryGet<TProperty>()
    {
        throw new InvalidOperationException(ErrorMessage);
    }

    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
    {
        throw new InvalidOperationException(ErrorMessage);
    }

    public void Set<TProperty>(TProperty? instance)
    {
        throw new InvalidOperationException(ErrorMessage);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
