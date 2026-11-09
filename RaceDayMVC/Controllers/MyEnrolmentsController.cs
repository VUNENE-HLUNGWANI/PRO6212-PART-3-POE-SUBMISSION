using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Helpers;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class MyEnrolmentsController : Controller
    {
        private readonly ApiClient _apiClient;

        public MyEnrolmentsController(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // GET: /MyEnrolments
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (HttpContext.Session.GetString("Role") != "Participant")
            {
                return RedirectToAction("Index", "Home");
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "api/enrolments/mine");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] =
                        "Unable to load your enrolments. Please try again.";

                    return View(EmptyArray());
                }

                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument document = JsonDocument.Parse(json);

                var enrolments = document.RootElement.Clone();
                var results = await LoadResultsAsync();

                ViewBag.Statuses = EnrolmentStatusResolver.Resolve(
                    enrolments,
                    results,
                    DateTime.Today);

                return View(enrolments);
            }
            catch
            {
                TempData["Error"] =
                    "Unable to connect to RaceDay. Please try again.";

                return View(EmptyArray());
            }
        }

        // POST: /MyEnrolments/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null ||
                HttpContext.Session.GetString("Role") != "Participant")
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Put,
                    $"api/enrolments/{id}/cancel");

                string responseText =
                    await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = GetApiMessage(
                        responseText,
                        "Your enrolment has been cancelled.");
                }
                else
                {
                    TempData["Error"] = GetApiMessage(
                        responseText,
                        "Unable to cancel this enrolment.");
                }
            }
            catch
            {
                TempData["Error"] =
                    "Unable to connect to RaceDay. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        // The participant's recorded results. An empty list is returned if the call
        // fails, so the page still loads and shows registration statuses.
        private async Task<List<JsonElement>> LoadResultsAsync()
        {
            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "api/Results/mine");

                if (!response.IsSuccessStatusCode)
                {
                    return new List<JsonElement>();
                }

                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument document = JsonDocument.Parse(json);

                return document.RootElement
                    .EnumerateArray()
                    .Select(item => item.Clone())
                    .ToList();
            }
            catch
            {
                return new List<JsonElement>();
            }
        }

        private static JsonElement EmptyArray()
        {
            return JsonDocument.Parse("[]").RootElement.Clone();
        }

        private static string GetApiMessage(
            string responseText,
            string fallback)
        {
            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(responseText);

                foreach (JsonProperty property in
                         document.RootElement.EnumerateObject())
                {
                    if (string.Equals(
                        property.Name,
                        "message",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value.GetString() ?? fallback;
                    }
                }
            }
            catch
            {
                // Use the fallback if the response is not JSON.
            }

            return fallback;
        }
    }
}