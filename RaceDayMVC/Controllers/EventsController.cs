using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using RaceDayMVC.Helpers;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApiClient _apiClient;

        public EventsController(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // GET: /Events
        // GET: /Events?type=Run  (type can be Run, Walk or Cycle)
        public async Task<IActionResult> Index(string? type = null)
        {
            string? eventType = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
            ViewBag.EventType = eventType;

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "/api/Events");

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = "Unable to load events.";
                    return View("~/Views/Events/Index.cshtml", new List<JsonElement>());
                }

                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    ViewBag.Error = "The events response was not in the expected format.";
                    return View("~/Views/Events/Index.cshtml", new List<JsonElement>());
                }

                List<JsonElement> events = document.RootElement
                    .EnumerateArray()
                    .Select(item => item.Clone())
                    .ToList();

                // Only keep events of the requested type, such as Run, Walk or Cycle.
                if (eventType != null)
                {
                    events = events
                        .Where(ev => string.Equals(
                            GetText(ev, "eventType") ?? string.Empty,
                            eventType,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                return View("~/Views/Events/Index.cshtml", events);
            }
            catch
            {
                ViewBag.Error = "Unable to connect to RaceDay.";

                return View("~/Views/Events/Index.cshtml", new List<JsonElement>());
            }
        }

        // GET: /Events/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    $"/api/Events/{id}");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    TempData["Error"] = "Event not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Unable to load event details.";
                    return RedirectToAction(nameof(Index));
                }

                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    TempData["Error"] = "The event details were returned in an unexpected format.";
                    return RedirectToAction(nameof(Index));
                }

                JsonElement raceEvent = document.RootElement.Clone();

                var myEnrolment = await GetMyEnrolmentForEventAsync(id);

                ViewBag.MyEnrolment = myEnrolment;
                ViewBag.MyStatus = myEnrolment.HasValue
                    ? await ResolveMyStatusAsync(myEnrolment.Value)
                    : null;

                await PrepareEligibilityAsync(raceEvent);

                return View("~/Views/Events/Details.cshtml", raceEvent);
            }
            catch
            {
                TempData["Error"] = "Unable to load the event details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Events/Enrol
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enrol(int eventId, int categoryId)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!IsParticipant())
            {
                TempData["Error"] = "Only participants can enrol in events.";
                return RedirectToAction(nameof(Details), new { id = eventId });
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Post,
                    "/api/enrolments",
                    new
                    {
                        EventID = eventId,
                        CategoryID = categoryId
                    });

                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = GetApiMessage(
                        responseText,
                        "You have enrolled successfully.");
                }
                else
                {
                    TempData["Error"] = GetApiMessage(
                        responseText,
                        "Unable to enrol in this category.");
                }
            }
            catch
            {
                TempData["Error"] = "Unable to connect to RaceDay.";
            }

            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        // POST: /Events/ChangeCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeCategory(
            int eventId,
            int enrolmentId,
            int categoryId)
        {
            if (HttpContext.Session.GetInt32("UserId") == null || !IsParticipant())
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Put,
                    $"/api/enrolments/{enrolmentId}/category",
                    new { CategoryID = categoryId });

                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = GetApiMessage(
                        responseText,
                        "Your category has been changed.");
                }
                else
                {
                    TempData["Error"] = GetApiMessage(
                        responseText,
                        "Unable to change your category.");
                }
            }
            catch
            {
                TempData["Error"] = "Unable to connect to RaceDay.";
            }

            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        private bool IsParticipant()
        {
            return string.Equals(
                HttpContext.Session.GetString("Role"),
                "Participant",
                StringComparison.OrdinalIgnoreCase);
        }

        // Works out the participant's status for this event, using their results when they exist.
        private async Task<EnrolmentStatusView> ResolveMyStatusAsync(JsonElement enrolment)
        {
            var results = new List<JsonElement>();

            try
            {
                using var response = await _apiClient.SendAsync(HttpMethod.Get, "api/Results/mine");

                if (response.IsSuccessStatusCode)
                {
                    using JsonDocument document =
                        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                    results = document.RootElement
                        .EnumerateArray()
                        .Select(item => item.Clone())
                        .ToList();
                }
            }
            catch
            {
                // If results cannot be loaded, the registration status is shown instead.
            }

            return EnrolmentStatusResolver.ResolveOne(enrolment, results, DateTime.Today);
        }

        private async Task<JsonElement?> GetMyEnrolmentForEventAsync(int eventId)
        {
            if (HttpContext.Session.GetInt32("UserId") == null || !IsParticipant())
            {
                return null;
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "/api/enrolments/mine");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                foreach (JsonElement enrolment in document.RootElement.EnumerateArray())
                {
                    if (!TryGetPropertyIgnoreCase(enrolment, "eventID", out JsonElement eventIdValue))
                    {
                        continue;
                    }

                    if (!eventIdValue.TryGetInt32(out int enrolmentEventId) ||
                        enrolmentEventId != eventId)
                    {
                        continue;
                    }

                    if (TryGetPropertyIgnoreCase(enrolment, "registrationStatus", out JsonElement status) &&
                        string.Equals(status.ToString(), "Cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return enrolment.Clone();
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        // Works out, for each category, whether the signed-in participant can enter it.
        // Guests and organisers see no warnings.
        private async Task PrepareEligibilityAsync(JsonElement raceEvent)
        {
            var messages = new Dictionary<int, string>();

            var profile = await GetParticipantProfileAsync();

            ViewBag.ParticipantAge = profile.Age;

            if (!IsParticipant() ||
                !TryGetPropertyIgnoreCase(raceEvent, "categories", out JsonElement categories) ||
                categories.ValueKind != JsonValueKind.Array)
            {
                ViewBag.CategoryAgeMessages = messages;
                return;
            }

            foreach (JsonElement category in categories.EnumerateArray())
            {
                if (!TryGetPropertyIgnoreCase(category, "categoryID", out JsonElement categoryIdValue) ||
                    !categoryIdValue.TryGetInt32(out int categoryId))
                {
                    continue;
                }

                string? error = GetEligibilityMessage(category, profile);

                if (error != null)
                {
                    messages[categoryId] = error;
                }
            }

            ViewBag.CategoryAgeMessages = messages;
        }

        private async Task<ParticipantProfile> GetParticipantProfileAsync()
        {
            var profile = new ParticipantProfile();

            if (!IsParticipant())
            {
                return profile;
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "/api/Profile");

                if (!response.IsSuccessStatusCode)
                {
                    return profile;
                }

                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument document = JsonDocument.Parse(json);

                profile.Gender = GetText(document.RootElement, "gender");

                if (TryGetPropertyIgnoreCase(document.RootElement, "dateOfBirth", out JsonElement dobElement) &&
                    DateTime.TryParse(dobElement.ToString(), out DateTime dateOfBirth))
                {
                    profile.Age = CalculateAge(dateOfBirth);
                }
            }
            catch
            {
                // Leave the profile empty; the category will show as not eligible.
            }

            return profile;
        }

        // Returns a short message when the participant cannot enter the category, or null if they can.
        private static string? GetEligibilityMessage(JsonElement category, ParticipantProfile profile)
        {
            string? gender = GetText(category, "gender");

            if (!string.IsNullOrWhiteSpace(gender) &&
                !string.Equals(profile.Gender, gender, StringComparison.OrdinalIgnoreCase))
            {
                return $"Not eligible: this is a {gender.ToLowerInvariant()}-only category.";
            }

            int? minAge = GetNullableInt(category, "minAge");
            int? maxAge = GetNullableInt(category, "maxAge");

            // Categories with no age limits accept everyone.
            if (!minAge.HasValue && !maxAge.HasValue)
            {
                return null;
            }

            if (profile.Age == null)
            {
                return "Not eligible: your age could not be confirmed for this category.";
            }

            if (minAge.HasValue && profile.Age.Value < minAge.Value)
            {
                return "Not eligible: your age does not match this category.";
            }

            if (maxAge.HasValue && profile.Age.Value > maxAge.Value)
            {
                return "Not eligible: your age does not match this category.";
            }

            return null;
        }

        private static int CalculateAge(DateTime dateOfBirth)
        {
            DateTime today = DateTime.Today;
            int age = today.Year - dateOfBirth.Year;

            if (dateOfBirth.Date > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }

        private static string? GetText(JsonElement element, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static int? GetNullableInt(JsonElement element, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            {
                return number;
            }

            return null;
        }

        private static bool TryGetPropertyIgnoreCase(
            JsonElement element,
            string propertyName,
            out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static string GetApiMessage(string responseText, string fallback)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(responseText);

                if (TryGetPropertyIgnoreCase(document.RootElement, "message", out JsonElement message))
                {
                    string text = message.ToString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
            catch
            {
                // Use the fallback if the response is not JSON.
            }

            return fallback;
        }

        private sealed class ParticipantProfile
        {
            public int? Age { get; set; }

            public string? Gender { get; set; }
        }
    }
}