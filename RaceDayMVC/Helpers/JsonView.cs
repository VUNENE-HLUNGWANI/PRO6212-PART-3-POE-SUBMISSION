using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace RaceDayMVC.Helpers
{
    /// <summary>
    /// Read helpers for JSON returned by the RaceDay API inside Razor views.
    /// Property names are matched case-insensitively and null values are treated as missing.
    /// </summary>
    public static class JsonView
    {
        public static JsonElement? Get(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        return null;
                    }

                    return property.Value;
                }
            }

            return null;
        }

        public static string Text(JsonElement element, string name, string fallback = "")
        {
            var value = Get(element, name);

            if (!value.HasValue)
            {
                return fallback;
            }

            return value.Value.ValueKind == JsonValueKind.String
                ? value.Value.GetString() ?? fallback
                : value.Value.ToString();
        }

        public static int? IntValue(JsonElement element, string name)
        {
            var value = Get(element, name);

            if (value.HasValue
                && value.Value.ValueKind == JsonValueKind.Number
                && value.Value.TryGetInt32(out var number))
            {
                return number;
            }

            return null;
        }

        public static decimal DecimalValue(JsonElement element, string name)
        {
            var value = Get(element, name);

            if (value.HasValue
                && value.Value.ValueKind == JsonValueKind.Number
                && value.Value.TryGetDecimal(out var number))
            {
                return number;
            }

            return 0m;
        }

        public static DateTime? DateValue(JsonElement element, string name)
        {
            if (DateTime.TryParse(
                Text(element, name),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            {
                return date;
            }

            return null;
        }

        public static string TimeText(JsonElement element, string name)
        {
            var raw = Text(element, name);

            if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var time))
            {
                return time.ToString(@"hh\:mm");
            }

            return raw;
        }

        public static List<JsonElement> ListOf(JsonElement element, string name)
        {
            var value = Get(element, name);

            return value.HasValue
                ? RootList(value.Value)
                : new List<JsonElement>();
        }

        public static List<JsonElement> RootList(JsonElement element)
        {
            var list = new List<JsonElement>();

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(item.Clone());
                }
            }

            return list;
        }
    }
}
