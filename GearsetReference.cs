namespace ClassySentinel;

[Serializable]
public sealed class GearsetReference : IEquatable<GearsetReference>
{
    public int GearsetId { get; set; }

    public uint ClassJobId { get; set; }

    public string GearsetName { get; set; } = string.Empty;

    public static GearsetReference From(GearsetInfo gearset)
        => new()
        {
            GearsetId = gearset.GearsetId,
            ClassJobId = gearset.ClassJobId,
            GearsetName = gearset.GearsetName,
        };

    public bool Matches(GearsetInfo gearset)
        => GearsetId == gearset.GearsetId
           && ClassJobId == gearset.ClassJobId
           && string.Equals(GearsetName ?? string.Empty, gearset.GearsetName, StringComparison.Ordinal);

    public bool Equals(GearsetReference? other)
        => other is not null
           && GearsetId == other.GearsetId
           && ClassJobId == other.ClassJobId
           && string.Equals(GearsetName ?? string.Empty, other.GearsetName ?? string.Empty, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as GearsetReference);

    public override int GetHashCode()
        => HashCode.Combine(GearsetId, ClassJobId, StringComparer.Ordinal.GetHashCode(GearsetName ?? string.Empty));

    public string ImGuiId => $"{ClassJobId}-{GearsetId}";
}
