using System;
using System.Collections.Generic;
using System.Linq;

namespace BeeSwarmGame;

public class Unit
{
    private static readonly Random Rng = new Random();
    private UnitState _state;

    // Base properties
    public string SwarmId { get; }
    public Vector2 Position { get; set; }
    public string Type { get; } // "basic", "gatherer", "combat"
    public string Role { get; set; } // "basic", "gatherer", "combat", "scout"

    // Stats (calculated based on swarm upgrades)
    public double Health { get; set; }
    public double Speed { get; set; }
    public double ResourceCapacity { get; set; }
    public double CurrentResources { get; set; }
    public double AttackPower { get; set; }
    public double AttackRange { get; set; }
    public double SightRange { get; set; }
    public double GatherRate { get; set; }

    // Current state and target
    public object? Target { get; set; } // Can be Vector2, ResourceNode, or Unit
    public Vector2? LastKnownResourcePos { get; set; }
    private int AvoidanceCooldown { get; set; }
    private List<Vector2> PrevPositions { get; } = new List<Vector2>();
    private int StuckCount { get; set; }

    // Life cycle attributes
    public int Age { get; private set; }
    public int MaxAge { get; }
    public Dictionary<Vector2, ResourceKnowledge> Knowledge { get; } = new Dictionary<Vector2, ResourceKnowledge>();
    public double ResourcesCollected { get; private set; }

    // Bee-like movement properties
    private double OscillationPhase { get; set; }
    private double OscillationAmplitude { get; set; }
    private double OscillationFrequency { get; set; }
    public Vector2 Momentum { get; set; } // Public for swarm coordination access
    private int HoverCooldown { get; set; }
    public bool CanFlyOver { get; set; } // Public for environment check access

