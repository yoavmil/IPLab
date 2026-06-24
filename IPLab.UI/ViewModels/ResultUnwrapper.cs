namespace IPLab.UI.ViewModels;

internal static class ResultUnwrapper
{
    /// <summary>Finds the first value of type T in a raw executor result blob (direct value, port dictionary, or LoopEnd accumulator).</summary>
    internal static T? Unwrap<T>(object? result) where T : class
    {
        if (result is T direct)
            return direct;

        if (result is IReadOnlyDictionary<string, object?> dict)
        {
            foreach (var val in dict.Values)
            {
                var found = Unwrap<T>(val);
                if (found is not null) return found;
            }
            return null;
        }

        // LoopEnd accumulator: object?[] where each slot holds one iteration's value.
        // Each slot may be an array of elementType (flatten it) or a single elementType value (collect it).
        if (result is object?[] accumulator && typeof(T).IsArray)
        {
            var elementType = typeof(T).GetElementType()!;
            var items = new List<object?>();
            foreach (var slot in accumulator)
            {
                if (slot is null) continue;
                if (slot is Array arr && elementType.IsAssignableFrom(arr.GetType().GetElementType()))
                    items.AddRange(arr.Cast<object?>().Where(v => v is not null));
                else if (elementType.IsInstanceOfType(slot))
                    items.Add(slot);
            }
            if (items.Count == 0) return null;
            var merged = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
                merged.SetValue(items[i], i);
            return (T)(object)merged;
        }

        return null;
    }

    /// <summary>Finds the first value of struct type T in a raw executor result blob.</summary>
    internal static T? UnwrapStruct<T>(object? result) where T : struct
    {
        if (result is T direct) return direct;
        if (result is IReadOnlyDictionary<string, object?> dict)
            return dict.Values.OfType<T>().Cast<T?>().FirstOrDefault();
        if (result is object?[] acc)
            return acc.OfType<T>().Cast<T?>().FirstOrDefault();
        return null;
    }

    /// <summary>
    /// Returns the raw value for a named output port from a result blob.
    /// If the result is a port dictionary, looks up by port name.
    /// Otherwise (single-output raw value) returns the result itself regardless of port name.
    /// </summary>
    internal static object? GetPortValue(object? result, string portName)
    {
        if (result is IReadOnlyDictionary<string, object?> dict)
            return dict.GetValueOrDefault(portName);
        return result;
    }
}
