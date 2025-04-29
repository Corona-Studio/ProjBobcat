using System;
using System.Globalization;

namespace ProjBobcat.Class.Model;

/// <summary>
///     A unified impl for the progress value
///     <br />
///     <b>
///         Implicit convert this struct to <seealso cref="double" /> will get the value of
///         <seealso cref="NormalizedValue" />
///     </b>
/// </summary>
/// <param name="NormalizedValue"></param>
public readonly record struct ProgressValue(double NormalizedValue) : IFormattable
{
    /// <summary>
    ///     Display value for the progress, range usually in 0-100
    /// </summary>
    public double DisplayValue => this.NormalizedValue * 100;

    public static ProgressValue Start => new(0);
    public static ProgressValue Finished => new(1);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return this.NormalizedValue.ToString(format, formatProvider);
    }

    public static implicit operator double(ProgressValue value)
    {
        return value.DisplayValue;
    }

    /// <summary>
    ///     Create a new progress base on <seealso cref="numerator" /> and <seealso cref="denominator" />
    /// </summary>
    /// <param name="numerator"></param>
    /// <param name="denominator"></param>
    /// <returns></returns>
    public static ProgressValue Create(double numerator, double denominator)
    {
        return FromNormalized(numerator / denominator);
    }

    public static ProgressValue FromNormalized(double normalizedValue)
    {
        return new ProgressValue(normalizedValue);
    }

    public static ProgressValue FromDisplay(double displayValue)
    {
        return new ProgressValue(displayValue / 100);
    }

    public override string ToString()
    {
        return NormalizedValue.ToString(CultureInfo.CurrentCulture);
    }
}