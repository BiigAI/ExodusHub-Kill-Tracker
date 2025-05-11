using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ExodusHub_Kill_Tracker;
// Add for JSON config reading
using System.Text.Json;

namespace ExodusHub_Kill_Tracker
{
    class Program
    {
        static bool keepRunning = true;
        static HTTPClient httpClient;

        static async Task Main(string[] args)
        {
            Console.WriteLine("ExodusHub Kill Tracker");
            Console.WriteLine("=-=-=-=-=-=-=-=-=-=-=-=");

            // Try to load saved user settings
            var userSettings = UserSettings.Load();
            string logFilePath = userSettings?.GameLogPath;
            string username = userSettings?.Username;

            // Step 1: Get the path to the game.log file
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                Console.Write("Enter the path to your game.log file: ");
                logFilePath = Console.ReadLine();

                // Validate file exists
                while (!File.Exists(logFilePath))
                {
                    Console.WriteLine("File not found. Please enter a valid path:");
                    logFilePath = Console.ReadLine();
                }
            }
            else
            {
                // Validate saved file path still exists
                if (!File.Exists(logFilePath))
                {
                    Console.WriteLine($"Saved game log path not found: {logFilePath}");
                    Console.Write("Enter the path to your game.log file: ");
                    logFilePath = Console.ReadLine();

                    // Validate file exists
                    while (!File.Exists(logFilePath))
                    {
                        Console.WriteLine("File not found. Please enter a valid path:");
                        logFilePath = Console.ReadLine();
                    }
                }
                else
                {
                    Console.WriteLine($"Saved game log path: {logFilePath}");
                    Console.Write("Press Enter to use this path, or type a new one: ");
                    string newPath = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(newPath))
                    {
                        logFilePath = newPath;
                        // Validate file exists
                        while (!File.Exists(logFilePath))
                        {
                            Console.WriteLine("File not found. Please enter a valid path:");
                            logFilePath = Console.ReadLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Using saved game log path: {logFilePath}");
                    }
                }
            }

