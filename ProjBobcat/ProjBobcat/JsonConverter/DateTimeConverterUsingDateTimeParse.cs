using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.JsonConverter;

public class DateTimeConverterUsingDateTimeParse : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert != typeof(DateTime))
            throw new ArgumentException(
                $"{nameof(DateTimeConverterUsingDateTimeParse)} cannot deserialize " +
                $"an object of {typeToConvert.Name}",
                nameof(typeToConvert));

        return DateTime.Parse(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}