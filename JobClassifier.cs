using Lumina.Excel.Sheets;

namespace ClassySentinel;

public static class JobClassifier
{
    // These are game-data category rows, not job IDs. New jobs inherit one of these
    // discipline categories from the ClassJob sheet without a plugin update.
    private const uint DisciplesOfTheLandCategoryId = 32;
    private const uint DisciplesOfTheHandCategoryId = 33;

    public static JobCategory Classify(ClassJob classJob)
    {
        if (classJob.IsLimitedJob)
            return JobCategory.LimitedJobs;

        if (classJob.ClassJobCategory.RowId == DisciplesOfTheHandCategoryId)
            return JobCategory.Crafters;

        if (classJob.ClassJobCategory.RowId == DisciplesOfTheLandCategoryId)
            return JobCategory.Gatherers;

        return (classJob.Role, classJob.PrimaryStat) switch
        {
            (1, _) => JobCategory.Tank,
            (4, _) => JobCategory.Healer,
            (2, _) => JobCategory.MeleeDps,
            (3, 2) => JobCategory.PhysicalRangedDps,
            (3, 4) => JobCategory.MagicalRangedDps,
            _ => JobCategory.OtherNewJobs,
        };
    }
}

