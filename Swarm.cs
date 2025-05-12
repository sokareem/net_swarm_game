using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // For Interlocked

namespace BeeSwarmGame;

public class Swarm
{
    private static readonly Random Rng = new Random();
    private double _totalResources; // Use backing field for thread safety if needed

    public string Id { get; }
    public (byte R, byte G, byte B) Color { get; } // Simple color representation
    public List<Unit> Units { get; } = new List<Unit>();
    public Vector2 BasePosition { get; }
    public double SpawnRate { get; private set; } = 0.05;
    public double UnitCost { get; private set; } = 20;

    // Upgrades tracking
    public Dictionary<string, int> Upgrades { get; } = new Dictionary<string, int>
    {
        { "health_lvl", 0 }, { "speed_lvl", 0 }, { "resource_capacity_lvl", 0 },
        { "attack_power_lvl", 0 }, { "attack_range_lvl", 0 }, { "sight_range_lvl", 0 },
        { "gather_rate_lvl", 0 }, { "spawn_rate_lvl", 0 }, { "unit_cost_lvl", 0 },
        { "combat_power_lvl", 0 }, { "resource_efficiency_lvl", 0 },
        { "lifespan_extension_lvl", 0 }, { "energy_output_lvl", 0 },
        { "nest_structure_lvl", 0 }, { "road_distribution_lvl", 0 }
    };

    public List<string> AvailableUnitTypes { get; } = new List<string> { "basic" };

    // Base stats (could be moved to a static config or separate class)
    private static readonly Dictionary<string, Dictionary<string, double>> BaseStats = new()
    {
        ["basic"] = new() { ["health"]=100, ["speed"]=1.0, ["resource_capacity"]=20, ["attack_power"]=10, ["attack_range"]=20, ["sight_range"]=50, ["gather_rate"]=5 },
        ["gatherer"] = new() { ["health"]=80, ["speed"]=1.2, ["resource_capacity"]=40, ["attack_power"]=5, ["attack_range"]=10, ["sight_range"]=60, ["gather_rate"]=10 },
        ["combat"] = new() { ["health"]=150, ["speed"]=0.8, ["resource_capacity"]=10, ["attack_power"]=20, ["attack_range"]=30, ["sight_range"]=70, ["gather_rate"]=2 },
        // Added new defender unit stats
        ["defender"] = new() { ["health"]=180, ["speed"]=0.9, ["resource_capacity"]=15, ["attack_power"]=12, ["attack_range"]=25, ["sight_range"]=55, ["gather_rate"]=3 }
    };
     // Note: Ranges were small (2.0, 1.0, 3.0), adjusted to be larger pixel values (20, 10, 30)
     // Note: Sight ranges were small (5.0, 6.0, 7.0), adjusted to be larger pixel values (50, 60, 70)


    // Upgrade costs (using delegates for lambda-like behavior)
    private static readonly Dictionary<string, Func<int, double>> UpgradeCosts = new()
    {
        ["health_lvl"] = lvl => 50 * (lvl + 1),
        ["speed_lvl"] = lvl => 60 * (lvl + 1),
        ["resource_capacity_lvl"] = lvl => 40 * (lvl + 1),
        ["attack_power_lvl"] = lvl => 70 * (lvl + 1),
        ["attack_range_lvl"] = lvl => 80 * (lvl + 1),
        ["sight_range_lvl"] = lvl => 30 * (lvl + 1),
        ["gather_rate_lvl"] = lvl => 45 * (lvl + 1),
        ["spawn_rate_lvl"] = lvl => 100 * (lvl + 1),
        ["unit_cost_lvl"] = lvl => 90 * (lvl + 1),
        // New upgrade costs:
        ["combat_power_lvl"] = lvl => 120 * (lvl + 1),
        ["resource_efficiency_lvl"] = lvl => 110 * (lvl + 1),
        ["lifespan_extension_lvl"] = lvl => 130 * (lvl + 1),
        ["energy_output_lvl"] = lvl => 140 * (lvl + 1),
        ["nest_structure_lvl"] = lvl => 150 * (lvl + 1),
        ["road_distribution_lvl"] = lvl => 160 * (lvl + 1)
    };

