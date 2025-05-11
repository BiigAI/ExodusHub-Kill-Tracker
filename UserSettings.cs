using System;
using System.IO;
using System.Text.Json;

namespace ExodusHub_Kill_Tracker
{
    public class UserSettings
    {
        public string GameLogPath { get; set; }
        public string Username { get; set; }

        public static UserSettings Load()
        {
            try
            {
                string configPath = "appsettings.json";
                if (!File.Exists(configPath))
                    return null;

                string json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("UserSettings", out var userSettingsElement))
                {
                    return new UserSettings
                    {
                        GameLogPath = userSettingsElement.TryGetProperty("GameLogPath", out var pathElement) ? 
                            pathElement.GetString() : null,
                        Username = userSettingsElement.TryGetProperty("Username", out var usernameElement) ? 
                            usernameElement.GetString() : null
                    };
                }
            }
            catch (Exception)
            {
                // Ignore and return null on any errors
            }
            return null;
        }

        public static void Save(string gameLogPath, string username)
        {
            try
            {
                string configPath = "appsettings.json";
                string json = "{}";
                
                if (File.Exists(configPath))
                {
                    json = File.ReadAllText(configPath);
                }

                using var jsonDoc = JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                // Create a new JSON object with existing properties
                var newJson = new System.Text.Json.Nodes.JsonObject();
                
                // Copy all existing properties
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "UserSettings")
                    {
                        newJson.Add(property.Name, System.Text.Json.Nodes.JsonNode.Parse(property.Value.GetRawText()));
                    }
                }

                // Add or update UserSettings
                var userSettings = new System.Text.Json.Nodes.JsonObject
                {
                    ["GameLogPath"] = gameLogPath,
                    ["Username"] = username
                };
                
                newJson.Add("UserSettings", userSettings);
                
                // Write back to file
                File.WriteAllText(configPath, newJson.ToJsonString(options));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving user settings: {ex.Message}");
            }
        }
    }
}