    public UnitState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                // Consider adding logging here if needed
                // Console.WriteLine($"DEBUG: Unit({SwarmId}) state {_state} -> {value}");
                _state = value;
            }
        }
    }

    public Unit(string swarmId, Vector2 position, string unitType = "basic")
    {
        SwarmId = swarmId;
        Position = position;
        Type = unitType;
        Role = unitType; // Default role is the type

        MaxAge = Rng.Next(2000, 3001);
        OscillationPhase = Rng.NextDouble() * 2 * Math.PI;
        OscillationAmplitude = Rng.NextDouble() * (0.7 - 0.2) + 0.2;
        OscillationFrequency = Rng.NextDouble() * (0.3 - 0.1) + 0.1;
        Momentum = Vector2.Zero;

        State = UnitState.IDLE;
    }

    public bool MoveTowards(Vector2 targetPosition, Environment environment)
    {
        if (AvoidanceCooldown > 0) AvoidanceCooldown--;

        bool movedThisTick = false;
        bool reachedTarget = false;

        try
        {
            Vector2 direction = targetPosition - Position;
            double distance = direction.Length();

            if (distance < 1e-6) // Use a small tolerance for floating point comparison
            {
                reachedTarget = true;
                movedThisTick = false;
            }
            else
            {
                Vector2 normDir = direction / distance; // Normalized direction

                // --- Bee-like Movement ---
                OscillationPhase += OscillationFrequency;
                Vector2 perpDir = new Vector2(-normDir.Y, normDir.X); // Perpendicular direction
                double oscillationFactor = 0;
                if (distance > Speed * 3 && Speed > 0.3)
                {
                    oscillationFactor = Math.Min(1.0, distance / 50.0) * OscillationAmplitude;
                }

                if (HoverCooldown <= 0 && Rng.NextDouble() < 0.01)
                {
                    HoverCooldown = Rng.Next(3, 9);
                    oscillationFactor *= 2.0;
                }

                Vector2 moveDir;
                if (HoverCooldown > 0)
                {
                    HoverCooldown--;
                    moveDir = normDir * 0.1; // Minimal movement during hover
                }
                else
                {
                    double oscillation = Math.Sin(OscillationPhase) * oscillationFactor;
                    Vector2 oscillatedDir = normDir + perpDir * oscillation;
                    // Apply momentum
                    moveDir = Momentum * 0.75 + oscillatedDir * 0.25;
                    moveDir.Normalize(); // Ensure the final direction is normalized
                    Momentum = moveDir; // Update momentum
                }
                // --- End Bee-like Movement ---

                Vector2 nextPos = Position + moveDir * Speed;

                // --- Obstacle Avoidance ---
                int avoidMargin = 5;
                Vector2 lookaheadPos = Position + moveDir * (Speed * 2);
                bool nextPosInvalid = !environment.IsPositionValid(nextPos, avoidMargin, this.CanFlyOver);
                bool lookaheadInvalid = !environment.IsPositionValid(lookaheadPos, avoidMargin, this.CanFlyOver);

                if (nextPosInvalid || (lookaheadInvalid && AvoidanceCooldown == 0))
                {
                    // Simplified avoidance: Try a few angles
                    bool foundAlternative = false;
                    double[] avoidanceAngles = { Math.PI / 4, -Math.PI / 4, Math.PI / 2, -Math.PI / 2, Math.PI }; // Radians

                    foreach (double angleOffset in avoidanceAngles)
                    {
                        double currentAngle = Math.Atan2(moveDir.Y, moveDir.X);
                        double newAngle = currentAngle + angleOffset;
                        Vector2 altDir = new Vector2(Math.Cos(newAngle), Math.Sin(newAngle));
                        Vector2 altNextPos = Position + altDir * Speed;

                        if (environment.IsPositionValid(altNextPos, avoidMargin, this.CanFlyOver))
                        {
                            Position = altNextPos;
                            Momentum = altDir; // Update momentum to the avoidance direction
                            AvoidanceCooldown = 5;
                            movedThisTick = true;
                            foundAlternative = true;
                            // Console.WriteLine($"DEBUG: Unit({SwarmId}) avoiding obstacle.");
                            break;
                        }
                    }
                    if (!foundAlternative)
                    {
                        // Blocked, don't move
                        movedThisTick = false;
                        // Console.WriteLine($"DEBUG: Unit({SwarmId}) blocked by obstacle.");
                    }
                }
                else
                {
                    // No collision or avoidance needed
                    if (distance <= Speed)
                    {
                        Position = targetPosition;
                        reachedTarget = true;
                    }
                    else
                    {
                        Position = nextPos;
                    }
                    movedThisTick = true;
                }
            }
        }
        catch (Exception ex) // Catch potential errors like invalid target
        {
            Console.WriteLine($"ERROR: Unit({SwarmId}) move_towards error: {ex.Message}. Target: {Target}");
            State = UnitState.IDLE; // Reset state
            return false;
        }

        // --- Stuck Detection ---
        if (movedThisTick)
        {
            PrevPositions.Add(Position);
            if (PrevPositions.Count > 5) PrevPositions.RemoveAt(0);

            // Check if movement is minimal over the last 5 ticks
            if (PrevPositions.Count == 5 &&
                PrevPositions.Zip(PrevPositions.Skip(1), Vector2.Distance).All(d => d < Speed * 0.1))
            {
                StuckCount++;
            }
            else
            {
                StuckCount = 0; // Reset if moved significantly
            }
        }
        else if (!reachedTarget) // Didn't move and not at target
        {
            StuckCount++;
        }

        if (StuckCount > 3)
        {
            double detourX = Position.X + Rng.NextDouble() * 70 - 35;
            double detourY = Position.Y + Rng.NextDouble() * 70 - 35;
            detourX = Math.Clamp(detourX, 0, environment.Width);
            detourY = Math.Clamp(detourY, 0, environment.Height);

            Vector2? detourTarget = environment.FindNearestValidPosition(new Vector2(detourX, detourY), Position);

            if (detourTarget.HasValue && Vector2.Distance(Position, detourTarget.Value) > Speed * 2)
            {
                Console.WriteLine($"INFO: Unit({SwarmId}) STUCK! Detouring to {detourTarget.Value}");
                Target = detourTarget.Value; // Target becomes the detour position
                StuckCount = 0;
                PrevPositions.Clear();
                // State remains MOVING or SCOUTING
                return false; // Not at the *original* target
            }
            else
            {
                 Console.WriteLine($"WARNING: Unit({SwarmId}) stuck, failed to find suitable detour.");
                 // Don't reset stuck count, try again next tick
            }
        }
        // --- End Stuck Detection ---

        return reachedTarget;
    }

    public bool GatherResources(ResourceNode resourceNode)
    {
        LastKnownResourcePos = resourceNode.Position;

        double amountToGather = Math.Min(GatherRate, ResourceCapacity - CurrentResources);
        double extracted = resourceNode.ExtractResources(amountToGather);
        CurrentResources += extracted;
        ResourcesCollected += extracted;

        if (extracted > 0)
        {
            LearnResourceLocation(resourceNode.Position, resourceNode.ResourceAmount);
            // Console.WriteLine($"DEBUG: Unit({SwarmId}) gathered {extracted:F0} resources from {resourceNode.Position}");
        }

        if (CurrentResources >= ResourceCapacity)
        {
            State = UnitState.RETURNING;
            // Console.WriteLine($"DEBUG: Unit({SwarmId}) is full, returning to base");
            return true; // Indicate state change
        }

        if (resourceNode.IsDepleted())
        {
            Console.WriteLine($"INFO: Resource node at {resourceNode.Position} depleted");
            State = UnitState.IDLE;
            LastKnownResourcePos = null;
            return true; // Indicate state change
        }

        return false; // Continue gathering
    }

    public void DepositResources(Swarm swarm)
    {
        if (CurrentResources > 0)
        {
            double depositedAmount = CurrentResources;
            swarm.AddResources(depositedAmount);
            // Console.WriteLine($"DEBUG: Unit({SwarmId}) deposited {depositedAmount:F0} resources. Swarm total: {swarm.TotalResources:F0}");

            if (LastKnownResourcePos.HasValue)
            {
                swarm.ReportResourceLocation(LastKnownResourcePos.Value, depositedAmount);
                if (depositedAmount > ResourceCapacity * 0.75)
                {
                     Console.WriteLine($"INFO: {SwarmId} unit reported rich resource at {LastKnownResourcePos.Value}");
                }
            }
            CurrentResources = 0;
        }

        LastKnownResourcePos = null;
        State = UnitState.IDLE;
    }

    public bool Attack(Unit targetUnit)
    {
        if (Vector2.Distance(Position, targetUnit.Position) <= AttackRange)
        {
            double damage = AttackPower; // Add randomness later if needed
            targetUnit.Health -= damage;
            // Console.WriteLine($"DEBUG: Unit({SwarmId}) attacked Unit({targetUnit.SwarmId}) for {damage:F0}. Target health: {targetUnit.Health:F0}");

            if (targetUnit.Health <= 0)
            {
                Console.WriteLine($"INFO: Unit({SwarmId}) killed Unit({targetUnit.SwarmId})");
                targetUnit.State = UnitState.DEAD;
                return true; // Target died
            }
        }
        return false; // Target still alive or out of range
    }

    public double DistanceTo(Vector2 position)
    {
        return Vector2.Distance(Position, position);
    }

    public void UpdateStats(Swarm swarm)
    {
        // Apply swarm upgrades - Using Swarm's GetUnitStat method
        Health = swarm.GetUnitStat(Type, "health");
        Speed = swarm.GetUnitStat(Type, "speed");
        ResourceCapacity = swarm.GetUnitStat(Type, "resource_capacity");
        AttackPower = swarm.GetUnitStat(Type, "attack_power");
        AttackRange = swarm.GetUnitStat(Type, "attack_range");
        SightRange = swarm.GetUnitStat(Type, "sight_range");
        GatherRate = swarm.GetUnitStat(Type, "gather_rate");

        // Determine flight capability based on type (simplified)
        double flyChance = Type switch
        {
            "gatherer" => 0.5,
            "combat" => 0.4,
            _ => 0.3 // basic
        };
        CanFlyOver = Rng.NextDouble() < flyChance;

        // Adjust oscillation based on type
        if (Type == "gatherer")
        {
            OscillationAmplitude *= 0.8;
            OscillationFrequency *= 0.9;
        }
        else if (Type == "combat")
        {
            OscillationAmplitude *= 1.2;
            OscillationFrequency *= 1.2;
        }
    }

    public bool UpdateAge(Swarm swarm)
    {
        Age++;

        // Natural death check (simplified probability)
        double maxHealth = swarm.GetUnitStat(Type, "health");
        double healthFactor = maxHealth > 0 ? Health / maxHealth : 0;
        double ageFactor = (double)Age / MaxAge;

        double deathProb = 0.0001; // Base probability
        if (ageFactor > 0.7) deathProb *= (1 + (ageFactor - 0.7) * 10);
        if (healthFactor < 0.5) deathProb *= (2 - healthFactor);

        if (Rng.NextDouble() < deathProb)
        {
            Console.WriteLine($"INFO: Unit({SwarmId}) died of natural causes at age {Age}");
            Health = 0; // Ensure health is 0
            State = UnitState.DEAD;
            return true; // Died naturally
        }
        return false; // Survived
    }

    public void LearnResourceLocation(Vector2 position, double amount)
    {
        Knowledge[position] = new ResourceKnowledge { Amount = amount, LastSeen = Age };
    }

    public Dictionary<Vector2, ResourceKnowledge> ShareKnowledge()
    {
        // Return knowledge seen recently
        return Knowledge.Where(kvp => (Age - kvp.Value.LastSeen) < 500)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    // Helper struct for knowledge dictionary
    public struct ResourceKnowledge
    {
        public double Amount;
        public int LastSeen;
    }
}
