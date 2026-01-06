namespace Pokebar.Core;

public static class DexConstants
{
    public const int MinDex = 1;
    public const int MaxDex = 1025;
    public const int TotalDex = MaxDex - MinDex + 1;

    public static bool IsValid(int dex) => dex >= MinDex && dex <= MaxDex;
}
