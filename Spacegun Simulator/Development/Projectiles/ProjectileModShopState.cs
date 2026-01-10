namespace Spacegun_Simulator.Development.Projectiles
{
    /// <summary>
    /// Wave-refresh "roguelike" mod shop state for projectile enhancement modules.
    ///
    /// - Offers refresh once per wave.
    /// - Offers are generated strictly from the current Projectiles tech level.
    /// - Owned modules persist for the campaign.
    /// </summary>
    public sealed class ProjectileModShopState
    {
        public int OffersWaveNumber { get; set; } = 0;

        public HashSet<string> OwnedCoreIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> OwnedPropulsionIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> OwnedGuidanceModuleIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> OwnedPayloadModuleIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> OwnedArmorModuleIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> CoreOfferIds { get; } = new();
        public List<string> PropulsionOfferIds { get; } = new();

        public List<string> GuidanceOfferModuleIds { get; } = new();
        public List<string> PayloadOfferModuleIds { get; } = new();
        public List<string> ArmorOfferModuleIds { get; } = new();

        public void EnsureBaselineComponentsAreOwned(string baselineCoreId, string propulsionNoneId)
        {
            if (!string.IsNullOrWhiteSpace(baselineCoreId)) OwnedCoreIds.Add(baselineCoreId);
            if (!string.IsNullOrWhiteSpace(propulsionNoneId)) OwnedPropulsionIds.Add(propulsionNoneId);
        }

        public void EnsureNoneModulesAreOwned(string guidanceNoneId, string payloadNoneId, string armorNoneId)
        {
            if (!string.IsNullOrWhiteSpace(guidanceNoneId)) OwnedGuidanceModuleIds.Add(guidanceNoneId);
            if (!string.IsNullOrWhiteSpace(payloadNoneId)) OwnedPayloadModuleIds.Add(payloadNoneId);
            if (!string.IsNullOrWhiteSpace(armorNoneId)) OwnedArmorModuleIds.Add(armorNoneId);
        }

        public bool IsOwned(ProjectileEnhancementSlot slot, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return false;

            return slot switch
            {
                ProjectileEnhancementSlot.Guidance => OwnedGuidanceModuleIds.Contains(moduleId),
                ProjectileEnhancementSlot.Payload => OwnedPayloadModuleIds.Contains(moduleId),
                ProjectileEnhancementSlot.Armor => OwnedArmorModuleIds.Contains(moduleId),
                _ => false,
            };
        }

        public bool TryAddOwned(ProjectileEnhancementSlot slot, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return false;

            return slot switch
            {
                ProjectileEnhancementSlot.Guidance => OwnedGuidanceModuleIds.Add(moduleId),
                ProjectileEnhancementSlot.Payload => OwnedPayloadModuleIds.Add(moduleId),
                ProjectileEnhancementSlot.Armor => OwnedArmorModuleIds.Add(moduleId),
                _ => false,
            };
        }

        public IReadOnlyList<string> GetOfferIds(ProjectileEnhancementSlot slot)
        {
            return slot switch
            {
                ProjectileEnhancementSlot.Guidance => GuidanceOfferModuleIds,
                ProjectileEnhancementSlot.Payload => PayloadOfferModuleIds,
                ProjectileEnhancementSlot.Armor => ArmorOfferModuleIds,
                _ => Array.Empty<string>(),
            };
        }

        public void ClearOffersForNewWave(int waveNumber)
        {
            OffersWaveNumber = waveNumber;
            CoreOfferIds.Clear();
            PropulsionOfferIds.Clear();
            GuidanceOfferModuleIds.Clear();
            PayloadOfferModuleIds.Clear();
            ArmorOfferModuleIds.Clear();
        }
    }
}
