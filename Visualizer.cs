using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Windows; // Added for System.Windows.Application
using OxyPlot; // Added for OxyPlot.DataPoint

namespace BeeSwarmGame
{
    /// <summary>
    /// Provides real-time data visualization capabilities for the bee swarm simulation
    /// using MonoGame for rendering charts, graphs, and other visual metrics.
    /// </summary>
    public static class Visualizer
    {
        private static bool _isRunning = false;
        private static Environment? _environment;
        private static Dictionary<string, List<DataPoint>> _timeSeriesData = new();
        private static int _updateIntervalMs = 500; // Update metrics every half-second

        // MonoGame specific objects
        private static VisualizerWindow? _visualizerWindow;
        
        // Define data point structure for time series data
        private struct DataPoint
        {
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
        }

        // Add a public property to check status
        public static bool IsRunning => _isRunning;

        /// <summary>
        /// Starts the visualization for the given environment.
        /// </summary>
        /// <param name="environment">The environment to visualize.</param>
        public static void Start(Environment environment)
        {
            if (_isRunning)
            {
                Console.WriteLine("Visualizer is already running.");
                return;
            }

            _environment = environment;
            _isRunning = true;

            Console.WriteLine("Visualizer.Start: Data visualization starting...");
            Console.WriteLine($"Environment size: {environment.Width}x{environment.Height}");
            Console.WriteLine($"Number of swarms: {environment.Swarms.Count}");
            Console.WriteLine($"Number of resources: {environment.ResourceNodes.Count}");

            // Start metrics collection loop on a background thread
            Task.Run(() => MetricsCollectionLoop());

            // Instead of launching the MonoGame visualizer, launch our WPF window on a new STA thread.
            Thread uiThread = new Thread(() =>
            {
                // Create and run the WPF window
                var app = new System.Windows.Application();

                // Convert _timeSeriesData to the format expected by VisualizerWpfWindow
                var wpfTimeSeriesData = _timeSeriesData
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value
                                  .Select(dp => new OxyPlot.DataPoint(dp.Timestamp.ToOADate(), dp.Value))
                                  .ToList()
                    );

                // Pass shared metric data to the window constructor
                var window = new VisualizerWpfWindow(wpfTimeSeriesData);
                app.Run(window);
            });
            uiThread.SetApartmentState(System.Threading.ApartmentState.STA);
            uiThread.Start();

            Console.WriteLine("Visualizer.Start: WPF visualizer thread launched.");
        }
        
        /// <summary>
        /// Stops the visualization.
        /// </summary>
        public static void Stop()
        {
            Console.WriteLine("Visualizer.Stop: Stopping visualization...");
            _isRunning = false; // Signal loops to stop
            try
            {
                 _visualizerWindow?.Exit(); // Request window close
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Visualizer.Stop: Error calling Exit() on visualizer window: {ex.Message}");
            }
            _visualizerWindow = null; // Release reference
            Console.WriteLine("Visualizer.Stop: Data visualization stopped.");
        }
        
