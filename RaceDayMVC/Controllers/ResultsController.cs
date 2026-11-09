using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Models;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class ResultsController : Controller
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApiClient _apiClient;

        public ResultsController(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // GET: /Results/Enter/5
        [HttpGet]
        public async Task<IActionResult> Enter(int id)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                var model = new ResultsEntryViewModel { EventId = id };

                using (var eventResponse = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    $"/api/Events/{id}"))
                {
                    if (eventResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        return NotFound();
                    }

                    if (eventResponse.IsSuccessStatusCode)
                    {
                        var eventBody = await eventResponse.Content.ReadAsStringAsync();
                        var eventElement = JsonSerializer.Deserialize<JsonElement>(eventBody, JsonOptions);
                        model.EventName = GetText(eventElement, "name");
                        model.EventDate = GetDate(eventElement, "date");
                    }
                }

                // Results can only be entered once the race has taken place.
                if (model.EventDate.HasValue && model.EventDate.Value.Date > DateTime.Today)
                {
                    TempData["Error"] =
                        $"Results for {model.EventName} can be entered from {model.EventDate.Value:dd MMM yyyy}, after the race.";

                    return RedirectToAction("Index", "Organiser");
                }

                using var enrolmentResponse = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    $"/api/events/{id}/enrolments");

                if (enrolmentResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "You can only enter results for your own events.";

                    return RedirectToAction("Index", "Organiser");
                }

                var enrolments = await ReadListAsync(enrolmentResponse);

                using var resultResponse = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    $"/api/Results/event/{id}");

                var results = await ReadListAsync(resultResponse);

                foreach (var enrolment in enrolments)
                {
                    var participantEventId = GetInt(enrolment, "participantEventID");

                    if (participantEventId == 0)
                    {
                        continue;
                    }

                    // Withdrawn registrations are not racing, so they are not listed.
                    if (IsWithdrawn(GetText(enrolment, "registrationStatus")))
                    {
                        continue;
                    }

                    var row = new ResultRowViewModel
                    {
                        ParticipantEventId = participantEventId,
                        Bib = GetText(enrolment, "bib"),
                        ParticipantName = GetParticipantName(enrolment),
                        CategoryName = GetCategoryName(enrolment),
                        Gender = GetParticipantGender(enrolment),
                        CategoryGender = GetCategoryGender(enrolment),
                        Status = ResultStatuses.Finished
                    };

                    // A gender mismatch starts as DSQ with the suggested message.
                    if (row.IsGenderMismatch)
                    {
                        row.Status = ResultStatuses.DSQ;
                        row.Reason = ResultStatuses.GenderMismatchReason;
                    }

                    var existing = results.FirstOrDefault(r =>
                        GetInt(r, "participantEventID") == participantEventId);

                    if (existing.ValueKind == JsonValueKind.Object)
                    {
                        row.ResultId = GetInt(existing, "resultID");
                        row.Time = GetText(existing, "time");
                        row.Position = GetNullableInt(existing, "position");

                        var status = GetText(existing, "status");

                        if (!string.IsNullOrWhiteSpace(status))
                        {
                            row.Status = status;
                        }

                        var savedReason = GetText(existing, "reason");

                        if (!string.IsNullOrWhiteSpace(savedReason))
                        {
                            row.Reason = savedReason;
                        }
                    }

                    model.Rows.Add(row);
                }

                model.Rows = model.Rows
                    .OrderBy(r => r.Bib, StringComparer.Ordinal)
                    .ThenBy(r => r.ParticipantName)
                    .ToList();

                return View(model);
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Unable to connect to the RaceDay API. Make sure the API is running.";

                return RedirectToAction("Index", "Organiser");
            }
        }

        // POST: /Results/Enter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enter(ResultsEntryViewModel model)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            var saved = new List<string>();
            var errors = new List<string>();

            try
            {
                foreach (var row in model.Rows)
                {
                    var name = string.IsNullOrWhiteSpace(row.ParticipantName)
                        ? "Participant"
                        : row.ParticipantName;

                    var status = ResultStatuses.Normalise(row.Status);

                    if (status == null)
                    {
                        errors.Add($"{name}: choose a valid result status.");
                        continue;
                    }

                    // A gender mismatch is always a disqualification.
                    if (row.IsGenderMismatch)
                    {
                        status = ResultStatuses.DSQ;
                    }

                    var reason = row.Reason?.Trim();
                    var hasTime = !string.IsNullOrWhiteSpace(row.Time);
                    var hasPosition = row.Position.HasValue;

                    // A row with nothing entered and no saved result is skipped.
                    if (row.ResultId == 0 && !hasTime && !hasPosition && status == ResultStatuses.Finished)
                    {
                        continue;
                    }

                    string? timeText = null;
                    int? position = null;
                    string? savedReason = null;

                    if (status == ResultStatuses.Finished)
                    {
                        var missing = new List<string>();
                        var finishTime = TimeSpan.Zero;

                        if (!hasTime)
                        {
                            missing.Add("time");
                        }
                        else if (!TimeSpan.TryParse(row.Time, out finishTime))
                        {
                            errors.Add($"{name}: time must be in hh:mm:ss format.");
                            continue;
                        }

                        if (!hasPosition)
                        {
                            missing.Add("position");
                        }
                        else if (row.Position < 1)
                        {
                            errors.Add($"{name}: position must be 1 or more.");
                            continue;
                        }

                        if (missing.Count > 0)
                        {
                            var fields = string.Join(" and ", missing);
                            var verb = missing.Count == 1 ? "field is" : "fields are";

                            errors.Add($"{name}: the {fields} {verb} missing.");
                            continue;
                        }

                        timeText = finishTime.ToString(@"hh\:mm\:ss");
                        position = row.Position;
                    }
                    else if (status == ResultStatuses.DSQ)
                    {
                        // The message is required. It is never filled in on the organiser's behalf.
                        if (string.IsNullOrWhiteSpace(reason))
                        {
                            errors.Add($"{name}: Message field is required.");
                            continue;
                        }

                        savedReason = reason;
                    }

                    // DNF, DNS and DSQ have no time or position. Only DSQ keeps a message.
                    using var response = row.ResultId > 0
                        ? await _apiClient.SendAsync(
                            HttpMethod.Put,
                            $"/api/Results/{row.ResultId}",
                            new
                            {
                                resultID = row.ResultId,
                                participantEventID = row.ParticipantEventId,
                                time = timeText,
                                position,
                                status,
                                reason = savedReason
                            })
                        : await _apiClient.SendAsync(
                            HttpMethod.Post,
                            "/api/Results",
                            new
                            {
                                participantEventID = row.ParticipantEventId,
                                time = timeText,
                                position,
                                status,
                                reason = savedReason
                            });

                    if (response.IsSuccessStatusCode)
                    {
                        saved.Add(name);
                    }
                    else if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                        errors.Add($"{name}: a result already exists. Refresh the page and try again.");
                    }
                    else
                    {
                        errors.Add(
                            $"{name}: the result could not be saved (status {(int)response.StatusCode}).");
                    }
                }
            }
            catch (Exception)
            {
                errors.Add("Unable to connect to the RaceDay API. Make sure the API is running.");
            }

            if (saved.Count > 0)
            {
                TempData["Success"] = saved.Count == 1
                    ? $"{saved[0]}'s result was captured successfully."
                    : $"Results captured successfully for {string.Join(", ", saved)}.";
            }

            if (errors.Count > 0)
            {
                TempData["Error"] = string.Join(" ", errors);
            }

            if (saved.Count == 0 && errors.Count == 0)
            {
                TempData["Error"] =
                    "Enter a finish time and position, or choose DNF, DNS or DSQ, for at least one participant before saving.";
            }

            return RedirectToAction(nameof(Enter), new { id = model.EventId });
        }

        private static async Task<List<JsonElement>> ReadListAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new List<JsonElement>();
            }

            var body = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<JsonElement>>(body, JsonOptions)
                ?? new List<JsonElement>();
        }

        private static string GetText(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return string.Empty;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Null => string.Empty,
                JsonValueKind.String => value.GetString() ?? string.Empty,
                _ => value.ToString()
            };
        }

        private static DateTime? GetDate(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                && DateTime.TryParse(
                    value.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                return date;
            }

            return null;
        }

        private static int GetInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number)
                    ? number
                    : 0;
        }

        private static int? GetNullableInt(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number))
            {
                return number;
            }

            return null;
        }

        private static bool IsWithdrawn(string? status)
        {
            return string.Equals(status, "Withdrawn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetParticipantName(JsonElement enrolment)
        {
            if (enrolment.TryGetProperty("participant", out var participant)
                && participant.ValueKind == JsonValueKind.Object
                && participant.TryGetProperty("user", out var user)
                && user.ValueKind == JsonValueKind.Object)
            {
                var name = $"{GetText(user, "firstName")} {GetText(user, "lastName")}".Trim();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            return "Participant";
        }

        private static string GetCategoryName(JsonElement enrolment)
        {
            if (enrolment.TryGetProperty("category", out var category)
                && category.ValueKind == JsonValueKind.Object)
            {
                var categoryName = GetText(category, "categoryName");

                if (!string.IsNullOrWhiteSpace(categoryName))
                {
                    return categoryName;
                }
            }

            return "-";
        }

        private static string GetParticipantGender(JsonElement enrolment)
        {
            if (enrolment.TryGetProperty("participant", out var participant)
                && participant.ValueKind == JsonValueKind.Object)
            {
                return GetText(participant, "gender");
            }

            return string.Empty;
        }

        private static string GetCategoryGender(JsonElement enrolment)
        {
            if (enrolment.TryGetProperty("category", out var category)
                && category.ValueKind == JsonValueKind.Object)
            {
                return GetText(category, "gender");
            }

            return string.Empty;
        }

        private bool IsOrganiser()
        {
            return HttpContext.Session.GetInt32("UserId") != null
                && string.Equals(
                    HttpContext.Session.GetString("Role"),
                    "Organiser",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}