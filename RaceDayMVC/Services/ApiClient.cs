
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RaceDayMVC.Services
{
    public class ApiClient
    {
        private const string ApiCookieKey = "RaceDayApiCookie";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiClient(
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string endpoint,
            object? body = null)
        {
            HttpContent? content = body != null
                ? JsonContent.Create(body)
                : null;

            return SendInternalAsync(method, endpoint, content);
        }

        public async Task<HttpResponseMessage> SendFileAsync(
            HttpMethod method,
            string endpoint,
            IFormFile file)
        {
            using var fileStream = file.OpenReadStream();
            using var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType);

            using var formContent = new MultipartFormDataContent();

            formContent.Add(fileContent, "file", file.FileName);

            return await SendInternalAsync(
                method, endpoint, formContent);
        }

        private async Task<HttpResponseMessage> SendInternalAsync(
            HttpMethod method,
            string endpoint,
            HttpContent? content)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "No active HTTP request is available.");

            var client = _httpClientFactory.CreateClient("RaceDayApi");

            using var request = new HttpRequestMessage(method, endpoint);

            // Forward the authenticated RaceDay API session.
            var apiCookie =
                httpContext.Session.GetString(ApiCookieKey);

            if (!string.IsNullOrWhiteSpace(apiCookie))
            {
                request.Headers.TryAddWithoutValidation(
                    "Cookie", apiCookie);
            }

            if (content != null)
                request.Content = content;

            var response = await client.SendAsync(request);

            // Capture the API session cookie returned by the API.
            if (response.Headers.TryGetValues(
                "Set-Cookie", out var cookies))
            {
                var sessionCookie = cookies
                    .Select(cookie => cookie.Split(';')[0])
                    .FirstOrDefault(cookie =>
                        cookie.StartsWith(
                            ".AspNetCore.Session=",
                            StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(sessionCookie))
                {
                    httpContext.Session.SetString(
                        ApiCookieKey, sessionCookie);
                }
            }

            return response;
        }

        public void ClearApiSession()
        {
            _httpContextAccessor.HttpContext?
                .Session.Remove(ApiCookieKey);
        }
    }
}
