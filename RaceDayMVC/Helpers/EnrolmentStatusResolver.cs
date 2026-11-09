using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using RaceDayMVC.Models;

namespace RaceDayMVC.Helpers
{
    public sealed class EnrolmentStatusView
    {
        public string Kind { get; init; } = "pending";
        public string Label { get; init; } = string.Empty;
        public string CssClass { get; init; } = "rd-badge-soft";
        public string Note { get; init; } = string.Empty;
        public string? Time { get; init; }
        public int? Position { get; init; }
        public int? TotalFinishers { get; init; }
        public bool CanCancel { get; init; }
    }

    public static class EnrolmentStatusResolver
    {
        public static Dictionary<int, EnrolmentStatusView> Resolve(
            JsonElement enrolments,
            IReadOnlyList<JsonElement> results,
            DateTime today)
        {
            var statuses = new Dictionary<int, EnrolmentStatusView>();

            if (enrolments.ValueKind != JsonValueKind.Array)
            {
                return statuses;
            }

            foreach (JsonElement enrolment in enrolments.EnumerateArray())
            {
                statuses[ReadInt(enrolment, "participantEventID") ?? 0] =
                    ResolveOne(enrolment, results, today);
            }

            return statuses;
        }

        public static EnrolmentStatusView ResolveOne(
            JsonElement enrolment,
            IReadOnlyList<JsonElement> results,
            DateTime today)
        {
            string registration = ReadString(enrolment, "registrationStatus");

            if (Is(registration, "Withdrawn") || Is(registration, "Cancelled"))
            {
                return new EnrolmentStatusView
                {
                    Kind = "withdrawn",
                    Label = "Withdrawn",
                    CssClass = "rd-badge-danger",
                    Note = "This enrolment was withdrawn."
                };
            }

            JsonElement? raceEvent = ReadElement(enrolment, "event");
            JsonElement? category = ReadElement(enrolment, "category");

            string eventName = raceEvent.HasValue ? ReadString(raceEvent.Value, "name") : string.Empty;
            DateTime? eventDate = raceEvent.HasValue ? ReadDate(raceEvent.Value, "date") : null;
            string categoryName = category.HasValue ? ReadString(category.Value, "categoryName") : string.Empty;

            JsonElement? result = FindResult(results, enrolment, eventName, eventDate, categoryName);

            if (result.HasValue)
            {
                return FromResult(result.Value);
            }

            bool raceHasPassed = eventDate.HasValue && eventDate.Value.Date < today.Date;

            if (raceHasPassed)
            {
                return new EnrolmentStatusView
                {
                    Kind = "awaiting",
                    Label = "Awaiting results",
                    CssClass = "rd-badge-awaiting",
                    Note = "The race has taken place. Your result will appear here once the organiser records it."
                };
            }

            if (Is(registration, "Confirmed"))
            {
                return new EnrolmentStatusView
                {
                    Kind = "confirmed",
                    Label = "Confirmed",
                    CssClass = "rd-badge-success",
                    Note = "Your place is confirmed.",
                    CanCancel = true
                };
            }

            if (Is(registration, "Completed"))
            {
                return new EnrolmentStatusView
                {
                    Kind = "completed",
                    Label = "Completed",
                    CssClass = "rd-badge-success"
                };
            }

            if (Is(registration, "No-show"))
            {
                return new EnrolmentStatusView
                {
                    Kind = "dns",
                    Label = "No-show",
                    CssClass = "rd-badge-soft"
                };
            }

            return new EnrolmentStatusView
            {
                Kind = "pending",
                Label = "Pending",
                CssClass = "rd-badge-pending",
                Note = "You are enrolled. Your place is waiting to be confirmed.",
                CanCancel = true
            };
        }

        private static EnrolmentStatusView FromResult(JsonElement result)
        {
            string status = ReadString(result, "status").Trim();

            if (Is(status, "Finished"))
            {
                string time = ReadString(result, "time");

                return new EnrolmentStatusView
                {
                    Kind = "finished",
                    Label = "Finished",
                    CssClass = "rd-badge-success",
                    Time = string.IsNullOrEmpty(time) ? null : FormatTime(time),
                    Position = ReadInt(result, "position"),
                    TotalFinishers = ReadInt(result, "totalFinishers")
                };
            }

            if (Is(status, "DNF"))
            {
                return new EnrolmentStatusView
                {
                    Kind = "dnf",
                    Label = "Did not finish",
                    CssClass = "rd-badge-warn",
                    Note = "The organiser recorded that you did not finish."
                };
            }

            if (Is(status, "DNS"))
            {
                return new EnrolmentStatusView
                {
                    Kind = "dns",
                    Label = "Did not start",
                    CssClass = "rd-badge-soft",
                    Note = "The organiser recorded that you did not start."
                };
            }

            if (Is(status, "DSQ") || Is(status, "Disqualified"))
            {
                return new EnrolmentStatusView
                {
                    Kind = "dsq",
                    Label = "Disqualified",
                    CssClass = "rd-badge-danger",
                    Note = "The organiser recorded this result as disqualified."
                };
            }

            return new EnrolmentStatusView
            {
                Kind = "recorded",
                Label = string.IsNullOrEmpty(status) ? "Result recorded" : status,
                CssClass = "rd-badge-soft"
            };
        }

        // 1. Match on the enrolment ID when the API returns it.
        // 2. Otherwise match on event name, race day and category, so the page
        //    works before the API change is deployed.
        private static JsonElement? FindResult(
            IReadOnlyList<JsonElement> results,
            JsonElement enrolment,
            string eventName,
            DateTime? eventDate,
            string categoryName)
        {
            int enrolmentId = ReadInt(enrolment, "participantEventID") ?? 0;

            if (enrolmentId != 0)
            {
                foreach (JsonElement result in results)
                {
                    if (ReadInt(result, "participantEventID") == enrolmentId)
                    {
                        return result;
                    }
                }
            }

            if (!eventDate.HasValue)
            {
                return null;
            }

            foreach (JsonElement result in results)
            {
                if (ReadInt(result, "participantEventID").HasValue)
                {
                    continue;
                }

                DateTime? resultDate = ReadDate(result, "eventDate");

                if (Is(ReadString(result, "eventName"), eventName)
                    && resultDate.HasValue
                    && resultDate.Value.Date == eventDate.Value.Date
                    && Is(ReadString(result, "categoryName"), categoryName))
                {
                    return result;
                }
            }

            return null;
        }

        private static bool Is(string? value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatTime(string raw)
        {
            if (!TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out TimeSpan time))
            {
                return raw;
            }

            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
        }

        private static JsonElement? ReadElement(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }

            return null;
        }

        private static string ReadString(JsonElement element, string name)
        {
            JsonElement? value = ReadElement(element, name);

            if (value is not { } found)
            {
                return string.Empty;
            }

            return found.ValueKind switch
            {
                JsonValueKind.String => found.GetString() ?? string.Empty,
                JsonValueKind.Number => found.ToString(),
                _ => string.Empty
            };
        }

        private static int? ReadInt(JsonElement element, string name)
        {
            JsonElement? value = ReadElement(element, name);

            return value is { ValueKind: JsonValueKind.Number } number && number.TryGetInt32(out int parsed)
                ? parsed
                : null;
        }

        private static DateTime? ReadDate(JsonElement element, string name)
        {
            return DateTime.TryParse(
                ReadString(element, name),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date)
                ? date
                : null;
        }
    }
}