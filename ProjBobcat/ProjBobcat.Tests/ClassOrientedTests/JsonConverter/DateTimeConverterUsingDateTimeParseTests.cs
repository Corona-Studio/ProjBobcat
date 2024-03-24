using ProjBobcat.JsonConverter;
using System.Text.Json;

namespace ProjBobcat.Tests.ClassOrientedTests.JsonConverter;

[TestClass()]
public class DateTimeConverterUsingDateTimeParseTests
{
    [TestMethod()]
    public void ReadTest()
    {
        var converter = new DateTimeConverterUsingDateTimeParse();
        var dateTime = DateTime.Now;
        var jsonData = JsonSerializer.SerializeToUtf8Bytes($"{dateTime}");
        Utf8JsonReader reader = new Utf8JsonReader(jsonData);
        Assert.IsTrue(reader.Read());
        var result = converter.Read(ref reader, typeof(DateTime), JsonSerializerOptions.Default);
        Assert.AreEqual(dateTime.Ticks, result.Ticks, TimeSpan.FromSeconds(1).Ticks);
        Assert.IsFalse(reader.Read());
    }

    [TestMethod()]
    public void WriteTest()
    {
        var converter = new DateTimeConverterUsingDateTimeParse();
        using var stream = new MemoryStream();
        var dateTime = DateTime.Now;
        using (var writer = new Utf8JsonWriter(stream))
            converter.Write(writer, dateTime, JsonSerializerOptions.Default);

        var actual = stream.ToArray();
        var expected = JsonSerializer.SerializeToUtf8Bytes(dateTime);
        CollectionAssert.AreEqual(expected, actual);
    }
}
