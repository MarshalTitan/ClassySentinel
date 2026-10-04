namespace ClassySentinel;

public static class ThemeMigrationPolicy
{
    public const int CurrentConfigurationVersion = 5;
    public const int Classic = 0;
    public const int Modern = 1;

    public static bool Apply(ref int configurationVersion, ref int theme)
    {
        // Version 3 still needs live gear-set data before its version 4 migration
        // can complete. Do not skip that migration just to add a theme value.
        if (configurationVersion < 4)
            return false;

        if (configurationVersion == 4)
        {
            theme = theme is Classic or Modern ? theme : Classic;
            configurationVersion = CurrentConfigurationVersion;
            return true;
        }

        var normalizedTheme = theme is Classic or Modern ? theme : Classic;
        if (theme == normalizedTheme)
            return false;

        theme = normalizedTheme;
        return true;
    }
}
