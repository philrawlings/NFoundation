using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFoundation.Json
{
    public class JsonDateTimeConverter : JsonConverter<DateTime>
    {
        private readonly string _formatString;
        private readonly string _utcFormatString;

        public JsonDateTimeConverter(int fractionalSecondsDecimalPlaces)
        {
            string fractionalSecondsString;
            if (fractionalSecondsDecimalPlaces < 0 || fractionalSecondsDecimalPlaces > 7)
                throw new ArgumentOutOfRangeException(nameof(fractionalSecondsDecimalPlaces));
            else if (fractionalSecondsDecimalPlaces == 0)
                fractionalSecondsString = string.Empty;
            else
                fractionalSecondsString = "." + new string('f', fractionalSecondsDecimalPlaces);

            _formatString = $"yyyy'-'MM'-'dd'T'HH':'mm':'ss{fractionalSecondsString}";
            _utcFormatString = $"yyyy'-'MM'-'dd'T'HH':'mm':'ss{fractionalSecondsString}Z";

        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Expected string token for DateTime");
            var dateTimeString = reader.GetString();
            if (dateTimeString == null)
                throw new JsonException("DateTime string cannot be null");
            return DateTime.Parse(dateTimeString);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            if (value.Kind == DateTimeKind.Utc)
                writer.WriteStringValue(value.ToString(_utcFormatString));
            else
                writer.WriteStringValue(value.ToString(_formatString));
        }
    }
}
