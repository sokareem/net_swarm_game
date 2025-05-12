using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // Add for Thread
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input; // For keyboard input
// Add alias for clarity if needed, though explicit conversion methods are preferred now
// using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace BeeSwarmGame;

// Inherit from MonoGame's Game class
public class Game : Microsoft.Xna.Framework.Game
{
    // Keep original properties
    public int SimWidth { get; }
    public int SimHeight { get; }
    public int NumSwarms { get; }
    public int NumResources { get; }
    // public bool IsRunning { get; private set; } // Managed by MonoGame loop
    public bool IsPaused { get; private set; }
    // public int TickRate { get; } = 60; // Managed by MonoGame's FixedTimeStep
    public Environment Environment { get; private set; } = null!; // Initialized in Initialize
    public string? Winner { get; private set; }
    public bool DebugMode { get; }
    private readonly bool _dataVisualizerEnabled; // Store the flag

    // MonoGame specific members
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!; // For drawing text
    private Texture2D _pixelTexture = null!; // For drawing shapes
    private Texture2D _circleTexture = null!; // Pre-rendered circle

    // Store previous keyboard state for edge detection (press/release)
    private KeyboardState _previousKeyboardState;

    // Map swarm IDs to MonoGame Colors
    private readonly Dictionary<string, Color> _swarmMonoGameColors = new();
    private static readonly Color[] DefaultSwarmColors = {
        Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Magenta, Color.Cyan
    };

    // Constructor updated for MonoGame and visualizer flag
    public Game(int width = 800, int height = 600, int numSwarms = 2, int numResources = 20, bool debugMode = false, bool dataVisualizerEnabled = false) // Add parameter
    {
        SimWidth = width;
        SimHeight = height;
        NumSwarms = numSwarms;
        NumResources = numResources;
        DebugMode = debugMode;
        _dataVisualizerEnabled = dataVisualizerEnabled; // Store the flag

        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content"; // Default content directory
        IsMouseVisible = true;

        // Set window size
        _graphics.PreferredBackBufferWidth = SimWidth;
        _graphics.PreferredBackBufferHeight = SimHeight;

        // Optional: Use fixed time step for predictable simulation updates
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0); // Target 60 FPS/UPS

