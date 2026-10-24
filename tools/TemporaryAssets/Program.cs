using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Offline asset export only. The game loads the checked-in PNGs and never runs this tool.
string output = args.Length == 1 ? Path.GetFullPath(args[0]) :
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../resource/Temporary"));
Directory.CreateDirectory(output);
int count = 0;

void Bake(string name, int width, int height, Action<Canvas> draw)
{
    using var canvas = new Canvas(width, height);
    draw(canvas);
    canvas.Save(Path.Combine(output, name + ".png"));
    count++;
}

Color ink = C(11, 14, 23), red = C(248, 99, 92), gold = C(240, 190, 104), cyan = C(108, 216, 225);

Bake("sky", 1280, 496, c =>
{
    for (int i = 0; i < 24; i++)
        c.Box(0, i * 21, 1280, 22, C(24 + i / 2, 23 + i / 5, 39 + i / 3));
});
Bake("moon", 192, 192, c =>
{
    c.Circle(96, 96, 95, 95, C(68, 43, 57, 0.4f));
    c.Circle(96, 96, 71, 71, C(152, 75, 78, 0.18f));
    c.Circle(96, 96, 68, 68, C(114, 64, 76));
    c.Circle(69, 78, 60, 62, C(32, 26, 44));
    c.Circle(96, 96, 93, 93, C(122, 77, 85, 0.3f), false);
});
Bake("buildings", 2880, 400, c =>
{
    c.Translate(0, -220);
    for (int i = 0; i < 18; i++)
    {
        float x = i * 160 + 8, h = 110 + i * 73 % 200;
        c.Box(x, 610 - h, 105, h, C(26, 28, 43));
        c.Poly(C(26, 28, 43), (x - 8, 610 - h), (x + 52, 570 - h), (x + 113, 610 - h));
        c.Line(x + 52, 570 - h, x + 52, 535 - h, C(42, 37, 53), 2);
        for (int r = 0; r < 4; r++)
            for (int col = 0; col < 3; col++)
                if ((i + r + col) % 3 != 0)
                    c.Box(x + 18 + col * 26, 635 - h + r * 35, 8, 14, C(96, 63, 68, 0.4f));
    }
});
Bake("pillars", 3600, 392, c =>
{
    c.Translate(0, -292);
    for (int i = 0; i < 10; i++)
    {
        float x = i * 350 + 8;
        c.Box(x, 292 + i % 3 * 30, 20, 330, C(35, 32, 47));
        c.Box(x - 8, 292 + i % 3 * 30, 36, 14, C(45, 38, 52));
        c.Line(x + 10, 320, x + 360, 355, C(59, 45, 60), 2);
        if (i % 2 == 0)
        {
            c.Box(x + 50, 361, 85, 117, C(66, 39, 53));
            c.Poly(C(66, 39, 53), (x + 50, 477), (x + 92, 501), (x + 135, 477));
            c.Circle(x + 92, 410, 18, 18, C(123, 65, 73), false, 2);
            c.Line(x + 92, 390, x + 92, 436, C(123, 65, 73), 2);
        }
    }
});
Bake("ground", 1280, 30, c =>
{
    c.Box(0, 0, 1280, 30, C(23, 25, 34));
    c.Box(0, 0, 1280, 3, C(111, 77, 78));
});
Bake("ground_tile", 62, 31, c =>
{
    c.Line(0, 7, 22, 23, C(41, 39, 48));
    c.Box(8, 0, 15, 3, C(184, 117, 94, 0.55f));
});
foreach (int width in new[] { 210, 230, 240, 260 })
    Bake($"platform_{width}", width, 50, c =>
    {
        c.Box(0, 0, width, 22, C(49, 46, 59));
        c.Box(0, 0, width, 3, C(150, 116, 122));
        c.Box(8, 22, width - 16, 6, C(26, 27, 38));
        for (float j = 22; j < width; j += 46)
            c.Line(j, 5, j - 6, 18, C(74, 61, 72), 2);
        c.Poly(C(37, 33, 47), (20, 22), (55, 49), (65, 22));
        c.Poly(C(37, 33, 47), (width - 70, 22), (width - 30, 48), (width - 20, 22));
    });
