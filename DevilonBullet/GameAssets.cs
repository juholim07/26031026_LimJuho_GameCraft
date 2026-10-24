using Vortice.Mathematics;

sealed partial class GameMain
{
    private readonly Dictionary<string, G2Texture> _sprites = new();

    private void LoadSprites()
    {
        // Resolve from the executable directory, independently of the working directory.
        string root = Path.Combine(AppContext.BaseDirectory, "resource", "Temporary");
        var names = new List<string>
        {
            "sky", "moon", "buildings", "pillars", "ground", "ground_tile", "start_flag",
            "platform_210", "platform_230", "platform_240", "platform_260",
            "portal_open", "portal_locked", "player_gun"
        };
        for (int frame = 0; frame < 8; frame++) names.Add($"torch_{frame}");
        foreach (string facing in new[] { "left", "right" })
        {
            for (int frame = 0; frame < 8; frame++) names.Add($"player_{facing}_{frame}");
            foreach (string kind in new[] { "grunt", "gunner", "lord" })
            {
                names.Add($"{kind}_{facing}");
                names.Add($"{kind}_{facing}_flash");
            }
        }
        foreach (string name in names)
            _sprites.Add(name, new G2Texture(Path.Combine(root, name + ".png")));
    }

    private void Sprite(string name, float x, float y, float width, float height) =>
        _sprites[name].Draw(new Rect(x, y, width, height));

    private static int AnimationFrame(float angle) =>
        ((int)MathF.Floor(angle / MathF.Tau * 8 + 0.5f) % 8 + 8) % 8;
}
