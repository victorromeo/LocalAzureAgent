using System.Collections.Generic;
using System.Globalization;

namespace LocalAgent.Utilities
{
    /// <summary>
    /// Normalizes task input values from YAML into strongly typed values.
    /// </summary>
    public class InputNormalizationService
    {
        private readonly IDictionary<string, string> _inputs;

        public InputNormalizationService(IDictionary<string, string> inputs)
        {
            _inputs = inputs;
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (_inputs == null || key == null)
            {
                return defaultValue ?? string.Empty;
            }

            return _inputs.TryGetValue(key, out var value)
                ? value ?? string.Empty
                : defaultValue ?? string.Empty;
        }

        public bool GetBool(string key, bool defaultValue = default)
        {
            var raw = GetString(key, null);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (bool.TryParse(raw, out var result))
            {
                return result;
            }

            return raw switch
            {
                "1" => true,
                "0" => false,
                _ => defaultValue
            };
        }

        public int GetInt(string key, int defaultValue = default)
        {
            var raw = GetString(key, null);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        public long GetLong(string key, long defaultValue = default)
        {
            var raw = GetString(key, null);
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        public double GetDouble(string key, double defaultValue = default)
        {
            var raw = GetString(key, null);
            return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }
    }
}
