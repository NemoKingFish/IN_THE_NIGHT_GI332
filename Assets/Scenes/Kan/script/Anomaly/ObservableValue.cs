using System;
using System.Collections.Generic;

public class ObservableValue<T>
{
    private T currentValue;

    public ObservableValue(T initialValue)
    {
        currentValue = initialValue;
    }

    public event Action<T, T> OnValueChanged;

    public T Value
    {
        get => currentValue;
        set => SetValue(value);
    }

    public void SetValue(T newValue)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return;
        }

        var oldValue = currentValue;
        currentValue = newValue;
        OnValueChanged?.Invoke(oldValue, newValue);
    }

    public void SetSilently(T newValue)
    {
        currentValue = newValue;
    }
}
