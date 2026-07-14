using System.Collections;
using System.Diagnostics;

namespace Mamemaki.Newconn;

/// <summary>
/// Default implementation for <see cref="IConnectionProperties"/>.
/// </summary>
[DebuggerDisplay("Count = {GetCount()}")]
[DebuggerTypeProxy(typeof(PropertiesDebugView))]
public class ConnectionProperties : IConnectionProperties
{
    private static readonly KeyComparer PropKeyComparer = new KeyComparer();
    private readonly IConnectionProperties? defaults;
    private readonly int initialCapacity;
    private Dictionary<Type, object>? properties;
    private volatile int containerRevision;

    /// <summary>
    /// Initializes a new instance of <see cref="ConnectionProperties"/>.
    /// </summary>
    public ConnectionProperties()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConnectionProperties"/> with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial number of elements that the collection can contain.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is less than 0</exception>
    public ConnectionProperties(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        this.initialCapacity = initialCapacity;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConnectionProperties"/> with the specified defaults.
    /// </summary>
    /// <param name="defaults">The property defaults.</param>
    public ConnectionProperties(IConnectionProperties? defaults)
    {
        this.defaults = defaults;
    }

    /// <inheritdoc />
    public virtual int Revision
    {
        get { return containerRevision + (defaults?.Revision ?? 0); }
    }

    /// <inheritdoc />
    public object? this[Type key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);

            return properties != null && properties.TryGetValue(key, out var result) ? result : defaults?[key];
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key);

            if (value == null)
            {
                if (properties != null && properties.Remove(key))
                {
                    containerRevision++;
                }
                return;
            }

            if (properties == null)
            {
                properties = new Dictionary<Type, object>(initialCapacity);
            }
            properties[key] = value;
            containerRevision++;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
    {
        if (properties != null)
        {
            foreach (var pair in properties)
            {
                yield return pair;
            }
        }

        if (defaults != null)
        {
            // Don't return properties masked by the wrapper.
            foreach (var pair in properties == null ? defaults : defaults.Except(properties, PropKeyComparer))
            {
                yield return pair;
            }
        }
    }

    public TProperty Get<TProperty>()
    {
        var property = TryGet<TProperty>();
        if (property == null)
            throw new KeyNotFoundException($"'{typeof(TProperty).FullName}' was not found in the properties.");
        return property;
    }

    /// <inheritdoc />
    public TProperty? TryGet<TProperty>()
    {
        if (typeof(TProperty).IsValueType)
        {
            var property = this[typeof(TProperty)];
            if (property is null && Nullable.GetUnderlyingType(typeof(TProperty)) is null)
            {
                throw new InvalidOperationException(
                    $"{typeof(TProperty).FullName} does not exist in the properties " +
                    $"and because it is a struct the method can't return null. Use 'properties[typeof({typeof(TProperty).FullName})] is not null' to check if the property exists.");
            }
            return (TProperty?)property;
        }
        return (TProperty?)this[typeof(TProperty)];
    }

    /// <inheritdoc />
    public void Set<TProperty>(TProperty? instance)
    {
        this[typeof(TProperty)] = instance;
    }

    // Used by the debugger. Count over enumerable is required to get the correct value.
    private int GetCount() => this.Count();

    private sealed class KeyComparer : IEqualityComparer<KeyValuePair<Type, object>>
    {
        public bool Equals(KeyValuePair<Type, object> x, KeyValuePair<Type, object> y)
        {
            return x.Key.Equals(y.Key);
        }

        public int GetHashCode(KeyValuePair<Type, object> obj)
        {
            return obj.Key.GetHashCode();
        }
    }

    private sealed class PropertiesDebugView(ConnectionProperties properties)
    {
        private readonly ConnectionProperties properties = properties;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<string, object>[] Items => 
            properties.Select(pair => new KeyValuePair<string, object>(pair.Key.FullName ?? string.Empty, pair.Value))
            .ToArray();
    }
}
