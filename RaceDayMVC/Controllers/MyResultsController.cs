using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaceDayMVC.Services;

namespace RaceDayMVC.Controllers
{
    public class MyResultsController : Controller
    {
        private readonly ApiClient _apiClient;

        public MyResultsController(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // GET: /MyResults
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
                    "api/Results/mine");

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.ResultsError =
                        "Unable to load your results. Please try again.";

                    return View(new List<JsonElement>());
                }

                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument document = JsonDocument.Parse(json);

                var results = document.RootElement
                    .EnumerateArray()
                    .Select(item => item.Clone())
                    .ToList();

                return View(results);
            }
            catch
            {
                ViewBag.ResultsError =
                    "Unable to connect to RaceDay to load your results.";

                return View(new List<JsonElement>());
            }
        }
    }
}