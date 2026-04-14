using System;
using System.Collections.Generic;

public class ObservableValue<T>
{
    private T value;

    public ObservableValue(T initialValue)
    {
        value = initialValue;
    }

    public event Action<T, T> OnValueChanged;

    public T Value
    {
        get => value;
        set => SetValue(value);
    }

    public void SetValue(T newValue)
    {
        if (EqualityComparer<T>.Default.Equals(value, newValue))
        {
            return;
        }

        var previousValue = value;
        value = newValue;
        OnValueChanged?.Invoke(previousValue, newValue);
    }
}
