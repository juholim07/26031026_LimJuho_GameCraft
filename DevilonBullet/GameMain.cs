using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

sealed partial class GameMain : G2AppBase
{
    private readonly GameWorld _world = new();
    private readonly Dictionary<int, G2Font> _fonts = new();
    private ID2D1SolidColorBrush _brush = null!;
    private Vector2 _mouse = new(700, 450);
    private float _visualTime;
    private bool _showHelp;

    public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
    public override string GameName => GameGlobal.GameName;

    protected override void Initialize()
    {
        ClearColor = new Color4(0.035f, 0.043f, 0.065f, 1);
        _brush = RenderTarget.CreateSolidColorBrush(new Color4(1, 1, 1, 1));
        LoadSprites();
        foreach (int size in new[] { 11, 13, 15, 18, 22, 28, 42, 64 })
        {
            _fonts[size] = new G2Font("Malgun Gothic", size,
                size >= 22 ? FontWeight.Bold : FontWeight.Normal);
            _fonts[-size] = new G2Font("Malgun Gothic", size,
                size >= 22 ? FontWeight.Bold : FontWeight.Normal,
                Vortice.DirectWrite.FontStyle.Normal, TextAlignment.Center);
        }
    }

    private bool Held(Keys key) => Input.IsKeyDown(key) || Input.IsKeyPress(key);

    protected override void Update()
    {
        float dt = Math.Min((float)DeltaTime, 0.04f);
        _visualTime += dt;
        _mouse = new Vector2(Input.MousePosition.X, Input.MousePosition.Y);
        bool click = Input.IsButtonDown(MouseButtons.Left) && Input.MouseInViewport;
        if (Input.IsKeyDown(Keys.F1))
        {
            _showHelp = !_showHelp;
            if (_showHelp && _world.Phase == GamePhase.Playing) _world.TogglePause();
            else if (!_showHelp && _world.Phase == GamePhase.Paused) _world.TogglePause();
        }
        if (Input.IsKeyDown(Keys.Escape))
        {
            if (_showHelp) _showHelp = false;
            else _world.TogglePause();
        }
        if ((Input.IsKeyDown(Keys.Enter) && !Held(Keys.Menu)) ||
            (click && InBox(_mouse, 476, 440, 328, 58) && _world.Phase != GamePhase.Playing))
        {
            _showHelp = false;
            switch (_world.Phase)
            {
                case GamePhase.Ready: _world.Start(); break;
                case GamePhase.Paused: _world.TogglePause(); break;
                case GamePhase.Failed: _world.RestartMap(); break;
                case GamePhase.Won: _world.NewRun(); _world.Start(); break;
            }
            return;
        }
        if (_showHelp) return;
        float move = (Held(Keys.D) || Held(Keys.Right) ? 1 : 0) -
                     (Held(Keys.A) || Held(Keys.Left) ? 1 : 0);
        var input = new GameInput(move,
            Input.IsKeyDown(Keys.Space) || Input.IsKeyDown(Keys.W) || Input.IsKeyDown(Keys.Up),
            Input.IsKeyDown(Keys.ShiftKey) ||
                (Input.IsButtonDown(MouseButtons.Right) && Input.MouseInViewport),
            click && _mouse.Y is > 150 and < 650,
            Input.MouseWheelDelta < 0,
            Input.MouseWheelDelta > 0,
            Input.IsKeyDown(Keys.Q),
            _mouse + new Vector2(_world.CameraX, 0));
        _world.Step(dt, input);
    }

    private static bool InBox(Vector2 p, float x, float y, float w, float h) =>
        p.X >= x && p.X <= x + w && p.Y >= y && p.Y <= y + h;

    protected override void Render() => DrawGame();

    public override void Dispose()
    {
        foreach (var sprite in _sprites.Values) sprite.Dispose();
        foreach (var font in _fonts.Values) font.Dispose();
        _brush?.Dispose();
        base.Dispose();
    }
}
