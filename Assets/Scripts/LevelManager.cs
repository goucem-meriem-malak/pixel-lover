using System.Collections;
using UnityEngine;

public enum PlayerRole { Boy, Girl }

/// <summary>
/// Builds each level from LevelGenerator data, spawns the player, handles level transitions,
/// the Level-10 finale for the boy (walk to flag, sit, register room, show invitation code)
/// and the Level-10 reunion trigger for the girl (starts EndingCutscene).
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public const int TotalLevels = 10;
    public const string SavedCodeKey = "lastRoomCode";

    [Header("Optional custom assets (leave empty to use the built-in pixel sprites)")]
    public GameObject playerPrefab;
    public Sprite customBoySprite;
    public Sprite customBoySittingSprite;
    public Sprite customGirlSprite;

    [Header("Camera")]
    public float cameraSize = 5.5f;

    [Header("Debug")]
    [Tooltip("Shows a SKIP button in the HUD so you can reach level 10 quickly while testing.")]
    public bool debugSkipButton = false;

    public PlayerRole Role { get; private set; }
    public int CurrentLevel { get; private set; }
    public int Coins { get; private set; }
    public Transform LevelRoot { get { return levelRoot != null ? levelRoot.transform : null; } }

    public Sprite BoyStandingSprite { get { return customBoySprite != null ? customBoySprite : SpriteFactory.BoyStand(); } }
    public Sprite BoySittingSprite { get { return customBoySittingSprite != null ? customBoySittingSprite : SpriteFactory.BoySit(); } }
    public Sprite GirlStandingSprite { get { return customGirlSprite != null ? customGirlSprite : SpriteFactory.GirlStand(); } }
    public Sprite HeartSprite { get { return SpriteFactory.Heart(); } }
    public Sprite BouquetSprite { get { return SpriteFactory.Bouquet(); } }

    GameObject levelRoot;
    PlayerController2D player;
    LevelData data;
    Camera cam;
    CameraFollow2D camFollow;
    EndingCutscene cutscene;
    GameObject boyNpc;
    Vector3 spawnPoint;
    bool levelEnding;

    static readonly Color[] SkyColors =
    {
        new Color(0.36f, 0.62f, 0.95f), new Color(0.45f, 0.75f, 0.95f), new Color(0.98f, 0.72f, 0.45f),
        new Color(0.55f, 0.55f, 0.90f), new Color(0.40f, 0.80f, 0.75f), new Color(0.95f, 0.60f, 0.65f),
        new Color(0.30f, 0.45f, 0.75f), new Color(0.85f, 0.55f, 0.40f), new Color(0.22f, 0.28f, 0.55f),
        new Color(1.00f, 0.75f, 0.80f)
    };

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

    public void StartGame(PlayerRole role)
    {
        Role = role;
        Coins = 0;
        LoadLevel(1);
    }

    public void ReturnToMenu()
    {
        StopAllCoroutines();
        if (cutscene != null) cutscene.Abort();
        ClearLevel();
        if (camFollow != null) camFollow.target = null;
        if (cam != null) cam.orthographicSize = cameraSize;
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

        SetupCamera(n);
        BuildScenery(n);
        BuildTerrain();
        BuildPickupsAndHazards();
        BuildFlagAndGoal();

        spawnPoint = new Vector3(data.startPos.x, data.startPos.y + 0.05f, 0f);
        player = CreatePlayer(spawnPoint);

        boyNpc = null;
        if (n == TotalLevels && Role == PlayerRole.Girl) CreateWaitingBoyAndTrigger();

        camFollow.target = player.transform;
        camFollow.minX = 0f;
        camFollow.maxX = data.endX;
        camFollow.Snap();

        UIManager.Instance.ShowGameplay();
        UIManager.Instance.SetHud(Role, CurrentLevel, TotalLevels, Coins);
        UIManager.Instance.ShowToast("LEVEL " + n, 1.4f);
    }

    public void AddCoin()
    {
        Coins++;
        UIManager.Instance.SetHud(Role, CurrentLevel, TotalLevels, Coins);
    }

    public void OnPlayerDied() { StartCoroutine(RespawnRoutine()); }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(0.8f);
        if (player == null || levelEnding) yield break;
        player.Respawn(spawnPoint);
        camFollow.Snap();
    }

    /// <summary>Called by the debug SKIP button.</summary>
    public void DebugSkipLevel()
    {
        if (player == null || levelEnding) return;
        if (CurrentLevel < TotalLevels) { levelEnding = true; StartCoroutine(NextLevelRoutine()); }
        else if (Role == PlayerRole.Boy) { levelEnding = true; StartCoroutine(BoyFinaleRoutine()); }
        else if (boyNpc != null) OnReunionTriggered();
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

    /// <summary>Boy cleared level 10: walk to the flag, sit, register the room on Firebase, show the code.</summary>
    IEnumerator BoyFinaleRoutine()
    {
        player.Invincible = true;
        player.ControlEnabled = false;
        TouchInput.Reset();
        UIManager.Instance.HideTouchControls();

        yield return player.WalkTo(data.flagX - 1.0f);
        player.SitDown();
        UIManager.Instance.ShowToast("Now we wait for her...", 2f);
        yield return new WaitForSeconds(1.2f);

        yield return RegisterRoomRoutine();
    }

    IEnumerator RegisterRoomRoutine()
    {
        UIManager.Instance.ShowToast("Creating your invitation code...", 20f);
        bool done = false, ok = false;
        string code = null, error = null;

        FirebaseManager.Instance.CreateRoom((success, roomCode, err) =>
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
    public void RetryRegisterRoom()
    {
        StartCoroutine(RegisterRoomRoutine());
    }

    public void OnReunionTriggered()
    {
        if (levelEnding || player == null || player.IsDead || boyNpc == null) return;
        levelEnding = true;
        cutscene.Play(player, boyNpc);
    }

    public void OnEndingFinished()
    {
        UIManager.Instance.ShowTheEnd();
    }

    // ------------------------------------------------------------------ level construction

    void ClearLevel()
    {
        if (levelRoot != null)
        {
            levelRoot.SetActive(false); // disable immediately, destroy at end of frame
            Destroy(levelRoot);
            levelRoot = null;
        }
        player = null;
        boyNpc = null;
    }

    void SetupCamera(int level)
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
        cam.orthographicSize = cameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColors[(level - 1) % SkyColors.Length];
        if (!cam.TryGetComponent(out camFollow)) camFollow = cam.gameObject.AddComponent<CameraFollow2D>();
    }

    GameObject Piece(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 scale, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go;
    }

    GameObject Tiled(string name, Transform parent, Sprite sprite, Vector2 center, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingOrder = order;
        return go;
    }

    void BuildScenery(int level)
    {
        var rnd = new System.Random(level * 31);
        var white = SpriteFactory.Square(new Color(1f, 1f, 1f, 0.85f));
        int count = Mathf.CeilToInt(data.endX / 9f);
        for (int i = 0; i < count; i++)
        {
            float x = i * 9f + (float)rnd.NextDouble() * 6f;
            float y = 5.5f + (float)rnd.NextDouble() * 4f;
            float s = 0.8f + (float)rnd.NextDouble() * 0.8f;
            var cloud = new GameObject("Cloud").transform;
            cloud.SetParent(levelRoot.transform, false);
            Piece("a", cloud, white, new Vector2(x, y), new Vector2(3f * s, 0.9f * s), -10);
            Piece("b", cloud, white, new Vector2(x + 0.2f * s, y + 0.7f * s), new Vector2(1.8f * s, 0.8f * s), -10);
        }
    }

    void BuildTerrain()
    {
        Sprite dirt = SpriteFactory.Dirt(), grass = SpriteFactory.Grass(), brick = SpriteFactory.Brick();
        const float depth = 8f;

        foreach (var g in data.grounds)
        {
            float w = g.x1 - g.x0;
            float cx = (g.x0 + g.x1) * 0.5f;
            var go = Tiled("Ground", levelRoot.transform, dirt, new Vector2(cx, g.top - depth * 0.5f), new Vector2(w, depth), 0);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(w, depth);
            Tiled("Grass", go.transform, grass, new Vector2(cx, g.top - 0.5f), new Vector2(w, 1f), 1);
        }

        foreach (var p in data.platforms)
        {
            float w = p.x1 - p.x0;
            var go = Tiled("Platform", levelRoot.transform, brick, new Vector2((p.x0 + p.x1) * 0.5f, p.top - 0.5f), new Vector2(w, 1f), 2);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(w, 1f);
            bc.usedByEffector = true;                       // one-way: jump up through it, land on top
            var eff = go.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 170f;
            eff.useSideFriction = false;
            eff.useSideBounce = false;
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

    void BuildPickupsAndHazards()
    {
        var coinSprite = SpriteFactory.Coin();
        foreach (var c in data.coins)
        {
            var go = Piece("Coin", levelRoot.transform, coinSprite, c, new Vector2(0.7f, 0.7f), 5);
            var cc = go.AddComponent<CircleCollider2D>();
            cc.isTrigger = true;
            cc.radius = 0.5f;
            go.AddComponent<Coin>();
        }

        var spikeSprite = SpriteFactory.Spike();
        foreach (var s in data.spikes)
        {
            var go = Piece("Spike", levelRoot.transform, spikeSprite, new Vector2(s.x, s.y), Vector2.one, 4);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(0.8f, 0.3f);
            bc.offset = new Vector2(0f, 0.18f);
            go.AddComponent<Hazard>();
        }

        var enemySprite = SpriteFactory.Enemy();
        foreach (var e in data.enemies)
        {
            var go = Piece("Enemy", levelRoot.transform, enemySprite, new Vector2(e.x, e.y), Vector2.one, 6);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(0.65f, 0.5f);
            bc.offset = new Vector2(0f, 0.3f);
            var en = go.AddComponent<Enemy>();
            en.minX = e.minX; en.maxX = e.maxX; en.speed = e.speed;
        }
    }

    void BuildFlagAndGoal()
    {
        float fx = data.flagX, top = data.flagTop;
        var root = levelRoot.transform;
        Piece("FlagBase", root, SpriteFactory.Square(new Color(0.35f, 0.35f, 0.40f)), new Vector2(fx, top + 0.25f), new Vector2(0.9f, 0.5f), 3);
        Piece("FlagPole", root, SpriteFactory.Square(new Color(0.85f, 0.85f, 0.88f)), new Vector2(fx, top + 2.75f), new Vector2(0.14f, 4.5f), 3);
        Piece("FlagCloth", root, SpriteFactory.Square(new Color(0.9f, 0.15f, 0.2f)), new Vector2(fx + 0.55f, top + 4.4f), new Vector2(1.0f, 0.7f), 3);
        Piece("FlagKnob", root, SpriteFactory.Circle(new Color(1f, 0.85f, 0.2f)), new Vector2(fx, top + 5.1f), new Vector2(0.4f, 0.4f), 3);

        // The girl never "clears" level 10 with a goal: she triggers the reunion instead.
        if (CurrentLevel == TotalLevels && Role == PlayerRole.Girl) return;

        // Boy on level 10 starts his finale a few steps before the flag, then walks the rest.
        float goalX = CurrentLevel == TotalLevels ? fx - 4f : fx;
        var goal = new GameObject("Goal");
        goal.transform.SetParent(root, false);
        goal.transform.position = new Vector3(goalX, top + 4f, 0f);
        var bc = goal.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
        bc.size = new Vector2(1f, 12f);
        goal.AddComponent<LevelGoal>();
    }

    PlayerController2D CreatePlayer(Vector3 pos)
    {
        GameObject go;
        if (playerPrefab != null)
        {
            go = Instantiate(playerPrefab, pos, Quaternion.identity, levelRoot.transform);
        }
        else
        {
            go = new GameObject("Player");
            go.transform.SetParent(levelRoot.transform, false);
            go.transform.position = pos;
        }

        PlayerController2D p;
        if (!go.TryGetComponent(out p)) p = go.AddComponent<PlayerController2D>(); // adds Rigidbody2D + BoxCollider2D

        if (Role == PlayerRole.Boy) p.SetLook(BoyStandingSprite, BoySittingSprite);
        else p.SetLook(GirlStandingSprite, null);
        return p;
    }

    void CreateWaitingBoyAndTrigger()
    {
        float x = data.flagX - 1.0f;
        boyNpc = new GameObject("Boy_Waiting");
        boyNpc.transform.SetParent(levelRoot.transform, false);
        boyNpc.transform.position = new Vector3(x, data.flagTop, 0f);
        var vis = new GameObject("Visual");
        vis.transform.SetParent(boyNpc.transform, false);
        var sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = BoySittingSprite;
        sr.sortingOrder = 9;

        var trig = new GameObject("ReunionTrigger");
        trig.transform.SetParent(levelRoot.transform, false);
        trig.transform.position = new Vector3(x - 4.5f, data.flagTop + 4f, 0f);
        var bc = trig.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
        bc.size = new Vector2(1f, 12f);
        trig.AddComponent<ReunionTrigger>();
    }
}
