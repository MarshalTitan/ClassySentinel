namespace ClassySentinel;

internal static class GearsetReferenceLookup
{
    public static GearsetInfo? FindExact(
        IReadOnlyList<GearsetInfo> gearsets,
        GearsetReference? reference)
    {
        if (!GearsetReference.IsValidSavedReference(reference))
            return null;

        return gearsets.FirstOrDefault(reference.Matches);
    }
}
