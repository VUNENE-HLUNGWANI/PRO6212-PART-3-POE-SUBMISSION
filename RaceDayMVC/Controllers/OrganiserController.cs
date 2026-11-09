using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Models;
using RaceDayMVC.Services;
using System.Globalization;

namespace RaceDayMVC.Controllers
{
    public class OrganiserController : Controller
    {
        private readonly ApiClient _apiClient;

        public OrganiserController(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // GET: /Organiser
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!string.Equals(
                HttpContext.Session.GetString("Role"),
                "Organiser",
                StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "That page is for organisers only.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "/api/Events/mine");

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error =
                        "Unable to load your events. Please try again.";

                    return View(new List<JsonElement>());
                }

                var responseBody =
                    await response.Content.ReadAsStringAsync();

                var events = JsonSerializer.Deserialize<List<JsonElement>>(
                    responseBody,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                var eventList = events ?? new List<JsonElement>();

                // Active enrolments per event for the dashboard.
                ViewBag.EnrolmentCounts = await LoadEnrolmentCountsAsync(eventList);

                return View(eventList);
            }
            catch (Exception)
            {
                ViewBag.Error =
                    "Unable to connect to the RaceDay API. Make sure the API is running.";

                return View(new List<JsonElement>());
            }
        }

        // GET: /Organiser/Create
        [HttpGet]
        public IActionResult Create()
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            return View(new CreateEventViewModel());
        }