    // Known resource locations (position -> knowledge)
    public Dictionary<Vector2, ResourceSighting> KnownResourceLocations { get; } = new Dictionary<Vector2, ResourceSighting>();
    private const double ResourceMemoryDecaySeconds = 300.0; // Time in seconds

    // Collective knowledge and experience
    public Dictionary<Vector2, CollectiveResourceKnowledge> CollectiveKnowledge { get; } = new Dictionary<Vector2, CollectiveResourceKnowledge>();
    public int TotalBirths { get; private set; }
    public int TotalDeaths { get; private set; }
    public double ExperiencePoints { get; private set; } // Could be used for upgrades later

    public double TotalResources => _totalResources;

    public Swarm(string id, (byte, byte, byte) color, Vector2 basePosition)
    {
        Id = id;
        Color = color;
        BasePosition = basePosition;
        _totalResources = 100; // Starting resources
    }

    // Thread-safe way to add resources
    public void AddResources(double amount)
    {
        // Using Interlocked for thread safety if multiple units deposit concurrently
        // For simplicity, if updates happen sequentially per swarm, direct add is fine.
        // Let's assume sequential update within a swarm's turn for now.
        _totalResources += amount;
    }

    // Method to spend resources (ensure it doesn't go negative)
    private bool SpendResources(double amount)
    {
        if (_totalResources >= amount)
        {
            _totalResources -= amount;
            return true;
        }
        return false;
    }


    public double GetUnitStat(string unitType, string statName)
    {
        if (!BaseStats.ContainsKey(unitType)) unitType = "basic"; // Fallback

        double baseValue = BaseStats[unitType][statName];
        int upgradeLevel = Upgrades.GetValueOrDefault($"{statName}_lvl", 0);

        // Apply scaling based on stat type
        double multiplier = statName switch
        {
            "health" => 1.0 + 0.15 * upgradeLevel,
            "speed" => 1.0 + 0.1 * upgradeLevel,
            "resource_capacity" => 1.0 + 0.2 * upgradeLevel,
            "attack_power" => 1.0 + 0.15 * upgradeLevel,
            "attack_range" => 1.0 + 0.1 * upgradeLevel, // Adjusted range scaling if needed
            "sight_range" => 1.0 + 0.1 * upgradeLevel,   // Adjusted range scaling if needed
            "gather_rate" => 1.0 + 0.15 * upgradeLevel,
            _ => 1.0
        };

        return baseValue * multiplier;
    }

    public double GetEffectiveSpawnRate()
    {
        return SpawnRate * (1.0 + 0.2 * Upgrades["spawn_rate_lvl"]);
    }

    public double GetEffectiveUnitCost(string unitType = "basic")
    {
        double baseCost = UnitCost;
        if (unitType == "gatherer") baseCost *= 1.2;
        else if (unitType == "combat") baseCost *= 1.5;

        return baseCost * (1.0 - 0.1 * Upgrades["unit_cost_lvl"]);
    }

    public bool CanAffordUpgrade(string upgradeType)
    {
        double cost = upgradeType switch
        {
            "gatherer_unlock" => 150,
            "combat_unlock" => 200,
            _ => UpgradeCosts.TryGetValue(upgradeType, out var costFunc) ? costFunc(Upgrades[upgradeType]) : double.MaxValue
        };
        return TotalResources >= cost;
    }