        Console.WriteLine($"Game Initialized (MonoGame): {SimWidth}x{SimHeight}, {NumSwarms} Swarms, {NumResources} Resources, Visualizer={_dataVisualizerEnabled}");
    }

    /// <summary>
    /// Allows the game to perform any initialization it needs to before starting to run.
    /// This is where it can query for any required services and load any non-graphic
    /// related content. Calling base.Initialize will enumerate through any components
    /// and initialize them as well.
    /// </summary>
    protected override void Initialize()
    {
        Console.WriteLine("Game.Initialize() Start...");
        InitializeEnvironment(); // Call the simulation setup logic
        _previousKeyboardState = Keyboard.GetState(); // Initialize keyboard state

        Console.WriteLine("Calling base.Initialize()...");
        base.Initialize(); // Important: Initialize MonoGame systems
        Console.WriteLine("base.Initialize() Complete.");

        // Start the visualizer thread *after* the environment and base game are initialized
        if (_dataVisualizerEnabled)
        {
            Console.WriteLine("Data Visualizer enabled. Starting visualizer thread...");
            if (Environment == null)
            {
                 Console.WriteLine("ERROR: Environment is null when trying to start visualizer!");
            }
            else
            {
                // Capture the environment in a local variable for the thread
                var environmentForThread = this.Environment;
                Thread dataVizThread = new Thread(() =>
                {
                    try
                    {
                        Console.WriteLine("Visualizer Thread: Starting Visualizer.Start()...");
                        BeeSwarmGame.Visualizer.Start(environmentForThread);
                        Console.WriteLine("Visualizer Thread: Visualizer.Start() returned."); // Should not happen if Run() blocks

                        // Keep the thread alive while the visualizer window is running (Visualizer manages its own loop)
                        // This part might not be strictly necessary if Visualizer.Start blocks until its window closes.
                        while (BeeSwarmGame.Visualizer.IsRunning) // Add an IsRunning property to Visualizer
                        {
                            Thread.Sleep(2000);
                        }
                        Console.WriteLine("Visualizer Thread: Exiting.");
                    }
                    catch (Exception ex)
                    {
                         Console.ForegroundColor = ConsoleColor.Red;
                         Console.WriteLine($"\n--- VISUALIZER THREAD EXCEPTION ---");
                         Console.WriteLine($"Error: {ex.Message}");
                         Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
                         Console.ResetColor();
                    }
                })
                {
                    IsBackground = true,
                    Name = "DataVisualizerThread" // Give the thread a name for debugging
                };
                dataVizThread.Start();
            }
        }

        Console.WriteLine("Game.Initialize() Complete.");
    }

    /// <summary>
    /// LoadContent will be called once per game and is the place to load
    /// all of your content.
    /// </summary>
    protected override void LoadContent()
    {
        Console.WriteLine("MonoGame LoadContent...");
        // Create a new SpriteBatch, which can be used to draw textures.
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Load a font (requires a .spritefont file processed by MGCB)
        // If you don't have MGCB set up, you might need to skip font loading or use a different method.
        try
        {
             // Assumes a "DefaultFont.spritefont" exists in the Content folder and is processed by MGCB
             _font = Content.Load<SpriteFont>("DefaultFont");
        }
        catch (Exception ex)
        {
             Console.WriteLine($"WARNING: Could not load font. UI text will not be drawn. Error: {ex.Message}");
             // Create a dummy font or handle the absence gracefully
             // For now, we'll proceed without a font if loading fails.
             _font = null!; // Explicitly set to null if loading fails
        }


        // Create a 1x1 white pixel texture for drawing shapes
        _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // Create a simple circle texture (e.g., 32x32)
        _circleTexture = CreateCircleTexture(GraphicsDevice, 32, Color.White);
        Console.WriteLine("MonoGame LoadContent Complete.");
    }

    /// <summary>
    /// Allows the game to run logic such as updating the world,
    /// checking for collisions, gathering input, and playing audio.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    protected override void Update(GameTime gameTime)
    {
        KeyboardState currentKeyboardState = Keyboard.GetState();

        // Handle Global Input (Exit, Pause, Reset)
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || currentKeyboardState.IsKeyDown(Keys.Escape))
            Exit();

        // Toggle Pause (only trigger on key press, not hold)
        if (currentKeyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space))
        {
            IsPaused = !IsPaused;
            Console.WriteLine(IsPaused ? "Game Paused." : "Game Resumed.");
        }

        // Reset Game (only trigger on key press)
        if (currentKeyboardState.IsKeyDown(Keys.R) && _previousKeyboardState.IsKeyUp(Keys.R))
        {
            Console.WriteLine("Resetting game...");
            InitializeEnvironment(); // Re-initialize simulation
            // Reset winner state etc. if needed
            Winner = null;
            IsPaused = false;
        }

        // Update simulation only if not paused and no winner
        if (!IsPaused && Winner == null)
        {
            try
            {
                Environment.Update(); // Update the simulation state
                Winner = Environment.CheckVictory(); // Check for winner after update
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR during simulation update: {ex.Message}\n{ex.StackTrace}");
                // Optionally pause the game on error
                // IsPaused = true;
            }
        }

        _previousKeyboardState = currentKeyboardState; // Store state for next frame
        base.Update(gameTime);
    }

    /// <summary>
    /// This is called when the game should draw itself.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue); // Default background

        _spriteBatch.Begin();

        // --- Draw Simulation Elements ---
        if (Environment != null)
        {
            // Draw Obstacles
            foreach (var (pos, size) in Environment.Obstacles)
            {
                // Use ToXnaVector2 for position and size if they were custom Vector2
                // Assuming Environment.Obstacles uses BeeSwarmGame.Vector2
                DrawRectangle(_spriteBatch, new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y), Color.Gray);
            }

            // Draw Resource Nodes
            foreach (var node in Environment.ResourceNodes)
            {
                float depletion = (float)node.GetDepletionPercentage() / 100f;
                Color resourceColor = Color.Lerp(Color.Yellow, Color.DarkGoldenrod, depletion);
                // Cast result of Math.Max to float
                float size = (float)Math.Max(3f, 10f * (1f - depletion));
                // Use conversion method for position
                DrawCircle(_spriteBatch, (Microsoft.Xna.Framework.Vector2)node.Position.ToXnaVector2(), size, resourceColor);
            }

            // Draw Swarms (Bases and Units)
            foreach (var swarm in Environment.Swarms)
            {
                Color swarmColor = _swarmMonoGameColors.GetValueOrDefault(swarm.Id, Color.White);

                // Draw Base
                float baseSize = 10f; // Simple fixed size for now
                // Use conversion method for position
                DrawRectangle(_spriteBatch, new Rectangle(
                    (int)(swarm.BasePosition.X - baseSize), (int)(swarm.BasePosition.Y - baseSize),
                    (int)(baseSize * 2), (int)(baseSize * 2)),
                    swarmColor);

                // Draw Units with role-specific shapes
                foreach (var unit in swarm.Units)
                {
                    float unitSize = 5f;
                    Color unitDrawColor = swarmColor;
                    var unitPos = (Microsoft.Xna.Framework.Vector2)unit.Position.ToXnaVector2();
                    switch (unit.Role)
                    {
                        case "gatherer":
                            DrawSquare(_spriteBatch, unitPos, unitSize, unitDrawColor);
                            break;
                        case "combat":
                            DrawTriangle(_spriteBatch, unitPos, unitSize, unitDrawColor);
                            break;
                        case "scout":
                            DrawDiamond(_spriteBatch, unitPos, unitSize, unitDrawColor);
                            break;
                        default: // basic
                            DrawCircle(_spriteBatch, unitPos, unitSize, unitDrawColor);
                            break;
                    }
                    // Indicate carrying resources
                    if (unit.CurrentResources > 0)
                    {
                        DrawCircleOutline(_spriteBatch, unitPos, unitSize + 1, Color.Yellow, 1f);
                    }
                }
            }
        }

        // --- Draw UI Elements ---
        if (_font != null) // Only draw text if font loaded successfully
        {
            float yOffset = 10f;
            if (Environment != null)
            {
                foreach (var swarm in Environment.Swarms)
                {
                    Color swarmColor = _swarmMonoGameColors.GetValueOrDefault(swarm.Id, Color.White);
                    var roles = swarm.Units.GroupBy(u => u.Role)
                                           .ToDictionary(g => g.Key, g => g.Count());
                    string roleString = $"B:{roles.GetValueOrDefault("basic", 0)} " +
                                        $"G:{roles.GetValueOrDefault("gatherer", 0)} " +
                                        $"C:{roles.GetValueOrDefault("combat", 0)} " +
                                        $"S:{roles.GetValueOrDefault("scout", 0)}";

                    string swarmText = $"{swarm.Id}: {swarm.Units.Count} U, {swarm.TotalResources:F0} R | {roleString}";
                    _spriteBatch.DrawString(_font, swarmText, new Microsoft.Xna.Framework.Vector2(10, yOffset), swarmColor);
                    yOffset += _font.LineSpacing;
                }
            }

            // --- Draw Global Simulation Stats ---
            if (Environment != null)
            {
                // Aggregate total resources collected from all units
                int totalResourcesCollected = Environment.Swarms.Sum(s => s.Units.Sum(u => (int)u.ResourcesCollected));
                var globalRoles = Environment.Swarms.SelectMany(s => s.Units)
                                      .GroupBy(u => u.Role)
                                      .ToDictionary(g => g.Key, g => g.Count());
                string roleDist = string.Join(" ", globalRoles.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                string globalStatsText = $"Global Resources Collected: {totalResourcesCollected}\nRole Distribution: {roleDist}";
                _spriteBatch.DrawString(_font, globalStatsText, new Microsoft.Xna.Framework.Vector2(10, yOffset), Color.White);
            }

            // Draw Pause/Winner message
            string statusText = "";
            if (Winner != null)
            {
                statusText = Winner == "draw" ? "GAME OVER: Draw!" : $"GAME OVER: {Winner} wins!";
            }
            else if (IsPaused)
            {
                statusText = "PAUSED (Space to Resume, R to Reset, Esc to Exit)";
            }
             else
            {
                 statusText = "(Space to Pause, R to Reset, Esc to Exit)";
            }

            if (!string.IsNullOrEmpty(statusText))
            {
                // Use MonoGame's Vector2 directly here
                Microsoft.Xna.Framework.Vector2 textSize = _font.MeasureString(statusText);
                Microsoft.Xna.Framework.Vector2 position = new Microsoft.Xna.Framework.Vector2((SimWidth - textSize.X) / 2, SimHeight - textSize.Y - 10);
                _spriteBatch.DrawString(_font, statusText, position, Color.White);
            }
        }


        _spriteBatch.End();

        base.Draw(gameTime);
    }

    // --- Helper Methods ---

    private void InitializeEnvironment()
    {
        Console.WriteLine("Initializing Simulation Environment...");
        Environment = new Environment(SimWidth, SimHeight); // Reset environment
        Winner = null;
        IsPaused = false;
        _swarmMonoGameColors.Clear(); // Clear previous color mapping

        // Define base positions (same logic as before)
        var swarmPositions = new List<Vector2>
        {
            new Vector2(50, 50),
            new Vector2(SimWidth - 50, SimHeight - 50),
            new Vector2(SimWidth - 50, 50),
            new Vector2(50, SimHeight - 50)
        };

        // Create initial resources
        CreateResourceGardens(Environment, swarmPositions);

        // Create obstacles
        int numObstacles = 3;
        var rng = new Random();
        for (int i = 0; i < numObstacles; i++)
        {
            double obsW = rng.Next(20, 81);
            double obsH = rng.Next(20, 81);
            double obsX = rng.NextDouble() * (SimWidth - obsW);
            double obsY = rng.NextDouble() * (SimHeight - obsH);
            Vector2 obsPos = new Vector2(obsX, obsY);
            Vector2 obsSize = new Vector2(obsW, obsH);

            bool tooCloseToBase = false;
            for(int j=0; j < NumSwarms; ++j)
            {
                Vector2 basePos = swarmPositions[j % swarmPositions.Count];
                Vector2 obsCenter = obsPos + obsSize * 0.5;
                 if (Vector2.Distance(obsCenter, basePos) < 50)
                 {
                     tooCloseToBase = true;
                     break;
                 }
            }

            if (!tooCloseToBase)
            {
                Environment.AddObstacle(obsPos, obsSize);
                 Console.WriteLine($"-- Added Obstacle at {obsPos} Size {obsSize}");
            }
        }

        // Create swarms
        for (int i = 0; i < NumSwarms; i++)
        {
            // Use default MonoGame colors
            Color color = DefaultSwarmColors[i % DefaultSwarmColors.Length];
            Vector2 position = swarmPositions[i % swarmPositions.Count];
            // Convert MonoGame color back to tuple if Swarm expects it, or update Swarm
            // For simplicity, let's assume Swarm can take the MonoGame Color or we adapt Swarm later
            // Swarm swarm = new Swarm($"Swarm-{i + 1}", (color.R, color.G, color.B), position);
            // Let's map ID to Color for drawing instead
             string swarmId = $"Swarm-{i + 1}";
             Swarm swarm = new Swarm(swarmId, (color.R, color.G, color.B), position); // Keep tuple for Swarm internal?
             _swarmMonoGameColors[swarmId] = color; // Store mapping for drawing


            int initialUnits = 5;
            for (int j = 0; j < initialUnits; j++)
            {
                string role = (j >= 3) ? "scout" : "basic";
                Unit? unit = swarm.SpawnUnit("basic", role);
                if (unit != null && role == "scout")
                {
                    unit.Target = swarm.FindExplorationTarget(Environment, unit);
                    unit.State = UnitState.SCOUTING;
                }
            }
            Environment.AddSwarm(swarm);
            Console.WriteLine($"-- Created {swarm.Id} with {swarm.Units.Count} units at {position}");
        }
         Console.WriteLine("Environment Initialized.");
    }

    // Resource garden creation logic (same as before)
    private void CreateResourceGardens(Environment environment, List<Vector2> swarmPositions)
    {
        int numGardens = NumSwarms + 2;
        int resourcesPerGarden = Math.Max(1, NumResources / numGardens);
        var rng = new Random();

        for (int i = 0; i < Math.Min(NumSwarms, swarmPositions.Count); i++)
        {
            Vector2 basePos = swarmPositions[i];
            double offsetX = (rng.NextDouble() * 50 + 50) * (basePos.X < SimWidth / 2 ? 1 : -1);
            double offsetY = (rng.NextDouble() * 50 + 50) * (basePos.Y < SimHeight / 2 ? 1 : -1);
            Vector2 gardenCenter = new Vector2(basePos.X + offsetX, basePos.Y + offsetY);
            SpawnNodesInGarden(environment, gardenCenter, resourcesPerGarden, rng);
        }

        for (int i = 0; i < numGardens - NumSwarms; i++)
        {
            Vector2 gardenCenter = new Vector2(
                rng.NextDouble() * (SimWidth - 200) + 100,
                rng.NextDouble() * (SimHeight - 200) + 100
            );
            SpawnNodesInGarden(environment, gardenCenter, resourcesPerGarden, rng);
        }
         Console.WriteLine($"-- Created {environment.ResourceNodes.Count} initial resource nodes.");
    }

    // Node spawning logic (same as before)
    private void SpawnNodesInGarden(Environment environment, Vector2 center, int count, Random rng)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = rng.NextDouble() * 2 * Math.PI;
            double distance = rng.NextDouble() * 35 + 5;
            Vector2 pos = new Vector2(
                center.X + Math.Cos(angle) * distance,
                center.Y + Math.Sin(angle) * distance
            );
            pos.X = Math.Clamp(pos.X, 10, SimWidth - 10);
            pos.Y = Math.Clamp(pos.Y, 10, SimHeight - 10);

            if (environment.IsPositionValid(pos, margin: 5))
            {
                int amount = rng.Next(500, 1501);
                environment.AddResourceNode(new ResourceNode(pos, amount));
            }
        }
    }

    // --- Drawing Helpers using _pixelTexture and _circleTexture ---

    private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        spriteBatch.Draw(_pixelTexture, rect, color);
    }

    // Update DrawLine to accept XnaVector2 and cast thickness
    private void DrawLine(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Vector2 point1, Microsoft.Xna.Framework.Vector2 point2, Color color, float thickness = 1f)
    {
        // Use MonoGame's Vector2 methods
        float distance = Microsoft.Xna.Framework.Vector2.Distance(point1, point2);
        float angle = (float)Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
        // Use MonoGame's Vector2.Zero and constructor
        spriteBatch.Draw(_pixelTexture, point1, null, color, angle, Microsoft.Xna.Framework.Vector2.Zero, new Microsoft.Xna.Framework.Vector2(distance, thickness), SpriteEffects.None, 0);
    }

     // Update DrawCircle to accept XnaVector2
     private void DrawCircle(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Vector2 center, float radius, Color color)
    {
        // Use MonoGame's Vector2 constructor for scale and origin
        Microsoft.Xna.Framework.Vector2 scale = new Microsoft.Xna.Framework.Vector2(radius * 2 / _circleTexture.Width); // Uniform scale
        Microsoft.Xna.Framework.Vector2 origin = new Microsoft.Xna.Framework.Vector2(_circleTexture.Width / 2f, _circleTexture.Height / 2f); // Center origin
        spriteBatch.Draw(_circleTexture, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

     // Update DrawCircleOutline to accept XnaVector2
     private void DrawCircleOutline(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Vector2 center, float radius, Color color, float thickness)
    {
        // ... existing outline logic ...
        // Example using DrawCircle:
        // DrawCircle(spriteBatch, center, radius + thickness, color * 0.5f);
        // DrawCircle(spriteBatch, center, radius, color);
        // Make sure DrawCircle is called with XnaVector2
    }

    private void DrawSquare(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Vector2 center, float size, Color color)
    {
        Rectangle rect = new Rectangle((int)(center.X - size), (int)(center.Y - size), (int)(2 * size), (int)(2 * size));
        spriteBatch.Draw(_pixelTexture, rect, color);
    }

    private void DrawTriangle(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Vector2 center, float size, Color color)
    {
        Microsoft.Xna.Framework.Vector2 v1 = new Microsoft.Xna.Framework.Vector2(center.X, center.Y - size);
        Microsoft.Xna.Framework.Vector2 v2 = new Microsoft.Xna.Framework.Vector2(center.X - size, center.Y + size);
        Microsoft.Xna.Framework.Vector2 v3 = new Microsoft.Xna.Framework.Vector2(center.X + size, center.Y + size);
        DrawLine(spriteBatch, v1, v2, color);
        DrawLine(spriteBatch, v2, v3, color);
        DrawLine(spriteBatch, v3, v1, color);
    }

    private void DrawDiamond(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Vector2 center, float size, Color color)
    {
        Microsoft.Xna.Framework.Vector2 v1 = new Microsoft.Xna.Framework.Vector2(center.X, center.Y - size);
        Microsoft.Xna.Framework.Vector2 v2 = new Microsoft.Xna.Framework.Vector2(center.X + size, center.Y);
        Microsoft.Xna.Framework.Vector2 v3 = new Microsoft.Xna.Framework.Vector2(center.X, center.Y + size);
        Microsoft.Xna.Framework.Vector2 v4 = new Microsoft.Xna.Framework.Vector2(center.X - size, center.Y);
        DrawLine(spriteBatch, v1, v2, color);
        DrawLine(spriteBatch, v2, v3, color);
        DrawLine(spriteBatch, v3, v4, color);
        DrawLine(spriteBatch, v4, v1, color);
    }

    // Helper to create a circle texture dynamically
    private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, int diameter, Color color)
    {
        Texture2D texture = new Texture2D(graphicsDevice, diameter, diameter);
        Color[] data = new Color[diameter * diameter];
        float radius = diameter / 2f;
        float radiusSq = radius * radius;
        // Use MonoGame's Vector2 here for calculations within the texture
        Microsoft.Xna.Framework.Vector2 center = new Microsoft.Xna.Framework.Vector2(radius); // Center of texture

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                // Use MonoGame's Vector2 for position within texture
                Microsoft.Xna.Framework.Vector2 pos = new Microsoft.Xna.Framework.Vector2(x, y);
                // Use MonoGame's DistanceSquared
                float distanceSq = Microsoft.Xna.Framework.Vector2.DistanceSquared(pos, center);
                if (distanceSq <= radiusSq)
                {
                    // Basic anti-aliasing: fade near the edge
                    float distFactor = (radiusSq - distanceSq) / 3.0f; // Adjust divisor for softness
                    float alpha = Math.Clamp(distFactor, 0f, 1f);
                    data[y * diameter + x] = color * alpha;
                }
                else
                {
                    data[y * diameter + x] = Color.Transparent;
                }
            }
        }
        texture.SetData(data);
        return texture;
    }

    // Removed old console Render, HandleInput, Run methods
    // ...

    // Method to print roles (needed by Program.cs interactive loop)
    public void PrintRoles()
    {
        if (Environment != null)
        {
            Console.WriteLine("Available Unit Roles:");
            // Assuming Unit class or similar has role definitions, or list known ones
            Console.WriteLine("- basic");
            Console.WriteLine("- gatherer");
            Console.WriteLine("- combat");
            Console.WriteLine("- scout");
            Console.WriteLine("- defender"); // If defender role exists
        }
        else
        {
            Console.WriteLine("Environment not initialized yet.");
        }
    }
}