        private static void MetricsCollectionLoop()
        {
            while (_isRunning && _environment != null)
            {
                try
                {
                    CollectMetrics();
                    Thread.Sleep(_updateIntervalMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in visualization metrics collection: {ex.Message}");
                }
            }
        }
        
        private static void CollectMetrics()
        {
            if (_environment == null) return;
            
            CollectPopulationMetrics();
            CollectResourceMetrics();
            CollectTerritoryMetrics();
            CollectPerformanceMetrics();
            
            // Occasionally log summary to console
            if (DateTime.Now.Second % 30 == 0)
            {
                DisplayConsoleSummary();
            }
        }
        
        private static void CollectPopulationMetrics()
        {
            if (_environment == null) return;
            
            // Track total units per swarm
            foreach (var swarm in _environment.Swarms)
            {
                string swarmKey = $"swarm_{swarm.Id}_total_units";
                AddTimeSeriesDataPoint(swarmKey, swarm.Units.Count);
                
                // Track unit roles distribution
                var roleGroups = swarm.Units.GroupBy(u => u.Role).ToDictionary(g => g.Key, g => g.Count());
                foreach (var role in roleGroups)
                {
                    string roleKey = $"swarm_{swarm.Id}_role_{role.Key}";
                    AddTimeSeriesDataPoint(roleKey, role.Value);
                }
            }
        }
        
        private static void CollectResourceMetrics()
        {
            if (_environment == null) return;
            
            // Track total resources
            int totalResources = _environment.ResourceNodes.Sum(n => (int)n.ResourceAmount);
            AddTimeSeriesDataPoint("total_resources", totalResources);
            
            // Track resources per swarm
            foreach (var swarm in _environment.Swarms)
            {
                string resourceKey = $"swarm_{swarm.Id}_resources";
                AddTimeSeriesDataPoint(resourceKey, swarm.TotalResources);
                
                // Track resources collected by units
                double totalCollected = swarm.Units.Sum(u => u.ResourcesCollected);
                string collectedKey = $"swarm_{swarm.Id}_collected";
                AddTimeSeriesDataPoint(collectedKey, totalCollected);
            }
        }
        
        private static void CollectTerritoryMetrics()
        {
            if (_environment == null) return;
            
            // For demonstration, we'll track simple territory metrics:
            // - For each swarm, count units in different quadrants of the map
            
            double halfWidth = _environment.Width / 2;
            double halfHeight = _environment.Height / 2;
            
            foreach (var swarm in _environment.Swarms)
            {
                // Count units in top-left quadrant
                int topLeft = swarm.Units.Count(u => 
                    u.Position.X < halfWidth && u.Position.Y < halfHeight);
                AddTimeSeriesDataPoint($"swarm_{swarm.Id}_territory_topleft", topLeft);
                
                // Count units in top-right quadrant
                int topRight = swarm.Units.Count(u => 
                    u.Position.X >= halfWidth && u.Position.Y < halfHeight);
                AddTimeSeriesDataPoint($"swarm_{swarm.Id}_territory_topright", topRight);
                
                // Count units in bottom-left quadrant
                int bottomLeft = swarm.Units.Count(u => 
                    u.Position.X < halfWidth && u.Position.Y >= halfHeight);
                AddTimeSeriesDataPoint($"swarm_{swarm.Id}_territory_bottomleft", bottomLeft);
                
                // Count units in bottom-right quadrant
                int bottomRight = swarm.Units.Count(u => 
                    u.Position.X >= halfWidth && u.Position.Y >= halfHeight);
                AddTimeSeriesDataPoint($"swarm_{swarm.Id}_territory_bottomright", bottomRight);
            }
        }
        
        private static void CollectPerformanceMetrics()
        {
            if (_environment == null) return;
            
            foreach (var swarm in _environment.Swarms)
            {
                // Track birth and death counts
                AddTimeSeriesDataPoint($"swarm_{swarm.Id}_births", swarm.TotalBirths);
                AddTimeSeriesDataPoint($"swarm_{swarm.Id}_deaths", swarm.TotalDeaths);
                
                // Track average distance from base
                if (swarm.Units.Any())
                {
                    // FIX: Use XNA vectors and cast result to double
                    double avgDistance = swarm.Units.Average(u =>
                        (double)Microsoft.Xna.Framework.Vector2.Distance(
                            u.Position.ToXnaVector2(),
                            swarm.BasePosition.ToXnaVector2()
                        )
                    );
                    AddTimeSeriesDataPoint($"swarm_{swarm.Id}_avg_distance", avgDistance);
                }
            }
        }
        
        private static void AddTimeSeriesDataPoint(string key, double value)
        {
            if (!_timeSeriesData.ContainsKey(key))
            {
                _timeSeriesData[key] = new List<DataPoint>();
            }
            
            _timeSeriesData[key].Add(new DataPoint
            {
                Timestamp = DateTime.Now,
                Value = value
            });
            
            // Keep only the last 100 data points to avoid memory growth
            if (_timeSeriesData[key].Count > 100)
            {
                _timeSeriesData[key].RemoveAt(0);
            }
        }
        
        private static void DisplayConsoleSummary()
        {
            if (_environment == null) return;
            
            Console.WriteLine("\n=== Swarm Simulation Metrics ===");
            Console.WriteLine($"Time: {DateTime.Now.ToString("HH:mm:ss")}");
            Console.WriteLine($"Environment: {_environment.Width}x{_environment.Height}, {_environment.ResourceNodes.Count} resources");
            
            foreach (var swarm in _environment.Swarms)
            {
                Console.WriteLine($"\n{swarm.Id}:");
                Console.WriteLine($"  Units: {swarm.Units.Count} total");
                
                // Role distribution
                var roles = swarm.Units.GroupBy(u => u.Role)
                                    .ToDictionary(g => g.Key, g => g.Count());
                Console.Write("  Roles: ");
                foreach (var role in roles)
                {
                    Console.Write($"{role.Key}:{role.Value} ");
                }
                
                Console.WriteLine($"\n  Resources: {swarm.TotalResources:F0}");
                
                // Calculate growth rate if we have enough data
                string birthsKey = $"swarm_{swarm.Id}_births";
                if (_timeSeriesData.ContainsKey(birthsKey) && _timeSeriesData[birthsKey].Count > 1)
                {
                    var first = _timeSeriesData[birthsKey].First();
                    var last = _timeSeriesData[birthsKey].Last();
                    double timeSpan = (last.Timestamp - first.Timestamp).TotalSeconds;
                    if (timeSpan > 0)
                    {
                        double growthRate = (last.Value - first.Value) / timeSpan;
                        Console.WriteLine($"  Growth rate: {growthRate:F2} units/sec");
                    }
                }
                
                // Average distance from base
                string distanceKey = $"swarm_{swarm.Id}_avg_distance";
                if (_timeSeriesData.ContainsKey(distanceKey) && _timeSeriesData[distanceKey].Any())
                {
                    double avgDistance = _timeSeriesData[distanceKey].Last().Value;
                    Console.WriteLine($"  Avg distance from base: {avgDistance:F1}");
                }
            }
            
            Console.WriteLine("\n=== Resource Distribution ===");
            int totalResources = _environment.ResourceNodes.Sum(n => (int)n.ResourceAmount);
            Console.WriteLine($"Total available: {totalResources}");
            double totalCollected = _environment.Swarms.Sum(s => s.Units.Sum(u => u.ResourcesCollected));
            Console.WriteLine($"Total collected: {totalCollected:F0}");
            
            Console.WriteLine("===============================\n");
        }
        
        /// <summary>
        /// MonoGame-based window for real-time visualization
        /// </summary>
        private class VisualizerWindow : Microsoft.Xna.Framework.Game
        {
            private GraphicsDeviceManager _graphics;
            private SpriteBatch _spriteBatch = null!;
            private SpriteFont _font = null!;
            private Texture2D _pixelTexture = null!;
            private Texture2D _circleTexture = null!;
            
            private Dictionary<string, List<DataPoint>> _data;
            private Environment _environment;
            
            // Visualization state
            private bool _showPopulationMetrics = true;
            private bool _showResourceMetrics = true;
            private bool _showTerritoryMetrics = true;
            private bool _showPerformanceMetrics = true;
            
            // Colors for different swarms (matching main game colors)
            private static readonly Color[] _swarmColors = {
                Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Magenta, Color.Cyan
            };
            
            // UI Layout settings
            private int _windowWidth = 800;
            private int _windowHeight = 600;
            
            public VisualizerWindow(Dictionary<string, List<DataPoint>> data, Environment environment)
            {
                Console.WriteLine("VisualizerWindow.Constructor: Initializing...");
                _data = data;
                _environment = environment;
                _graphics = new GraphicsDeviceManager(this);
                Content.RootDirectory = "Content";
                IsMouseVisible = true;
                
                // Set window size
                _graphics.PreferredBackBufferWidth = _windowWidth;
                _graphics.PreferredBackBufferHeight = _windowHeight;
                Window.Title = "Bee Swarm - Data Visualizer";
                
                // Update visualizations at 30fps
                IsFixedTimeStep = true;
                TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30.0);
                Console.WriteLine("VisualizerWindow.Constructor: Finished.");
            }
            
            protected override void Initialize()
            {
                Console.WriteLine("VisualizerWindow.Initialize: Start...");
                // Set window to not be the main game window
                Window.AllowUserResizing = true;
                
                base.Initialize();
                Console.WriteLine("VisualizerWindow.Initialize: Finished.");
            }
            
            protected override void LoadContent()
            {
                Console.WriteLine("VisualizerWindow.LoadContent: Start...");
                _spriteBatch = new SpriteBatch(GraphicsDevice);
                Console.WriteLine("VisualizerWindow.LoadContent: SpriteBatch created.");
                
                try
                {
                    // Try to load a font - fallback to default if not available
                    Console.WriteLine("VisualizerWindow.LoadContent: Loading font 'DefaultFont'...");
                    _font = Content.Load<SpriteFont>("DefaultFont");
                    Console.WriteLine("VisualizerWindow.LoadContent: Font loaded successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"VisualizerWindow.LoadContent: WARNING - Unable to load font 'DefaultFont'. Using basic rendering. Error: {ex.Message}");
                    _font = null!; // Ensure font is null if loading failed
                }
                
                // Create a basic pixel texture regardless
                try
                {
                     Console.WriteLine("VisualizerWindow.LoadContent: Creating pixel texture...");
                     _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
                     _pixelTexture.SetData(new[] { Color.White });
                     Console.WriteLine("VisualizerWindow.LoadContent: Pixel texture created.");
                }
                 catch (Exception ex)
                {
                     Console.WriteLine($"VisualizerWindow.LoadContent: ERROR creating pixel texture: {ex.Message}");
                     // Handle error - maybe exit? For now, just log.
                     _pixelTexture = null!;
                }


                // Create a simple circle texture for graphs
                 try
                {
                    Console.WriteLine("VisualizerWindow.LoadContent: Creating circle texture...");
                    _circleTexture = CreateCircleTexture(GraphicsDevice, 16, Color.White);
                    Console.WriteLine("VisualizerWindow.LoadContent: Circle texture created.");
                }
                 catch (Exception ex)
                {
                     Console.WriteLine($"VisualizerWindow.LoadContent: ERROR creating circle texture: {ex.Message}");
                     // Handle error
                     _circleTexture = null!;
                }
                Console.WriteLine("VisualizerWindow.LoadContent: Finished.");
            }
            
            protected override void Update(GameTime gameTime)
            {
                KeyboardState keyState = Keyboard.GetState();
                
                // Toggle visualizations with number keys
                if (keyState.IsKeyDown(Keys.D1)) _showPopulationMetrics = !_showPopulationMetrics;
                if (keyState.IsKeyDown(Keys.D2)) _showResourceMetrics = !_showResourceMetrics;
                if (keyState.IsKeyDown(Keys.D3)) _showTerritoryMetrics = !_showTerritoryMetrics;
                if (keyState.IsKeyDown(Keys.D4)) _showPerformanceMetrics = !_showPerformanceMetrics;
                
                // Press Escape to close the visualizer
                if (keyState.IsKeyDown(Keys.Escape)) Exit();
                
                base.Update(gameTime);
            }
            
            protected override void Draw(GameTime gameTime)
            {
                GraphicsDevice.Clear(Color.Black);
                
                _spriteBatch.Begin();
                
                // Draw title
                DrawText("Bee Swarm - Real-Time Data Visualizer", 10, 10, Color.White);
                DrawText("Press keys 1-4 to toggle visualizations. ESC to close.", 10, 30, Color.LightGray);
                
                int yOffset = 60;
                
                // Draw Population Metrics (1)
                if (_showPopulationMetrics)
                {
                    DrawSectionHeader("Population Metrics (1)", 10, yOffset, Color.LightBlue);
                    yOffset += 25;
                    
                    // Draw pie charts for role distribution
                    DrawRoleDistributionPieCharts(10, yOffset);
                    yOffset += 200;
                }
                
                // Draw Resource Metrics (2)
                if (_showResourceMetrics)
                {
                    DrawSectionHeader("Resource Metrics (2)", 10, yOffset, Color.LightGreen);
                    yOffset += 25;
                    
                    // Draw line graphs for resource accumulation
                    DrawResourceAccumulationGraphs(10, yOffset);
                    yOffset += 150;
                }
                
                // Draw Territory Metrics (3)
                if (_showTerritoryMetrics)
                {
                    DrawSectionHeader("Territory Control (3)", 10, yOffset, Color.LightYellow);
                    yOffset += 25;
                    
                    // Draw territory heatmap visualization
                    DrawTerritoryControlMaps(10, yOffset);
                    yOffset += 150;
                }
                
                // Draw Performance Metrics (4)
                if (_showPerformanceMetrics)
                {
                    DrawSectionHeader("Performance Metrics (4)", 10, yOffset, Color.LightPink);
                    yOffset += 25;
                    
                    // Draw performance bar graphs
                    DrawPerformanceGraphs(10, yOffset);
                    yOffset += 150;
                }
                
                _spriteBatch.End();
                
                base.Draw(gameTime);
            }
            
            private void DrawText(string text, int x, int y, Color color)
            {
                if (_font != null)
                {
                    // FIX: Explicitly use Microsoft.Xna.Framework.Vector2
                    _spriteBatch.DrawString(_font, text, new Microsoft.Xna.Framework.Vector2(x, y), color);
                }
                else
                {
                    // Fallback if font not available - draw a colored rectangle
                    DrawRectangle(new Rectangle(x, y, text.Length * 5, 10), color);
                }
            }
            
            private void DrawSectionHeader(string text, int x, int y, Color color)
            {
                DrawText(text, x, y, color);
                // Ensure XNA Vector2 is used for DrawLine
                DrawLine(new Microsoft.Xna.Framework.Vector2(x, y + 20), new Microsoft.Xna.Framework.Vector2(x + 400, y + 20), color, 1);
            }
            
            private void DrawRoleDistributionPieCharts(int x, int y)
            {
                int swarmIndex = 0;
                int pieSize = 80;
                int spacing = 120;
                
                foreach (var swarm in _environment.Swarms)
                {
                    Color swarmColor = _swarmColors[swarmIndex % _swarmColors.Length];
                    swarmIndex++;
                    
                    // Draw swarm identification
                    DrawText(swarm.Id, x + spacing * (swarmIndex - 1), y, swarmColor);
                    
                    // Get role distribution for this swarm
                    var roles = swarm.Units.GroupBy(u => u.Role)
                                      .ToDictionary(g => g.Key, g => g.Count());
                    
                    if (roles.Any())
                    {
                        // Draw pie chart for roles
                        float totalUnits = swarm.Units.Count;
                        float startAngle = 0;
                        
                        int centerX = x + spacing * (swarmIndex - 1) + pieSize / 2;
                        int centerY = y + 40 + pieSize / 2;
                        
                        foreach (var role in roles)
                        {
                            float slicePercentage = role.Value / totalUnits;
                            float sliceAngle = slicePercentage * 360;
                            
                            // Draw slice
                            DrawPieSlice(centerX, centerY, pieSize / 2, startAngle, 
                                        startAngle + sliceAngle, GetRoleColor(role.Key, swarmColor));
                            
                            startAngle += sliceAngle;
                        }
                        
                        // Draw legend
                        int legendY = y + 40 + pieSize + 10;
                        foreach (var role in roles)
                        {
                            DrawRectangle(new Rectangle(centerX - 40, legendY, 10, 10), 
                                        GetRoleColor(role.Key, swarmColor));
                            DrawText($"{role.Key}: {role.Value}", centerX - 25, legendY, Color.White);
                            legendY += 15;
                        }
                    }
                    else
                    {
                        DrawText("No units", x + spacing * (swarmIndex - 1), y + 40, Color.Gray);
                    }
                }
            }
            
            private void DrawResourceAccumulationGraphs(int x, int y)
            {
                int chartWidth = 380;
                int chartHeight = 100;
                
                // Draw chart background
                DrawRectangle(new Rectangle(x, y, chartWidth, chartHeight), new Color(20, 20, 20));
                
                // Draw axes
                DrawLine(new Microsoft.Xna.Framework.Vector2(x, y + chartHeight), new Microsoft.Xna.Framework.Vector2(x + chartWidth, y + chartHeight), Color.White, 1);
                DrawLine(new Microsoft.Xna.Framework.Vector2(x, y), new Microsoft.Xna.Framework.Vector2(x, y + chartHeight), Color.White, 1);
                
                // Draw resource accumulation lines for each swarm
                int swarmIndex = 0;
                foreach (var swarm in _environment.Swarms)
                {
                    Color swarmColor = _swarmColors[swarmIndex % _swarmColors.Length];
                    swarmIndex++;
                    
                    string resourceKey = $"swarm_{swarm.Id}_resources";
                    if (_data.ContainsKey(resourceKey) && _data[resourceKey].Count > 1)
                    {
                        // Get min/max values for scaling
                        double maxVal = Math.Max(100, _data[resourceKey].Max(d => d.Value));
                        
                        // Draw last 50 data points (or fewer if we don't have 50)
                        var dataPoints = _data[resourceKey].Skip(Math.Max(0, _data[resourceKey].Count - 50)).ToList();
                        
                        // Calculate points for line graph
                        Microsoft.Xna.Framework.Vector2[] points = new Microsoft.Xna.Framework.Vector2[dataPoints.Count];
                        for (int i = 0; i < dataPoints.Count; i++)
                        {
                            float xPos = x + (float)i / (dataPoints.Count - 1) * chartWidth;
                            float yPos = y + chartHeight - (float)(dataPoints[i].Value / maxVal * chartHeight);
                            points[i] = new Microsoft.Xna.Framework.Vector2(xPos, yPos);
                        }
                        
                        // Draw lines connecting points
                        for (int i = 1; i < points.Length; i++)
                        {
                            DrawLine(points[i - 1], points[i], swarmColor, 1);
                        }
                        
                        // Label the swarm and its current value
                        DrawText($"{swarm.Id}: {swarm.TotalResources:F0}", 
                                 x + chartWidth + 10, 
                                 y + (swarmIndex - 1) * 20, 
                                 swarmColor);
                    }
                }
                
                // Draw title
                DrawText("Resource Accumulation Over Time", x, y - 20, Color.White);
            }
            
            private void DrawTerritoryControlMaps(int x, int y)
            {
                int mapWidth = 200;
                int mapHeight = 120;
                
                // Draw map background
                DrawRectangle(new Rectangle(x, y, mapWidth, mapHeight), new Color(20, 20, 20));
                
                // Create a simplified territory control heatmap
                // Divide the map into a grid
                int gridSize = 20;
                int gridCellWidth = mapWidth / gridSize;
                int gridCellHeight = mapHeight / gridSize;
                
                // Calculate unit density for each grid cell
                Dictionary<(int x, int y), Dictionary<string, int>> territoryGrid = new();
                
                // Initialize grid
                for (int gx = 0; gx < gridSize; gx++)
                {
                    for (int gy = 0; gy < gridSize; gy++)
                    {
                        territoryGrid[(gx, gy)] = new Dictionary<string, int>();
                    }
                }
                
                // Map environment coordinates to grid coordinates
                double envToGridX = gridSize / _environment.Width;
                double envToGridY = gridSize / _environment.Height;
                
                // Count units in each grid cell
                foreach (var swarm in _environment.Swarms)
                {
                    foreach (var unit in swarm.Units)
                    {
                        int gx = (int)(unit.Position.X * envToGridX);
                        int gy = (int)(unit.Position.Y * envToGridY);
                        
                        // Clamp to valid grid range
                        gx = Math.Clamp(gx, 0, gridSize - 1);
                        gy = Math.Clamp(gy, 0, gridSize - 1);
                        
                        if (!territoryGrid[(gx, gy)].ContainsKey(swarm.Id))
                        {
                            territoryGrid[(gx, gy)][swarm.Id] = 0;
                        }
                        territoryGrid[(gx, gy)][swarm.Id]++;
                    }
                }
                
                // Draw the heatmap
                for (int gx = 0; gx < gridSize; gx++)
                {
                    for (int gy = 0; gy < gridSize; gy++)
                    {
                        var cellData = territoryGrid[(gx, gy)];
                        if (cellData.Any())
                        {
                            // Find dominant swarm in this cell
                            var dominantSwarm = cellData.OrderByDescending(kv => kv.Value).First();
                            
                            // Calculate alpha based on unit count (higher count = more intense color)
                            float alpha = Math.Min(1f, dominantSwarm.Value * 0.2f);
                            
                            // Find swarm color
                            int swarmIndex = int.Parse(dominantSwarm.Key.Split('-').Last()) - 1;
                            Color baseColor = _swarmColors[swarmIndex % _swarmColors.Length];
                            Color cellColor = new Color(baseColor.R, baseColor.G, baseColor.B, (byte)(alpha * 255));
                            
                            // Draw cell with swarm color and appropriate alpha
                            int cellX = x + gx * gridCellWidth;
                            int cellY = y + gy * gridCellHeight;
                            DrawRectangle(new Rectangle(cellX, cellY, gridCellWidth, gridCellHeight), cellColor);
                        }
                    }
                }
                
                // Draw grid lines
                for (int gx = 0; gx <= gridSize; gx++)
                {
                    int lineX = x + gx * gridCellWidth;
                    DrawLine(new Microsoft.Xna.Framework.Vector2(lineX, y), new Microsoft.Xna.Framework.Vector2(lineX, y + mapHeight), new Color(40, 40, 40), 1);
                }
                
                for (int gy = 0; gy <= gridSize; gy++)
                {
                    int lineY = y + gy * gridCellHeight;
                    DrawLine(new Microsoft.Xna.Framework.Vector2(x, lineY), new Microsoft.Xna.Framework.Vector2(x + mapWidth, lineY), new Color(40, 40, 40), 1);
                }
                
                // Draw swarm legends
                int legendX = x + mapWidth + 20;
                int legendY = y;
                DrawText("Territory Control", legendX, legendY, Color.White);
                legendY += 20;
                
                int swarmIdx = 0;
                foreach (var swarm in _environment.Swarms)
                {
                    Color swarmColor = _swarmColors[swarmIdx % _swarmColors.Length];
                    DrawRectangle(new Rectangle(legendX, legendY, 10, 10), swarmColor);
                    DrawText(swarm.Id, legendX + 15, legendY, swarmColor);
                    legendY += 20;
                    swarmIdx++;
                }
            }
            
            private void DrawPerformanceGraphs(int x, int y)
            {
                int barHeight = 15;
                int barSpacing = 20;
                
                DrawText("Swarm Performance", x, y, Color.White);
                y += 20;
                
                int swarmIndex = 0;
                foreach (var swarm in _environment.Swarms)
                {
                    Color swarmColor = _swarmColors[swarmIndex % _swarmColors.Length];
                    swarmIndex++;
                    
                    // Draw swarm label
                    DrawText(swarm.Id, x, y, swarmColor);
                    y += 20;
                    
                    // Birth rate
                    DrawText("Births:", x + 15, y, Color.White);
                    // FIX: Use a faded color by multiplying with a float (not an int)
                    DrawRectangle(new Rectangle(x + 70, y, (int)(swarm.TotalBirths * 2), barHeight), swarmColor * 0.7f);
                    DrawText(swarm.TotalBirths.ToString(), x + 80 + (int)(swarm.TotalBirths * 2), y, Color.White);
                    y += barSpacing;
                    
                    // Death rate
                    DrawText("Deaths:", x + 15, y, Color.White);
                    // FIX: Add missing barHeight argument to Rectangle constructor
                    DrawRectangle(new Rectangle(x + 70, y, (int)(swarm.TotalDeaths * 2), barHeight),
                                 new Color(swarmColor.R / 2, swarmColor.G / 2, swarmColor.B / 2));
                    DrawText(swarm.TotalDeaths.ToString(), x + 80 + (int)(swarm.TotalDeaths * 2), y, Color.White);
                    y += barSpacing;
                    
                    // Average distance from base
                    string distanceKey = $"swarm_{swarm.Id}_avg_distance";
                    if (_data.ContainsKey(distanceKey) && _data[distanceKey].Any())
                    {
                        double avgDistance = _data[distanceKey].Last().Value;
                        DrawText("Avg dist:", x + 15, y, Color.White);
                        DrawRectangle(new Rectangle(x + 70, y, (int)(avgDistance), barHeight), swarmColor * 0.5f);
                        DrawText($"{avgDistance:F1}", x + 80 + (int)(avgDistance), y, Color.White);
                        y += barSpacing;
                    }
                    
                    y += 10; // Add extra space between swarms
                }
            }
            
            // Helper drawing methods
            private void DrawRectangle(Rectangle rect, Color color)
            {
                _spriteBatch.Draw(_pixelTexture, rect, color);
            }
            
            private void DrawLine(Microsoft.Xna.Framework.Vector2 start, Microsoft.Xna.Framework.Vector2 end, Color color, float thickness)
            {
                Microsoft.Xna.Framework.Vector2 delta = end - start;
                float angle = (float)Math.Atan2(delta.Y, delta.X);
                float length = delta.Length();
                
                _spriteBatch.Draw(_pixelTexture,
                                 start,
                                 null,
                                 color,
                                 angle,
                                 Microsoft.Xna.Framework.Vector2.Zero,
                                 new Microsoft.Xna.Framework.Vector2(length, thickness),
                                 SpriteEffects.None,
                                 0);
            }
            
            private void DrawPieSlice(int centerX, int centerY, int radius, float startAngle, float endAngle, Color color)
            {
                const int segments = 32;
                float angleStep = (endAngle - startAngle) / segments;
                for (int i = 0; i < segments; i++)
                {
                    float angle1 = MathHelper.ToRadians(startAngle + i * angleStep);
                    float angle2 = MathHelper.ToRadians(startAngle + (i + 1) * angleStep);

                    var point1 = new Microsoft.Xna.Framework.Vector2(centerX, centerY);
                    var point2 = new Microsoft.Xna.Framework.Vector2(
                        centerX + (float)Math.Cos(angle1) * radius,
                        centerY + (float)Math.Sin(angle1) * radius);
                    var point3 = new Microsoft.Xna.Framework.Vector2(
                        centerX + (float)Math.Cos(angle2) * radius,
                        centerY + (float)Math.Sin(angle2) * radius);

                    DrawTriangle(point1, point2, point3, color);
                }
            }

            private void DrawTriangle(Microsoft.Xna.Framework.Vector2 p1, Microsoft.Xna.Framework.Vector2 p2, Microsoft.Xna.Framework.Vector2 p3, Color color)
            {
                DrawLine(p1, p2, color, 1);
                DrawLine(p2, p3, color, 1);
                DrawLine(p3, p1, color, 1);
            }
            
            private Color GetRoleColor(string role, Color baseColor)
            {
                switch (role.ToLower())
                {
                    case "basic": return new Color(baseColor.R, baseColor.G, baseColor.B);
                    case "gatherer": return new Color(baseColor.R / 2, baseColor.G, baseColor.B / 2);
                    case "combat": return new Color(baseColor.R, baseColor.G / 2, baseColor.B / 2);
                    case "scout": return new Color(baseColor.R / 2, baseColor.G / 2, baseColor.B);
                    case "defender": return new Color(baseColor.R / 2, baseColor.G, baseColor.B);
                    default: return baseColor;
                }
            }
            
            // Helper to create a circle texture dynamically for visualization points
            private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, int diameter, Color color)
            {
                Texture2D texture = new Texture2D(graphicsDevice, diameter, diameter);
                Color[] data = new Color[diameter * diameter];
                float radius = diameter / 2f;
                float radiusSq = radius * radius;
                
                for (int y = 0; y < diameter; y++)
                {
                    for (int x = 0; x < diameter; x++)
                    {
                        var pos = new Microsoft.Xna.Framework.Vector2(x, y);
                        var center = new Microsoft.Xna.Framework.Vector2(radius, radius);
                        float distanceSq = Microsoft.Xna.Framework.Vector2.DistanceSquared(pos, center);

                        if (distanceSq <= radiusSq)
                        {
                            float distFactor = (radiusSq - distanceSq) / 3.0f; // Use 3.0f for float division
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

            protected override void UnloadContent()
            {
                Console.WriteLine("VisualizerWindow.UnloadContent: Unloading content...");
                // Dispose textures if they were created
                _pixelTexture?.Dispose();
                _circleTexture?.Dispose();
                // No need to dispose _font, ContentManager handles it.
                // No need to dispose _spriteBatch here, MonoGame handles it.
                base.UnloadContent();
                Console.WriteLine("VisualizerWindow.UnloadContent: Finished.");
            }
        }
    }

    // Change from private to internal
    internal static class VectorExtensions 
    {
        public static Microsoft.Xna.Framework.Vector2 ToXnaVector2(this BeeSwarmGame.Vector2 vector)
        {
            return new Microsoft.Xna.Framework.Vector2((float)vector.X, (float)vector.Y);
        }
        
        public static BeeSwarmGame.Vector2 ToBeeSwarmVector2(this Microsoft.Xna.Framework.Vector2 vector)
        {
            return new BeeSwarmGame.Vector2(vector.X, vector.Y);
        }
    }
}