    public bool PurchaseUpgrade(string upgradeType)
    {
        double cost;
        bool success = false;

        if (upgradeType == "gatherer_unlock")
        {
            cost = 150;
            if (TotalResources >= cost && !AvailableUnitTypes.Contains("gatherer"))
            {
                if (SpendResources(cost))
                {
                    AvailableUnitTypes.Add("gatherer");
                    Console.WriteLine($"INFO: {Id} unlocked Gatherer units.");
                    success = true;
                }
            }
        }
        else if (upgradeType == "combat_unlock")
        {
            cost = 200;
            if (TotalResources >= cost && !AvailableUnitTypes.Contains("combat"))
            {
                 if (SpendResources(cost))
                 {
                    AvailableUnitTypes.Add("combat");
                    Console.WriteLine($"INFO: {Id} unlocked Combat units.");
                    success = true;
                 }
            }
        }
        // New upgrade for defender unlock
        else if (upgradeType == "defender_unlock")
        {
            cost = 250;
            if (TotalResources >= cost && !AvailableUnitTypes.Contains("defender"))
            {
                 if (SpendResources(cost))
                 {
                    AvailableUnitTypes.Add("defender");
                    Console.WriteLine($"INFO: {Id} unlocked Defender units.");
                    success = true;
                 }
            }
        }
        else if (Upgrades.ContainsKey(upgradeType) && UpgradeCosts.ContainsKey(upgradeType))
        {
            int currentLevel = Upgrades[upgradeType];
            cost = UpgradeCosts[upgradeType](currentLevel);
            if (TotalResources >= cost)
            {
                 if (SpendResources(cost))
                 {
                    Upgrades[upgradeType]++;
                    Console.WriteLine($"INFO: {Id} upgraded {upgradeType} -> lvl {Upgrades[upgradeType]} (cost {cost:F0})");
                    // Update existing units' stats
                    foreach (var unit in Units) unit.UpdateStats(this);
                    success = true;
                 }
            }
            else {
                 // Console.WriteLine($"DEBUG: {Id} cannot afford {upgradeType} (cost {cost:F0})");
            }
        }

        return success;
    }

    public bool CanAffordUnit(string unitType = "basic")
    {
        return TotalResources >= GetEffectiveUnitCost(unitType) && AvailableUnitTypes.Contains(unitType);
    }

    public Unit? SpawnUnit(string unitType = "basic", string? role = null)
    {
        if (!CanAffordUnit(unitType)) return null;

        double cost = GetEffectiveUnitCost(unitType);
        if (!SpendResources(cost)) return null; // Double check spending

        Vector2 position = BasePosition + new Vector2(Rng.NextDouble() * 2.0 - 1.0, Rng.NextDouble() * 2.0 - 1.0);
        Unit unit = new Unit(Id, position, unitType);
        unit.Role = role ?? unitType; // Assign role
        unit.UpdateStats(this); // Initialize stats based on current upgrades

        Console.WriteLine($"INFO: {Id} spawned {unitType} (role={unit.Role}) at {position}");

        // Inherit knowledge (simplified: top 3 known resources)
        var topLocations = CollectiveKnowledge.OrderByDescending(kvp => kvp.Value.ReportedAmount)
                                             .Take(3);
        foreach (var kvp in topLocations)
        {
            unit.LearnResourceLocation(kvp.Key, kvp.Value.ReportedAmount);
        }

        Units.Add(unit);
        TotalBirths++;
        return unit;
    }

    public void ReportResourceLocation(Vector2 position, double amountGathered)
    {
        DateTime now = DateTime.UtcNow;
        KnownResourceLocations[position] = new ResourceSighting { ReportedAmount = amountGathered, LastSeen = now };

        if (CollectiveKnowledge.TryGetValue(position, out var knowledge))
        {
            knowledge.ReportedAmount = Math.Max(knowledge.ReportedAmount, amountGathered);
            knowledge.LastSeen = now;
            knowledge.Confirmations++;
        }
        else
        {
            CollectiveKnowledge[position] = new CollectiveResourceKnowledge
            {
                ReportedAmount = amountGathered,
                LastSeen = now,
                Confirmations = 1
            };
        }
    }

    public void CleanupResourceMemory()
    {
        DateTime cutoff = DateTime.UtcNow.AddSeconds(-ResourceMemoryDecaySeconds);
        var expired = KnownResourceLocations.Where(kvp => kvp.Value.LastSeen < cutoff).Select(kvp => kvp.Key).ToList();
        foreach (var pos in expired)
        {
            KnownResourceLocations.Remove(pos);
            // Also remove from collective knowledge? Optional.
            // CollectiveKnowledge.Remove(pos);
            // Console.WriteLine($"DEBUG: {Id} forgot resource at {pos}");
        }
    }