Bake("start_flag", 52, 76, c =>
{
    c.Line(3, 3, 3, 74, C(115, 76, 85), 3);
    c.Poly(red, (3, 3), (48, 3), (38, 20), (3, 20));
});
for (int frame = 0; frame < 8; frame++)
{
    float flicker = 1 + 0.15f * MathF.Sin(frame * MathF.Tau / 8);
    Bake($"torch_{frame}", 64, 84, c =>
    {
        c.Translate(32, -538);
        c.Box(-5, 579, 10, 40, C(64, 50, 57));
        c.Circle(0, 574, 24 * flicker, 30, C(236, 107, 78, 0.045f));
        c.Poly(gold, (-7, 579), (-4, 563), (3, 550 - flicker * 3), (8, 576));
        c.Poly(red, (-5, 581), (0, 567), (7, 580));
    });
}
foreach (bool open in new[] { false, true })
    Bake(open ? "portal_open" : "portal_locked", 96, 180, c =>
    {
        c.Translate(48, -444);
        Color color = open ? cyan : C(118, 75, 94);
        c.Box(-38, 488, 76, 132, C(42, 34, 49));
        c.Circle(0, 489, 38, 38, C(42, 34, 49));
        c.Box(-26, 490, 52, 130, ink);
        c.Circle(0, 490, 26, 26, ink);
        if (open)
        {
            c.Box(-24, 492, 48, 126, Color.FromArgb(38, color));
            for (int i = 0; i < 6; i++)
                c.Line(-22 + i * 9, 485, -22 + i * 9, 620, Color.FromArgb(64, color), 2);
        }
        else
            for (int i = -2; i <= 2; i++) c.Line(i * 10, 483, i * 10, 620, color, 2);
        c.Circle(0, 488, 37, 37, Color.FromArgb(128, color), false, 2);
    });

foreach (int facing in new[] { -1, 1 })
    for (int frame = 0; frame < 8; frame++)
    {
        float walk = MathF.Sin(frame * MathF.Tau / 8) * 6;
        Bake($"player_{(facing > 0 ? "right" : "left")}_{frame}", 80, 80, c =>
        {
            c.Translate(40, 48);
            c.Circle(0, 26, 24, 5, C(4, 7, 13, 0.7f));
            c.Poly(C(120, 50, 71), (-12, -8), (-18 - facing * 12, 21),
                (10 - facing * 15, 16), (12, -8));
            c.Line(-6, 9, -8 - walk, 24, C(28, 32, 43), 8);
            c.Line(6, 9, 8 + walk, 24, C(28, 32, 43), 8);
            c.Box(-15 - walk, 22, 15, 5, C(198, 179, 165));
            c.Box(3 + walk, 22, 15, 5, C(198, 179, 165));
            c.Box(-12, -9, 24, 24, C(53, 63, 80));
            c.Line(-12, 5, 12, 5, gold, 3);
            c.Circle(0, -20, 15, 14, C(224, 204, 189));
            c.Poly(C(224, 204, 189), (-13, -25), (-19, -44), (-3, -31));
            c.Poly(C(224, 204, 189), (4, -32), (18, -44), (13, -23));
            c.Box(-13, -24, 26, 9, C(39, 34, 47));
            c.Box(-9, -22, 6, 3, red);
            c.Box(4, -22, 6, 3, red);
            c.Line(-9, -6, 12, -6, red, 5);
        });
    }
