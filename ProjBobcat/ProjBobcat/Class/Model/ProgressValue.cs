using System;
using System.Globalization;

namespace ProjBobcat.Class.Model;

/// <summary>
/// A unified impl for the progress value
/// <br/>
/// <b>Implicit convert this struct to <seealso cref="double"/> will get the value of <seealso cref="NormalizedValue"/> </b>
/// </summary>
/// <param name="NormalizedValue"></param>
public readonly record struct ProgressValue(double NormalizedValue) : IFormattable
{
    /// <summary>
    /// Display value for the progress, range usually in 0-100
    /// </summary>
    public double DisplayValue => this.NormalizedValue * 100;

    public static implicit operator double(ProgressValue value) => value.DisplayValue;

    public static ProgressValue Start => new (0);
    public static ProgressValue Finished => new (1);

    /// <summary>
    /// Create a new progress base on <seealso cref="numerator"/> and <seealso cref="denominator"/>
    /// </summary>
    /// <param name="numerator"></param>
    /// <param name="denominator"></param>
    /// <returns></returns>
    public static ProgressValue Create(double numerator, double denominator) => FromNormalized(numerator / denominator);
    public static ProgressValue FromNormalized(double normalizedValue) => new (normalizedValue);
    public static ProgressValue FromDisplay(double displayValue) => new (displayValue / 100);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return NormalizedValue.ToString(format, formatProvider);
    }

    public override string ToString() => NormalizedValue.ToString(CultureInfo.CurrentCulture);
}