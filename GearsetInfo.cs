namespace ClassySentinel;

public sealed record GearsetInfo(
    int GearsetId,
    uint ClassJobId,
    string GearsetName,
    string JobName,
    string JobAbbreviation,
    uint IconId,
    short ItemLevel,
    byte UiPriority,
    JobCategory Category);

