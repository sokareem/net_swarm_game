using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // For Parallel processing

namespace BeeSwarmGame;

public class Environment
{
    private static readonly Random Rng = new Random();

    public double Width { get; }
    public double Height { get; }
    public List<ResourceNode> ResourceNodes { get; private set; } = new List<ResourceNode>();
    public List<Swarm> Swarms { get; } = new List<Swarm>();
    public List<(Vector2 Position, Vector2 Size)> Obstacles { get; } = new List<(Vector2, Vector2)>();

    // Resource regeneration
    public bool ResourceRegenerationEnabled { get; set; } = true;
    private int _lastResourceCount = 0;
    private const int RegenerationCooldownTicks = 300;
    public int RegenerationCooldownRemaining { get; private set; } = 0; // Public for Swarm access
    private int _totalRegenerationCycles = 0;

    public Environment(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public void AddResourceNode(ResourceNode resourceNode)
    {
        ResourceNodes.Add(resourceNode);
    }

    public void AddSwarm(Swarm swarm)
    {
        Swarms.Add(swarm);
    }

    public void AddObstacle(Vector2 position, Vector2 size)
    {
        Obstacles.Add((position, size));
    }

    public List<Unit> GetAllUnits()
    {
        return Swarms.SelectMany(s => s.Units).ToList();
    }

    public bool IsPositionValid(Vector2 position, double margin = 0, bool flyingUnit = false)
    {
        // Check boundaries
        if (position.X < margin || position.X >= Width - margin || position.Y < margin || position.Y >= Height - margin)
        {
            return false;
        }

        if (flyingUnit) return true; // Flying units ignore obstacles (for this check)

        // Check obstacles
        foreach (var (obsPos, obsSize) in Obstacles)
        {
            if (position.X >= obsPos.X - margin &&
                position.X <= obsPos.X + obsSize.X + margin &&
                position.Y >= obsPos.Y - margin &&
                position.Y <= obsPos.Y + obsSize.Y + margin)
            {
                return false;
            }
        }
        return true;
    }

     public List<(Vector2 Position, Vector2 Size)> GetObstaclesNear(Vector2 position, double radius)
    {
        var nearby = new List<(Vector2 Position, Vector2 Size)>();
        double radiusSq = radius * radius; // Use squared distances

        foreach (var obstacle in Obstacles)
        {
            Vector2 obsCenter = obstacle.Position + obstacle.Size * 0.5;
            double obsDiagonal = obstacle.Size.Length() * 0.5; // Half diagonal
            double effectiveRadius = radius + obsDiagonal;

            if (Vector2.DistanceSquared(position, obsCenter) < effectiveRadius * effectiveRadius)
            {
                nearby.Add(obstacle);
            }
        }
        return nearby;
    }

    public Vector2? FindNearestValidPosition(Vector2 position, Vector2? currentPos = null)
    {
        if (IsPositionValid(position) && position != currentPos)
        {
            return position;
        }

        // Search in expanding circles
        for (int radius = 1; radius < 30; radius++)
        {
            int numSteps = Math.Max(8, (int)(Math.PI * 2 * radius / 4));
            for (int i = 0; i < numSteps; i++)
            {
                double angle = 2 * Math.PI * i / numSteps;
                Vector2 checkPos = new Vector2(
                    position.X + Math.Cos(angle) * radius,
                    position.Y + Math.Sin(angle) * radius
                );

                // Quick bounds check
                if (checkPos.X >= 0 && checkPos.X < Width && checkPos.Y >= 0 && checkPos.Y < Height)
                {
                    if (IsPositionValid(checkPos) && checkPos != currentPos)
                    {
                        return checkPos;
                    }
                }
            }
        }

         // Wider random search if circle search fails
        for(int i=0; i< 20; i++)
        {
            double randX = position.X + Rng.NextDouble() * 60 - 30; // Search within +/- 30
            double randY = position.Y + Rng.NextDouble() * 60 - 30;
            Vector2 randPos = new Vector2(Math.Clamp(randX, 0, Width -1), Math.Clamp(randY, 0, Height -1));
             if (IsPositionValid(randPos) && randPos != currentPos)
             {
                  return randPos;
             }
        }


        Console.WriteLine($"WARNING: Could not find valid position near {position}. Defaulting to center.");
        Vector2 center = new Vector2(Width / 2, Height / 2);
        return center != currentPos ? center : null; // Return null if center is the current pos
    }


    public void Update()
    {
        // --- Resource Regeneration Logic ---
        int currentResourceCount = ResourceNodes.Count;
        if (currentResourceCount == 0 && _lastResourceCount > 0)
        {
            Console.WriteLine("INFO: All resources depleted. Starting regeneration countdown.");
            RegenerationCooldownRemaining = RegenerationCooldownTicks;
        }

        if (RegenerationCooldownRemaining > 0)
        {
            RegenerationCooldownRemaining--;
            if (RegenerationCooldownRemaining == 0 && ResourceRegenerationEnabled)
            {
                RegenerateResources();
            }
        }
        _lastResourceCount = currentResourceCount;
        // --- End Resource Regeneration ---

        // --- Update Swarms (Parallel) ---
        // Use Parallel.ForEach for potentially faster updates on multi-core systems
        Parallel.ForEach(Swarms, swarm =>
        {
             try // Add error handling per swarm update
             {
                 swarm.MakeDecisions(this);
                 swarm.UpdateUnits(this);
             }
             catch (Exception ex)
             {
                 Console.WriteLine($"ERROR updating swarm {swarm.Id}: {ex.Message}\n{ex.StackTrace}");
             }
        });
        // --- End Update Swarms ---


        // Remove depleted resource nodes
        // Create a new list to avoid modification issues during potential parallel access elsewhere
        ResourceNodes = ResourceNodes.Where(node => !node.IsDepleted()).ToList();

        // Check victory condition (optional, can be done in Game loop)
        // CheckVictory();
    }

    private void RegenerateResources()
    {
        if (!ResourceRegenerationEnabled) return;

        _totalRegenerationCycles++;
        Console.WriteLine($"INFO: Starting resource regeneration cycle #{_totalRegenerationCycles}");

        int numNewResources = 20 + Rng.Next(0, 11); // 20-30 base
        numNewResources += Math.Min(10, _totalRegenerationCycles * 2); // Bonus

        int numGardens = 4 + Rng.Next(0, 4); // 4-7 gardens
        int resourcesPerGarden = numNewResources / Math.Max(1, numGardens);

        for (int g = 0; g < numGardens; g++)
        {
            Vector2 gardenCenter = FindGardenLocation();

            for (int i = 0; i < resourcesPerGarden; i++)
            {
                // Flower-like distribution
                double distance = Rng.NextDouble() * 45 + 5; // 5 to 50
                double angle = Rng.NextDouble() * 2 * Math.PI;
                Vector2 offset = new Vector2(Math.Cos(angle) * distance, Math.Sin(angle) * distance);
                Vector2 pos = gardenCenter + offset;

                pos.X = Math.Clamp(pos.X, 30, Width - 30);
                pos.Y = Math.Clamp(pos.Y, 30, Height - 30);

                if (IsPositionValid(pos, margin: 5))
                {
                    double baseAmount = Rng.Next(500, 1501);
                    double centerProximity = 1.0 - (distance / 50.0);
                    double amount = (int)(baseAmount * (0.7 + 0.6 * centerProximity));

                    if (i == 0 && Rng.NextDouble() < 0.3) // Chance for first node to be rich
                    {
                        amount *= 2;
                        Console.WriteLine($"INFO: Created rich resource node ({amount:F0}) at {pos}");
                    }

                    AddResourceNode(new ResourceNode(pos, amount));
                }
            }
        }

        Console.WriteLine($"INFO: Regeneration complete: Added {ResourceNodes.Count} nodes in {numGardens} gardens.");

        // Clear old swarm memory
        foreach (var swarm in Swarms)
        {
            swarm.KnownResourceLocations.Clear(); // Simple clear for now
            swarm.CollectiveKnowledge.Clear();
        }
    }

     private Vector2 FindGardenLocation()
    {
        int attempts = 0;
        double minDistanceBase = 80.0;
        Vector2 gardenPos = Vector2.Zero;

        while (attempts < 10)
        {
            gardenPos = new Vector2(
                Rng.NextDouble() * (Width - 200) + 100,
                Rng.NextDouble() * (Height - 200) + 100
            );

            bool tooClose = Swarms.Any(s => Vector2.Distance(gardenPos, s.BasePosition) < minDistanceBase);

            if (!tooClose) return gardenPos; // Found a good spot

            attempts++;
            if (attempts > 5) minDistanceBase *= 0.8; // Relax constraint
        }
        return gardenPos; // Return last attempt if no ideal spot found
    }


    public string? CheckVictory()
    {
        var activeSwarms = Swarms.Where(s => s.Units.Any()).ToList();
        if (activeSwarms.Count == 1)
        {
            return activeSwarms[0].Id;
        }
        else if (activeSwarms.Count == 0)
        {
            return "draw";
        }
        return null; // Game continues
    }
}
