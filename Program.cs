using System;
using System.Threading;
using BeeSwarmGame; // Ensure this brings in BeeSwarmGame.Visualizer

public static class Program
{
    [STAThread] // Required for some platforms
    public static void Main(string[] args)
    {
        // Basic argument parsing (remains the same)
        int width = 800;
        int height = 600;
        int numSwarms = 2;
        int numResources = 20;
        bool debugMode = false;
        bool interactiveMode = false;  // New flag
        bool dataVisualizerEnabled = false; // New flag for data visualization

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--width":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int w)) width = w;
                    break;
                case "--height":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int h)) height = h;
                    break;
                case "--swarms":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int s)) numSwarms = s;
                    break;
                case "--resources":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int r)) numResources = r;
                    break;
                case "--debug":
                    debugMode = true;
                    break;
                case "--interactive":
                    interactiveMode = true;
                    break;
                case "--datavisualizer":
                case "--datavis":
                    dataVisualizerEnabled = true;
                    break;
            }
        }

        Console.WriteLine("Starting Bee Swarm Simulation (MonoGame)");
        Console.WriteLine($"Settings: Width={width}, Height={height}, Swarms={numSwarms}, Resources={numResources}, Debug={debugMode}, Interactive={interactiveMode}, DataVisualizer={dataVisualizerEnabled}"); // Log visualizer flag

        try
        {
            // Create and run the MonoGame Game instance, passing the visualizer flag
            using var game = new Game(width, height, numSwarms, numResources, debugMode, dataVisualizerEnabled); // Pass flag here

            // If interactive mode is enabled, start an extra thread to handle user commands
            if (interactiveMode)
            {
                Thread interactiveThread = new Thread(() =>
                {
                    Console.WriteLine("Interactive mode active. Type 'roles' to list unit roles or 'exit' to quit.");
                    // Use the base class IsActive property here
                    while (game.IsActive)
                    {
                        string? command = Console.ReadLine(); // Fix nullable warning
                        if (command == null)
                            continue;
                        command = command.Trim().ToLower();
                        if (command == "roles")
                        {
                            game.PrintRoles();
                        }
                        else if (command == "exit")
                        {
                            Console.WriteLine("Exiting simulation by user request.");
                            game.Exit();
                            break;
                        }
                        else
                        {
                            Console.WriteLine("Unknown command. Available: roles, exit");
                        }
                    }
                })
                {
                    IsBackground = true
                };
                interactiveThread.Start();
            }

            // Remove visualizer thread creation from here

            Console.WriteLine("Calling game.Run()..."); // Log before running game
            game.Run(); // This starts the MonoGame loop, which calls Initialize()
            Console.WriteLine("game.Run() finished."); // Log after game finishes
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n--- UNHANDLED EXCEPTION ---");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            Console.ResetColor();
        }
        finally
        {
             Console.WriteLine("\nSimulation finished.");
        }
    }
}
