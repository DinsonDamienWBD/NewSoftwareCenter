using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoftwareCenter.Core.Serialization.Converters
{
    public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        private const string Format = "O"; // ISO 8601

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            return DateTimeOffset.Parse(s);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(Format));
        }
    }
}
