using System.Numerics;

enum GamePhase { Ready, Playing, Paused, Failed, Won }
enum BulletKind { Piercing, Split, Explosive }
enum EnemyKind { Grunt, Gunner, Lord }
readonly record struct GameInput(float Move, bool Jump, bool Dash, bool Fire,
    bool Empower, bool CancelEmpower, bool Barrage, Vector2 Aim);
readonly record struct Platform(float X, float Y, float Width, float Height);

sealed class Fighter
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool Grounded;
    public float Health = 100;
    public float Invincible;
    public float DashTime;
    public float DashCooldown;
    public Vector2 DashDirection = Vector2.UnitX;
    public float Facing = 1;
    public Vector2 Aim = Vector2.UnitX;
}
sealed class Enemy
{
    public EnemyKind Kind;
    public Vector2 Position;
    public float Health;
    public float MaxHealth;
    public float Cooldown;
    public float Telegraph;
    public float Flash;
    public float Facing = -1;
    public float Radius => Kind == EnemyKind.Lord ? 37 : 23;
    public float Left;
    public float Right;
}
sealed class Projectile
{
    public Vector2 Position;
    public Vector2 Velocity;
    public BulletKind Kind;
    public float Damage;
    public float Radius;
    public float Life;
    public bool Empowered;
    public bool Hostile;
    public float TravelLeft = float.PositiveInfinity;
    public readonly HashSet<Enemy> Hit = new();
}
sealed class LaserBeam
{
    public Vector2 Start;
    public Vector2 End;
    public float Radius;
    public float Life = 0.18f;
}
sealed class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public BulletKind Kind;
    public float Life;
    public float MaxLife;
    public float Size;
    public bool Ring;
    public string? Text;
}

// Simulation stays independent of Direct2D/WinForms for headless gameplay checks.
sealed class GameWorld
{
    public const float GroundY = 620;
    public const float ReloadDuration = 1.15f;
    public const float SlowDuration = 0.32f;
    public const float SlowScale = 0.34f;
    public const float NormalAttackSpeed = 1.3f;
    public const float NormalShotInterval = 1f / NormalAttackSpeed;
    public const float NormalShotRecovery = 0.2f;
    public const int EmpowerCost = 3;
    public const float DashSpeed = 890;
    public const float DashDuration = 0.15f;
    public const float LaserRange = 1600;
    public const float EnhancedExplosiveTravel = 90;
    public const float EnhancedExplosionRadius = 190;
    public Fighter Player { get; } = new();
    public List<Enemy> Enemies { get; } = new();
    public List<Projectile> Projectiles { get; } = new();
    public List<LaserBeam> Beams { get; } = new();
    public List<Particle> Particles { get; } = new();
    public List<Platform> Platforms { get; } = new();
    public BulletKind[] Cylinder { get; } = new BulletKind[6];
    public GamePhase Phase { get; private set; }
    public int Map { get; private set; }
    public float MapWidth => Map == 0 ? 2480 : 1880;
    public float CameraX { get; private set; }
    public int AmmoIndex { get; private set; }
    public int Ammo => 6 - AmmoIndex;
    public BulletKind CurrentBullet => Cylinder[Math.Min(AmmoIndex, 5)];
    public bool EmpowerAvailable { get; private set; }
    public bool EmpowerSelected { get; private set; }
    public float ReloadLeft { get; private set; }
    public float Heat { get; private set; }
    public float SlowLeft { get; private set; }
    public float Shake { get; private set; }
    public float MuzzleFlash { get; private set; }
    public float ShotRecoveryLeft { get; private set; }
    public float Elapsed { get; private set; }
    public int Kills { get; private set; }
    public int ShotsFired { get; private set; }
    public bool ExitOpen => Enemies.All(e => e.Health <= 0);
    public string Notice { get; private set; } = "";
    public float NoticeLeft { get; private set; }
    private float _fireCooldown;
    private float _burstDelay;
    private Vector2 _burstDirection;
    private float _coyote;
    private float _jumpBuffer;
    private float _checkpointTime;
    private int _checkpointKills;
    private readonly Random _random;

    public GameWorld(int seed = 7)
    {
        _random = new Random(seed);
        NewRun();
    }

