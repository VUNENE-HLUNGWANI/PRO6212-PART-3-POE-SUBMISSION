using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApiClient _apiClient;

        public AuthController(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string email = model.Email.Trim();

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Post,
                    "/api/Auth/login",
                    new
                    {
                        Email = email,
                        model.Password
                    });

                string responseBody =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = GetApiError(responseBody);
                    return View(model);
                }

                using var document = JsonDocument.Parse(responseBody);
                var result = document.RootElement;

                int userId = result.GetProperty("userId").GetInt32();

                string role =
                    result.GetProperty("role").GetString() ?? string.Empty;

                string firstName =
                    result.GetProperty("firstName").GetString() ?? string.Empty;

                string lastName =
                    result.GetProperty("lastName").GetString() ?? string.Empty;

                // Use the email returned by the API/database.
                string actualEmail = result.TryGetProperty(
                    "email", out var emailElement)
                    ? emailElement.GetString() ?? email
                    : email;

                HttpContext.Session.SetInt32("UserId", userId);
                HttpContext.Session.SetString("Role", role);
                HttpContext.Session.SetString("FirstName", firstName);
                HttpContext.Session.SetString("LastName", lastName);
                HttpContext.Session.SetString("Email", actualEmail);

                return RedirectToAction("Index", "Home");
            }
            catch (JsonException)
            {
                ViewBag.Error = "The API returned an invalid login response.";
                return View(model);
            }
            catch (Exception)
            {
                ViewBag.Error =
                    "Unable to connect to the RaceDay API. Make sure the API is running and try again.";

                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.Email = model.Email.Trim();

            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Post,
                    "/api/Auth/register",
                    model);

                string responseBody =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = GetApiError(responseBody);
                    return View(model);
                }

                TempData["Success"] =
                    "Registration successful. You can now log in.";

                return RedirectToAction(nameof(Login));
            }
            catch (Exception)
            {
                ViewBag.Error =
                    "Unable to connect to the RaceDay API. Make sure the API is running and try again.";

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                using var response = await _apiClient.SendAsync(
                    HttpMethod.Post,
                    "/api/Auth/logout");
            }
            catch
            {
                // Clear the local session even if the API is unavailable.
            }

            HttpContext.Session.Clear();
            _apiClient.ClearApiSession();

            return RedirectToAction("Index", "Home");
        }

        private static string GetApiError(string responseBody)
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);

                if (document.RootElement.TryGetProperty(
                    "message", out var message))
                {
                    return message.GetString()
                        ?? "The request could not be completed.";
                }
            }
            catch (JsonException)
            {
                // Use the fallback message.
            }

            return "The request could not be completed. Please check your details and try again.";
        }

        public class LoginViewModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;
        }

        public class RegisterViewModel
        {
            [Required]
            [StringLength(50)]
            public string FirstName { get; set; } = string.Empty;

            [Required]
            [StringLength(50)]
            public string LastName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [StringLength(100)]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 8)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required]
            [StringLength(20)]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required]
            public string Role { get; set; } = "Participant";

            public DateTime? DateOfBirth { get; set; }

            public string? Gender { get; set; }

            public string? EmergencyContactName { get; set; }

            public string? EmergencyContactNumber { get; set; }

            public string? OrganisationName { get; set; }

            public string? OrganisationContact { get; set; }

            public string? OrganiserCode { get; set; }
        }
    }
}