Bake("player_gun", 52, 24, c =>
{
    c.Translate(8, 12);
    c.Line(0, 0, 34, 0, C(38, 43, 54), 12);
    c.Line(12, 0, 34, 0, C(204, 199, 181), 7);
    c.Circle(18, 0, 7, 7, C(94, 100, 107));
});
foreach (string kind in new[] { "grunt", "gunner", "lord" })
    foreach (int facing in new[] { -1, 1 })
        foreach (bool flash in new[] { false, true })
            Bake($"{kind}_{(facing > 0 ? "right" : "left")}{(flash ? "_flash" : "")}", 144, 128, c =>
            {
                c.Translate(72, 76);
                float s = kind == "lord" ? 1.6f : 1;
                Color body = flash ? C(239, 235, 221) : kind == "gunner" ? C(124, 91, 147) : C(149, 67, 79);
                c.Circle(0, 26 * s, 25 * s, 5, C(4, 7, 13, 0.65f));
                if (kind == "gunner") c.Poly(C(65, 52, 83), (0, -10), (-23, 25), (23, 25));
                else c.Box(-16 * s, -8 * s, 32 * s, 27 * s, body);
                c.Line(-8 * s, 12 * s, -13 * s, 26 * s, body, 8 * s);
                c.Line(8 * s, 12 * s, 13 * s, 26 * s, body, 8 * s);
                c.Circle(0, -17 * s, 18 * s, 15 * s, body);
                c.Poly(body, (-14 * s, -20 * s), (-22 * s, -40 * s), (-3 * s, -29 * s));
                c.Poly(body, (3 * s, -29 * s), (22 * s, -40 * s), (14 * s, -20 * s));
                c.Box(-11 * s, -21 * s, 7 * s, 4 * s, gold);
                c.Box(4 * s, -21 * s, 7 * s, 4 * s, gold);
                if (kind != "grunt") c.Line(0, 0, facing * 35 * s, -5, C(177, 146, 152), 7 * s);
                else c.Line(facing * 18, -1, facing * 27, 17, C(209, 175, 165), 5);
            });

Console.WriteLine($"Exported {count} PNG assets to {output}");

static Color C(int r, int g, int b, float a = 1) => Color.FromArgb((int)MathF.Round(a * 255), r, g, b);

sealed class Canvas : IDisposable
{
    private readonly Bitmap _bitmap;
    private readonly Graphics _graphics;

    public Canvas(int width, int height)
    {
        // Supersampling keeps small characters and scaled windows smooth.
        _bitmap = new Bitmap(width * 2, height * 2, PixelFormat.Format32bppArgb);
        _bitmap.SetResolution(96, 96);
        _graphics = Graphics.FromImage(_bitmap);
        _graphics.Clear(Color.Transparent);
        _graphics.SmoothingMode = SmoothingMode.AntiAlias;
        _graphics.PixelOffsetMode = PixelOffsetMode.Half;
        _graphics.ScaleTransform(2, 2);
    }

    public void Translate(float x, float y) => _graphics.TranslateTransform(x, y);
    public void Box(float x, float y, float w, float h, Color color)
    {
        using var brush = new SolidBrush(color);
        _graphics.FillRectangle(brush, x, y, w, h);
    }
    public void Line(float x1, float y1, float x2, float y2, Color color, float width = 1)
    {
        using var pen = new Pen(color, width);
        _graphics.DrawLine(pen, x1, y1, x2, y2);
    }
    public void Circle(float x, float y, float rx, float ry, Color color, bool fill = true, float stroke = 1)
    {
        if (fill)
        {
            using var brush = new SolidBrush(color);
            _graphics.FillEllipse(brush, x - rx, y - ry, rx * 2, ry * 2);
        }
        else
        {
            using var pen = new Pen(color, stroke);
            _graphics.DrawEllipse(pen, x - rx, y - ry, rx * 2, ry * 2);
        }
    }
    public void Poly(Color color, params (float X, float Y)[] vertices)
    {
        using var brush = new SolidBrush(color);
        _graphics.FillPolygon(brush, vertices.Select(p => new PointF(p.X, p.Y)).ToArray());
    }
    public void Save(string path) => _bitmap.Save(path, ImageFormat.Png);
    public void Dispose() { _graphics.Dispose(); _bitmap.Dispose(); }
}
