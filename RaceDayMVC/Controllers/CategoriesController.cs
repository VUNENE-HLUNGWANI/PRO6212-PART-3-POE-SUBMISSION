
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApiClient _apiClient;

        private readonly JsonSerializerOptions _jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        public CategoriesController(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // GET: /Categories
        public async Task<IActionResult> Index()
        {
            if (!IsOrganiser())
                return RedirectToAction("Login", "Auth");

            var model = new CategoryManagementViewModel();

            await LoadEvents(model);

            return View(model);
        }

        // POST: /Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryManagementViewModel model)
        {
            if (!IsOrganiser())
                return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
            {
                await LoadEvents(model);
                return View("Index", model);
            }

            try
            {
                using var eventsResponse = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "/api/Events/mine");

                if (!eventsResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Unable to verify your events. Please log in again.");

                    await LoadEvents(model);
                    return View("Index", model);
                }

                var eventsJson =
                    await eventsResponse.Content.ReadAsStringAsync();

                var events =
                    JsonSerializer.Deserialize<List<CategoryEventViewModel>>(
                        eventsJson, _jsonOptions)
                    ?? new List<CategoryEventViewModel>();

                bool ownsEvent = events.Exists(
                    e => e.EventID == model.Category.EventID);

                if (!ownsEvent)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Please select one of your own events.");

                    await LoadEvents(model);
                    return View("Index", model);
                }

                using var response = await _apiClient.SendAsync(
                    HttpMethod.Post,
                    "/api/Categories",
                    model.Category);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] =
                        "Category created successfully.";

                    return RedirectToAction(nameof(Index));
                }

                var errorBody =
                    await response.Content.ReadAsStringAsync();

                ModelState.AddModelError(
                    string.Empty,
                    GetApiMessage(
                        errorBody,
                        "The category could not be created. Please check your details and try again."));
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Could not connect to the RaceDay API. Check that the API is running.");
            }

            await LoadEvents(model);
            return View("Index", model);
        }

        // GET: /Categories/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsOrganiser())
                return RedirectToAction("Login", "Auth");

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    $"/api/Categories/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Unable to find this category.";
                    return RedirectToAction(nameof(Index));
                }

                var json = await response.Content.ReadAsStringAsync();

                var category =
                    JsonSerializer.Deserialize<CategoryEditViewModel>(
                        json, _jsonOptions);

                if (category == null)
                {
                    TempData["Error"] = "Unable to load category details.";
                    return RedirectToAction(nameof(Index));
                }

                var model = new CategoryManagementViewModel();
                await LoadEvents(model);

                bool ownsEvent = model.Events.Exists(
                    e => e.EventID == category.EventID);

                if (!ownsEvent)
                {
                    TempData["Error"] =
                        "You can only edit categories for your own events.";
                    return RedirectToAction(nameof(Index));
                }

                return View(category);
            }
            catch (Exception)
            {
                TempData["Error"] = "Could not connect to the RaceDay API.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            CategoryEditViewModel model)
        {
            if (!IsOrganiser())
                return RedirectToAction("Login", "Auth");

            if (id != model.CategoryID)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var updatedCategory = new
                {
                    categoryName = model.CategoryName,
                    distance = model.Distance,
                    categoryLevel = model.CategoryLevel,
                    entryFee = model.EntryFee,
                    maxParticipants = model.MaxParticipants
                };

                using var response = await _apiClient.SendAsync(
                    HttpMethod.Put,
                    $"/api/Categories/{id}",
                    updatedCategory);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] =
                        "Category updated successfully.";

                    return RedirectToAction(nameof(Index));
                }

                var errorBody =
                    await response.Content.ReadAsStringAsync();

                ModelState.AddModelError(
                    string.Empty,
                    GetApiMessage(
                        errorBody,
                        "Unable to update the category. Please check your details and try again."));

                return View(model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Could not connect to the RaceDay API.");

                return View(model);
            }
        }

        // POST: /Categories/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsOrganiser())
                return RedirectToAction("Login", "Auth");

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Delete,
                    $"/api/Categories/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] =
                        "Category deleted successfully.";
                }
                else
                {
                    var errorBody =
                        await response.Content.ReadAsStringAsync();

                    TempData["Error"] = response.StatusCode ==
                        System.Net.HttpStatusCode.Conflict
                        ? "This category has enrolments and cannot be deleted."
                        : GetApiMessage(
                            errorBody,
                            "Unable to delete the category. Please try again.");
                }
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Could not connect to the RaceDay API.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadEvents(CategoryManagementViewModel model)
        {
            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "/api/Events/mine");

                if (!response.IsSuccessStatusCode)
                {
                    model.Error =
                        "Unable to load your events. Please log in again.";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();

                model.Events =
                    JsonSerializer.Deserialize<List<CategoryEventViewModel>>(
                        json, _jsonOptions)
                    ?? new List<CategoryEventViewModel>();
            }
            catch (Exception)
            {
                model.Error =
                    "Could not connect to the RaceDay API.";
            }
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
            return HttpContext.Session.GetInt32("UserId").HasValue
                && HttpContext.Session.GetString("Role") == "Organiser";
        }
    }

    public class CategoryManagementViewModel
    {
        public CategoryCreateViewModel Category { get; set; } =
            new CategoryCreateViewModel();

        public List<CategoryEventViewModel> Events { get; set; } =
            new List<CategoryEventViewModel>();

        public string? Error { get; set; }
    }

    public class CategoryCreateViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Please select an event.")]
        public int EventID { get; set; }

        [Required(ErrorMessage = "Enter a category name.")]
        [StringLength(50)]
        public string CategoryName { get; set; } = string.Empty;

        [Range(0.01, 9999.99,
            ErrorMessage = "Distance must be greater than zero.")]
        public decimal Distance { get; set; }

        [Required(ErrorMessage = "Enter a category level.")]
        [StringLength(50)]
        public string CategoryLevel { get; set; } = string.Empty;

        [Range(0, 99999999.99,
            ErrorMessage = "Entry fee cannot be negative.")]
        public decimal EntryFee { get; set; }

        [Range(1, int.MaxValue,
            ErrorMessage = "Maximum participants must be at least 1.")]
        public int MaxParticipants { get; set; }
    }

    public class CategoryEventViewModel
    {
        public int EventID { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public List<CategoryItemViewModel> Categories { get; set; } =
            new List<CategoryItemViewModel>();
    }

    public class CategoryItemViewModel
    {
        public int CategoryID { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public decimal Distance { get; set; }

        public string CategoryLevel { get; set; } = string.Empty;

        public decimal EntryFee { get; set; }

        public int MaxParticipants { get; set; }
    }

    // This is now a separate class in the Controllers namespace.
    public class CategoryEditViewModel
    {
        public int CategoryID { get; set; }

        public int EventID { get; set; }

        public string EventName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a category name.")]
        [StringLength(50)]
        public string CategoryName { get; set; } = string.Empty;

        [Range(0.01, 9999.99,
            ErrorMessage = "Distance must be greater than zero.")]
        public decimal Distance { get; set; }

        [Required(ErrorMessage = "Select a category level.")]
        [StringLength(50)]
        public string CategoryLevel { get; set; } = string.Empty;

        [Range(0, 99999999.99,
            ErrorMessage = "Entry fee cannot be negative.")]
        public decimal EntryFee { get; set; }

        [Range(1, int.MaxValue,
            ErrorMessage = "Maximum participants must be at least 1.")]
        public int MaxParticipants { get; set; }
    }
}

