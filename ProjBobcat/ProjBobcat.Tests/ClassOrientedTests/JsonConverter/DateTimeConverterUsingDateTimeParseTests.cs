using ProjBobcat.JsonConverter;
using System.Globalization;
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

        IEnumerable<(string, bool)> formats = [
            ("o", true), ("r", true), ("s", false)
        ];
        foreach (var (format, toUtc) in formats)
        {
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                var modifiedTime = toUtc ? dateTime.ToUniversalTime() : dateTime;
                var jsonData = JsonSerializer.SerializeToUtf8Bytes(
                    modifiedTime.ToString(format, culture));

                Utf8JsonReader reader = new Utf8JsonReader(jsonData);
                Assert.IsTrue(reader.Read());
                var result = converter.Read(ref reader, typeof(DateTime), JsonSerializerOptions.Default);
                Assert.IsFalse(reader.Read());

                Assert.AreEqual(dateTime.Ticks, result.Ticks, TimeSpan.FromSeconds(1).Ticks);
            }
        }
    }

    [TestMethod()]
    public void WriteTest()
    {
        var converter = new DateTimeConverterUsingDateTimeParse();
        var dateTime = DateTime.Now;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            converter.Write(writer, dateTime, JsonSerializerOptions.Default);
        var actual = stream.ToArray();

        var expected = JsonSerializer.SerializeToUtf8Bytes(dateTime);
        CollectionAssert.AreEqual(expected, actual);
    }
}
