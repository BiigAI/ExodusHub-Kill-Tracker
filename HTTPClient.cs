using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;


namespace ExodusHub_Kill_Tracker
{
    internal class HTTPClient
    {
        private readonly HttpClient _client;
        private string _apiBaseUrl; // Store the base API URL

        //public HTTPClient(string apiUrl = "http://localhost:3000/api")
        public HTTPClient(string apiUrl = "https://sc.exoduspmc.org/api")
        {
            _client = new HttpClient();
            _apiBaseUrl = apiUrl.TrimEnd('/'); // Don't append /kills here
        }

        public void SetApiUrl(string apiUrl)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new ArgumentException("API URL cannot be empty", nameof(apiUrl));

            _apiBaseUrl = apiUrl.TrimEnd('/');
        }

        public async Task<bool> SendKillDataAsync(KillData killData)
        {
            try
            {
                string json = JsonSerializer.Serialize(killData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Send to /kills endpoint
                var response = await _client.PostAsync(_apiBaseUrl + "/kills", content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<(bool Success, string Message)> SendKillDataWithDetailsAsync(KillData killData)
        {
            try
            {
                string json = JsonSerializer.Serialize(killData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Send to /kills endpoint
                var response = await _client.PostAsync(_apiBaseUrl + "/kills", content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Kill data sent successfully");
                }
                else
                {
                    try
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var jsonContent = System.Text.Json.JsonDocument.Parse(responseContent);
                        if (jsonContent.RootElement.TryGetProperty("message", out var messageElement))
                        {
                            return (false, messageElement.GetString());
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors, fall back to status code
                    }
                    return (false, $"Server returned status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error sending data: {ex.Message}");
            }
        }

        // Verifies connection to the API by sending a simple GET request
        public async Task<(bool Success, string Message)> VerifyApiConnectionAsync()
        {
            try
            {
                // Use the base API URL for a health check
                var response = await _client.GetAsync(_apiBaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Successfully connected to the server API.");
                }
                else
                {
                    return (false, $"Server responded with status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error connecting to server API: {ex.Message}");
            }
        }
    }
}