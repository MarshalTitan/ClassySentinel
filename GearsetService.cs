using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;

namespace ClassySentinel;

public sealed class GearsetService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private readonly IDataManager dataManager;
    private readonly IPlayerState playerState;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private DateTime nextRefreshUtc;
    private IReadOnlyList<GearsetInfo> gearsets = Array.Empty<GearsetInfo>();

    public GearsetService(
        IDataManager dataManager,
        IPlayerState playerState,
        IChatGui chatGui,
        IPluginLog log,
        Configuration configuration)
    {
        this.dataManager = dataManager;
        this.playerState = playerState;
        this.chatGui = chatGui;
        this.log = log;
        this.configuration = configuration;
    }

    public IReadOnlyList<GearsetInfo> Gearsets => gearsets;

    public IEnumerable<IGrouping<uint, GearsetInfo>> JobsIn(JobCategory category)
        => gearsets
            .Where(x => x.Category == category)
            .GroupBy(x => x.ClassJobId)
            .OrderBy(x => x.Min(y => y.UiPriority))
            .ThenBy(x => x.Key);

    public void RefreshIfDue() => Refresh(false);

    public void ForceRefresh() => Refresh(true);

    public GearsetInfo? GetDefault(uint classJobId)
    {
        var matching = gearsets.Where(x => x.ClassJobId == classJobId).ToArray();
        if (matching.Length == 0)
            return null;

        if (configuration.DefaultGearsets.TryGetValue(classJobId, out var configuredId))
        {
            var configured = matching.FirstOrDefault(x => x.GearsetId == configuredId);
            if (configured is not null)
                return configured;
        }

        return matching.OrderBy(x => x.GearsetId).First();
    }

    public bool EquipDefault(uint classJobId)
    {
        var gearset = GetDefault(classJobId);
        if (gearset is null)
            return false;

        return Equip(gearset);
    }

    public unsafe bool Equip(GearsetInfo gearset)
    {
        var module = RaptureGearsetModule.Instance();
        if (!playerState.IsLoaded || module is null || !module->IsValidGearset(gearset.GearsetId))
        {
            chatGui.PrintError($"Classy Sentinel could not find gear set #{gearset.GearsetId + 1}.");
            ForceRefresh();
            return false;
        }

        var result = module->EquipGearset(gearset.GearsetId, 0);
        if (result == 0)
            return true;

        log.Warning("EquipGearset failed for gearset {GearsetId} ({GearsetName}) with result {Result}.", gearset.GearsetId, gearset.GearsetName, result);
        chatGui.PrintError($"Classy Sentinel could not equip '{gearset.GearsetName}'. The game may currently prevent gear changes.");
        return false;
    }

    private unsafe void Refresh(bool force)
    {
        var now = DateTime.UtcNow;
        if (!force && now < nextRefreshUtc)
            return;

        nextRefreshUtc = now + RefreshInterval;

        if (!playerState.IsLoaded)
        {
            gearsets = Array.Empty<GearsetInfo>();
            return;
        }

        try
        {
            var module = RaptureGearsetModule.Instance();
            if (module is null)
            {
                gearsets = Array.Empty<GearsetInfo>();
                return;
            }

            var classJobs = dataManager.GetExcelSheet<ClassJob>();
            var discovered = new List<GearsetInfo>();
            var entries = module->Entries;

            // The native collection length is authoritative; do not assume a fixed cap.
            for (var index = 0; index < entries.Length; index++)
            {
                ref var entry = ref entries[index];
                if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0)
                    continue;

                var classJobId = (uint)entry.ClassJob;
                var icon = module->GetClassJobIconForGearset(entry.Id);
                var gearsetName = string.IsNullOrWhiteSpace(entry.NameString)
                    ? $"Gear Set {entry.Id + 1}"
                    : entry.NameString;

                if (classJobs.TryGetRow(classJobId, out var classJob))
                {
                    discovered.Add(new GearsetInfo(
                        entry.Id,
                        classJobId,
                        gearsetName,
                        classJob.Name.ToString(),
                        classJob.Abbreviation.ToString(),
                        icon > 0 ? (uint)icon : 0,
                        entry.ItemLevel,
                        classJob.UIPriority,
                        JobClassifier.Classify(classJob)));
                }
                else
                {
                    // Patch-day resilience: preserve the gearset even if Lumina has not
                    // yet learned about the new ClassJob row.
                    discovered.Add(new GearsetInfo(
                        entry.Id,
                        classJobId,
                        gearsetName,
                        $"ClassJob {classJobId}",
                        $"CJ{classJobId}",
                        icon > 0 ? (uint)icon : 0,
                        entry.ItemLevel,
                        byte.MaxValue,
                        JobCategory.OtherNewJobs));
                }
            }

            gearsets = discovered
                .OrderBy(x => x.Category)
                .ThenBy(x => x.UiPriority)
                .ThenBy(x => x.ClassJobId)
                .ThenBy(x => x.GearsetId)
                .ToArray();

        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to discover FFXIV gearsets.");
        }
    }
}
