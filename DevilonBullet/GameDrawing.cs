using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

sealed partial class GameMain
{
    private static readonly Color4 Ink = C(11, 14, 23);
    private static readonly Color4 Panel = C(19, 24, 35);
    private static readonly Color4 Edge = C(48, 55, 70);
    private static readonly Color4 White = C(239, 235, 221);
    private static readonly Color4 Muted = C(141, 149, 166);
    private static readonly Color4 Red = C(248, 99, 92);
    private static readonly Color4 Cyan = C(108, 216, 225);
    private static readonly Color4 Gold = C(240, 190, 104);
    private static readonly Color4 Purple = C(185, 150, 241);

    private static Color4 C(int r, int g, int b, float a = 1) => new(r / 255f, g / 255f, b / 255f, a);
    private static Color4 Alpha(Color4 c, float a) => new(c.R, c.G, c.B, a);
    private static Color4 BulletColor(BulletKind kind) => kind switch
    { BulletKind.Piercing => Cyan, BulletKind.Split => Gold, _ => Red };
    private static string BulletName(BulletKind kind) => kind switch
    { BulletKind.Piercing => "저격탄", BulletKind.Split => "산탄", _ => "폭발탄" };

    private void Box(float x, float y, float w, float h, Color4 color)
    {
        if (w <= 0 || h <= 0) return;
        _brush.Color = color;
        RenderTarget.FillRectangle(new Rect(x, y, w, h), _brush);
    }
    private void Border(float x, float y, float w, float h, Color4 color, float stroke = 1)
    {
        _brush.Color = color;
        RenderTarget.DrawRectangle(new Rect(x, y, w, h), _brush, stroke);
    }
    private void Line(float x1, float y1, float x2, float y2, Color4 color, float width = 1)
    {
        _brush.Color = color;
        RenderTarget.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), _brush, width);
    }
    private void Circle(float x, float y, float rx, float ry, Color4 color, bool fill = true, float stroke = 1)
    {
        _brush.Color = color;
        var ellipse = new Ellipse(new Vector2(x, y), rx, ry);
        if (fill) RenderTarget.FillEllipse(ellipse, _brush);
        else RenderTarget.DrawEllipse(ellipse, _brush, stroke);
    }
    private void Poly(Color4 color, params Vector2[] points)
    {
        using var path = G2D2DContext.Instance!.Factory.CreatePathGeometry();
        using (var sink = path.Open())
        {
            sink.BeginFigure(points[0], FigureBegin.Filled);
            for (int i = 1; i < points.Length; i++) sink.AddLine(points[i]);
            sink.EndFigure(FigureEnd.Closed);
            sink.Close();
        }
        _brush.Color = color;
        RenderTarget.FillGeometry(path, _brush);
    }
    private void T(string text, float x, float y, int size, Color4 color, float width = 500, float height = 70, bool center = false) =>
        _fonts[center ? -size : size].DrawText(text, new Rect(x, y, width, height), color);

    private void DrawGame()
    {
        RenderTarget.PushAxisAlignedClip(new Rect(0, 154, 1280, 496), AntialiasMode.PerPrimitive);
        Matrix3x2 saved = RenderTarget.Transform;
        float shakeX = MathF.Sin(_visualTime * 127) * _world.Shake * 0.5f;
        float shakeY = MathF.Cos(_visualTime * 113) * _world.Shake * 0.3f;
        RenderTarget.Transform = Matrix3x2.CreateTranslation(shakeX, shakeY) * saved;
        DrawBackground();
        DrawTerrain();
        foreach (var enemy in _world.Enemies.Where(e => e.Health > 0)) DrawEnemy(enemy);
        DrawPlayer();
        DrawEffects();
        RenderTarget.Transform = saved;
        RenderTarget.PopAxisAlignedClip();
        DrawHeader();
        DrawHud();
        if (_world.SlowLeft > 0)
        {
            Border(3, 157, 1274, 490, Alpha(Gold, 0.6f), 3);
            T("E M P O W E R E D   /   0.34 ×", 450, 165, 13, Gold, 380, 30, true);
        }
        if (_world.NoticeLeft > 0 && _world.Phase == GamePhase.Playing)
        {
            Box(360, 193, 560, 38, Alpha(Ink, 0.92f));
            T(_world.Notice, 360, 201, 15, Gold, 560, 30, true);
        }
        if (_world.Phase != GamePhase.Playing || _showHelp) DrawOverlay();
        else if (_mouse.Y is > 154 and < 650) DrawCrosshair();
    }

    private void DrawHeader()
    {
        Box(0, 0, 1280, 154, Ink);
        Box(28, 29, 34, 34, Red);
        Poly(Ink, new(35, 53), new(38, 34), new(45, 43), new(54, 34), new(55, 54));
        T("DEVIL ON BULLET", 78, 20, 28, White, 460, 44);
        T("데빌 온 불릿   /   악마들의 랭킹 쇼", 80, 61, 13, Muted, 470, 30);
        Box(616, 27, 1, 49, Edge);
        T("CHALLENGE  /  001", 646, 24, 11, Muted, 230, 20);
        T(_world.Map == 0 ? "잿빛 영지의 입구" : "지배 악마의 결투장", 644, 46, 18, White, 360, 35);
        Circle(1103, 39, 4, 4, Red);
        T("ON AIR", 1117, 28, 13, Red, 120, 25);
        T("F1 도움말   ·   ESC 일시정지", 1020, 65, 11, Muted, 250, 25);
        Line(28, 96, 1252, 96, Edge);
        T("0" + (_world.Map + 1), 29, 108, 18, Red, 40, 35);
        T(_world.Map == 0 ? "영지 입구" : "결투장", 67, 111, 15, White, 140, 30);
        T(_world.ExitOpen ? "출구 개방  →  오른쪽으로 이동" : "모든 적을 처치하고 영지를 돌파하세요", 208, 114, 13,
            _world.ExitOpen ? Cyan : Muted, 530, 30);
        int alive = _world.Enemies.Count(e => e.Health > 0);
        T($"남은 적  {alive:00}     /     처치  {_world.Kills:00}", 849, 112, 13, White, 280, 30);
        T(TimeLabel(_world.Elapsed), 1160, 107, 22, White, 100, 35);
        Box(28, 149, 1224, 2, Edge);
        Box(28, 149, _world.Map == 0 ? 612 : 1224, 2, Red);
    }

    private static string TimeLabel(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";

    private void DrawBackground()
    {
        Sprite("sky", 0, 154, 1280, 496);
        float camera = _world.CameraX;
        float moonX = 1020 - camera * 0.08f;
        Sprite("moon", moonX - 96, 199, 192, 192);
        Sprite("buildings", -158 - camera * 0.2f, 220, 2880, 400);
        Sprite("pillars", -148 - camera * 0.48f, 292, 3600, 392);
        for (int i = 0; i < 36; i++)
        {
            float x = ((i * 139 + _visualTime * (8 + i % 5)) % 1360) - 40;
            float y = 600 - ((i * 61 + _visualTime * 18) % 430);
            Circle(x, y, i % 4 == 0 ? 2 : 1, 1.5f, C(225, 129, 92, 0.25f + 0.2f * MathF.Sin(_visualTime + i)));
        }
        T(_world.Map == 0 ? "THE ASHEN DISTRICT" : "THE LORD'S ARENA", 35, 175, 11, C(128, 110, 128), 400, 25);
        T("RANKING SHOW   /   SEASON 06", 1020, 615, 11, C(122, 99, 114), 250, 25);
    }

    private void DrawTerrain()
    {
        float cam = _world.CameraX;
        Sprite("ground", 0, 620, 1280, 30);
        for (int i = 0; i < 22; i++)
        {
            float x = i * 62 - cam % 62;
            Sprite("ground_tile", x, 619, 62, 31);
        }
        foreach (var p in _world.Platforms)
        {
            float x = p.X - cam;
            if (x > 1320 || x + p.Width < -40) continue;
            Sprite($"platform_{(int)p.Width}", x, p.Y, p.Width, 50);
        }
        DrawPortal(_world.MapWidth - 85 - cam, _world.ExitOpen);
        Sprite("start_flag", 65 - cam, 545, 52, 76);
        for (int i = 0; i < 8; i++)
        {
            float x = i * 350 + 305 - cam;
            Sprite($"torch_{AnimationFrame(_visualTime * 10 + i)}", x - 32, 538, 64, 84);
        }
    }

    private void DrawPortal(float x, bool open)
    {
        Color4 color = open ? Cyan : C(118, 75, 94);
        Sprite(open ? "portal_open" : "portal_locked", x - 48, 444, 96, 180);
        T(open ? "EXIT →" : "LOCKED", x - 60, 425, 13, color, 120, 30, true);
    }

    private void DrawPlayer()
    {
        Fighter p = _world.Player;
        float x = p.Position.X - _world.CameraX;
        float y = p.Position.Y;
        if (p.Invincible > 0 && p.DashTime <= 0 && (int)(_visualTime * 18) % 2 == 0) return;
        int frame = Math.Abs(p.Velocity.X) > 5 && p.Grounded ? AnimationFrame(_visualTime * 20) : 0;
        if (p.DashTime > 0)
            for (int i = 1; i < 5; i++)
                Box(x - p.DashDirection.X * i * 22 - 10, y - p.DashDirection.Y * i * 22 - 18,
                    18, 39, Alpha(Cyan, 0.15f - i * 0.025f));
        Sprite($"player_{(p.Facing > 0 ? "right" : "left")}_{frame}", x - 40, y - 48, 80, 80);
        float recoil = 9 * _world.ShotRecoveryLeft / GameWorld.NormalShotRecovery;
        Vector2 arm = new Vector2(x, y - 5) - p.Aim * recoil;
        Vector2 dir = p.Aim;
        Vector2 gun = arm + dir * 34;
        Matrix3x2 saved = RenderTarget.Transform;
        RenderTarget.Transform = Matrix3x2.CreateRotation(MathF.Atan2(dir.Y, dir.X), arm) * saved;
        Sprite("player_gun", arm.X - 8, arm.Y - 12, 52, 24);
        RenderTarget.Transform = saved;
        if (_world.EmpowerSelected) Circle(x, y, 34, 40, Alpha(Gold, 0.7f), false, 1.5f);
        if (_world.MuzzleFlash > 0)
        {
            Vector2 tip = gun + dir * 17;
            Vector2 side = new(-dir.Y, dir.X);
            Poly(Gold, gun + side * 7, tip + dir * 13, gun - side * 7, tip - dir * 7);
            Circle(gun.X, gun.Y, 25, 25, Alpha(Gold, 0.14f));
        }
        T("슈케트", x - 50, y - 72, 11, Cyan, 100, 25, true);
        if (_world.ReloadLeft > 0)
        {
            Box(x - 25, y - 53, 50, 3, Edge);
            Box(x - 25, y - 53, 50 * (1 - _world.ReloadLeft / GameWorld.ReloadDuration), 3, Gold);
        }
    }

    private void DrawEnemy(Enemy e)
    {
        float x = e.Position.X - _world.CameraX;
        float y = e.Position.Y;
        if (x < -80 || x > 1360) return;
        float s = e.Kind == EnemyKind.Lord ? 1.6f : 1;
        string kind = e.Kind switch { EnemyKind.Grunt => "grunt", EnemyKind.Gunner => "gunner", _ => "lord" };
        Sprite($"{kind}_{(e.Facing > 0 ? "right" : "left")}{(e.Flash > 0 ? "_flash" : "")}",
            x - 72, y - 76, 144, 128);
        float barWidth = e.Kind == EnemyKind.Lord ? 100 : 48;
        Box(x - barWidth / 2, y - 53 * s, barWidth, 4, C(50, 35, 47));
        Box(x - barWidth / 2, y - 53 * s, barWidth * e.Health / e.MaxHealth, 4, e.Kind == EnemyKind.Lord ? Gold : Red);
        if (e.Kind == EnemyKind.Lord) T("영주 · 아스모", x - 100, y - 113, 13, Gold, 200, 30, true);
        if (e.Telegraph > 0)
        {
            Circle(x, y, 32 * s + e.Telegraph * 15, 35 * s + e.Telegraph * 15, Alpha(Red, 0.7f), false, 2);
            T("!", x - 10, y - 90 * s, 22, Gold, 20, 35, true);
            Vector2 target = _world.Player.Position - new Vector2(_world.CameraX, 0);
            Line(x, y, target.X, target.Y, Alpha(Red, 0.16f));
        }
    }

    private void DrawEffects()
    {
        float cam = _world.CameraX;
        foreach (var beam in _world.Beams)
        {
            Vector2 start = beam.Start - new Vector2(cam, 0);
            Vector2 end = beam.End - new Vector2(cam, 0);
            float fade = Math.Clamp(beam.Life / 0.18f, 0, 1);
            Line(start.X, start.Y, end.X, end.Y, Alpha(Cyan, fade * 0.12f), beam.Radius * 5);
            Line(start.X, start.Y, end.X, end.Y, Alpha(Cyan, fade * 0.65f), beam.Radius * 2);
            Line(start.X, start.Y, end.X, end.Y, Alpha(White, fade), 3);
            Circle(end.X, end.Y, 11 * fade, 11 * fade, Alpha(Cyan, fade));
        }
        foreach (var bullet in _world.Projectiles)
        {
            Vector2 p = bullet.Position - new Vector2(cam, 0);
            Color4 color = bullet.Hostile ? Red : BulletColor(bullet.Kind);
            Vector2 tail = p - Vector2.Normalize(bullet.Velocity) * (bullet.Empowered ? 42 : 23);
            Line(tail.X, tail.Y, p.X, p.Y, Alpha(color, 0.25f), bullet.Radius * 3);
            Line(tail.X, tail.Y, p.X, p.Y, color, bullet.Hostile ? 4 : Math.Min(bullet.Radius, 5));
            Circle(p.X, p.Y, bullet.Radius, bullet.Radius, color);
            Circle(p.X, p.Y, 2, 2, White);
        }
        foreach (var p in _world.Particles)
        {
            float x = p.Position.X - cam;
            float alpha = Math.Clamp(p.Life / p.MaxLife, 0, 1);
            Color4 color = Alpha(BulletColor(p.Kind), alpha);
            if (p.Text != null) T(p.Text, x, p.Position.Y, 15, color, 70, 30);
            else if (p.Ring)
            {
                float radius = p.Size * (1 - alpha * 0.75f);
                Circle(x, p.Position.Y, radius, radius, Alpha(color, alpha * 0.12f));
                Circle(x, p.Position.Y, radius, radius, color, false, 3 * alpha + 1);
            }
            else Box(x, p.Position.Y, p.Size * alpha, p.Size * alpha, color);
        }
    }

    private void DrawHud()
    {
        Box(0, 650, 1280, 150, Ink);
        Line(28, 650, 1252, 650, Edge);
        T("01  /  슈케트", 29, 668, 11, Muted, 250, 25);
        T($"{(int)Math.Ceiling(_world.Player.Health):000}", 28, 686, 28, White, 80, 45);
        T("/ 100 HP", 106, 703, 11, Muted, 100, 24);
        Box(29, 738, 255, 6, Edge);
        Box(29, 738, 255 * _world.Player.Health / 100, 6, Red);
        string dashState = _world.ReloadLeft > 0 ? "재장전 중 · 대시 불가" :
            _world.Player.DashCooldown > 0 ? $"대시  {_world.Player.DashCooldown:0.0}s" : "우클릭 / SHIFT  대시 · 1발";
        T(dashState, 30, 757, 11,
            _world.Player.DashCooldown > 0 || _world.ReloadLeft > 0 ? Muted : Cyan, 270, 25);
        Box(311, 670, 1, 106, Edge);

        T($"REVOLVER   /   일반 초당 {GameWorld.NormalAttackSpeed:0.0}회 · 후딜 {GameWorld.NormalShotRecovery:0.0}초", 337, 668, 11, Muted, 450, 25);
        T($"{_world.Ammo} / 6", 811, 665, 18, White, 90, 30);
        for (int i = 0; i < 6; i++)
        {
            float x = 338 + i * 73;
            bool spent = i < _world.AmmoIndex;
            bool head = i == _world.AmmoIndex && _world.Ammo > 0;
            Color4 color = spent ? C(58, 63, 78) : BulletColor(_world.Cylinder[i]);
            Box(x, 698, 64, 48, head ? Alpha(color, 0.13f) : Panel);
            Border(x, 698, 64, 48, head ? color : Edge);
            if (head) Box(x, 698, 3, 48, color);
            DrawBulletIcon(x + 17, 720, _world.Cylinder[i], color);
            T(spent ? "—" : BulletName(_world.Cylinder[i]), x + 30, 713, 11, color, 33, 30);
            if (spent) Line(x + 8, 739, x + 55, 703, C(64, 68, 79));
        }
        if (_world.ReloadLeft > 0)
        {
            float progress = 1 - _world.ReloadLeft / GameWorld.ReloadDuration;
            Box(338, 757, 430, 4, Edge);
            Box(338, 757, 430 * progress, 4, Gold);
            T($"자동 재장전  {_world.ReloadLeft:0.0}s", 785, 752, 11, Gold, 135, 25);
        }
        else
        {
            string state = _world.EmpowerSelected ? $"강화 선택됨 · 다음 사격에 {Math.Min(_world.Ammo, GameWorld.EmpowerCost)}발 소모" :
                !_world.EmpowerAvailable ? "강화권 소모 · 재장전 시 복구" :
                "휠 ↓ 강화 선택 · 휠 ↑ 강화 취소 · 최대 3발 소모";
            T(state, 338, 757, 11, _world.EmpowerSelected ? Gold : Muted, 540, 25);
        }
        Box(921, 670, 1, 106, Edge);
        T("OVERDRIVE", 948, 668, 11, Muted, 190, 25);
        T($"{(int)_world.Heat}%", 1185, 665, 18, _world.Heat >= 100 ? Gold : White, 80, 30);
        for (int i = 0; i < 20; i++)
            Box(949 + i * 15, 703, 11, 14, _world.Heat >= (i + 1) * 5 ? (_world.Heat >= 100 ? Gold : Red) : Edge);
        T(_world.Heat >= 100 ? "Q  전탄격발 준비 완료" : "사격과 명중으로 과열 충전", 948, 732, 15,
            _world.Heat >= 100 ? Gold : White, 310, 30);
        T("남은 탄환을 모두 쏟아붓습니다", 948, 760, 11, Muted, 310, 25);
    }

    private void DrawBulletIcon(float x, float y, BulletKind kind, Color4 color)
    {
        if (kind == BulletKind.Piercing)
        {
            Box(x - 3, y - 6, 6, 14, color);
            Poly(color, new(x - 3, y - 6), new(x, y - 12), new(x + 3, y - 6));
        }
        else if (kind == BulletKind.Split)
        {
            for (int i = -1; i <= 1; i++)
            {
                Circle(x + i * 5, y - 6 + Math.Abs(i) * 2, 2, 2, color);
                Line(x, y + 8, x + i * 5, y, color);
            }
        }
        else
        {
            Circle(x, y, 6, 6, color, false, 2);
            Line(x, y - 7, x + 4, y - 12, color, 2);
            Line(x + 3, y - 13, x + 7, y - 11, color);
        }
    }

    private void DrawOverlay()
    {
        Box(0, 154, 1280, 496, C(6, 9, 17, 0.75f));
        if (_showHelp)
        {
            Box(308, 185, 664, 424, Panel);
            Border(308, 185, 664, 424, Edge);
            T("HOW TO PLAY", 340, 205, 22, White, 600, 45);
            T("A / D 이동 · SPACE / W 점프 · 우클릭 / SHIFT 마우스 방향 대시", 340, 266, 15, White, 610, 35);
            T("마우스 조준 · 좌클릭 사격 · 휠 ↓ 강화 선택 · 휠 ↑ 강화 취소", 340, 307, 15, White, 610, 35);
            T("저격탄   강화 시 즉발 관통 레이저", 340, 357, 15, Cyan, 610, 35);
            T("산탄      강화 시 2연속 집중 사격", 340, 392, 15, Gold, 610, 35);
            T("폭발탄   강화 시 짧은 사거리 · 넓은 폭발 범위", 340, 427, 15, Red, 610, 35);
            T("강화: 최대 3발(부족하면 전부) · 장전당 1회 / 대시: 1발", 340, 480, 13, Muted, 610, 30);
            T("6발 소모 후 자동 재장전 · 과열 100%에서 Q 전탄격발", 340, 512, 13, Muted, 610, 30);
            T("F1 / ESC  닫기", 340, 563, 13, Gold, 600, 30, true);
            return;
        }
        Box(328, 227, 624, 345, Panel);
        Border(328, 227, 624, 345, Edge);
        Box(328, 227, 624, 3, Red);
        string tag = "WELCOME TO THE RANKING SHOW";
        string title = "여섯 발로, 영지를 빼앗아라.";
        string detail = "총의 악마 슈케트의 첫 번째 도전";
        string action = "ENTER  /  도전 시작";
        switch (_world.Phase)
        {
            case GamePhase.Paused:
                tag = "TAKE A BREATH"; title = "일시정지";
                detail = "준비가 되면 쇼를 계속하세요."; action = "ENTER  /  계속하기"; break;
            case GamePhase.Failed:
                tag = "THE SHOW IS NOT OVER"; title = "도전 실패";
                detail = "현재 맵의 처음부터 다시 도전할 수 있습니다."; action = "ENTER  /  다시 도전"; break;
            case GamePhase.Won:
                tag = "A NEW NAME ON THE THRONE"; title = "도전 성공";
                detail = $"처치  {_world.Kills}     ·     클리어  {TimeLabel(_world.Elapsed)}     ·     남은 체력  {(int)_world.Player.Health}";
                action = "ENTER  /  새로운 도전"; break;
        }
        T(tag, 350, 258, 11, Red, 580, 25, true);
        T(title, 344, 303, 28, White, 592, 60, true);
        T(detail, 350, 364, 15, Muted, 580, 40, true);
        bool hover = InBox(_mouse, 476, 440, 328, 58);
        Box(476, 440, 328, 58, hover ? C(255, 124, 111) : Red);
        T(action, 476, 456, 18, Ink, 328, 35, true);
        T("A D 이동   ·   SPACE 점프   ·   좌클릭 사격   ·   F1 도움말", 345, 531, 11, Muted, 590, 30, true);
    }

    private void DrawCrosshair()
    {
        Color4 color = _world.EmpowerSelected ? Gold : White;
        float x = _mouse.X;
        float y = _mouse.Y;
        Circle(x, y, _world.EmpowerSelected ? 11 : 7, _world.EmpowerSelected ? 11 : 7, Alpha(color, 0.8f), false);
        Line(x - 15, y, x - 9, y, color);
        Line(x + 9, y, x + 15, y, color);
        Line(x, y - 15, x, y - 9, color);
        Line(x, y + 9, x, y + 15, color);
        Circle(x, y, 1.5f, 1.5f, color);
    }
}
