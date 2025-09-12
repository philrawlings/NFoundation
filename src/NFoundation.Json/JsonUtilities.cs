using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFoundation.Json
{
    public static class JsonUtilities
    {
        public static JsonSerializerOptions GetSerializerOptions(bool writeIndented = false)
        {
            var serializerOptions = new JsonSerializerOptions();
            UpdateSerializerOptions(serializerOptions, writeIndented);
            return serializerOptions;
        }

        public static void UpdateSerializerOptions(JsonSerializerOptions serializerOptions, bool writeIndented = false)
        {
            var enumConverter = new JsonStringEnumConverter();
            serializerOptions.Converters.Add(enumConverter);

            var dateTimeConverter = new JsonDateTimeConverter(6);
            serializerOptions.Converters.Add(dateTimeConverter);

            serializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
            serializerOptions.PropertyNameCaseInsensitive = true;
            serializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            serializerOptions.WriteIndented = writeIndented;
        }
    }
}
