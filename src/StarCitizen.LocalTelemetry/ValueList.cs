using System.Collections;

namespace StarCitizen.LocalTelemetry;

/// <summary>An immutable list that compares by its elements, so telemetry records keep value equality.</summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[] _items;

    /// <summary>Creates a list with a copy of <paramref name="items"/>.</summary>
    /// <param name="items">The elements.</param>
    public ValueList(IEnumerable<T> items) => _items = [.. items];

    /// <summary>An empty list.</summary>
    public static ValueList<T> Empty { get; } = new([]);

    /// <inheritdoc/>
    public int Count => _items.Length;

    /// <inheritdoc/>
    public T this[int index] => _items[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    /// <summary>Whether <paramref name="other"/> has the same elements in the same order.</summary>
    /// <param name="other">The list to compare with.</param>
    public bool Equals(ValueList<T>? other) =>
        other is not null && _items.SequenceEqual(other._items);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ValueList<T>);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items) hash.Add(item);
        return hash.ToHashCode();
    }

    /// <summary>Formats the elements as <c>[a, b, c]</c>.</summary>
    public override string ToString() => $"[{string.Join(", ", _items)}]";
}
