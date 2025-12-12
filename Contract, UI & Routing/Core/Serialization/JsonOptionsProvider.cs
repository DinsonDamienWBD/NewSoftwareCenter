using System.Text.Json;
using System.Text.Json.Serialization;
using SoftwareCenter.Core.Serialization.Converters;

namespace SoftwareCenter.Core.Serialization
{
    /// <summary>
    /// Central provider for shared JsonSerializerOptions used across the solution.
    /// Placeholders for custom converters (DateTimeOffset, polymorphic, etc.) can be registered here.
    /// </summary>
    public static class JsonOptionsProvider
    {
        private static readonly JsonSerializerOptions _options;

        static JsonOptionsProvider()
        {
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            // Register well-known converters here if needed, e.g. DateTimeOffset handling
            _options.Converters.Add(new DateTimeOffsetConverter());
        }

        public static JsonSerializerOptions Options => _options;
    }
}
