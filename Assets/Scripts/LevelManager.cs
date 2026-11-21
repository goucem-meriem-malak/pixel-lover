using System.Collections;
using UnityEngine;

/// <summary>
/// Builds each level from LevelGenerator data and runs the story:
///  * Player 1 (boy, a knight) plays an EARTH world: grass, hills, bushes, green pipes. He collects coins.
///  * Player 2 (girl, a princess) plays a CLOUD world: cloud ground, rainbows, sparkles, pink pipes.
///    She collects flowers and finds growth mushrooms / fire flowers in the question blocks.
///  * After level 10 the boy walks to the flag, sits down, spends his coins on a ring, and gets his
///    invitation code. When the girl reaches him on her level 10 the proposal cutscene plays.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public const int TotalLevels = 10;
    public const string SavedCodeKey = "lastRoomCode";
    public const string SavedNameKey = "playerName";

    [Header("Camera")]
    [Tooltip("About how many tiles tall the screen shows (the zoom snaps to whole pixels).")]
    public float rows = 11f;

    [Header("Debug")]
    [Tooltip("Shows a SKIP button in the HUD so you can reach level 10 quickly while testing.")]
    public bool debugSkipButton = false;

    public PlayerRole Role { get; private set; }
    public string PlayerName { get; private set; }
    public string BoyName { get; private set; }
    public int RingId { get; private set; }
    public int CurrentLevel { get; private set; }
    public int Coins { get; private set; }
    public bool IsSky { get { return Role == PlayerRole.Girl; } }
    public Transform LevelRoot { get { return levelRoot != null ? levelRoot.transform : null; } }
    public Transform PlayerTransform { get { return player != null ? player.transform : null; } }
    public Collider2D PlayerCollider { get { return player != null ? player.BodyCollider : null; } }

    GameObject levelRoot, skyObj;
    PlayerController2D player;
    LevelData data;
    Camera cam;
    CameraFollow2D camFollow;
    EndingCutscene cutscene;
    GameObject boyNpc;
    Vector3 spawnPoint;
    bool levelEnding;

    float WaitX { get { return data.flagX - 1.5f; } }   // where the boy sits down

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Physics2D.queriesStartInColliders = false;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        if (!TryGetComponent(out cutscene)) cutscene = gameObject.AddComponent<EndingCutscene>();
    }

    // ------------------------------------------------------------------ public flow API

    /// <param name="room">Only for the girl: what she learned about the boy's room (his name and the ring).</param>
    public void StartGame(PlayerRole role, string playerName, RoomInfo room)
    {
        Role = role;
        PlayerName = NameUtil.Clean(playerName, role == PlayerRole.Boy ? "KNIGHT" : "PRINCESS");
        BoyName = room != null ? NameUtil.Clean(room.boyName, "KNIGHT") : PlayerName;
        RingId = room != null ? room.ring : 0;
        Coins = 0;
        PlayerPrefs.SetString(SavedNameKey, PlayerName);
        LoadLevel(1);
    }

    public void ReturnToMenu()
    {
        StopAllCoroutines();
        if (cutscene != null) cutscene.Abort();
        ClearLevel();
        if (camFollow != null) camFollow.target = null;
        TouchInput.Reset();
        UIManager.Instance.ShowStartMenu();
    }

    public void LoadLevel(int n)
    {
        n = Mathf.Clamp(n, 1, TotalLevels);
        ClearLevel();
        CurrentLevel = n;
        levelEnding = false;
        TouchInput.Reset();

        data = LevelGenerator.Generate(n);
        levelRoot = new GameObject("Level_" + n);

        SetupCamera();
        BuildScenery(n);
        BuildTerrain();
        BuildStairsAndPipes();
        BuildBlocks();
        BuildPickups();
        BuildEnemies();
        BuildFlagCastleGoal();

        spawnPoint = new Vector3(data.startPos.x, data.startPos.y + 0.05f, 0f);
        CreatePlayer(spawnPoint);
        if (n == TotalLevels && Role == PlayerRole.Girl) CreateWaitingBoyAndTrigger();

        camFollow.target = player.transform;
        camFollow.minX = 0f;
        camFollow.maxX = data.endX;
        camFollow.Snap();

        UIManager.Instance.ShowGameplay();
        UpdateHud();
        UIManager.Instance.ShowToast("LEVEL " + n, 1.4f);
    }

    public void UpdateHud()
    {
        UIManager.Instance.SetHud(Role, CurrentLevel, TotalLevels, Coins, player != null ? player.Form : PowerState.Small);
    }

    public void OnPlayerDied() { StartCoroutine(RespawnRoutine()); }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(0.9f);
        if (player == null || levelEnding) yield break;
        player.Respawn(spawnPoint);
        camFollow.Snap();
        UpdateHud();
    }

    /// <summary>Called by the debug SKIP button.</summary>
    public void DebugSkipLevel()
    {
        if (player == null || levelEnding) return;
        if (CurrentLevel < TotalLevels) { levelEnding = true; StartCoroutine(NextLevelRoutine()); }
        else if (Role == PlayerRole.Boy) { levelEnding = true; StartCoroutine(BoyFinaleRoutine()); }
        else if (boyNpc != null) OnReunionTriggered();
    }

    // ------------------------------------------------------------------ collecting

    /// <summary>A coin (boy) or flower (girl) was touched.</summary>
    public void Collect(PickupKind kind, Vector3 pos)
    {
        Coins++;
        UpdateHud();
        PopFx.Spawn(levelRoot.transform, SpriteFactory.Get("sparkle_1"), pos, new Vector2(0f, 1.5f), 0f, 0.35f, 20);
    }

    /// <summary>A block released a coin/flower: it jumps out and is collected straight away.</summary>
    public void PopCollectible(Vector3 pos)
    {
        Coins++;
        UpdateHud();
        string sprite = IsSky ? "flower_1" : "coin_1";
        PopFx.Spawn(levelRoot.transform, SpriteFactory.Get(sprite), pos + (IsSky ? new Vector3(0f, -0.5f, 0f) : Vector3.zero),
                    new Vector2(0f, 9f), 28f, 0.75f, 20);
    }

    public void PopCoins(Vector3 pos, int count) { StartCoroutine(PopMany(pos, count)); }

    IEnumerator PopMany(Vector3 pos, int count)
    {
        for (int i = 0; i < count; i++)
        {
            PopCollectible(pos);
            yield return new WaitForSeconds(0.12f);
        }
    }

    /// <summary>Growth mushroom / fire flower coming out of a block (girl only).</summary>
    public void SpawnPowerUp(Vector3 blockCentre, PowerKind kind)
    {
        var go = new GameObject("PowerUp");
        go.transform.SetParent(levelRoot.transform, false);
        go.transform.position = blockCentre + new Vector3(0f, -0.2f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get(kind == PowerKind.Grow ? "item_mushroom" : "item_fireflower");
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        var mat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
        var solid = go.AddComponent<BoxCollider2D>();
        solid.size = new Vector2(0.8f, 0.8f);
        solid.offset = new Vector2(0f, 0.4f);
        solid.sharedMaterial = mat;
        var trig = go.AddComponent<CircleCollider2D>();
        trig.isTrigger = true;
        trig.radius = 0.6f;
        trig.offset = new Vector2(0f, 0.5f);
        var pu = go.AddComponent<PowerUp>();
        pu.kind = kind;
    }

    public void SpawnFireball(Vector2 pos, int dir)
    {
        var go = new GameObject("Fireball");
        go.transform.SetParent(levelRoot.transform, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        var anim = go.AddComponent<SpriteAnimator>();
        anim.SetFrames(SpriteFactory.Frames("fireball_", 2), 12f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var cc = go.AddComponent<CircleCollider2D>();
        cc.radius = 0.22f;
        cc.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
        var fb = go.AddComponent<Fireball>();
        fb.dir = dir;
        if (PlayerCollider != null) Physics2D.IgnoreCollision(cc, PlayerCollider);
    }

    // ------------------------------------------------------------------ goals / finale

    public void OnGoalReached()
    {
        if (levelEnding || player == null || player.IsDead) return;
        levelEnding = true;
        if (CurrentLevel < TotalLevels) StartCoroutine(NextLevelRoutine());
        else if (Role == PlayerRole.Boy) StartCoroutine(BoyFinaleRoutine());
    }

    IEnumerator NextLevelRoutine()
    {
        player.Invincible = true;
        player.ControlEnabled = false;
        TouchInput.Reset();
        UIManager.Instance.ShowToast("LEVEL " + CurrentLevel + " CLEAR!", 1.4f);
        yield return new WaitForSeconds(1.4f);
        LoadLevel(CurrentLevel + 1);
    }

    /// <summary>Boy cleared level 10: walk to the flag, sit, buy a ring, register the room, show the code.</summary>
    IEnumerator BoyFinaleRoutine()
    {
        player.Invincible = true;
        player.ControlEnabled = false;
        TouchInput.Reset();
        UIManager.Instance.HideTouchControls();

        yield return player.WalkTo(WaitX);
        player.SitDown();
        UIManager.Instance.ShowToast("NOW WE WAIT FOR HER...", 2f);
        yield return new WaitForSeconds(1.6f);
        UIManager.Instance.ShowRingShop(Coins);
    }

    /// <summary>The ring shop calls this when the boy has chosen (and can afford) a ring.</summary>
    public void ChooseRing(int ring)
    {
        RingId = Mathf.Clamp(ring, 0, Rings.Count - 1);
        StartCoroutine(RegisterRoomRoutine());
    }

    IEnumerator RegisterRoomRoutine()
    {
        UIManager.Instance.ShowToast("CREATING YOUR INVITATION CODE...", 20f);
        bool done = false, ok = false;
        string code = null, error = null;

        FirebaseManager.Instance.CreateRoom(PlayerName, RingId, (success, roomCode, err) =>
        {
            ok = success; code = roomCode; error = err; done = true;
        });
        while (!done) yield return null;

        UIManager.Instance.HideToast();
        if (ok)
        {
            PlayerPrefs.SetString(SavedCodeKey, code);
            PlayerPrefs.Save();
            UIManager.Instance.ShowInvitation(code);
        }
        else
        {
            UIManager.Instance.ShowInvitationError(error);
        }
    }

    /// <summary>Retry button on the invitation screen after a failed Firebase call.</summary>
    public void RetryRegisterRoom() { StartCoroutine(RegisterRoomRoutine()); }

    public void OnReunionTriggered()
    {
        if (levelEnding || player == null || player.IsDead || boyNpc == null) return;
        levelEnding = true;
        cutscene.Play(player, boyNpc, BoyName, PlayerName, RingId);
    }

    public void OnEndingFinished() { UIManager.Instance.ShowTheEnd(); }

    // ------------------------------------------------------------------ level construction

    void ClearLevel()
    {
        if (levelRoot != null)
        {
            levelRoot.SetActive(false);   // disable immediately, destroy at end of frame
            Destroy(levelRoot);
            levelRoot = null;
        }
        if (skyObj != null) { Destroy(skyObj); skyObj = null; }
        player = null;
        boyNpc = null;
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        // whole-pixel zoom: every art pixel covers the same whole number of screen pixels
        int zoom = Mathf.Max(1, Mathf.RoundToInt(Screen.height / (SpriteFactory.PPU * rows)));
        cam.orthographicSize = Screen.height / (2f * SpriteFactory.PPU * zoom);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = IsSky ? new Color(0.99f, 0.78f, 0.89f) : new Color(0.45f, 0.72f, 0.98f);
        if (!cam.TryGetComponent(out camFollow)) camFollow = cam.gameObject.AddComponent<CameraFollow2D>();

        skyObj = SkyGradient.Create(cam, IsSky
            ? new[] { new Color(0.70f, 0.62f, 0.98f), new Color(0.99f, 0.72f, 0.90f), new Color(1.00f, 0.91f, 0.84f) }
            : new[] { new Color(0.28f, 0.55f, 0.95f), new Color(0.50f, 0.77f, 0.98f), new Color(0.80f, 0.93f, 1.00f) });
    }

    GameObject Spr(string name, Transform parent, string sprite, Vector2 pos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get(sprite);
        sr.sortingOrder = order;
        return go;
    }

    GameObject Tiled(string name, Transform parent, string sprite, Vector2 center, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get(sprite);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingOrder = order;
        return go;
    }

    /// <summary>A layer whose items repeat every 'period' units and scroll slower than the camera.</summary>
    void AddParallax(string name, string sprite, float[] xs, float[] ys, float period, float scroll, int order, float scale, Color tint)
    {
        var layer = new GameObject(name);
        layer.transform.SetParent(levelRoot.transform, false);
        var p = layer.AddComponent<ParallaxLayer>();
        p.cam = cam.transform;
        p.scroll = scroll;
        p.period = period;
        p.lockWorldY = true;
        p.worldY = 0f;
        int copies = Mathf.CeilToInt(18f / period) + 1;
        for (int k = -copies; k <= copies; k++)
            for (int i = 0; i < xs.Length; i++)
            {
                var go = new GameObject(sprite);
                go.transform.SetParent(layer.transform, false);
                go.transform.localPosition = new Vector3(k * period + xs[i], ys[i], 0f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Get(sprite);
                sr.sortingOrder = order;
                sr.color = tint;
            }
    }

    void BuildScenery(int level)
    {
        var rnd = new System.Random(level * 31);
        if (!IsSky)
        {
            AddParallax("FarHills", "bg_hill", new[] { 0f }, new[] { -0.8f }, 10.2f, 0.20f, -60, 1.7f, new Color(0.80f, 0.92f, 1f, 0.9f));
            AddParallax("Clouds", "bg_cloud", new[] { 2f, 11f, 19f }, new[] { 7.3f, 8.6f, 6.6f }, 26f, 0.55f, -55, 1f, Color.white);
            AddParallax("NearHills", "bg_hill", new[] { 0f }, new[] { -0.6f }, 6f, 0.45f, -50, 1f, Color.white);

            // bushes on the grass
            foreach (var g in data.grounds)
                for (float bx = g.x0 + 1.5f; bx < g.x1 - 1.5f; bx += 4f + (float)rnd.NextDouble() * 5f)
                    if (rnd.NextDouble() < 0.5)
                        Spr("Bush", levelRoot.transform, "bg_bush", new Vector2(bx, g.top), 2);
        }
        else
        {
            AddParallax("Rainbows", "bg_rainbow", new[] { 4f, 19f }, new[] { -1.2f, -1.6f }, 30f, 0.25f, -60, 1.5f, new Color(1f, 1f, 1f, 0.9f));
            AddParallax("BigClouds", "bg_cloud_pink", new[] { 0f, 10f, 20f }, new[] { 6.5f, 8.2f, 5.6f }, 28f, 0.55f, -55, 1.6f, Color.white);
            AddParallax("Clouds", "bg_cloud_pink", new[] { 5f, 15f, 24f }, new[] { 3.5f, 4.8f, 3.0f }, 30f, 0.7f, -50, 1f, new Color(1f, 1f, 1f, 0.9f));

            // twinkling sparkles scattered over the level
            for (int i = 0; i < data.endX / 3f; i++)
            {
                var go = Spr("Sparkle", levelRoot.transform, rnd.NextDouble() < 0.5 ? "sparkle_1" : "sparkle_2",
                             new Vector2((float)rnd.NextDouble() * (data.endX + 6f) - 3f, 1.5f + (float)rnd.NextDouble() * 8f), -40);
                var tw = go.AddComponent<Twinkle>();
                tw.speed = 2f + (float)rnd.NextDouble() * 3f;
            }
        }
    }

    void BuildTerrain()
    {
        string top = IsSky ? "tile_cloud_top" : "tile_grass";
        string body = IsSky ? "tile_cloud_body" : "tile_dirt";
        float depth = IsSky ? 3f : 8f;

        foreach (var g in data.grounds)
        {
            float w = g.x1 - g.x0;
            float cx = (g.x0 + g.x1) * 0.5f;
            var go = Tiled("Ground", levelRoot.transform, body, new Vector2(cx, g.top - depth * 0.5f), new Vector2(w, depth), 0);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(w, depth);
            Tiled("Top", go.transform, top, new Vector2(cx, g.top - 0.5f), new Vector2(w, 1f), 1);
        }

        // invisible walls at both ends of the level
        MakeWall(-0.5f);
        MakeWall(data.endX + 0.5f);
    }

    void MakeWall(float x)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(levelRoot.transform, false);
        go.transform.position = new Vector3(x, 10f, 0f);
        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(1f, 60f);
    }

    void BuildStairsAndPipes()
    {
        string stone = IsSky ? "tile_block_used_sky" : "tile_block_used";
        foreach (var s in data.stairs)
            for (int i = 0; i < s.steps; i++)
            {
                float h = i + 1;
                var go = Tiled("Stair", levelRoot.transform, stone, new Vector2(s.x0 + i + 0.5f, s.top + h * 0.5f), new Vector2(1f, h), 3);
                var bc = go.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(1f, h);
            }

        string pipe = IsSky ? "pipe_pink" : "pipe_green";
        foreach (var p in data.pipes)
        {
            // collider for the whole pipe
            var root = new GameObject("Pipe");
            root.transform.SetParent(levelRoot.transform, false);
            root.transform.position = new Vector3(p.x, p.top + p.height * 0.5f, 0f);
            var bc = root.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(1.9f, p.height);

            Spr("Cap", root.transform, pipe + "_cap", new Vector2(p.x, p.top + p.height - 0.5f), 4);
            if (p.height > 1f)
                Tiled("Body", root.transform, pipe + "_body", new Vector2(p.x, p.top + (p.height - 1f) * 0.5f), new Vector2(1.75f, p.height - 1f), 4);

            if (p.plant) CreatePlant(p.x, p.top + p.height);
        }
    }

    void CreatePlant(float x, float pipeTop)
    {
        var go = new GameObject("PiranhaPlant");
        go.transform.SetParent(levelRoot.transform, false);
        go.transform.position = new Vector3(x, pipeTop - 1.55f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 3;                       // behind the pipe so it hides inside it
        go.AddComponent<SpriteAnimator>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var bc = go.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
        bc.size = new Vector2(0.7f, 1.2f);
        bc.offset = new Vector2(0f, 0.75f);
        var plant = go.AddComponent<PiranhaPlant>();
        plant.pipeTop = pipeTop;
    }

    void BuildBlocks()
    {
        foreach (var b in data.blocks)
        {
            string sprite = b.type == BlockType.Question ? (IsSky ? "tile_question_pink" : "tile_question") : (IsSky ? "tile_brick_sky" : "tile_brick");
            var go = Spr("Block", levelRoot.transform, sprite, new Vector2(b.x, b.y), 3);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            var blk = go.AddComponent<Block>();
            blk.type = b.type;
            blk.content = b.content;
            blk.sky = IsSky;
        }
    }

    void BuildPickups()
    {
        foreach (var c in data.coins)
        {
            var go = new GameObject(IsSky ? "Flower" : "Coin");
            go.transform.SetParent(levelRoot.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 7;
            var anim = go.AddComponent<SpriteAnimator>();
            var cc = go.AddComponent<CircleCollider2D>();
            cc.isTrigger = true;
            cc.radius = 0.45f;
            var pk = go.AddComponent<Pickup>();
            if (IsSky)
            {
                go.transform.position = new Vector3(c.x, c.y - 0.5f, 0f);   // flower sprites have their pivot at the stem
                cc.offset = new Vector2(0f, 0.5f);
                anim.SetFrames(SpriteFactory.Frames("flower_", 2), 2.5f);
                pk.kind = PickupKind.Flower;
            }
            else
            {
                go.transform.position = new Vector3(c.x, c.y, 0f);
                anim.SetFrames(SpriteFactory.Frames("coin_", 4), 8f);
                pk.kind = PickupKind.Coin;
            }
        }
    }

    void BuildEnemies()
    {
        foreach (var e in data.enemies)
        {
            var go = new GameObject(e.kind == EnemyKind.Cat ? "Cat" : "Turtle");
            go.transform.SetParent(levelRoot.transform, false);
            go.transform.position = new Vector3(e.x, e.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 6;
            go.AddComponent<SpriteAnimator>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;

            if (e.kind == EnemyKind.Cat)
            {
                bc.size = new Vector2(0.9f, 0.75f);
                bc.offset = new Vector2(0f, 0.4f);
                var cat = go.AddComponent<Cat>();
                cat.speed = e.speed;
                cat.variant = e.variant;
            }
            else
            {
                bc.size = new Vector2(0.8f, 1.1f);
                bc.offset = new Vector2(0f, 0.6f);
                var t = go.AddComponent<Turtle>();
                t.speed = e.speed;
                t.purple = (e.variant % 2) == 1;
            }
        }
    }

    void BuildFlagCastleGoal()
    {
        var root = levelRoot.transform;
        float top = data.flagTop;
        Spr("Castle", root, IsSky ? "castle_pink" : "castle_stone", new Vector2(data.castleX, top), -2);
        Spr("FlagPole", root, "flag_pole", new Vector2(data.flagX, top), 1);
        Spr("FlagCloth", root, "flag_cloth", new Vector2(data.flagX + 0.6f, top + 4.3f), 1);

        // The girl never "clears" level 10 with a goal: she triggers the proposal instead.
        if (CurrentLevel == TotalLevels && Role == PlayerRole.Girl) return;

        // The boy on level 10 starts his finale a few steps before the flag, then walks the rest.
        float goalX = CurrentLevel == TotalLevels ? data.flagX - 5f : data.flagX;
        var goal = new GameObject("Goal");
        goal.transform.SetParent(root, false);
        goal.transform.position = new Vector3(goalX, top + 4f, 0f);
        var bc = goal.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
        bc.size = new Vector2(1f, 12f);
        goal.AddComponent<LevelGoal>();
    }

    void CreatePlayer(Vector3 pos)
    {
        var go = new GameObject("Player");
        go.transform.SetParent(levelRoot.transform, false);
        go.transform.position = pos;
        player = go.AddComponent<PlayerController2D>();   // adds Rigidbody2D + BoxCollider2D
        player.Setup(Role, PlayerName);
    }

    void CreateWaitingBoyAndTrigger()
    {
        boyNpc = new GameObject("Boy_Waiting");
        boyNpc.transform.SetParent(levelRoot.transform, false);
        boyNpc.transform.position = new Vector3(WaitX, data.flagTop, 0f);
        var sr = boyNpc.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("knight_sit");
        sr.sortingOrder = 9;
        NameTag.Create(boyNpc.transform, BoyName, () => 1.75f);

        var trig = new GameObject("ReunionTrigger");
        trig.transform.SetParent(levelRoot.transform, false);
        trig.transform.position = new Vector3(WaitX - 5f, data.flagTop + 4f, 0f);
        var bc = trig.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
        bc.size = new Vector2(1f, 12f);
        trig.AddComponent<ReunionTrigger>();
    }
}