    public void NewRun()
    {
        Map = 0;
        Elapsed = 0;
        Kills = 0;
        ShotsFired = 0;
        _checkpointTime = 0;
        _checkpointKills = 0;
        LoadMap();
        Phase = GamePhase.Ready;
    }
    public void Start() => Phase = GamePhase.Playing;
    public void TogglePause()
    {
        if (Phase == GamePhase.Playing) Phase = GamePhase.Paused;
        else if (Phase == GamePhase.Paused) Phase = GamePhase.Playing;
    }
    public void RestartMap()
    {
        Elapsed = _checkpointTime;
        Kills = _checkpointKills;
        LoadMap();
        Phase = GamePhase.Playing;
    }
    private void LoadMap()
    {
        Enemies.Clear(); Projectiles.Clear(); Beams.Clear(); Particles.Clear(); Platforms.Clear();
        Player.Position = new Vector2(150, GroundY - 26);
        Player.Velocity = Vector2.Zero;
        Player.Health = 100;
        Player.Invincible = 0;
        Player.Grounded = true;
        Player.DashTime = Player.DashCooldown = 0;
        Player.DashDirection = Vector2.UnitX;
        Player.Aim = Vector2.UnitX;
        Player.Facing = 1;
        CameraX = 0;
        Heat = SlowLeft = Shake = MuzzleFlash = ShotRecoveryLeft = 0;
        ReloadLeft = _fireCooldown = _burstDelay = _coyote = _jumpBuffer = 0;
        NoticeLeft = 0;
        Reload(false);
        if (Map == 0)
        {
            Platforms.AddRange(new[] { new Platform(410, 505, 230, 22),
                new Platform(860, 442, 230, 22), new Platform(1270, 515, 230, 22),
                new Platform(1670, 430, 260, 22), new Platform(2060, 510, 210, 22) });
            SpawnEnemy(EnemyKind.Grunt, 700, GroundY, 540, 820);
            SpawnEnemy(EnemyKind.Gunner, 980, 442, 880, 1070);
            SpawnEnemy(EnemyKind.Grunt, 1280, GroundY, 1120, 1480);
            SpawnEnemy(EnemyKind.Grunt, 1600, GroundY, 1480, 1830);
            SpawnEnemy(EnemyKind.Gunner, 1810, 430, 1690, 1910);
            SpawnEnemy(EnemyKind.Gunner, 2190, GroundY, 2020, 2350);
        }
        else
        {
            Platforms.AddRange(new[] { new Platform(350, 495, 240, 22),
                new Platform(830, 420, 260, 22), new Platform(1320, 495, 240, 22) });
            SpawnEnemy(EnemyKind.Grunt, 680, GroundY, 610, 880);
            SpawnEnemy(EnemyKind.Gunner, 965, 420, 850, 1070);
            SpawnEnemy(EnemyKind.Lord, 1420, GroundY, 700, 1660);
        }
    }
    private void SpawnEnemy(EnemyKind kind, float x, float floor, float left, float right)
    {
        float hp = kind switch { EnemyKind.Lord => 700, EnemyKind.Gunner => 80, _ => 100 };
        Enemies.Add(new Enemy { Kind = kind, Position = new Vector2(x, floor -
            (kind == EnemyKind.Lord ? 42 : 26)), Health = hp, MaxHealth = hp,
            Left = left, Right = right, Cooldown = 1.2f + (float)_random.NextDouble() });
    }
    private void Reload(bool shuffle = true)
    {
        BulletKind[] rounds = { BulletKind.Piercing, BulletKind.Split, BulletKind.Explosive,
            BulletKind.Piercing, BulletKind.Split, BulletKind.Explosive };
        if (shuffle)
            for (int i = rounds.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (rounds[i], rounds[j]) = (rounds[j], rounds[i]);
            }
        rounds.CopyTo(Cylinder, 0);
        AmmoIndex = 0;
        EmpowerAvailable = true;
        EmpowerSelected = false;
    }
    public void Step(float realDt, GameInput input)
    {
        if (Phase != GamePhase.Playing) return;
        realDt = Math.Clamp(realDt, 0, 0.04f);
        Elapsed += realDt;
        float dt = realDt * (SlowLeft > 0 ? SlowScale : 1);
        SlowLeft = Math.Max(0, SlowLeft - realDt);
        Shake = Math.Max(0, Shake - realDt * 35);
        MuzzleFlash = Math.Max(0, MuzzleFlash - realDt);
        NoticeLeft = Math.Max(0, NoticeLeft - realDt);
        _fireCooldown = Math.Max(0, _fireCooldown - dt);
        ShotRecoveryLeft = Math.Max(0, ShotRecoveryLeft - dt);
        Player.Invincible = Math.Max(0, Player.Invincible - dt);
        Player.DashCooldown = Math.Max(0, Player.DashCooldown - dt);
        if (ReloadLeft > 0)
        {
            ReloadLeft = Math.Max(0, ReloadLeft - dt);
            if (ReloadLeft == 0) Reload();
        }
        if (_burstDelay > 0)
        {
            // Real time keeps both shotgun blasts distinct during slow motion.
            _burstDelay -= realDt;
            if (_burstDelay <= 0) Emit(BulletKind.Split, true, _burstDirection);
        }
        Vector2 aim = input.Aim - Player.Position;
        if (aim.LengthSquared() > 1) Player.Aim = Vector2.Normalize(aim);
        Player.Facing = Player.Aim.X >= 0 ? 1 : -1;
        if (input.Empower && EmpowerAvailable && Ammo > 0 && ReloadLeft == 0) EmpowerSelected = true;
        if (input.CancelEmpower) EmpowerSelected = false;
        bool dashed = input.Dash && TryDash();
        if (!dashed)
        {
            if (input.Barrage) TryBarrage();
            else if (input.Fire) TryFire();
        }
        MovePlayer(dt, input);
        UpdateEnemies(dt);
        UpdateProjectiles(dt);
        UpdateParticles(dt);
        foreach (var beam in Beams) beam.Life -= realDt;
        Beams.RemoveAll(b => b.Life <= 0);
        if (Player.Health <= 0)
        {
            Player.Health = 0;
            Phase = GamePhase.Failed;
            return;
        }
        if (ExitOpen)
        {
            if (Map == 1) { Phase = GamePhase.Won; return; }
            if (Player.Position.X > MapWidth - 140)
            {
                Map = 1;
                _checkpointKills = Kills;
                _checkpointTime = Elapsed;
                LoadMap();
                Say("결투장 진입 · 지배 악마를 처치하세요");
            }
        }
        float target = Math.Clamp(Player.Position.X - 420, 0, MapWidth - 1280);
        CameraX += (target - CameraX) * (1 - MathF.Exp(-dt * 8));
    }
    public bool TryFire()
    {
        if (Phase != GamePhase.Playing || ReloadLeft > 0 || _fireCooldown > 0 || Ammo == 0) return false;
        BulletKind kind = CurrentBullet;
        bool enhanced = EmpowerSelected && EmpowerAvailable;
        ConsumeAmmo(enhanced ? Math.Min(EmpowerCost, Ammo) : 1);
        if (enhanced) { EmpowerAvailable = false; EmpowerSelected = false; }
        Emit(kind, enhanced, Player.Aim);
        if (kind == BulletKind.Split && enhanced)
        {
            _burstDelay = 0.12f;
            _burstDirection = Player.Aim;
        }
        // Recovery is included in the firing interval, so normal fire stays at 1.3 shots/sec.
        _fireCooldown = enhanced ? 0.30f : NormalShotInterval;
        ShotRecoveryLeft = enhanced ? 0 : NormalShotRecovery;
        Heat = Math.Min(100, Heat + (enhanced ? 22 : 10));
        return true;
    }
    private void ConsumeAmmo(int count)
    {
        AmmoIndex += count;
        if (Ammo == 0) EmpowerSelected = false;
        if (Ammo == 0) ReloadLeft = ReloadDuration;
    }
    public bool TryDash()
    {
        if (Phase != GamePhase.Playing || Player.DashCooldown > 0 || Ammo == 0 || ReloadLeft > 0) return false;
        ConsumeAmmo(1);
        // Capture the mouse direction once: movement keys and later mouse motion do not steer a dash.
        Player.DashDirection = Player.Aim;
        Player.DashTime = DashDuration;
        Player.DashCooldown = 0.85f;
        Player.Velocity = Player.DashDirection * DashSpeed;
        Player.Invincible = Math.Max(Player.Invincible, 0.2f);
        Burst(Player.Position, BulletKind.Piercing, 14, 170);
        return true;
    }
    public bool TryBarrage()
    {
        if (Phase != GamePhase.Playing || Heat < 100 || Ammo == 0 || ReloadLeft > 0 ||
            _fireCooldown > 0 || _burstDelay > 0) return false;
        int remaining = Ammo;
        for (int i = 0; i < remaining; i++)
        {
            float angle = (i - (remaining - 1) / 2f) * 0.075f;
            Emit(Cylinder[AmmoIndex++], true, Rotate(Player.Aim, angle));
        }
        Heat = 0;
        EmpowerSelected = false;
        _fireCooldown = 0.4f;
        ReloadLeft = ReloadDuration;
        SlowLeft = 0.42f;
        Shake = 15;
        Say("전탄격발 · 남은 탄환 전체 소모");
        return true;
    }
    private void Emit(BulletKind kind, bool enhanced, Vector2 direction)
    {
        ShotsFired++;
        MuzzleFlash = 0.075f;
        Shake = Math.Max(Shake, enhanced ? 10 : 2.5f);
        if (enhanced) SlowLeft = SlowDuration;
        Vector2 origin = Player.Position + new Vector2(0, -5) + direction * 35;
        if (kind == BulletKind.Piercing && enhanced)
        {
            FireLaser(origin, direction);
            Burst(origin, kind, 13, 150);
            return;
        }
        int pellets = kind == BulletKind.Split ? (enhanced ? 8 : 6) : 1;
        for (int i = 0; i < pellets; i++)
        {
            float spread = kind == BulletKind.Split ? (i - (pellets - 1) / 2f) * (enhanced ? 0.055f : 0.09f) : 0;
            Projectiles.Add(new Projectile { Position = origin,
                Velocity = Rotate(direction, spread) * (kind == BulletKind.Explosive ? 730 : 1250),
                Kind = kind, Empowered = enhanced,
                Damage = kind switch { BulletKind.Piercing => enhanced ? 115 : 52,
                    BulletKind.Split => enhanced ? 17 : 14, _ => enhanced ? 140 : 78 },
                Radius = kind == BulletKind.Piercing ? enhanced ? 9 : 4 : kind == BulletKind.Split ? 3 : 7,
                Life = kind == BulletKind.Split ? 0.48f : 1.5f,
                TravelLeft = kind == BulletKind.Explosive && enhanced ? EnhancedExplosiveTravel : float.PositiveInfinity });
        }
        Burst(origin, kind, enhanced ? 13 : 6, 150);
    }
    private void FireLaser(Vector2 origin, Vector2 direction)
    {
        float length = LaserRange;
        if (direction.X > 0.001f) length = Math.Min(length, (MapWidth - origin.X) / direction.X);
        if (direction.X < -0.001f) length = Math.Min(length, -origin.X / direction.X);
        if (direction.Y > 0.001f) length = Math.Min(length, (GroundY - origin.Y) / direction.Y);
        if (direction.Y < -0.001f) length = Math.Min(length, (154 - origin.Y) / direction.Y);
        Vector2 end = origin + direction * Math.Max(0, length);
        Beams.Add(new LaserBeam { Start = origin, End = end, Radius = 9 });
        // Resolve damage now, without waiting for a projectile to travel or for the next simulation step.
        foreach (var enemy in Enemies)
            if (enemy.Health > 0 && SegmentDistance(enemy.Position, origin, end) <= enemy.Radius + 9)
                Damage(enemy, 115, BulletKind.Piercing);
    }
    private void MovePlayer(float dt, GameInput input)
    {
        _coyote = Player.Grounded ? 0.09f : Math.Max(0, _coyote - dt);
        _jumpBuffer = input.Jump ? 0.12f : Math.Max(0, _jumpBuffer - dt);
        float movementDt = dt;
        if (Player.DashTime > 0)
        {
            movementDt = Math.Min(dt, Player.DashTime);
            Player.DashTime = Math.Max(0, Player.DashTime - dt);
            Player.Velocity = Player.DashDirection * DashSpeed;
        }
        else
        {
            Player.Velocity.X = Math.Clamp(input.Move, -1, 1) * 310;
            Player.Velocity.Y += 1650 * dt;
            if (_jumpBuffer > 0 && _coyote > 0)
            {
                Player.Velocity.Y = -660;
                _jumpBuffer = _coyote = 0;
                Burst(Player.Position + new Vector2(0, 24), BulletKind.Piercing, 8, 85);
            }
        }
        float oldFeet = Player.Position.Y + 26;
        Player.Position += Player.Velocity * movementDt;
        Player.Position.X = Math.Clamp(Player.Position.X, 24, MapWidth - 30);
        if (Player.Position.Y < 200)
        {
            Player.Position.Y = 200;
            Player.Velocity.Y = 0;
        }
        Player.Grounded = false;
        float floor = GroundY;
        foreach (var p in Platforms)
            if (Player.Position.X + 16 > p.X && Player.Position.X - 16 < p.X + p.Width &&
                oldFeet <= p.Y + 1 && Player.Position.Y + 26 >= p.Y && Player.Velocity.Y >= 0)
                floor = Math.Min(floor, p.Y);
        if (Player.Position.Y + 26 >= floor && Player.Velocity.Y >= 0)
        {
            Player.Position.Y = floor - 26;
            Player.Velocity.Y = 0;
            Player.Grounded = true;
        }
    }
    private void UpdateEnemies(float dt)
    {
        foreach (var enemy in Enemies)
        {
            if (enemy.Health <= 0) continue;
            enemy.Flash = Math.Max(0, enemy.Flash - dt);
            Vector2 toPlayer = Player.Position - enemy.Position;
            enemy.Facing = toPlayer.X >= 0 ? 1 : -1;
            float distance = toPlayer.Length();
            if (distance > 1100) continue;
            enemy.Cooldown = Math.Max(0, enemy.Cooldown - dt);
            if (enemy.Telegraph > 0)
            {
                enemy.Telegraph -= dt;
                if (enemy.Telegraph <= 0)
                {
                    Vector2 direction = toPlayer.LengthSquared() > 0 ? Vector2.Normalize(toPlayer) : -Vector2.UnitX;
                    int count = enemy.Kind == EnemyKind.Lord ? 5 : 1;
                    for (int i = 0; i < count; i++)
                        Projectiles.Add(new Projectile { Hostile = true, Position = enemy.Position,
                            Velocity = Rotate(direction, (i - (count - 1) / 2f) * 0.17f) *
                                (enemy.Kind == EnemyKind.Lord ? 330 : 290),
                            Damage = enemy.Kind == EnemyKind.Lord ? 15 : 12,
                            Radius = enemy.Kind == EnemyKind.Lord ? 8 : 6, Life = 3.5f,
                            Kind = BulletKind.Explosive });
                    enemy.Cooldown = enemy.Kind == EnemyKind.Lord ? 2.6f : 2.2f;
                }
            }
            else if (enemy.Kind != EnemyKind.Grunt && enemy.Cooldown == 0 && distance < 920)
                enemy.Telegraph = enemy.Kind == EnemyKind.Lord ? 0.85f : 0.65f;
            if ((enemy.Kind is EnemyKind.Grunt or EnemyKind.Lord) && enemy.Telegraph <= 0)
            {
                float speed = enemy.Kind == EnemyKind.Lord ? 78 : 98;
                if (Math.Abs(toPlayer.X) > enemy.Radius + 18)
                    enemy.Position.X = Math.Clamp(enemy.Position.X + enemy.Facing * speed * dt, enemy.Left, enemy.Right);
            }
            if (distance < enemy.Radius + 21) HurtPlayer(enemy.Kind == EnemyKind.Lord ? 22 : 13);
        }
    }
    private void UpdateProjectiles(float dt)
    {
        foreach (var bullet in Projectiles)
        {
            if (bullet.Life <= 0) continue;
            Vector2 previous = bullet.Position;
            Vector2 travel = bullet.Velocity * dt;
            float distance = travel.Length();
            if (distance > bullet.TravelLeft && distance > 0)
                travel *= bullet.TravelLeft / distance;
            bullet.Position += travel;
            bullet.TravelLeft = Math.Max(0, bullet.TravelLeft - travel.Length());
            bullet.Life -= dt;
            if (bullet.Hostile)
            {
                if (SegmentDistance(Player.Position, previous, bullet.Position) < 19 + bullet.Radius)
                {
                    HurtPlayer(bullet.Damage);
                    bullet.Life = 0;
                    Burst(bullet.Position, BulletKind.Explosive, 7, 100);
                }
            }
            else
            {
                foreach (var enemy in Enemies.OrderBy(e => Vector2.DistanceSquared(previous, e.Position)))
                {
                    if (enemy.Health <= 0 || bullet.Hit.Contains(enemy)) continue;
                    if (SegmentDistance(enemy.Position, previous, bullet.Position) > enemy.Radius + bullet.Radius) continue;
                    bullet.Hit.Add(enemy);
                    if (bullet.Kind == BulletKind.Explosive)
                    {
                        Explode(enemy.Position, bullet);
                        bullet.Life = 0;
                        break;
                    }
                    Damage(enemy, bullet.Damage, bullet.Kind);
                    if (bullet.Kind != BulletKind.Piercing || !bullet.Empowered)
                    {
                        bullet.Life = 0;
                        break;
                    }
                }
            }
            if (bullet.Life > 0 && bullet.Position.Y >= GroundY)
            {
                bullet.Position.Y = GroundY;
                if (!bullet.Hostile && bullet.Kind == BulletKind.Explosive) Explode(bullet.Position, bullet);
                else Burst(bullet.Position, bullet.Kind, 4, 65);
                bullet.Life = 0;
            }
            if (bullet.Life > 0 && bullet.Kind == BulletKind.Explosive && bullet.TravelLeft == 0)
            {
                Explode(bullet.Position, bullet);
                bullet.Life = 0;
            }
            if (bullet.Position.X < -100 || bullet.Position.X > MapWidth + 100 || bullet.Position.Y < 100)
                bullet.Life = 0;
        }
        Projectiles.RemoveAll(p => p.Life <= 0);
    }
    private void Explode(Vector2 center, Projectile bullet)
    {
        float radius = bullet.Empowered ? EnhancedExplosionRadius : 92;
        foreach (var enemy in Enemies)
            if (enemy.Health > 0 && Vector2.Distance(center, enemy.Position) < radius + enemy.Radius)
                Damage(enemy, bullet.Damage, BulletKind.Explosive);
        Particles.Add(new Particle { Position = center, Ring = true, Size = radius,
            Life = 0.35f, MaxLife = 0.35f, Kind = BulletKind.Explosive });
        Burst(center, BulletKind.Explosive, bullet.Empowered ? 38 : 22, 260);
        Shake = Math.Max(Shake, bullet.Empowered ? 12 : 5);
    }
    private void Damage(Enemy enemy, float amount, BulletKind kind)
    {
        enemy.Health = Math.Max(0, enemy.Health - amount);
        enemy.Flash = 0.1f;
        Heat = Math.Min(100, Heat + 1.8f);
        Burst(enemy.Position, kind, 6, 110);
        Particles.Add(new Particle { Position = enemy.Position + new Vector2(-9, -36),
            Velocity = new Vector2(0, -45), Text = ((int)amount).ToString(), Kind = kind,
            Life = 0.65f, MaxLife = 0.65f });
        if (enemy.Health <= 0)
        {
            Kills++;
            Burst(enemy.Position, kind, 25, 220);
            if (ExitOpen && Map == 0) Say("영지 입구 정리 완료 · 오른쪽 출구로 이동하세요");
        }
    }
    private void HurtPlayer(float amount)
    {
        if (Player.Invincible > 0) return;
        Player.Health = Math.Max(0, Player.Health - amount);
        Player.Invincible = 0.85f;
        Shake = 7;
        Burst(Player.Position, BulletKind.Explosive, 14, 150);
    }
    private void Burst(Vector2 position, BulletKind kind, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float life = 0.2f + (float)_random.NextDouble() * 0.35f;
            Particles.Add(new Particle { Position = position,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * (float)_random.NextDouble(),
                Kind = kind, Life = life, MaxLife = life, Size = 2 + (float)_random.NextDouble() * 3 });
        }
    }
    private void UpdateParticles(float dt)
    {
        foreach (var p in Particles)
        {
            p.Life -= dt;
            p.Position += p.Velocity * dt;
            if (p.Text == null) p.Velocity.Y += dt * 320;
        }
        Particles.RemoveAll(p => p.Life <= 0);
    }
    private void Say(string text) { Notice = text; NoticeLeft = 4; }
    private static Vector2 Rotate(Vector2 v, float angle) =>
        new(v.X * MathF.Cos(angle) - v.Y * MathF.Sin(angle), v.X * MathF.Sin(angle) + v.Y * MathF.Cos(angle));
    internal static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.LengthSquared() < 0.001f ? 0 : Math.Clamp(Vector2.Dot(point - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(point, a + ab * t);
    }
}
