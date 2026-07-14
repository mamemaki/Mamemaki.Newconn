namespace Mamemaki.Newconn;

/// <summary>
/// Represents a collection of connection property.
/// The properties may include connection features or options.
/// </summary>
public interface IConnectionProperties : IEnumerable<KeyValuePair<Type, object>>
{
    /// <summary>
    /// Incremented for each modification and can be used to verify cached results.
    /// </summary>
    int Revision { get; }

    /// <summary>
    /// Gets or sets a given property. Setting a null value removes the property.
    /// </summary>
    /// <param name="key"></param>
    /// <returns>The requested property, or null if it is not present.</returns>
    object? this[Type key] { get; set; }

    /// <summary>
    /// Retrieves the requested property from the collection.
    /// </summary>
    /// <typeparam name="TProperty">The property key.</typeparam>
    /// <returns>The requested property, or throw <see cref="KeyNotFoundException"/> if it is not present.</returns>
    TProperty Get<TProperty>();

    /// <summary>
    /// Retrieves the requested property from the collection.
    /// </summary>
    /// <typeparam name="TProperty">The property key.</typeparam>
    /// <returns>The requested property, or null if it is not present.</returns>
    TProperty? TryGet<TProperty>();

    /// <summary>
    /// Sets the given property in the collection.
    /// </summary>
    /// <typeparam name="TProperty">The property key.</typeparam>
    /// <param name="instance">The property value.</param>
    void Set<TProperty>(TProperty? instance);
}
