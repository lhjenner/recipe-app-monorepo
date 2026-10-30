namespace RecipeApp.TestFixtures;

internal static class SyntheticImage
{
    // A generated, browser-decoded 1x1 green PNG, not a real recipe photo.
    public static byte[] Png => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4AWIynpn2HwAAAP//JItLpAAAAAZJREFUAwAEcQI0f7FoaQAAAABJRU5ErkJggg==");
}