     public void MakeDecisions(Environment environment)
    {
        // Console.WriteLine($"DEBUG: {Id} decision start: {Units.Count} units, {TotalResources:F0} res, {KnownResourceLocations.Count} known locs");
        var situation = AssessSituation(environment);
        int totalUnits = Math.Max(1, Units.Count);
        const int maxUnits = 50;

        if (Rng.NextDouble() < 0.005) // Occasionally print status
        {
            Console.WriteLine($"STATUS: {Id}: {Units.Count} U, {TotalResources:F0} R | Roles: B:{situation.UnitComposition["basic"]} G:{situation.UnitComposition["gatherer"]} C:{situation.UnitComposition["combat"]} | Known: {KnownResourceLocations.Count}");
            // Console.WriteLine($"STATUS: {Id} lifecycle: births={TotalBirths}, deaths={TotalDeaths}");
        }

        if (Rng.NextDouble() < 0.05) CleanupResourceMemory();

        // --- Bootstrapping ---
        if (environment.Swarms.Count > 1 && Units.Count < 5 && TotalResources < 50)
        {
             AddResources(20); // Use the thread-safe method
             Console.WriteLine($"INFO: {Id} received bootstrap boost: +20 resources");
        }

        // --- Upgrades ---
        if (TotalResources > 200)
        {
            if (!AvailableUnitTypes.Contains("gatherer")) { if (PurchaseUpgrade("gatherer_unlock")) return; }
            else if (!AvailableUnitTypes.Contains("combat")) { if (PurchaseUpgrade("combat_unlock")) return; }
            // Add logic for other upgrades later if needed
        }

        // --- Spawning ---
        double spawnChance = GetEffectiveSpawnRate();
        if (totalUnits < 10) spawnChance *= 2.0;

        if (Rng.NextDouble() < spawnChance && Units.Count < maxUnits)
        {
            string unitTypeToSpawn = "basic";
            string roleToAssign = "basic"; // Default role

            if (totalUnits > 0)
            {
                double gathererRatio = (double)situation.UnitComposition["gatherer"] / totalUnits;
                double combatRatio = (double)situation.UnitComposition["combat"] / totalUnits;
                // New: calculate defender ratio
                double defenderRatio = (double)situation.UnitComposition.GetValueOrDefault("defender", 0) / totalUnits;
                double desiredGatherers = KnownResourceLocations.Any() ? 0.5 : 0.3;
                double desiredCombat = situation.UnderAttack ? 0.4 : 0.2;
                // New: desired defenders increase if under attack
                double desiredDefenders = situation.UnderAttack ? 0.3 : 0.1;

                if (AvailableUnitTypes.Contains("gatherer") && gathererRatio < desiredGatherers)
                {
                    unitTypeToSpawn = "gatherer";
                    roleToAssign = "gatherer";
                }
                else if (AvailableUnitTypes.Contains("combat") && combatRatio < desiredCombat)
                {
                    unitTypeToSpawn = "combat";
                    roleToAssign = "combat";
                }
                // New: prioritize defender if ratio is low
                else if (AvailableUnitTypes.Contains("defender") && defenderRatio < desiredDefenders)
                {
                    unitTypeToSpawn = "defender";
                    roleToAssign = "defender";
                }
                else if (AvailableUnitTypes.Contains("gatherer") && CanAffordUnit("gatherer"))
                {
                     unitTypeToSpawn = "gatherer";
                     roleToAssign = "gatherer";
                }
                 else if (AvailableUnitTypes.Contains("combat") && CanAffordUnit("combat"))
                {
                     unitTypeToSpawn = "combat";
                     roleToAssign = "combat";
                }
                 // If basic is chosen, decide if it should be a scout
                 if (unitTypeToSpawn == "basic")
                 {
                     // Example: Make some basics into scouts
                     if (Rng.NextDouble() < 0.25) // 25% chance for a new basic to be a scout
                     {
                         roleToAssign = "scout";
                     }
                 }
            }

            Unit? newUnit = SpawnUnit(unitTypeToSpawn, roleToAssign);
            if (newUnit != null && Units.Count < 5) // Send early units to scout
            {
                newUnit.Target = FindExplorationTarget(environment, newUnit);
                newUnit.State = UnitState.SCOUTING;
            }
        }

        // --- Task Assignment ---
        var idleUnits = Units.Where(u => u.State == UnitState.IDLE).ToList();
        if (!idleUnits.Any()) return; // No idle units to assign

        // Filter known resources that still exist in the environment
        var validKnownResources = KnownResourceLocations
            .Where(kvp => environment.ResourceNodes.Any(node => node.Position == kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var allEnemies = environment.GetAllUnits().Where(u => u.SwarmId != Id).ToList();

        // Ensure minimum scouts
        int minScouts = Math.Max(1, (int)(Units.Count * 0.2));
        int currentScouts = Units.Count(u => u.Role == "scout" && (u.State == UnitState.SCOUTING || u.State == UnitState.MOVING)); // Count active scouts
        int scoutsNeeded = Math.Max(0, minScouts - currentScouts);

        foreach (var unit in idleUnits)
        {
            bool taskAssigned = false;

            // Role-based task assignment priority:
            // 1. Defend Base (Combat/Basic)
            // 2. Attack Visible Enemies (Combat/Basic)
            // 3. Gather Known Resources (Gatherer/Basic)
            // 4. Gather Visible Resources (Gatherer/Basic)
            // 5. Scout (Scout/Basic, or others if needed)
            // 6. Default Idle/Wander

            // 1. Defend Base (include defender units)
            if (situation.UnderAttack && (unit.Role == "combat" || unit.Role == "basic" || unit.Role == "defender"))
            {
                var nearbyAttackers = situation.NearbyEnemies
                                        .Where(e => unit.DistanceTo(e.Position) < unit.SightRange)
                                        .OrderBy(e => unit.DistanceTo(e.Position))
                                        .FirstOrDefault();
                if (nearbyAttackers != null)
                {
                    unit.Target = nearbyAttackers;
                    unit.State = unit.DistanceTo(nearbyAttackers.Position) <= unit.AttackRange ? UnitState.ATTACKING : UnitState.MOVING;
                    taskAssigned = true;
                }
            }

            // 2. Attack Visible Enemies
            if (!taskAssigned && (unit.Role == "combat" || unit.Role == "basic"))
            {
                 var visibleEnemy = allEnemies
                                    .Where(e => unit.DistanceTo(e.Position) < unit.SightRange)
                                    .OrderBy(e => unit.DistanceTo(e.Position))
                                    .FirstOrDefault();
                 if (visibleEnemy != null)
                 {
                     unit.Target = visibleEnemy;
                     unit.State = UnitState.MOVING; // Move first, attack state set on arrival/range check
                     taskAssigned = true;
                 }
            }

            // 3. Gather Known Resources
            if (!taskAssigned && (unit.Role == "gatherer" || unit.Role == "basic") && validKnownResources.Any())
            {
                Vector2 targetPos;
                if (Rng.NextDouble() < 0.7) // 70% closest
                {
                    targetPos = validKnownResources.Keys.OrderBy(pos => unit.DistanceTo(pos)).First();
                }
                else // 30% random known
                {
                    targetPos = validKnownResources.Keys.ElementAt(Rng.Next(validKnownResources.Count));
                }

                var targetNode = environment.ResourceNodes.FirstOrDefault(node => node.Position == targetPos);
                if (targetNode != null)
                {
                    unit.Target = targetNode;
                    unit.State = UnitState.MOVING;
                    taskAssigned = true;
                }
            }

            // 4. Gather Visible Resources
            if (!taskAssigned && (unit.Role == "gatherer" || unit.Role == "basic"))
            {
                var visibleResource = environment.ResourceNodes
                                        .Where(r => !r.IsDepleted() && unit.DistanceTo(r.Position) < unit.SightRange)
                                        .OrderBy(r => unit.DistanceTo(r.Position)) // Simple closest logic
                                        .FirstOrDefault();
                if (visibleResource != null)
                {
                    unit.Target = visibleResource;
                    unit.State = UnitState.MOVING;
                    taskAssigned = true;
                }
            }

            // 5. Scout
            if (!taskAssigned && (unit.Role == "scout" || scoutsNeeded > 0))
            {
                 unit.Target = FindExplorationTarget(environment, unit);
                 unit.State = UnitState.SCOUTING;
                 if (unit.Role != "scout") scoutsNeeded--; // Decrement if assigning non-scout to scout
                 taskAssigned = true;
            }

            // 6. Default - Wander/Idle (can implement wandering later)
            if (!taskAssigned)
            {
                // Optional: Assign a random wander target nearby
                // unit.Target = FindWanderTarget(environment, unit);
                // unit.State = UnitState.MOVING;
                unit.State = UnitState.IDLE; // Remain idle for now
            }
        }

        // Apply Swarm Coordination (simplified)
        ApplySwarmCoordination();

        // Handle resource depletion awareness
        if (TotalResources < 50 && KnownResourceLocations.Any() && !environment.ResourceNodes.Any())
        {
             if (environment.RegenerationCooldownRemaining > 0)
             {
                 if (Rng.NextDouble() < 0.01)
                 {
                     Console.WriteLine($"INFO: {Id} waiting for resource regeneration ({environment.RegenerationCooldownRemaining} ticks left)");
                 }
                 // Encourage scouting during cooldown
                 foreach (var unit in idleUnits.Where(u => u.Role != "combat")) // Don't send combat units to scout usually
                 {
                     if (Rng.NextDouble() < 0.5)
                     {
                         unit.Target = FindExplorationTarget(environment, unit);
                         unit.State = UnitState.SCOUTING;
                     }
                 }
             }
        }
    }

    private void ApplySwarmCoordination()
    {
        if (Rng.NextDouble() < 0.02) // 2% chance per tick
        {
            const double groupingRadiusSq = 15.0 * 15.0; // Squared radius for efficiency
            var unitsToProcess = new HashSet<Unit>(Units);

            while (unitsToProcess.Any())
            {
                var seedUnit = unitsToProcess.First();
                unitsToProcess.Remove(seedUnit);
                var group = new List<Unit> { seedUnit };

                // Find nearby units efficiently
                var potentialNeighbors = unitsToProcess.ToList(); // Copy to allow removal
                foreach (var neighbor in potentialNeighbors)
                {
                    if (Vector2.DistanceSquared(seedUnit.Position, neighbor.Position) < groupingRadiusSq)
                    {
                        group.Add(neighbor);
                        unitsToProcess.Remove(neighbor);
                    }
                }

                if (group.Count > 1)
                {
                    // Calculate average momentum
                    Vector2 avgMomentum = Vector2.Zero;
                    foreach (var unit in group) avgMomentum += unit.Momentum;
                    avgMomentum /= group.Count;
                    avgMomentum.Normalize(); // Ensure it's a direction

                    // Apply alignment
                    foreach (var unit in group)
                    {
                        // Blend group momentum with individual momentum
                        Vector2 newMomentum = avgMomentum * 0.6 + unit.Momentum * 0.4;
                        newMomentum.Normalize();
                        unit.Momentum = newMomentum;
                        // Could also synchronize oscillation phase slightly here
                    }
                }
            }
        }
    }


    public Vector2 FindExplorationTarget(Environment environment, Unit unit)
    {
        // Simplified: Pick a random point further away
        double angle = Rng.NextDouble() * 2 * Math.PI;
        double minDistance = unit.SightRange * 2;
        double maxDistance = Math.Min(environment.Width, environment.Height) * 0.5;
        double distance = Rng.NextDouble() * (maxDistance - minDistance) + minDistance;

        double targetX = BasePosition.X + Math.Cos(angle) * distance;
        double targetY = BasePosition.Y + Math.Sin(angle) * distance;

        // Clamp to bounds
        targetX = Math.Clamp(targetX, 10, environment.Width - 10);
        targetY = Math.Clamp(targetY, 10, environment.Height - 10);

        // Could add scoring based on distance to other units/known resources later
        return new Vector2(targetX, targetY);
    }

    public void UpdateUnits(Environment environment)
    {
        List<Unit> unitsToRemove = new List<Unit>();
        foreach (var unit in Units)
        {
            // Check age/health before state updates
            if (unit.UpdateAge(this) || unit.Health <= 0)
            {
                unit.State = UnitState.DEAD; // Ensure state is DEAD
            }

            if (unit.State == UnitState.DEAD)
            {
                unitsToRemove.Add(unit);
                continue;
            }

            // --- State Machine Logic ---
            object? currentTarget = unit.Target; // Cache target
            Vector2 targetPosition;

            switch (unit.State)
            {
                case UnitState.IDLE:
                    // Decision logic should assign a task if idle
                    break;

                case UnitState.SCOUTING:
                    if (currentTarget is Vector2 scoutPos)
                    {
                        bool reached = unit.MoveTowards(scoutPos, environment);
                        // Check for nearby resources while scouting
                        var foundResource = environment.ResourceNodes
                            .FirstOrDefault(r => !r.IsDepleted() && unit.DistanceTo(r.Position) < unit.SightRange);
                        if (foundResource != null)
                        {
                            ReportResourceLocation(foundResource.Position, foundResource.ResourceAmount);
                            unit.Target = foundResource;
                            unit.State = UnitState.MOVING;
                        }
                        else if (reached)
                        {
                            unit.State = UnitState.IDLE; // Reached scout point
                        }
                    }
                    else unit.State = UnitState.IDLE; // Invalid target
                    break;

                case UnitState.MOVING:
                    if (currentTarget is ResourceNode resNodeTarget) targetPosition = resNodeTarget.Position;
                    else if (currentTarget is Unit unitTarget) targetPosition = unitTarget.Position;
                    else if (currentTarget is Vector2 posTarget) targetPosition = posTarget;
                    else { unit.State = UnitState.IDLE; break; } // Invalid target

                    bool reachedMoveTarget = unit.MoveTowards(targetPosition, environment);

                    if (reachedMoveTarget)
                    {
                        if (currentTarget is ResourceNode rn) unit.State = rn.IsDepleted() ? UnitState.IDLE : UnitState.GATHERING;
                        else if (currentTarget is Unit ut) unit.State = (ut.SwarmId != Id && ut.State != UnitState.DEAD) ? UnitState.ATTACKING : UnitState.IDLE;
                        else unit.State = UnitState.IDLE; // Reached a position
                    }
                    break;

                case UnitState.GATHERING:
                    if (currentTarget is ResourceNode resNodeGather && !resNodeGather.IsDepleted())
                    {
                        bool stateChanged = unit.GatherResources(resNodeGather);
                        if (stateChanged && unit.State == UnitState.RETURNING)
                        {
                            unit.Target = BasePosition; // Set target for return trip
                        }
                    }
                    else unit.State = UnitState.IDLE; // Target invalid or depleted
                    break;

                case UnitState.RETURNING:
                     if (currentTarget is Vector2 basePos) // Target should be base position
                     {
                         bool reachedBase = unit.MoveTowards(basePos, environment);
                         if (reachedBase)
                         {
                             unit.DepositResources(this); // Handles state change to IDLE
                         }
                     }
                     else // Should always be returning to base position
                     {
                         unit.Target = BasePosition; // Correct the target
                         bool reachedBase = unit.MoveTowards(BasePosition, environment);
                          if (reachedBase) unit.DepositResources(this);
                     }
                    break;

                case UnitState.ATTACKING:
                    if (currentTarget is Unit enemyUnit && enemyUnit.State != UnitState.DEAD)
                    {
                        if (unit.DistanceTo(enemyUnit.Position) <= unit.AttackRange)
                        {
                            bool killed = unit.Attack(enemyUnit);
                            if (killed) unit.State = UnitState.IDLE; // Find new target
                        }
                        else
                        {
                            // Move closer if out of range
                            unit.MoveTowards(enemyUnit.Position, environment);
                        }
                    }
                    else unit.State = UnitState.IDLE; // Target dead or invalid
                    break;
            }
        }

        // Handle deaths after iterating
        foreach (var deadUnit in unitsToRemove)
        {
            HandleUnitDeath(deadUnit);
        }
    }

    private void HandleUnitDeath(Unit unit)
    {
        if (Units.Contains(unit))
        {
            var unitKnowledge = unit.ShareKnowledge();
            double experienceGained = unit.Age / 100.0 + unit.ResourcesCollected / 20.0;
            ExperiencePoints += experienceGained;

            // Transfer knowledge to collective
            foreach (var kvp in unitKnowledge)
            {
                 if (CollectiveKnowledge.TryGetValue(kvp.Key, out var knowledge))
                 {
                     knowledge.ReportedAmount = Math.Max(knowledge.ReportedAmount, kvp.Value.Amount);
                     knowledge.Confirmations++; // Increment confirmations on death report
                 }
                 else
                 {
                     // Add new if unit found something unique before dying
                     CollectiveKnowledge[kvp.Key] = new CollectiveResourceKnowledge
                     {
                         ReportedAmount = kvp.Value.Amount,
                         LastSeen = DateTime.UtcNow, // Use current time for death report
                         Confirmations = 1
                     };
                 }
            }

            TotalDeaths++;
            Units.Remove(unit);
            Console.WriteLine($"INFO: Unit from {Id} died. Total deaths: {TotalDeaths}");

            // Attempt to replace the unit (optional)
            if (AvailableUnitTypes.Contains(unit.Type) && CanAffordUnit(unit.Type) && Units.Count < 50)
            {
                if (Rng.NextDouble() < 0.7) // 70% chance to replace
                {
                    SpawnUnit(unit.Type, unit.Role); // Spawn replacement with same role
                }
            }
        }
    }

     private Situation AssessSituation(Environment environment)
    {
        var situation = new Situation();
        double baseSightRange = 50.0; // Increased range for base awareness

        foreach (var unit in Units)
        {
            situation.UnitComposition[unit.Role]++; // Count by role
        }

        foreach (var otherUnit in environment.GetAllUnits())
        {
            if (otherUnit.SwarmId != Id)
            {
                double distToBase = Vector2.Distance(otherUnit.Position, BasePosition);
                if (distToBase < baseSightRange)
                {
                    situation.NearbyEnemies.Add(otherUnit);
                    situation.EnemyStrength += otherUnit.AttackPower + otherUnit.Health / 10.0;
                    if (distToBase < baseSightRange / 2.0)
                    {
                        situation.UnderAttack = true;
                    }
                }
            }
        }

        foreach (var resource in environment.ResourceNodes)
        {
            double distToBase = Vector2.Distance(resource.Position, BasePosition);
            if (distToBase < baseSightRange * 2.0) // Wider range for resource awareness
            {
                situation.NearbyResources.Add(resource);
                situation.ResourceAvailability += resource.ResourceAmount;
            }
        }
        return situation;
    }


    // Helper classes for knowledge tracking
    public class ResourceSighting
    {
        public double ReportedAmount { get; set; }
        public DateTime LastSeen { get; set; }
    }

    public class CollectiveResourceKnowledge : ResourceSighting
    {
        public int Confirmations { get; set; }
    }

     // Helper class for situation assessment
    private class Situation
    {
        public List<Unit> NearbyEnemies { get; } = new List<Unit>();
        public List<ResourceNode> NearbyResources { get; } = new List<ResourceNode>();
        public double EnemyStrength { get; set; }
        public double ResourceAvailability { get; set; }
        public bool UnderAttack { get; set; }
        public Dictionary<string, int> UnitComposition { get; } = new Dictionary<string, int> {
            {"basic", 0}, {"gatherer", 0}, {"combat", 0}, {"scout", 0}, {"defender", 0}, {"unknown", 0} // Initialize all expected roles
        };
    }
}