        // POST: /Organiser/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateEventViewModel model)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var eventData = new
            {
                name = model.Name,
                description = model.Description,
                date = model.Date!.Value.ToString("yyyy-MM-dd"),
                location = model.Location,
                startTime = model.StartTime!.Value.ToString(@"hh\:mm\:ss"),
                routemap = model.Routemap,
                eventType = model.EventType,
                distance = model.Distance!.Value,
                registrationDeadline = model.RegistrationDeadline!.Value
            };

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Post,
                    "/api/Events",
                    eventData);

                var responseBody =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        GetApiMessage(
                            responseBody,
                            "The event could not be created. Please check your details and try again."));

                    return View(model);
                }

                if (model.BannerImage != null)
                {
                    using var createdEvent = JsonDocument.Parse(responseBody);

                    var eventId = createdEvent.RootElement
                        .GetProperty("eventID")
                        .GetInt32();

                    var bannerError =
                        await UploadBannerAsync(eventId, model.BannerImage);

                    if (bannerError != null)
                    {
                        TempData["Error"] =
                            $"The event was created, but its banner was not saved. {bannerError}";

                        return RedirectToAction(nameof(Index));
                    }
                }

                TempData["Success"] = "Event created successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to connect to the RaceDay API. Make sure the API is running.");

                return View(model);
            }
        }

        // GET: /Organiser/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    $"/api/Events/{id}");

                if (response.StatusCode ==
                    System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] =
                        "Unable to load the event. Please try again.";

                    return RedirectToAction(nameof(Index));
                }

                var responseBody =
                    await response.Content.ReadAsStringAsync();

                var raceEvent = JsonSerializer.Deserialize<EventDto>(
                    responseBody,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (raceEvent == null)
                {
                    TempData["Error"] =
                        "Unable to load the event. Please try again.";

                    return RedirectToAction(nameof(Index));
                }

                if (raceEvent.EventOrganiserID !=
                    HttpContext.Session.GetInt32("UserId"))
                {
                    TempData["Error"] =
                        "You can only edit your own events.";

                    return RedirectToAction(nameof(Index));
                }

                var model = new EditEventViewModel
                {
                    EventId = raceEvent.EventID,
                    Name = raceEvent.Name,
                    Description = raceEvent.Description,
                    Date = raceEvent.Date,
                    Location = raceEvent.Location,
                    StartTime = raceEvent.StartTime,
                    Routemap = raceEvent.Routemap,
                    Distance = raceEvent.Distance,
                    EventType = raceEvent.EventType,
                    RegistrationDeadline = raceEvent.RegistrationDeadline,
                    CurrentBannerUri = raceEvent.BannerImageUri
                };

                return View(model);
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Unable to connect to the RaceDay API. Make sure the API is running.";

                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Organiser/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditEventViewModel model)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var eventData = new
            {
                name = model.Name,
                description = model.Description,
                date = model.Date!.Value.ToString("yyyy-MM-dd"),
                location = model.Location,
                startTime = model.StartTime!.Value.ToString(@"hh\:mm\:ss"),
                routemap = model.Routemap,
                eventType = model.EventType,
                distance = model.Distance!.Value,
                registrationDeadline = model.RegistrationDeadline!.Value
            };

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Put,
                    $"/api/Events/{model.EventId}",
                    eventData);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode ==
                        System.Net.HttpStatusCode.Forbidden)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "You can only edit your own events.");
                    }
                    else if (response.StatusCode ==
                        System.Net.HttpStatusCode.NotFound)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "This event no longer exists.");
                    }
                    else
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "The event could not be updated. Please check your details and try again.");
                    }

                    return View(model);
                }

                if (model.BannerImage != null)
                {
                    var bannerError =
                        await UploadBannerAsync(model.EventId, model.BannerImage);

                    if (bannerError != null)
                    {
                        ModelState.AddModelError(string.Empty, bannerError);

                        return View(model);
                    }
                }

                TempData["Success"] = "Event updated successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to connect to the RaceDay API. Make sure the API is running.");

                return View(model);
            }
        }

        // POST: /Organiser/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Delete,
                    $"/api/Events/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Event deleted successfully.";
                }
                else if (response.StatusCode ==
                    System.Net.HttpStatusCode.Conflict)
                {
                    TempData["Error"] =
                        "This event has enrolments and cannot be deleted.";
                }
                else if (response.StatusCode ==
                    System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["Error"] =
                        "You can only delete your own events.";
                }
                else
                {
                    TempData["Error"] =
                        "The event could not be deleted. Please try again.";
                }
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Unable to connect to the RaceDay API. Make sure the API is running.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Organiser/Registrations/5
        [HttpGet]
        public async Task<IActionResult> Registrations(int id)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    $"/api/events/{id}/enrolments");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["Error"] =
                        "You can only view registrations for your own events.";

                    return RedirectToAction(nameof(Index));
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] =
                        "Unable to load registrations. Please try again.";

                    return RedirectToAction(nameof(Index));
                }

                var responseBody =
                    await response.Content.ReadAsStringAsync();

                var registrations =
                    JsonSerializer.Deserialize<List<JsonElement>>(
                        responseBody,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                ViewBag.EventId = id;
                ViewBag.RegistrationClosed = await IsRegistrationClosedAsync(id);

                return View(registrations ?? new List<JsonElement>());
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Unable to connect to the RaceDay API. Make sure the API is running.";

                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Organiser/UpdateEnrolmentStatus
        // Lets the organiser confirm (or return to pending) an enrolment for their own event.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEnrolmentStatus(
            int eventId,
            int enrolmentId,
            string status)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "That status cannot be set from this page.";
                return RedirectToAction(nameof(Registrations), new { id = eventId });
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Put,
                    $"/api/enrolments/{enrolmentId}/status",
                    new { Status = status });

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = $"Enrolment marked as {status}.";
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();

                    TempData["Error"] = GetApiMessage(
                        body,
                        "The enrolment status could not be updated. Please try again.");
                }
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Unable to connect to the RaceDay API. Make sure the API is running.";
            }

            return RedirectToAction(nameof(Registrations), new { id = eventId });
        }

        // GET: /Organiser/Results/5
        // Results are entered on the Results page, so this sends the organiser there.
        [HttpGet]
        public IActionResult Results(int id)
        {
            if (!IsOrganiser())
            {
                return RedirectToAction("Login", "Auth");
            }

            return RedirectToAction("Enter", "Results", new { id });
        }

        // Registration is closed once the deadline has passed or the race has run.
        // Returns false if the event cannot be loaded, so the page still shows statuses.
        private async Task<bool> IsRegistrationClosedAsync(int eventId)
        {
            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    $"/api/Events/{eventId}");

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());

                var raceDate = ReadEventDate(document.RootElement, "date");
                var deadline = ReadEventDate(document.RootElement, "registrationDeadline");

                bool raceHasRun = raceDate.HasValue && raceDate.Value.Date <= DateTime.Today;
                bool deadlinePassed = deadline.HasValue && deadline.Value <= DateTime.Now;

                return raceHasRun || deadlinePassed;
            }
            catch
            {
                return false;
            }
        }

        private static DateTime? ReadEventDate(JsonElement element, string propertyName)
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

        // Uploads a banner image through the API. The MVC app never talks to Azure directly.
        // Returns null on success, or a message describing the failure.
        private async Task<string?> UploadBannerAsync(int eventId, IFormFile banner)
        {
            using var response = await _apiClient.SendFileAsync(
                HttpMethod.Post,
                $"/api/Events/{eventId}/banner",
                banner);

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            var statusCode = (int)response.StatusCode;

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return "The image must be a JPG, PNG or WebP file of 5 MB or less.";
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return $"Your session did not allow the upload (status {statusCode}). Log out, log in again and retry.";
            }

            return $"The banner could not be saved (status {statusCode}). Check the API console for details.";
        }

        // Counts the enrolments that still hold a place, keyed by event ID.
        private async Task<Dictionary<int, int>> LoadEnrolmentCountsAsync(
            List<JsonElement> events)
        {
            var counts = new Dictionary<int, int>();

            foreach (var raceEvent in events)
            {
                if (!raceEvent.TryGetProperty("eventID", out var idValue)
                    || !idValue.TryGetInt32(out int eventId))
                {
                    continue;
                }

                counts[eventId] = 0;

                try
                {
                    using var response = await _apiClient.SendAsync(
                        HttpMethod.Get,
                        $"/api/events/{eventId}/enrolments");

                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    using var document = JsonDocument.Parse(
                        await response.Content.ReadAsStringAsync());

                    if (document.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    int active = 0;

                    foreach (var enrolment in document.RootElement.EnumerateArray())
                    {
                        string status =
                            enrolment.TryGetProperty("registrationStatus", out var s)
                            && s.ValueKind == JsonValueKind.String
                                ? s.GetString() ?? string.Empty
                                : string.Empty;

                        if (string.Equals(status, "Withdrawn", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        active++;
                    }

                    counts[eventId] = active;
                }
                catch
                {
                    // Leave the count at zero so the dashboard still loads.
                }
            }

            return counts;
        }

        private static string GetApiMessage(string responseBody, string fallback)
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("message", out var message)
                        && message.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(message.GetString()))
                    {
                        return message.GetString()!;
                    }

                    if (root.TryGetProperty("errors", out var errors)
                        && errors.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in errors.EnumerateObject())
                        {
                            if (property.Value.ValueKind != JsonValueKind.Array)
                                continue;

                            foreach (var error in property.Value.EnumerateArray())
                            {
                                if (error.ValueKind == JsonValueKind.String
                                    && !string.IsNullOrWhiteSpace(error.GetString()))
                                {
                                    return error.GetString()!;
                                }
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON, so use the fallback below.
            }

            return fallback;
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