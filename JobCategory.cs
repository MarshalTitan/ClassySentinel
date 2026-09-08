namespace ClassySentinel;

public enum JobCategory
{
    Tank,
    Healer,
    MeleeDps,
    PhysicalRangedDps,
    MagicalRangedDps,
    LimitedJobs,
    Crafters,
    Gatherers,
    OtherNewJobs,
}

public static class JobCategoryExtensions
{
    public static string DisplayName(this JobCategory category) => category switch
    {
        JobCategory.Tank => "Tank",
        JobCategory.Healer => "Healer",
        JobCategory.MeleeDps => "Melee DPS",
        JobCategory.PhysicalRangedDps => "Physical Ranged DPS",
        JobCategory.MagicalRangedDps => "Magical Ranged DPS",
        JobCategory.LimitedJobs => "Limited Jobs",
        JobCategory.Crafters => "Crafters",
        JobCategory.Gatherers => "Gatherers",
        _ => "Other / New Jobs",
    };
}

