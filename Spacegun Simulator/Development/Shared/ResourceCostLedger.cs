using Spacegun_Simulator.Economy;

namespace Spacegun_Simulator.Development.Shared;

public static class ResourceCostLedger
{
	// Keys used in GameState.AccumulatedResources (legacy naming preserved).
	private const string KeyBudget = "Budget";
	private const string KeySteel = "Steel";
	private const string KeyPowerCells = "PowerCells";
	private const string KeySpecializedAlloys = "SpecializedAlloys";
	private const string KeyRareEarthElements = "RareEarthElements";
	private const string KeyAdvancedOre = "AdvancedOre";
	private const string KeyExotic = "Exotic";

	private static double Get(IDictionary<string, double> resources, string key)
		=> resources.TryGetValue(key, out var value) ? value : 0;

	public static void EnsureKeys(IDictionary<string, double> resources)
	{
		Ensure(resources, KeyBudget);
		Ensure(resources, KeySteel);
		Ensure(resources, KeyPowerCells);
		Ensure(resources, KeySpecializedAlloys);
		Ensure(resources, KeyRareEarthElements);
		Ensure(resources, KeyAdvancedOre);
		Ensure(resources, KeyExotic);

		static void Ensure(IDictionary<string, double> dict, string key)
		{
			if (!dict.ContainsKey(key))
				dict[key] = 0;
		}
	}

	public static bool CanAfford(IDictionary<string, double> resources, ResourceCost cost)
	{
		if (cost is null)
			return true;

		return Get(resources, KeyBudget) >= cost.Budget
			&& Get(resources, KeySteel) >= cost.Steel
			&& Get(resources, KeyPowerCells) >= cost.PowerCells
			&& Get(resources, KeySpecializedAlloys) >= cost.SpecializedAlloys
			&& Get(resources, KeyRareEarthElements) >= cost.RareEarthElements
			&& Get(resources, KeyAdvancedOre) >= cost.AdvancedOre
			&& Get(resources, KeyExotic) >= cost.ExoticMaterials;
	}

	public static void Spend(IDictionary<string, double> resources, ResourceCost cost)
	{
		if (cost is null)
			return;

		if (!CanAfford(resources, cost))
			throw new InvalidOperationException("Insufficient resources to spend cost.");

		resources[KeyBudget] = Get(resources, KeyBudget) - cost.Budget;
		resources[KeySteel] = Get(resources, KeySteel) - cost.Steel;
		resources[KeyPowerCells] = Get(resources, KeyPowerCells) - cost.PowerCells;
		resources[KeySpecializedAlloys] = Get(resources, KeySpecializedAlloys) - cost.SpecializedAlloys;
		resources[KeyRareEarthElements] = Get(resources, KeyRareEarthElements) - cost.RareEarthElements;
		resources[KeyAdvancedOre] = Get(resources, KeyAdvancedOre) - cost.AdvancedOre;
		resources[KeyExotic] = Get(resources, KeyExotic) - cost.ExoticMaterials;
	}

	public static string FormatCost(ResourceCost cost)
	{
		if (cost is null)
			return "Free";

		var parts = new List<string>(8);
		Add(parts, cost.Budget, "Budget");
		Add(parts, cost.Steel, "Steel");
		Add(parts, cost.PowerCells, "Power Cells");
		Add(parts, cost.SpecializedAlloys, "Specialized Alloys");
		Add(parts, cost.RareEarthElements, "Rare Earth");
		Add(parts, cost.AdvancedOre, "Advanced Ore");
		Add(parts, cost.ExoticMaterials, "Exotic");

		return parts.Count == 0 ? "Free" : string.Join(", ", parts);

		static void Add(List<string> parts, double value, string label)
		{
			if (value <= 0) return;
			parts.Add($"{value:F0} {label}");
		}
	}

	/// <summary>
	/// Returns true if the cost only contains resources allowed for the given MK tier.
	/// Tier mapping:
	/// - MKI (1): Budget, Steel, Power Cells
	/// - MKII (2): + Specialized Alloys, Rare Earth Elements
	/// - MKIII (3): + Advanced Ore, Exotic Materials
	/// </summary>
	public static bool IsCostAllowedForMkTier(ResourceCost cost, int mkTier)
	{
		if (cost is null)
			return true;

		mkTier = Math.Clamp(mkTier, 1, 3);

		if (mkTier <= 1)
			return cost.SpecializedAlloys <= 0
				&& cost.RareEarthElements <= 0
				&& cost.AdvancedOre <= 0
				&& cost.ExoticMaterials <= 0;

		if (mkTier == 2)
			return cost.AdvancedOre <= 0
				&& cost.ExoticMaterials <= 0;

		return true;
	}

	/// <summary>
	/// Returns true if the tech tree currently allows gathering all resources required by this cost.
	/// This prevents offering items that are impossible to purchase due to locked resources.
	/// </summary>
	public static bool AreAllRequiredResourcesUnlocked(ResourceCost cost, Spacegun_Simulator.Development.Technology.TechTree techTree)
	{
		if (cost is null)
			return true;

		if (techTree is null)
			return false;

		bool Unlocked(ResourceType type) => techTree.GetProductionBonus(type) > 0;

		if (cost.Budget > 0 && !Unlocked(ResourceType.Budget)) return false;
		if (cost.Steel > 0 && !Unlocked(ResourceType.Steel)) return false;
		if (cost.PowerCells > 0 && !Unlocked(ResourceType.PowerCells)) return false;
		if (cost.SpecializedAlloys > 0 && !Unlocked(ResourceType.SpecializedAlloys)) return false;
		if (cost.RareEarthElements > 0 && !Unlocked(ResourceType.RareEarthElements)) return false;
		if (cost.AdvancedOre > 0 && !Unlocked(ResourceType.AdvancedOre)) return false;
		if (cost.ExoticMaterials > 0 && !Unlocked(ResourceType.ExoticMaterials)) return false;

		return true;
	}
}
