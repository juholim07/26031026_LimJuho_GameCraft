using System.Numerics;

var checks = new (string Name, Action Run)[]
{
    ("Normal fire cadence of 1.3/sec, 0.2s recovery, fixed order and automatic reload", () =>
    {
        var w = Start();
        var order = w.Cylinder.ToArray();
        for (int i = 0; i < 6; i++)
        {
            Check(w.CurrentBullet == order[i], "Cylinder order changed before reload");
            Check(w.TryFire(), "Normal shot rejected");
            Check(w.Ammo == 5 - i, "Normal shot must consume one round");
            Check(Math.Abs(w.ShotRecoveryLeft - 0.2f) < 0.001f, "Normal shot must have 0.2s recovery");
            Check(!w.TryFire(), "Immediate repeated shot must be blocked");
            if (i < 5)
            {
                Advance(w, 0.19f);
                Check(w.ShotRecoveryLeft > 0 && !w.TryFire(), "Recovery must last 0.2s");
                Advance(w, 0.02f);
                Check(w.ShotRecoveryLeft == 0 && !w.TryFire(), "Recovery ending must not bypass attack speed");
                Advance(w, GameWorld.NormalShotInterval - 0.22f);
                Check(!w.TryFire(), "Shot before the 1/1.3s interval must be blocked");
                Advance(w, 0.02f);
            }
        }
        Check(w.Ammo == 0 && w.ReloadLeft > 0, "Empty cylinder must start reload");
        Check(!w.TryFire(), "Cannot fire while reloading");
        Advance(w, 1.3f);
        Check(w.Ammo == 6 && w.ReloadLeft == 0 && w.EmpowerAvailable, "Reload did not restore cylinder");
        Check(w.Cylinder.GroupBy(x => x).All(g => g.Count() == 2), "Reload must contain two rounds per type");
    }),
    ("Empower consumes three rounds, resets selection, applies 0.32s slow motion", () =>
    {
        var w = Start();
        w.Step(0.01f, Input(empower: true));
        Check(w.EmpowerSelected && w.TryFire(), "Empowered shot rejected");
        Check(w.Ammo == 3 && !w.EmpowerAvailable && !w.EmpowerSelected, "Incorrect empowered consumption");
        Check(Math.Abs(w.SlowLeft - 0.32f) < 0.001f, "Incorrect slow duration");
        Check(w.Projectiles.Count == 0 && w.Beams.Single().Radius == 9, "Enhanced sniper must create an instant beam");
        Advance(w, 0.16f);
        Check(w.SlowLeft > 0, "Slow motion ended too early");
        Advance(w, 0.18f);
        Check(w.SlowLeft == 0, "Slow duration must use real time");
    }),
    ("Empowered shotgun fires twice even with only one or two rounds remaining", () =>
    {
        foreach (int shotsBefore in new[] { 1, 3, 4, 5 })
        {
            var w = Start();
            for (int i = 0; i < shotsBefore; i++) { Check(w.TryFire(), "Setup shot"); Advance(w, GameWorld.NormalShotInterval + 0.01f); }
            w.Cylinder[w.AmmoIndex] = BulletKind.Split;
            w.Step(0.01f, Input(empower: true));
            int previous = w.ShotsFired;
            Check(w.TryFire(), "Empowered shotgun rejected");
            Check(w.Ammo == Math.Max(0, 6 - shotsBefore - 3), "Shotgun must consume up to three remaining rounds");
            Check(w.ShotsFired == previous + 1, "First shotgun blast missing");
            Advance(w, 0.14f);
            Check(w.ShotsFired == previous + 2, "Delayed second shotgun blast missing");
            Check(w.SlowLeft > 0.28f, "Second blast must refresh slow motion");
            Advance(w, 2);
            if (shotsBefore >= 3) Check(w.EmpowerAvailable && w.Ammo == 6, "Reload must restore empower");
        }
    }),
    ("All enhanced ammo types work with one or two rounds and consume the remaining ammo", () =>
    {
        foreach (var kind in Enum.GetValues<BulletKind>())
            foreach (int remaining in new[] { 1, 2, 3, 6 })
            {
                var w = Start();
                for (int i = 0; i < 6 - remaining; i++)
                {
                    Check(w.TryFire(), "Setup shot failed");
                    Advance(w, GameWorld.NormalShotInterval + 0.01f);
                }
                w.Cylinder[w.AmmoIndex] = kind;
                w.Step(0.01f, Input(empower: true));
                Check(w.EmpowerSelected, "Empower must be selectable with any non-empty cylinder");
                Check(w.EmpowerAvailable, "Selection must not consume empower token");
                Check(w.TryFire(), "Shot rejected");
                Check(w.Ammo == Math.Max(0, remaining - 3), "Wrong ammo cost");
                Check(!w.EmpowerAvailable && !w.EmpowerSelected && w.SlowLeft > 0,
                    "Low-ammo shot must remain enhanced and consume the empower token");
                if (remaining <= 3)
                {
                    Check(w.Ammo == 0 && w.ReloadLeft > 0 && !w.TryFire(), "Empty cylinder must auto reload");
                    w.Step(0.01f, Input(empower: true));
                    Check(!w.EmpowerSelected, "Cannot select empower while empty and reloading");
                    Advance(w, 2);
                    Check(w.Ammo == 6 && w.EmpowerAvailable, "Reload must restore ammo and empower token");
                }
            }
    }),
    ("Empower cancellation and once-per-cylinder limit", () =>
    {
        var w = Start();
        w.Step(0.01f, Input(empower: true));
        w.Step(0.01f, Input(cancel: true));
        Check(!w.EmpowerSelected && w.EmpowerAvailable, "Cancel consumed empower");
        Check(w.TryFire() && w.Ammo == 5, "Cancelled shot must be normal");
        Advance(w, GameWorld.NormalShotInterval + 0.01f);
        w.Step(0.01f, Input(empower: true));
        Check(w.TryFire(), "Enhanced shot after normal interval must fire");
        Advance(w, 0.5f);
        w.Step(0.01f, Input(empower: true));
        Check(!w.EmpowerSelected, "Cannot select a spent empower");
    }),
    ("Overdrive requires full heat and consumes every remaining round", () =>
    {
        var w = Start();
        Check(!w.TryBarrage(), "Barrage requires 100% heat");
        while (w.Heat < 100)
        {
            if (w.ReloadLeft > 0) Advance(w, 1.3f);
            Check(w.TryFire(), "Heat setup shot failed");
            Advance(w, GameWorld.NormalShotInterval + 0.01f);
        }
        if (w.ReloadLeft > 0) Advance(w, 1.3f);
        int remaining = w.Ammo;
        int shots = w.ShotsFired;
        Check(w.TryBarrage(), "Full heat barrage rejected");
        Check(w.ShotsFired == shots + remaining && w.Ammo == 0, "Barrage must fire all remaining rounds");
        Check(w.Heat == 0 && w.ReloadLeft > 0, "Barrage must reset heat and auto reload");
    }),
    ("Sniper laser immediately hits every target on the ray and no off-axis targets", () =>
    {
        var w = Start();
        var first = Target(265, 589);
        var distant = Target(1450, 589);
        var offAxis = Target(600, 520);
        var behind = Target(80, 589);
        w.Enemies.AddRange(new[] { first, distant, offAxis, behind });
        w.Step(0.01f, Input(empower: true));
        Check(w.TryFire(), "Enhanced sniper shot rejected");
        Check(first.Health == 185 && distant.Health == 185, "Laser must deal damage before any further Step");
        Check(offAxis.Health == 300 && behind.Health == 300, "Laser hit off-axis or behind muzzle");
        Check(w.Projectiles.Count == 0 && w.Beams.Count == 1, "Laser must not be a travelling projectile");
        Advance(w, 0.4f);
        Check(first.Health == 185 && distant.Health == 185 && w.Beams.Count == 0, "Beam effect must expire without repeated damage");
    }),
    ("Normal sniper still travels, swept collision and ordinary radial explosion", () =>
    {
        var w = Start();
        var first = Target(265, 589);
        w.Enemies.Add(first);
        w.TryFire();
        Check(first.Health == 300 && w.Projectiles.Count == 1 && w.Beams.Count == 0,
            "Normal sniper must retain projectile travel");
        Advance(w, 0.1f);
        Check(first.Health == 248, "Normal sniper hit missing");
        w = Start();
        first = Target(280, 594); var second = Target(280, 520);
        w.Enemies.AddRange(new[] { first, second });
        w.Cylinder[0] = BulletKind.Explosive;
        w.TryFire(); Advance(w, 0.2f);
        Check(first.Health == 222 && second.Health == 222, "Explosion must damage each nearby target once");
        Check(GameWorld.SegmentDistance(new Vector2(50, 0), Vector2.Zero, new Vector2(100, 0)) == 0,
            "Swept hit calculation");
    }),
    ("Enhanced explosive detonates at short range in empty space with a larger damage radius", () =>
    {
        var w = Start();
        // Both targets are off the projectile path; only the larger explosion can hit the nearby one.
        var nearby = Target(275, 430);
        var distant = Target(900, 430);
        w.Enemies.AddRange(new[] { nearby, distant });
        w.Cylinder[0] = BulletKind.Explosive;
        w.Step(0.01f, Input(empower: true));
        Check(w.TryFire(), "Enhanced explosive rejected");
        Vector2 origin = w.Projectiles.Single().Position;
        Advance(w, 0.5f);
        var explosion = w.Particles.Single(p => p.Ring);
        Check(Math.Abs(Vector2.Distance(origin, explosion.Position) - GameWorld.EnhancedExplosiveTravel) < 0.1f,
            "Enhanced explosive must detonate at its short distance even without a collision");
        Check(explosion.Size == GameWorld.EnhancedExplosionRadius && explosion.Size > 92,
            "Enhanced explosion radius must exceed normal");
        Check(nearby.Health == 160 && distant.Health == 300 && w.Projectiles.Count == 0,
            "Short range explosion must hit nearby area once without reaching distant enemies");
    }),
    ("Jump, landing on platforms, dash and pause", () =>
    {
        var w = Start();
        w.Player.Position.X = 470;
        w.Step(0.01f, Input(jump: true));
        Check(w.Player.Velocity.Y < 0, "Jump did not start");
        Advance(w, 0.75f);
        Check(w.Player.Grounded && Math.Abs(w.Player.Position.Y - 479) < 0.1f, "Platform landing failed");
        float oldX = w.Player.Position.X;
        w.Step(0.02f, Input(dash: true));
        Check(w.Player.Position.X > oldX && w.Player.DashCooldown > 0 && w.Player.Invincible > 0,
            "Dash must move player and grant brief invulnerability");
        w.TogglePause();
        var old = w.Player.Position;
        float time = w.Elapsed;
        w.Step(0.04f, Input(move: 1));
        Check(w.Player.Position == old && w.Elapsed == time && !w.TryFire(), "Pause must freeze simulation");
    }),
    ("Dash follows mouse in every direction, ignores movement keys and spends one round", () =>
    {
        foreach (var direction in new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY,
            -Vector2.UnitY, Vector2.Normalize(new Vector2(-1, -1)) })
        {
            var w = Start();
            w.Player.Position = new Vector2(700, 390);
            w.Player.Grounded = false;
            Vector2 origin = w.Player.Position;
            w.Step(0.02f, Input(move: 1, dash: true, aim: origin + direction * 500));
            Check(w.Ammo == 5 && w.ShotsFired == 0 && w.Projectiles.Count == 0,
                "Dash must consume one round without firing it");
            Vector2 actual = Vector2.Normalize(w.Player.Position - origin);
            Check(Vector2.Dot(actual, direction) > 0.999f, "Dash direction must follow mouse rather than movement keys");
            Check(!w.TryDash() && w.Ammo == 5, "Cooldown rejection must not consume ammo");
            Vector2 before = w.Player.Position;
            w.Step(0.02f, Input(move: -1, aim: before - direction * 500));
            Check(Vector2.Dot(Vector2.Normalize(w.Player.Position - before), direction) > 0.999f,
                "Mouse motion must not turn an ongoing dash");
            Check(w.Ammo == 5, "Ongoing dash must not consume more ammo");
        }
    }),
    ("Last-round dash starts auto reload, blocks empty dashes, restores the empower token", () =>
    {
        var w = Start();
        for (int i = 0; i < 5; i++) { Check(w.TryFire(), "Setup shot"); Advance(w, GameWorld.NormalShotInterval + 0.01f); }
        Check(w.Ammo == 1 && w.TryDash(), "Last round must permit dash");
        Check(w.Ammo == 0 && w.ReloadLeft > 0, "Dash must trigger reload when it uses last round");
        Check(!w.TryDash() && !w.TryFire(), "Empty cylinder must block dash and shooting");
        Advance(w, 1.3f);
        Check(w.Ammo == 6 && w.EmpowerAvailable && w.TryDash() && w.Ammo == 5,
            "Reload must restore dash ammunition and empower token");
    }),
    ("Dash preserves low-ammo empower selection and wins over same-frame fire", () =>
    {
        var w = Start();
        for (int i = 0; i < 3; i++) { Check(w.TryFire(), "Setup shot"); Advance(w, GameWorld.NormalShotInterval + 0.01f); }
        w.Step(0.01f, Input(empower: true));
        Check(w.EmpowerSelected, "Setup selection");
        w.Step(0.01f, Input(dash: true, fire: true));
        Check(w.Ammo == 2 && w.EmpowerSelected && w.EmpowerAvailable,
            "Dash must preserve selected empower when ammo drops below three");
        Check(w.ShotsFired == 3, "Simultaneous dash/fire must not fire an extra shot");
        Check(w.TryFire() && w.Ammo == 0 && !w.EmpowerAvailable && w.ReloadLeft > 0,
            "Selected empower must still fire after a dash leaves only two rounds");
    }),
    ("Two-map progression, failure checkpoint and victory", () =>
    {
        var w = new GameWorld(); w.Start();
        w.Player.Position.X = w.MapWidth - 80;
        w.Step(0.01f, Input());
        Check(w.Map == 0, "Locked exit must not advance map");
        foreach (var e in w.Enemies) e.Health = 0;
        w.Step(0.01f, Input());
        Check(w.Map == 1 && w.Enemies.Any(e => e.Kind == EnemyKind.Lord), "Boss map did not load");
        w.Player.Health = 0; w.Step(0.01f, Input());
        Check(w.Phase == GamePhase.Failed, "Zero health must fail");
        w.RestartMap();
        Check(w.Map == 1 && w.Player.Health == 100 && w.Ammo == 6, "Retry must reset current map");
        foreach (var e in w.Enemies) e.Health = 0;
        w.Step(0.01f, Input());
        Check(w.Phase == GamePhase.Won, "Cleared boss map must win");
        w.NewRun();
        Check(w.Phase == GamePhase.Ready && w.Map == 0 && w.Elapsed == 0 && w.Kills == 0, "New run reset");
    }),
    ("Enemy warning, ranged damage and contact invulnerability", () =>
    {
        var w = Start();
        w.Enemies.Add(new Enemy { Kind = EnemyKind.Gunner, Position = new Vector2(450, 594),
            Health = 100, MaxHealth = 100, Cooldown = 0, Left = 450, Right = 450 });
        w.Step(0.01f, Input());
        Check(w.Enemies[0].Telegraph > 0 && w.Projectiles.Count == 0, "Enemy must warn before shooting");
        Advance(w, 1.9f);
        Check(w.Player.Health < 100, "Enemy bullet must damage player");
        w = Start();
        w.Enemies.Add(Target(150, 594));
        w.Step(0.01f, Input());
        float hp = w.Player.Health;
        Advance(w, 0.1f);
        Check(hp < 100 && w.Player.Health == hp, "Contact invulnerability must prevent frame-by-frame damage");
    })
};
int failed = 0;
foreach (var check in checks)
{
    try { check.Run(); Console.WriteLine("PASS  " + check.Name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL  " + check.Name + ": " + ex.Message); }
}
Console.WriteLine($"{checks.Length - failed}/{checks.Length} gameplay checks passed.");
return failed == 0 ? 0 : 1;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static GameWorld Start() { var w = new GameWorld(); w.Start(); w.Enemies.Clear(); return w; }
static Enemy Target(float x, float y) => new() { Kind = EnemyKind.Grunt,
    Position = new Vector2(x, y), Left = x, Right = x, Health = 300, MaxHealth = 300 };
static GameInput Input(float move = 0, bool jump = false, bool dash = false,
    bool empower = false, bool cancel = false, bool fire = false, Vector2? aim = null) =>
    new(move, jump, dash, fire, empower, cancel, false, aim ?? new Vector2(1200, 594));
static void Advance(GameWorld w, float duration)
{
    for (float t = 0; t < duration - 0.0001f; t += 0.01f) w.Step(Math.Min(0.01f, duration - t), Input());
}
