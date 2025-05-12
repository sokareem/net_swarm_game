using System;

namespace BeeSwarmGame;

public class ResourceNode
{
    public Vector2 Position { get; }
    public double InitialAmount { get; }
    public double ResourceAmount { get; private set; }
    public string ResourceType { get; }

    public ResourceNode(Vector2 position, double resourceAmount, string resourceType = "standard")
    {
        Position = position;
        InitialAmount = resourceAmount;
        ResourceAmount = resourceAmount;
        ResourceType = resourceType;
    }

    public double ExtractResources(double amount)
    {
        double extracted = Math.Min(amount, ResourceAmount);
        ResourceAmount -= extracted;
        return extracted;
    }

    public bool IsDepleted()
    {
        return ResourceAmount <= 0;
    }

    public double GetDepletionPercentage()
    {
        if (InitialAmount == 0)
        {
            return 100.0;
        }
        return 100.0 * (1.0 - ResourceAmount / InitialAmount);
    }

    // Optional: Add reference equality or specific ID if needed later
}
