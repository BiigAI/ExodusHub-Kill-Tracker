using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;


namespace ExodusHub_Kill_Tracker
{
    internal class HTTPClient
    {
        private readonly HttpClient _client;
        private string _apiUrl;

        public HTTPClient(string apiUrl = "http://localhost:3000/api/kills")
        {
            _client = new HttpClient();
            _apiUrl = apiUrl;

            // Ensure the URL ends with /kills to match our API structure
            if (!_apiUrl.EndsWith("/kills"))
            {
                _apiUrl = _apiUrl.TrimEnd('/') + "/kills";
            }
        }

        public void SetApiUrl(string apiUrl)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new ArgumentException("API URL cannot be empty", nameof(apiUrl));

            _apiUrl = apiUrl;
        }

        public async Task<bool> SendKillDataAsync(KillData killData)
        {
            try
            {
                // Serialize the kill data to JSON
                string json = JsonSerializer.Serialize(killData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Create the HTTP content
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Send the POST request to the API
                var response = await _client.PostAsync(_apiUrl, content);

                // Return success status
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Helper method to log additional details when needed
        public async Task<(bool Success, string Message)> SendKillDataWithDetailsAsync(KillData killData)
        {
            try
            {
                // Serialize the kill data to JSON
                string json = JsonSerializer.Serialize(killData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Create the HTTP content
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Send the POST request to the API
                var response = await _client.PostAsync(_apiUrl, content);

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
    }


}