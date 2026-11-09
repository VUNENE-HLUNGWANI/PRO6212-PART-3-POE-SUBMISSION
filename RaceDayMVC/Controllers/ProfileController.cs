using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Models;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class ProfileController : Controller
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApiClient _apiClient;

        public ProfileController(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // GET: /Profile
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Auth");

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Get,
                    "/api/Profile");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Login", "Auth");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Unable to load your profile. Please try again.";
                    return RedirectToAction("Index", "Home");
                }

                string body = await response.Content.ReadAsStringAsync();

                var profile = JsonSerializer.Deserialize<ProfileViewModel>(body, JsonOptions);

                if (profile == null)
                {
                    TempData["Error"] = "Your profile information could not be loaded.";
                    return RedirectToAction("Index", "Home");
                }

                HttpContext.Session.SetString("Email", profile.Email ?? "");
                HttpContext.Session.SetString("Role", profile.Role ?? "");
                HttpContext.Session.SetString("FirstName", profile.FirstName ?? "");
                HttpContext.Session.SetString("LastName", profile.LastName ?? "");

                return View(profile);
            }
            catch (Exception)
            {
                TempData["Error"] = "Unable to connect to RaceDay. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: /Profile/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            ProfileViewModel model,
            IFormFile? profilePicture)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Auth");

            model.Role = HttpContext.Session.GetString("Role") ?? "";

            if (!ModelState.IsValid)
                return await ReturnWithPictureAsync(model);

            string newEmail = model.Email?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(newEmail))
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email address is required.");

                return await ReturnWithPictureAsync(model);
            }

            var payload = new
            {
                firstName = model.FirstName.Trim(),
                lastName = model.LastName.Trim(),
                phoneNumber = model.PhoneNumber.Trim(),
                email = newEmail,
                dateOfBirth = model.DateOfBirth,
                gender = model.Gender,
                emergencyContactName = model.EmergencyContactName,
                emergencyContactNumber = model.EmergencyContactNumber,
                organisationName = model.OrganisationName,
                organisationContact = model.OrganisationContact
            };

            try
            {
                // STEP 1: Send the profile update to the API.
                using var updateResponse = await _apiClient.SendAsync(
                    HttpMethod.Put,
                    "/api/Profile",
                    payload);

                if (updateResponse.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToAction("Login", "Auth");

                string updateBody = await updateResponse.Content.ReadAsStringAsync();

                // STEP 2: Check that the API saved the profile.
                if (!updateResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        GetApiMessage(updateBody, "Your profile could not be saved. Please try again."));

                    return await ReturnWithPictureAsync(model);
                }

                // STEP 3: Confirm the API saved the email we sent.
                string savedEmail = GetApiString(updateBody, "email") ?? "";

                if (!string.Equals(savedEmail, newEmail, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Your email address could not be updated right now. Please try again later.");

                    return await ReturnWithPictureAsync(model);
                }

                string previousEmail = HttpContext.Session.GetString("Email") ?? "";
                bool emailChanged = !string.Equals(
                    previousEmail, newEmail, StringComparison.OrdinalIgnoreCase);

                HttpContext.Session.SetString("Email", savedEmail);
                HttpContext.Session.SetString("FirstName", model.FirstName.Trim());
                HttpContext.Session.SetString("LastName", model.LastName.Trim());

                // STEP 4: Upload the profile picture separately, if one was selected.
                if (profilePicture != null && profilePicture.Length > 0)
                {
                    string? pictureError = await UploadPictureAsync(profilePicture);

                    if (pictureError != null)
                    {
                        TempData["Error"] =
                            "Your details were saved, but your picture was not uploaded. " + pictureError;

                        return RedirectToAction(nameof(Index));
                    }
                }

                // STEP 5: If the email changed, sign out so the next login uses the new address.
                if (emailChanged)
                {
                    await _apiClient.SendAsync(HttpMethod.Post, "/api/Auth/logout");
                    HttpContext.Session.Clear();

                    TempData["Success"] =
                        "Your email address was changed. Log in with your new email address.";

                    return RedirectToAction("Login", "Auth");
                }

                // STEP 6: Success.
                TempData["Success"] = "Your profile has been saved successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to connect to RaceDay. Please try again.");

                return await ReturnWithPictureAsync(model);
            }
        }

        // Re-renders the form and keeps the current profile picture visible.
        private async Task<IActionResult> ReturnWithPictureAsync(ProfileViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ProfilePictureUri))
            {
                try
                {
                    using var response = await _apiClient.SendAsync(
                        HttpMethod.Get,
                        "/api/Profile");

                    if (response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync();

                        model.ProfilePictureUri = GetApiString(body, "profilePictureUri");
                    }
                }
                catch (Exception)
                {
                    // The form still works without the picture.
                }
            }

            return View("Index", model);
        }

        private async Task<string?> UploadPictureAsync(IFormFile picture)
        {
            if (picture.Length > 5 * 1024 * 1024)
                return "The picture must be 5 MB or smaller.";

            var allowedTypes = new[]
            {
                "image/jpeg",
                "image/png",
                "image/webp"
            };

            if (!allowedTypes.Contains(picture.ContentType, StringComparer.OrdinalIgnoreCase))
                return "Only JPG, PNG and WebP pictures are accepted.";

            using var response = await _apiClient.SendFileAsync(
                HttpMethod.Post,
                "/api/Profile/picture",
                picture);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return "Your session has expired. Please log in again.";

            if (response.IsSuccessStatusCode)
                return null;

            string body = await response.Content.ReadAsStringAsync();

            return GetApiMessage(body, "The picture could not be uploaded.");
        }

        private static string? GetApiString(string body, string propertyName)
        {
            try
            {
                using var document = JsonDocument.Parse(body);

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON, so there is no value to read.
            }
            catch (InvalidOperationException)
            {
                // JSON was not an object.
            }

            return null;
        }

        private static string GetApiMessage(string body, string fallback)
        {
            try
            {
                using var document = JsonDocument.Parse(body);

                if (document.RootElement.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString() ?? fallback;
                }

                if (document.RootElement.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in errors.EnumerateObject())
                    {
                        if (property.Value.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var error in property.Value.EnumerateArray())
                        {
                            if (error.ValueKind == JsonValueKind.String)
                                return error.GetString() ?? fallback;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Return the fallback message.
            }

            return fallback;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId") != null;
        }
    }
}