            // Step 2: Get the username
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.Write("Enter your username: ");
                username = Console.ReadLine();
            }
            else
            {
                Console.WriteLine($"Using saved username: {username}");
                Console.Write("Press Enter to start tracking with this username, or type a new one: ");
                string newUsername = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(newUsername))
                {
                    username = newUsername;
                }
            }

            // Save user settings for next time
            UserSettings.Save(logFilePath, username);

            // Step 3: Get the API URL from config file
            string apiUrl = LoadApiUrlFromConfig();
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                Console.WriteLine("API URL not found in config file. Please ensure appsettings.json exists and contains an 'ApiUrl' property.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Initialize the HTTP client
            httpClient = new HTTPClient();
            httpClient.SetApiUrl(apiUrl);

            Console.WriteLine($"\nTracking kills for user: {username}");
            Console.WriteLine("Monitoring log file in real-time... Press Ctrl+C to exit.\n");

            // Set up console cancellation handling
            Console.CancelKeyPress += (sender, e) => {
                e.Cancel = true;
                keepRunning = false;
                Console.WriteLine("\nStopping log monitor...");
            };

            try
            {
                // Monitor the log file in real-time
                await MonitorLogFileAsync(logFilePath, username);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error monitoring log file: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        // Helper to load API URL from appsettings.json
        static string LoadApiUrlFromConfig()
        {
            try
            {
                string configPath = "appsettings.json";
                if (!File.Exists(configPath))
                    return null;

                string json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ApiUrl", out var apiUrlElement))
                {
                    return apiUrlElement.GetString();
                }
            }
            catch
            {
                // Ignore and return null
            }
            return null;
        }

        static async Task MonitorLogFileAsync(string filePath, string username)
        {
            // Updated regex pattern with correct group order and named groups
            string pattern = @"<(?<timestamp>[\d\-T:.Z]+)> \[Notice\] <Actor Death> CActor::Kill: '(?<victim>[^']+)' \[\d+\] in zone '(?<zone>[^']+)' killed by '(?<killer>[^']+)' \[\d+\] using '(?<weapon>[^']+)' \[Class unknown\] with damage type '(?<damageType>[^']+)' from direction";
            Regex regex = new Regex(pattern);

            int killCount = 0;
            //long lastPosition = LoadLastPosition();
            long lastPosition;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(fs))
            {
                // Ensure lastPosition is not beyond the end of the file
                //if (lastPosition > fs.Length)
                //{
                //    lastPosition = fs.Length;
                //}
                lastPosition=fs.Length;

                // Seek to the last processed position
                fs.Seek(lastPosition, SeekOrigin.Begin);

                while (keepRunning)
                {
                    string line;
                    bool foundLine = false;
                    while ((line = reader.ReadLine()) != null)
                    {
                        killCount = await ProcessKillLogLineAsync(line, regex, username, killCount);
                        foundLine = true;
                    }

                    lastPosition = fs.Position;
                    //SaveLastPosition(lastPosition);

                    if (!foundLine)
                    {
                        await Task.Delay(500);
                    }
                }
            }

            Console.WriteLine($"\nTotal kills for {username}: {killCount}");
        }

        //// Helper to save last processed file position
        //static void SaveLastPosition(long position)
        //{
        //    File.WriteAllText("last_position.txt", position.ToString());
        //}

        //// Helper to load last processed file position
        //static long LoadLastPosition()
        //{
        //    if (File.Exists("last_position.txt"))
        //    {
        //        var text = File.ReadAllText("last_position.txt");
        //        if (long.TryParse(text, out long pos))
        //            return pos;
        //    }
        //    return 0;
        //}

        // Process a single log line and return updated killCount
        static async Task<int> ProcessKillLogLineAsync(string line, Regex regex, string username, int killCount)
        {
            // Check for kill-related content before attempting regex match
            if (!line.Contains("<Actor Death>") || !line.Contains("killed by"))
            {
                return killCount;
            }

            // Log potential kill lines for debugging
            //Console.WriteLine("Potential kill line found: " + line);

            // Try the primary regex first
            Match match = regex.Match(line);

            if (match.Success)
            {
                // Use named groups for clarity and correctness
                string timestamp = match.Groups["timestamp"].Value;
                string victim = match.Groups["victim"].Value;
                string zone = match.Groups["zone"].Value;
                string killer = match.Groups["killer"].Value;
                string weapon = match.Groups["weapon"].Value;
                string damageType = match.Groups["damageType"].Value;

                //Console.WriteLine($"Match found - Killer: {killer}, Victim: {victim}");

                if (killer.Equals(username, StringComparison.OrdinalIgnoreCase))
                {
                    killCount++;

                    // Remove trailing ID from victim, weapon, and zone
                    string simplifiedVictim = victim;
                    int lastUnderscorePos = victim.LastIndexOf('_');
                    if (lastUnderscorePos > 0)
                    {
                        simplifiedVictim = victim.Substring(0, lastUnderscorePos);
                    }

                    string simplifiedWeapon = weapon;
                    lastUnderscorePos = weapon.LastIndexOf('_');
                    if (lastUnderscorePos > 0)
                    {
                        simplifiedWeapon = weapon.Substring(0, lastUnderscorePos);
                    }

                    string simplifiedZone = zone;
                    lastUnderscorePos = zone.LastIndexOf('_');
                    if (lastUnderscorePos > 0)
                    {
                        simplifiedZone = zone.Substring(0, lastUnderscorePos);
                    }

                    DateTime parsedTime = DateTime.Parse(timestamp, null, DateTimeStyles.RoundtripKind);
                    string formattedTime = parsedTime.ToString("HH:mm:ss");

                    Console.WriteLine($"\n[{formattedTime}] | {killer} killed {simplifiedVictim} with {simplifiedWeapon} at location {simplifiedZone}. The kill was a {damageType}");

                    var killData = new KillData
                    {
                        Timestamp = parsedTime,
                        Killer = killer ?? throw new ArgumentNullException(nameof(killer)),
                        Victim = simplifiedVictim ?? throw new ArgumentNullException(nameof(simplifiedVictim)),
                        Weapon = simplifiedWeapon ?? throw new ArgumentNullException(nameof(simplifiedWeapon)),
                        Location = simplifiedZone ?? throw new ArgumentNullException(nameof(simplifiedZone)),
                        KillType = damageType ?? throw new ArgumentNullException(nameof(damageType))
                    };

                    var (success, message) = await httpClient.SendKillDataWithDetailsAsync(killData);
                    if (success)
                    {
                        Console.WriteLine("✓ Kill data sent to server successfully");
                    }
                    else
                    {
                        Console.WriteLine($"× Failed to send kill data to server: {message}");
                    }
                }
            }
            return killCount;
        }
    }
}