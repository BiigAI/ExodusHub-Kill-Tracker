using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ExodusHub_Kill_Tracker;
// Add for JSON config reading
using System.Text.Json;
// Add for process checking
using System.Diagnostics;

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

            // --- Check if starcitizen.exe is running ---
            var scProcesses = Process.GetProcessesByName("starcitizen");
            if (scProcesses == null || scProcesses.Length == 0)
            {
                SetConsoleColor(ConsoleColor.Red);
                Console.WriteLine("Warning: 'starcitizen.exe' must be running before launching the kill tracker.");
                ResetConsoleColor();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Try to load saved user settings
            var userSettings = UserSettings.Load();
            string logFilePath = userSettings?.GameLogPath;
            string username = userSettings?.Username;
            string authToken = userSettings?.AuthToken;

            // Step 1: Get the path to the game.log file
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                Console.Write("Enter the path to your game.log file: ");
                logFilePath = Console.ReadLine();

                // Validate file exists
                while (!File.Exists(logFilePath))
                {
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("File not found. Please enter a valid path:");
                    ResetConsoleColor();
                    logFilePath = Console.ReadLine();
                }
            }
            else
            {
                // Validate saved file path still exists
                if (!File.Exists(logFilePath))
                {
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine($"Saved game log path not found: {logFilePath}");
                    ResetConsoleColor();
                    Console.Write("Enter the path to your game.log file: ");
                    logFilePath = Console.ReadLine();

                    // Validate file exists
                    while (!File.Exists(logFilePath))
                    {
                        SetConsoleColor(ConsoleColor.Red);
                        Console.WriteLine("File not found. Please enter a valid path:");
                        ResetConsoleColor();
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
                            SetConsoleColor(ConsoleColor.Red);
                            Console.WriteLine("File not found. Please enter a valid path:");
                            ResetConsoleColor();
                            logFilePath = Console.ReadLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Using saved game log path: {logFilePath}");
                    }
                }
            }

            // --- Verification logic for Game.log file accessibility ---
            bool logFileVerified = false;
            while (!logFileVerified)
            {
                try
                {
                    using (FileStream fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        // If we can open the file for reading, consider it verified
                        logFileVerified = true;
                    }
                }
                catch (Exception ex)
                {
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine($"Error accessing log file: {ex.Message}");
                    ResetConsoleColor();
                    Console.Write("Please enter a valid path to your game.log file: ");
                    logFilePath = Console.ReadLine();
                    while (!File.Exists(logFilePath))
                    {
                        SetConsoleColor(ConsoleColor.Red);
                        Console.WriteLine("File not found. Please enter a valid path:");
                        ResetConsoleColor();
                        logFilePath = Console.ReadLine();
                    }
                }
            }
            SetConsoleColor(ConsoleColor.Green);
            Console.WriteLine($"Game.log file found and being actively tracked in real time: {logFilePath}");
            ResetConsoleColor();

            // --- Show the most recent log line for context (truncated for brevity) ---
            try
            {
                string lastLine = null;
                // Use FileStream with FileShare.ReadWrite to avoid file lock issues
                using (FileStream fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fs))
                {
                    string line;
                    // Read through the file to get the last non-empty line
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lastLine = line.Trim();
                        }
                    }
                }
                if (!string.IsNullOrEmpty(lastLine))
                {
                    // Truncate after 6 words for brevity
                    var words = lastLine.Split(' ');
                    string preview = string.Join(' ', words.Length > 6 ? words[..6] : words);
                    if (words.Length > 6) preview += " ...";
                    Console.WriteLine($"Most recent log line: {preview}");
                }
                else
                {
                    Console.WriteLine("Most recent log line: (log file is empty)");
                }
            }
            catch (Exception ex)
            {
                SetConsoleColor(ConsoleColor.Red);
                Console.WriteLine($"Could not read last log line: {ex.Message}");
                ResetConsoleColor();
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

            // Step 3: Get the authentication token
            if (string.IsNullOrWhiteSpace(authToken))
            {
                SetConsoleColor(ConsoleColor.Yellow);
                Console.WriteLine("\n--- Exodus Kill Tracker Authentication Token Required ---");
                Console.WriteLine("To get your authentication token, log in to the Exodus Hub website.");
                Console.WriteLine("Navigate to the Kill Tracker section and copy your token.");
                Console.WriteLine("Paste the token below and save settings.");
                Console.WriteLine("Tokens are valid for 60 days and can only be used on one client at a time.");
                ResetConsoleColor();
                Console.Write("Enter your authentication token: ");
                authToken = Console.ReadLine();
                while (string.IsNullOrWhiteSpace(authToken))
                {
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("Token cannot be empty. Please enter your authentication token:");
                    ResetConsoleColor();
                    authToken = Console.ReadLine();
                }
            }
            else
            {
                Console.WriteLine($"Using saved authentication token: {authToken.Substring(0, Math.Min(8, authToken.Length))}...");
                Console.Write("Press Enter to use this token, or type a new one: ");
                string newToken = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(newToken))
                {
                    authToken = newToken;
                }
            }

            // Save user settings for next time (now includes token)
            UserSettings.Save(logFilePath, username, authToken);

            // Initialize the HTTP client with token
            httpClient = new HTTPClient(authToken: authToken);

            // --- Verify connection to the server API before proceeding ---
            var (apiConnected, apiMessage) = await httpClient.VerifyApiConnectionAsync();
            if (apiConnected)
            {
                SetConsoleColor(ConsoleColor.Green);
                Console.WriteLine($"[API] {apiMessage}");
                ResetConsoleColor();
            }
            else
            {
                SetConsoleColor(ConsoleColor.Red);
                Console.WriteLine($"[API] {apiMessage}");
                Console.WriteLine("Unable to connect to the server API. Please check your internet connection or try again later.");
                ResetConsoleColor();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

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


        // Process a single log line and return updated killCount
        static async Task<int> ProcessKillLogLineAsync(string line, Regex regex, string username, int killCount)
        {
            // Check for kill-related content before attempting regex match
            if (!line.Contains("<Actor Death>") || !line.Contains("killed by"))
            {
                return killCount;
            }

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
                        SetConsoleColor(ConsoleColor.Green);
                        Console.WriteLine("✓ Kill data sent to server successfully");
                        ResetConsoleColor();
                    }
                    else
                    {
                        SetConsoleColor(ConsoleColor.Red);
                        Console.WriteLine($"× Kill data was not registered on the server - Reason: {message}");
                        ResetConsoleColor();
                    }
                }
            }
            return killCount;
        }

        // --- Helper methods for colored output ---
        static void SetConsoleColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }

        static void ResetConsoleColor()
        {
            Console.ResetColor();
        }
    }
}