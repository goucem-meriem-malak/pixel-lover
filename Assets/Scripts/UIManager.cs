using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Press-and-hold UI button (multi-touch friendly: hold Right and tap Jump together).</summary>
public class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action onDown;
    public Action onUp;
    public Image image;
    public Color idleColor = new Color(1f, 1f, 1f, 0.45f);
    public Color activeColor = new Color(1f, 1f, 1f, 0.85f);
    bool held;

    public void OnPointerDown(PointerEventData e)
    {
        held = true;
        if (image != null) image.color = activeColor;
        if (onDown != null) onDown();
    }

    public void OnPointerUp(PointerEventData e) { Release(); }
    public void OnPointerExit(PointerEventData e) { Release(); }
    void OnDisable() { Release(); }

    void Release()
    {
        if (!held) return;
        held = false;
        if (image != null) image.color = idleColor;
        if (onUp != null) onUp();
    }
}

/// <summary>
/// Crisp pixel text for the UI, drawn with the 5x7 bitmap font (PixelFont). Supports several lines ('\n').
/// The rect's anchor/pivot decide the alignment: anchor.x 0 = left aligned, 0.5 = centred, 1 = right aligned.
/// </summary>
public class PixelLabel : MonoBehaviour
{
    public static readonly Color Ink = new Color(0.17f, 0.15f, 0.25f, 1f);

    int scale = 3;
    Color fill = Color.white;
    Color outline = Ink;
    float alignX = 0.5f;
    string current;
    readonly List<Image> lines = new List<Image>();

    public Vector2 Size { get; private set; }

    public void Init(int pixelScale, Color fillColor, Color outlineColor, float align)
    {
        scale = pixelScale;
        fill = fillColor;
        outline = outlineColor;
        alignX = align;
        current = null;   // force the next Set() to redraw with the new style
    }

    public void Set(string text)
    {
        if (text == null) text = "";
        if (text == current) return;
        current = text;

        string[] parts = text.Split('\n');
        float lineH = 9f * scale;
        float step = lineH + 2f * scale;
        float maxW = 0f;

        for (int i = 0; i < parts.Length; i++)
        {
            Image img = i < lines.Count ? lines[i] : NewLine();
            Release(img);
            Sprite sp = PixelFont.MakeSprite(parts[i], fill, outline, 100f);
            img.sprite = sp;
            img.enabled = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(alignX, 1f);
            rt.anchoredPosition = new Vector2(0f, -i * step);
            rt.sizeDelta = new Vector2(sp.rect.width * scale, sp.rect.height * scale);
            maxW = Mathf.Max(maxW, sp.rect.width * scale);
        }
        for (int i = parts.Length; i < lines.Count; i++)
        {
            Release(lines[i]);
            lines[i].enabled = false;
        }

        Size = new Vector2(maxW, (parts.Length - 1) * step + lineH);
        ((RectTransform)transform).sizeDelta = Size;
    }

    Image NewLine()
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        lines.Add(img);
        return img;
    }

    static void Release(Image img)
    {
        if (img.sprite == null) return;
        Texture2D tex = img.sprite.texture;
        Destroy(img.sprite);
        if (tex != null) Destroy(tex);
        img.sprite = null;
    }

    void OnDestroy()
    {
        foreach (var img in lines) if (img != null) Release(img);
    }
}

/// <summary>
/// Builds the whole UI in code (no prefabs): start menu, name entry (boy), join screen (girl: name + invitation code),
/// HUD, touch buttons (Left / Right / Jump / Fire), ring shop, invitation code, proposal ending banner.
/// Everything is drawn with the pixel sprites and the pixel font so it matches the game.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // palette
    static readonly Color Ink = PixelLabel.Ink;
    static readonly Color BtnBlue = new Color(0.45f, 0.65f, 1.00f);
    static readonly Color BtnPink = new Color(1.00f, 0.55f, 0.78f);
    static readonly Color BtnGold = new Color(1.00f, 0.82f, 0.35f);
    static readonly Color BtnGray = new Color(0.70f, 0.72f, 0.85f);
    static readonly Color BtnGreen = new Color(0.50f, 0.88f, 0.55f);
    static readonly Color Dim = new Color(0.55f, 0.55f, 0.65f);
    static readonly Color Overlay = new Color(0.10f, 0.08f, 0.22f, 0.86f);

    Canvas canvas;
    Font hiddenFont;     // only used by the invisible text inside the input fields
    readonly List<RectTransform> safeRects = new List<RectTransform>();
    Rect lastSafeArea;

    GameObject startPanel, boyPanel, girlPanel, hudPanel, touchPanel, shopPanel, invitePanel, endPanel;
    GameObject toastBox, endBanner, fireButtonObj;
    Button myCodeButton, joinButton, retryButton, copyButton, skipButton, endMenuButton, buyButton;
    PixelLabel hudCount, hudLevel, toastLabel, joinStatus, shopCoins, shopInfo, inviteTitle, inviteCode, inviteInfo, endTitle, endNames, offlineLabel;
    Image hudIcon, hudForm, inviteRing;
    CanvasGroup endBannerGroup;
    Coroutine toastRoutine, bannerRoutine;

    class FieldView { public InputField input; public PixelLabel text, hint; }
    readonly List<FieldView> fields = new List<FieldView>();
    FieldView boyName, girlName, girlCode;

    readonly List<Button> ringCards = new List<Button>();
    int shopSelected, shopCoinsAvailable;
    string shownCode = "";

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

#if UNITY_2022_2_OR_NEWER
        hiddenFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        hiddenFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

        BuildCanvas();
        BuildStartPanel();
        BuildBoyPanel();
        BuildGirlPanel();
        BuildHud();
        BuildTouchControls();
        BuildShopPanel();
        BuildInvitePanel();
        BuildEndPanel();
        BuildToast();
        HideAll();
    }

    void Start()
    {
        EnsureEventSystem();
        ApplySafeArea();
    }

    void Update()
    {
        if (Screen.safeArea != lastSafeArea) ApplySafeArea();

        // mirror every input field into its pixel-font label (the real text of the field is invisible)
        bool blink = Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
        foreach (var f in fields)
        {
            if (!f.input.gameObject.activeInHierarchy) continue;
            bool focus = f.input.isFocused;
            string t = f.input.text ?? "";
            bool showHint = t.Length == 0 && !focus;
            f.hint.gameObject.SetActive(showHint);
            f.text.gameObject.SetActive(!showHint);
            if (!showHint) f.text.Set(t + (focus && blink ? "_" : ""));
        }
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    void ApplySafeArea()
    {
        Rect sa = Screen.safeArea;
        lastSafeArea = sa;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Vector2 min = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        Vector2 max = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        foreach (var rt in safeRects)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    // ------------------------------------------------------------------ public API (used by the other scripts)

    public void ShowStartMenu()
    {
        HideAll();
        TouchInput.Reset();
        myCodeButton.gameObject.SetActive(PlayerPrefs.HasKey(LevelManager.SavedCodeKey));
        startPanel.SetActive(true);
    }

    public void ShowGameplay()
    {
        HideAll();
        hudPanel.SetActive(true);
        touchPanel.SetActive(true);
        skipButton.gameObject.SetActive(LevelManager.Instance != null && LevelManager.Instance.debugSkipButton);
    }

    public void HideTouchControls() { touchPanel.SetActive(false); TouchInput.Reset(); }
    public void ShowTouchControls() { touchPanel.SetActive(true); }

    public void SetHud(PlayerRole role, int level, int total, int coins, PowerState form)
    {
        hudIcon.sprite = SpriteFactory.Get(role == PlayerRole.Boy ? "coin_1" : "flower_1");
        hudCount.Set("X " + coins);
        hudLevel.Set("LEVEL " + level + "/" + total);
        hudForm.gameObject.SetActive(form != PowerState.Small);
        if (form != PowerState.Small) hudForm.sprite = SpriteFactory.Get(form == PowerState.Fire ? "item_fireflower" : "item_wings");
        fireButtonObj.SetActive(form == PowerState.Fire);
    }

    public void ShowToast(string text, float seconds)
    {
        toastLabel.Set(text);
        var rt = (RectTransform)toastBox.transform;
        rt.sizeDelta = new Vector2(toastLabel.Size.x + 70f, toastLabel.Size.y + 36f);
        toastBox.SetActive(true);
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToastAfter(seconds));
    }

    public void HideToast()
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastBox.SetActive(false);
    }

    IEnumerator HideToastAfter(float s)
    {
        yield return new WaitForSeconds(s);
        toastBox.SetActive(false);
    }

    /// <summary>Boy finished level 10: let him spend his coins on a ring.</summary>
    public void ShowRingShop(int coins)
    {
        HideTouchControls();
        shopCoinsAvailable = coins;
        shopCoins.Set("YOUR COINS: " + coins);

        // pre-select the most expensive ring he can afford
        shopSelected = 0;
        for (int i = 0; i < Rings.Count; i++)
            if (Rings.PriceFor(i) <= coins) shopSelected = i;

        RefreshShop();
        shopPanel.SetActive(true);
    }

    /// <summary>The boy has his ring: show the 6-character invitation code.</summary>
    public void ShowInvitation(string code)
    {
        ShowInvitationInternal(code, true);
    }

    void ShowInvitationInternal(string code, bool withRing)
    {
        shownCode = code;
        hudPanel.SetActive(false);
        touchPanel.SetActive(false);
        shopPanel.SetActive(false);
        invitePanel.SetActive(true);

        string name = LevelManager.Instance != null && withRing ? LevelManager.Instance.PlayerName : "";
        inviteTitle.Set(name.Length > 0 ? "WELL DONE, " + name.ToUpperInvariant() + "!" : "YOUR INVITATION CODE");
        inviteCode.Set(Spaced(code));
        inviteInfo.Set("SEND THIS CODE TO PLAYER 2.\nSHE TYPES IT ON HER START SCREEN.\nI WILL BE WAITING FOR HER HERE!");
        copyButton.gameObject.SetActive(true);
        retryButton.gameObject.SetActive(false);

        bool showRing = withRing && LevelManager.Instance != null;
        inviteRing.gameObject.SetActive(showRing);
        if (showRing) inviteRing.sprite = Rings.Icon(LevelManager.Instance.RingId);
    }

    public void ShowInvitationError(string message)
    {
        hudPanel.SetActive(false);
        touchPanel.SetActive(false);
        shopPanel.SetActive(false);
        invitePanel.SetActive(true);
        inviteTitle.Set("COULD NOT MAKE THE ROOM");
        inviteCode.Set("");
        inviteInfo.Set(Wrap((message ?? "UNKNOWN ERROR").ToUpperInvariant(), 44) + "\n\nCHECK YOUR INTERNET AND TRY AGAIN.");
        copyButton.gameObject.SetActive(false);
        retryButton.gameObject.SetActive(true);
        inviteRing.gameObject.SetActive(false);
    }

    /// <summary>Proposal cutscene: the closing banner (title + both names).</summary>
    public void ShowEndingMessage(string title, string names)
    {
        hudPanel.SetActive(false);
        touchPanel.SetActive(false);
        endPanel.SetActive(true);
        endMenuButton.gameObject.SetActive(false);
        endTitle.Set(title);
        endNames.Set(names);
        endBanner.SetActive(true);
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(FadeIn(endBannerGroup, 1.2f));
    }

    public void ShowTheEnd()
    {
        endPanel.SetActive(true);
        endMenuButton.gameObject.SetActive(true);
    }

    IEnumerator FadeIn(CanvasGroup g, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Clamp01(t / seconds);
            yield return null;
        }
        g.alpha = 1f;
    }

    void HideAll()
    {
        startPanel.SetActive(false);
        boyPanel.SetActive(false);
        girlPanel.SetActive(false);
        hudPanel.SetActive(false);
        touchPanel.SetActive(false);
        shopPanel.SetActive(false);
        invitePanel.SetActive(false);
        endPanel.SetActive(false);
        endBanner.SetActive(false);
        toastBox.SetActive(false);
    }

    // ------------------------------------------------------------------ helpers: text

    static string Spaced(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>Word-wraps text at maxChars characters per line (the pixel font is single-line).</summary>
    static string Wrap(string text, int maxChars)
    {
        var sb = new StringBuilder();
        int lineLen = 0;
        foreach (string word in text.Split(' '))
        {
            if (lineLen > 0 && lineLen + 1 + word.Length > maxChars) { sb.Append('\n'); lineLen = 0; }
            else if (lineLen > 0) { sb.Append(' '); lineLen++; }
            sb.Append(word);
            lineLen += word.Length;
        }
        return sb.ToString();
    }

    static char ValidateName(string text, int index, char c)
    {
        c = char.ToUpperInvariant(c);
        if (c < 128 && (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '.')) return c;
        return '\0';
    }

    static char ValidateCode(string text, int index, char c)
    {
        c = char.ToUpperInvariant(c);
        if (c < 128 && char.IsLetterOrDigit(c)) return c;
        return '\0';
    }

    // ------------------------------------------------------------------ helpers: building blocks

    void BuildCanvas()
    {
        var go = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var cs = go.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280f, 720f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
    }

    RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Anchor == pivot, so pos is the margin from that corner / edge / centre.</summary>
    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>Full-screen panel; returns the safe-area content rect to put widgets in.</summary>
    RectTransform MakePanel(string name, Color bg, out GameObject panel)
    {
        var rt = NewRect(name, canvas.transform);
        Stretch(rt);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bg;
        img.raycastTarget = bg.a > 0.01f;
        var safe = NewRect("Safe", rt);
        Stretch(safe);
        safeRects.Add(safe);
        panel = rt.gameObject;
        return safe;
    }

    /// <summary>9-sliced pixel frame (each art pixel = 3 UI units, like the text).</summary>
    static void Slice(Image img, string sprite)
    {
        img.sprite = SpriteFactory.GetSliced(sprite, 3);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / 3f;
    }

    PixelLabel Label(Transform parent, string text, int scale, Color fill, Vector2 anchor, Vector2 pos)
    {
        var rt = NewRect("Label", parent);
        Place(rt, anchor, pos, Vector2.zero);
        var l = rt.gameObject.AddComponent<PixelLabel>();
        l.Init(scale, fill, Ink, anchor.x);
        l.Set(text);
        return l;
    }

    Image Pic(Transform parent, string sprite, float k, Vector2 anchor, Vector2 pos)
    {
        Sprite s = SpriteFactory.Get(sprite);
        var rt = NewRect(sprite, parent);
        Place(rt, anchor, pos, new Vector2(s.rect.width * k, s.rect.height * k));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = s;
        img.raycastTarget = false;
        return img;
    }

    Image Frame(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color tint)
    {
        var rt = NewRect("Frame", parent);
        Place(rt, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        Slice(img, "ui_panel");
        img.color = tint;
        img.raycastTarget = false;
        return img;
    }

    Button Btn(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color tint, int scale, UnityAction onClick)
    {
        var rt = NewRect(label, parent);
        Place(rt, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        Slice(img, "ui_button");
        img.color = tint;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        var cb = b.colors;
        cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        b.colors = cb;
        Label(rt, label, scale, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero);
        return b;
    }

    /// <summary>A vertical sky gradient (banded, so it stays "pixel").</summary>
    void Sky(Transform parent, Color top, Color bottom)
    {
        const int bands = 14;
        var tex = new Texture2D(1, bands, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < bands; i++) tex.SetPixel(0, i, Color.Lerp(bottom, top, i / (float)(bands - 1)));
        tex.Apply();
        var rt = NewRect("Sky", parent);
        Stretch(rt);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, bands), new Vector2(0.5f, 0.5f), 100f);
        img.raycastTarget = false;
    }

    /// <summary>A strip of repeating tiles along the bottom edge.</summary>
    void TileStrip(Transform parent, string tile, float y, float height)
    {
        var rt = NewRect("Strip", parent);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, height);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = SpriteFactory.GetSliced(tile, 0);
        img.type = Image.Type.Tiled;
        img.pixelsPerUnitMultiplier = 1f / 3f;
        img.raycastTarget = false;
    }

    /// <summary>Pastel sky + clouds + hills + ground, used behind the start / name screens.</summary>
    void Scenery(Transform panel, Transform safe, bool pink)
    {
        Sky(panel, pink ? new Color(0.72f, 0.62f, 0.98f) : new Color(0.28f, 0.55f, 0.95f),
                   pink ? new Color(1.00f, 0.90f, 0.86f) : new Color(0.80f, 0.93f, 1.00f));
        panel.GetChild(panel.childCount - 1).SetSiblingIndex(0);
        string cloud = pink ? "bg_cloud_pink" : "bg_cloud";
        Pic(panel, cloud, 4f, new Vector2(0.12f, 0.80f), Vector2.zero);
        Pic(panel, cloud, 3f, new Vector2(0.86f, 0.64f), Vector2.zero);
        Pic(panel, cloud, 5f, new Vector2(0.60f, 0.90f), Vector2.zero);
        Pic(panel, "bg_hill", 6f, new Vector2(0.15f, 0f), new Vector2(0f, 130f));
        Pic(panel, "bg_hill", 5f, new Vector2(0.85f, 0f), new Vector2(0f, 130f));
        TileStrip(panel, "tile_dirt", 0f, 96f);
        TileStrip(panel, "tile_grass", 96f, 48f);
    }

    // ------------------------------------------------------------------ screens

    void BuildStartPanel()
    {
        var safe = MakePanel("StartPanel", new Color(0f, 0f, 0f, 0f), out startPanel);
        Scenery(startPanel.transform, safe, false);
        safe.SetAsLastSibling();

        var top = new Vector2(0.5f, 1f);
        var mid = new Vector2(0.5f, 0.5f);
        Pic(safe, "logo", 0.25f, top, new Vector2(0f, -14f));
        Label(safe, "PIXEL LOVE", 9, Color.white, top, new Vector2(0f, -150f));
        Label(safe, "A TINY ADVENTURE FOR TWO", 3, new Color(1f, 1f, 1f, 0.95f), top, new Vector2(0f, -228f));

        Pic(safe, "knight_idle", 7f, new Vector2(0.5f, 0f), new Vector2(-440f, 140f));
        Pic(safe, "princess_idle", 7f, new Vector2(0.5f, 0f), new Vector2(440f, 140f));

        Btn(safe, "PLAYER 1 - BOY", mid, new Vector2(0f, -10f), new Vector2(480f, 84f), BtnBlue, 4, () => ShowBoyEntry());
        Btn(safe, "PLAYER 2 - GIRL", mid, new Vector2(0f, -110f), new Vector2(480f, 84f), BtnPink, 4, () => ShowGirlEntry());
        myCodeButton = Btn(safe, "MY INVITATION CODE", mid, new Vector2(0f, -200f), new Vector2(480f, 64f), BtnGold, 3, ShowSavedInvitation);

        if (!FirebaseManager.Enabled)
        {
            offlineLabel = Label(safe, "OFFLINE DEMO MODE - FIREBASE NOT ENABLED. CODES ONLY WORK ON THIS DEVICE.", 2,
                                 new Color(1f, 0.95f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 40f));
        }
    }

    void BuildBoyPanel()
    {
        var safe = MakePanel("BoyPanel", new Color(0f, 0f, 0f, 0f), out boyPanel);
        Scenery(boyPanel.transform, safe, false);
        safe.SetAsLastSibling();

        var top = new Vector2(0.5f, 1f);
        Pic(safe, "knight_idle", 6f, new Vector2(0f, 0f), new Vector2(60f, 140f));
        Label(safe, "WHO ARE YOU, KNIGHT?", 6, Color.white, top, new Vector2(0f, -50f));
        Label(safe, "THIS NAME WILL FLOAT OVER YOUR HEAD", 3, new Color(1f, 1f, 1f, 0.95f), top, new Vector2(0f, -125f));

        boyName = MakeField(safe, top, new Vector2(0f, -190f), new Vector2(520f, 84f), "YOUR NAME", NameUtil.MaxLength, false);
        Btn(safe, "START ADVENTURE", top, new Vector2(0f, -300f), new Vector2(520f, 84f), BtnBlue, 4, OnBoyStart);
        Btn(safe, "BACK", new Vector2(1f, 0f), new Vector2(-30f, 150f), new Vector2(170f, 60f), BtnGray, 3, () => ShowStartMenu());
    }

    void BuildGirlPanel()
    {
        var safe = MakePanel("GirlPanel", new Color(0f, 0f, 0f, 0f), out girlPanel);
        Scenery(girlPanel.transform, safe, true);
        safe.SetAsLastSibling();

        var top = new Vector2(0.5f, 1f);
        Pic(safe, "princess_idle", 6f, new Vector2(1f, 0f), new Vector2(-60f, 140f));
        Label(safe, "HELLO, PRINCESS!", 6, Color.white, top, new Vector2(0f, -40f));

        Label(safe, "YOUR NAME", 3, Color.white, new Vector2(0.5f, 1f), new Vector2(-270f, -118f));
        girlName = MakeField(safe, top, new Vector2(-270f, -150f), new Vector2(480f, 84f), "YOUR NAME", NameUtil.MaxLength, false);
        Label(safe, "INVITATION CODE", 3, Color.white, new Vector2(0.5f, 1f), new Vector2(270f, -118f));
        girlCode = MakeField(safe, top, new Vector2(270f, -150f), new Vector2(480f, 84f), "ABC123", FirebaseManager.CodeLength, true);

        joinButton = Btn(safe, "JOIN HIM", top, new Vector2(0f, -265f), new Vector2(420f, 84f), BtnPink, 4, OnJoinClicked);
        joinStatus = Label(safe, "ASK PLAYER 1 FOR THE 6-CHARACTER CODE.", 3, Color.white, top, new Vector2(0f, -365f));
        Btn(safe, "BACK", new Vector2(0f, 0f), new Vector2(30f, 150f), new Vector2(170f, 60f), BtnGray, 3, () => ShowStartMenu());
    }

    /// <summary>A text field that looks like pixel art: the real InputField is invisible, its text is mirrored into pixel labels.</summary>
    FieldView MakeField(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string hint, int maxLen, bool codeMode)
    {
        var rt = NewRect("Field", parent);
        Place(rt, anchor, pos, size);
        var bg = rt.gameObject.AddComponent<Image>();
        Slice(bg, "ui_panel");
        bg.color = Color.white;

        var input = rt.gameObject.AddComponent<InputField>();
        input.targetGraphic = bg;

        var hiddenRt = NewRect("Hidden", rt);
        Stretch(hiddenRt);
        var hidden = hiddenRt.gameObject.AddComponent<Text>();
        hidden.font = hiddenFont;
        hidden.fontSize = 28;
        hidden.color = new Color(0f, 0f, 0f, 0f);
        hidden.raycastTarget = false;

        input.textComponent = hidden;
        input.characterLimit = maxLen;
        input.lineType = InputField.LineType.SingleLine;
        input.customCaretColor = true;
        input.caretColor = new Color(0f, 0f, 0f, 0f);
        input.selectionColor = new Color(0f, 0f, 0f, 0f);
        input.shouldHideMobileInput = true;
        input.keyboardType = TouchScreenKeyboardType.ASCIICapable;
        input.onValidateInput = codeMode ? (InputField.OnValidateInput)ValidateCode : ValidateName;

        var f = new FieldView { input = input };
        f.text = Label(rt, "", 5, Ink, new Vector2(0f, 0.5f), new Vector2(24f, 0f));
        f.hint = Label(rt, hint, 5, Dim, new Vector2(0f, 0.5f), new Vector2(24f, 0f));
        // dark text on a light field: no outline wanted
        f.text.Init(5, Ink, new Color(0f, 0f, 0f, 0f), 0f);
        f.hint.Init(5, Dim, new Color(0f, 0f, 0f, 0f), 0f);
        f.text.Set(" ");
        f.hint.Set(hint);
        fields.Add(f);
        return f;
    }

    void BuildHud()
    {
        var safe = MakePanel("HudPanel", new Color(0f, 0f, 0f, 0f), out hudPanel);
        var tl = new Vector2(0f, 1f);
        var tr = new Vector2(1f, 1f);

        Frame(safe, tl, new Vector2(16f, -14f), new Vector2(560f, 66f), new Color(1f, 1f, 1f, 0.82f));
        hudIcon = Pic(safe, "coin_1", 3f, tl, new Vector2(28f, -23f));
        hudCount = Label(safe, "X 0", 4, Color.white, tl, new Vector2(88f, -30f));
        hudLevel = Label(safe, "LEVEL 1/10", 3, Color.white, tl, new Vector2(240f, -35f));
        hudForm = Pic(safe, "item_wings", 2.5f, tl, new Vector2(500f, -27f));

        Btn(safe, "MENU", tr, new Vector2(-16f, -14f), new Vector2(150f, 56f), BtnGray, 3, () => LevelManager.Instance.ReturnToMenu());
        skipButton = Btn(safe, "SKIP >>", tr, new Vector2(-16f, -80f), new Vector2(150f, 56f), BtnGold, 3, () => LevelManager.Instance.DebugSkipLevel());
    }

    TouchHoldButton MakeHold(Transform parent, string label, int scale, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = NewRect(label, parent);
        Place(rt, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        Slice(img, "ui_button");
        var hold = rt.gameObject.AddComponent<TouchHoldButton>();
        hold.image = img;
        img.color = hold.idleColor;
        Label(rt, label, scale, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero);
        return hold;
    }

    void BuildTouchControls()
    {
        var safe = MakePanel("TouchPanel", new Color(0f, 0f, 0f, 0f), out touchPanel);
        var bl = new Vector2(0f, 0f);
        var br = new Vector2(1f, 0f);

        var left = MakeHold(safe, "<", 7, bl, new Vector2(24f, 20f), new Vector2(112f, 112f));
        left.onDown = () => TouchInput.Left = true;
        left.onUp = () => TouchInput.Left = false;

        var right = MakeHold(safe, ">", 7, bl, new Vector2(150f, 20f), new Vector2(112f, 112f));
        right.onDown = () => TouchInput.Right = true;
        right.onUp = () => TouchInput.Right = false;

        var jump = MakeHold(safe, "JUMP", 4, br, new Vector2(-24f, 20f), new Vector2(140f, 140f));
        jump.onDown = TouchInput.PressJump;
        jump.onUp = TouchInput.ReleaseJump;

        var fire = MakeHold(safe, "FIRE", 3, br, new Vector2(-180f, 24f), new Vector2(104f, 104f));
        fire.idleColor = new Color(1f, 0.55f, 0.25f, 0.6f);
        fire.activeColor = new Color(1f, 0.7f, 0.4f, 0.95f);
        fire.image.color = fire.idleColor;
        fire.onDown = TouchInput.PressFire;
        fireButtonObj = fire.gameObject;
        fireButtonObj.SetActive(false);
    }

    void BuildShopPanel()
    {
        var safe = MakePanel("ShopPanel", Overlay, out shopPanel);
        var mid = new Vector2(0.5f, 0.5f);
        var top = new Vector2(0.5f, 1f);

        Frame(safe, mid, new Vector2(0f, 0f), new Vector2(1120f, 600f), new Color(0.78f, 0.80f, 1f, 1f));
        Label(safe, "CHOOSE HER RING", 7, Color.white, mid, new Vector2(0f, 235f));
        Pic(safe, "coin_1", 3f, mid, new Vector2(-150f, 172f));
        shopCoins = Label(safe, "YOUR COINS: 0", 4, Color.white, mid, new Vector2(30f, 172f));

        ringCards.Clear();
        const float cardW = 192f, gap = 16f;
        float startX = -((Rings.Count - 1) * (cardW + gap)) * 0.5f;
        for (int i = 0; i < Rings.Count; i++)
        {
            int index = i;
            float x = startX + i * (cardW + gap);
            var rt = NewRect("Ring" + i, safe);
            Place(rt, mid, new Vector2(x, 10f), new Vector2(cardW, 290f));
            var img = rt.gameObject.AddComponent<Image>();
            Slice(img, "ui_button");
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;   // the card colour is set in RefreshShop
            b.onClick.AddListener(() => OnRingCard(index));

            Pic(rt, "ring_" + i, 7f, new Vector2(0.5f, 1f), new Vector2(0f, -26f));
            Label(rt, Rings.NameOf(i).Replace(' ', '\n'), 3, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -160f));
            Label(rt, "", 4, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 20f)).name = "Price";
            ringCards.Add(b);
        }

        shopInfo = Label(safe, "", 3, Color.white, mid, new Vector2(0f, -170f));
        buyButton = Btn(safe, "GIVE THIS RING", mid, new Vector2(0f, -235f), new Vector2(520f, 80f), BtnGreen, 4, OnBuyClicked);
    }

    void OnRingCard(int index)
    {
        if (Rings.PriceFor(index) > shopCoinsAvailable) return;
        shopSelected = index;
        RefreshShop();
    }

    void RefreshShop()
    {
        for (int i = 0; i < ringCards.Count; i++)
        {
            int price = Rings.PriceFor(i);
            bool afford = price <= shopCoinsAvailable;
            var img = ringCards[i].GetComponent<Image>();
            img.color = i == shopSelected ? new Color(1f, 0.88f, 0.4f)
                      : afford ? new Color(0.92f, 0.94f, 1f) : new Color(0.55f, 0.55f, 0.62f);
            var priceLabel = ringCards[i].transform.Find("Price").GetComponent<PixelLabel>();
            priceLabel.Set(price == 0 ? "FREE" : afford ? price + " COINS" : "NEED " + price);
        }
        shopInfo.Set(Rings.NameOf(shopSelected) + " - " + (Rings.PriceFor(shopSelected) == 0 ? "A GIFT FROM THE HEART" : "YOU CAN AFFORD IT!"));
    }

    void OnBuyClicked()
    {
        shopPanel.SetActive(false);
        LevelManager.Instance.ChooseRing(shopSelected);
    }

    void BuildInvitePanel()
    {
        var safe = MakePanel("InvitePanel", Overlay, out invitePanel);
        var mid = new Vector2(0.5f, 0.5f);

        Frame(safe, mid, Vector2.zero, new Vector2(980f, 560f), new Color(0.78f, 0.80f, 1f, 1f));
        inviteTitle = Label(safe, "WELL DONE!", 6, Color.white, mid, new Vector2(0f, 215f));
        Label(safe, "YOUR INVITATION CODE", 3, Color.white, mid, new Vector2(0f, 150f));

        Frame(safe, mid, new Vector2(0f, 70f), new Vector2(820f, 130f), new Color(0.25f, 0.22f, 0.42f, 1f));
        inviteCode = Label(safe, "", 10, new Color(1f, 0.88f, 0.4f), mid, new Vector2(0f, 70f));

        inviteRing = Pic(safe, "ring_0", 5f, mid, new Vector2(-330f, -70f));
        inviteInfo = Label(safe, "", 3, Color.white, mid, new Vector2(40f, -60f));

        copyButton = Btn(safe, "COPY CODE", mid, new Vector2(-200f, -210f), new Vector2(340f, 78f), BtnGreen, 4, OnCopyClicked);
        retryButton = Btn(safe, "TRY AGAIN", mid, new Vector2(-200f, -210f), new Vector2(340f, 78f), BtnGold, 4, () =>
        {
            invitePanel.SetActive(false);
            LevelManager.Instance.RetryRegisterRoom();
        });
        Btn(safe, "MENU", mid, new Vector2(200f, -210f), new Vector2(340f, 78f), BtnGray, 4, () => LevelManager.Instance.ReturnToMenu());
    }

    void BuildEndPanel()
    {
        var safe = MakePanel("EndPanel", new Color(0f, 0f, 0f, 0f), out endPanel);

        var bannerRt = NewRect("Banner", safe);
        Place(bannerRt, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(980f, 200f));
        var bg = bannerRt.gameObject.AddComponent<Image>();
        Slice(bg, "ui_panel");
        bg.color = new Color(0.30f, 0.20f, 0.45f, 0.82f);
        bg.raycastTarget = false;
        endBannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
        endBannerGroup.alpha = 0f;
        endBanner = bannerRt.gameObject;

        endTitle = Label(bannerRt, "TOGETHER FOREVER", 8, new Color(1f, 0.82f, 0.92f), new Vector2(0.5f, 1f), new Vector2(0f, -30f));
        endNames = Label(bannerRt, "", 5, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 28f));

        endMenuButton = Btn(safe, "BACK TO MENU", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(440f, 84f), BtnPink, 4, () => LevelManager.Instance.ReturnToMenu());
    }

    void BuildToast()
    {
        var rt = NewRect("Toast", canvas.transform);
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(600f, 80f));
        var bg = rt.gameObject.AddComponent<Image>();
        Slice(bg, "ui_panel");
        bg.color = new Color(0.30f, 0.20f, 0.45f, 0.88f);
        bg.raycastTarget = false;
        toastBox = rt.gameObject;
        toastLabel = Label(rt, "", 4, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero);
    }

    // ------------------------------------------------------------------ button handlers

    void ShowBoyEntry()
    {
        HideAll();
        boyPanel.SetActive(true);
        boyName.input.text = PlayerPrefs.GetString(LevelManager.SavedNameKey, "");
    }

    void ShowGirlEntry()
    {
        HideAll();
        girlPanel.SetActive(true);
        girlName.input.text = PlayerPrefs.GetString(LevelManager.SavedNameKey, "");
        girlCode.input.text = "";
        joinStatus.Set("ASK PLAYER 1 FOR THE 6-CHARACTER CODE.");
        joinButton.interactable = true;
    }

    void OnBoyStart()
    {
        string name = NameUtil.Clean(boyName.input.text, "KNIGHT");
        LevelManager.Instance.StartGame(PlayerRole.Boy, name, null);
    }

    void OnJoinClicked()
    {
        string name = NameUtil.Clean(girlName.input.text, "PRINCESS");
        string code = FirebaseManager.NormalizeCode(girlCode.input.text);
        joinButton.interactable = false;
        joinStatus.Set("CHECKING THE ROOM...");
        FirebaseManager.Instance.VerifyRoom(code, (ok, msg, room) =>
        {
            joinButton.interactable = true;
            if (ok)
            {
                joinStatus.Set(("ROOM FOUND! GET READY, " + name + "!").ToUpperInvariant());
                StartCoroutine(DelayThen(1.0f, () => LevelManager.Instance.StartGame(PlayerRole.Girl, name, room)));
            }
            else
            {
                joinStatus.Set(Wrap((msg ?? "SOMETHING WENT WRONG").ToUpperInvariant(), 50));
            }
        });
    }

    IEnumerator DelayThen(float seconds, Action a)
    {
        yield return new WaitForSeconds(seconds);
        a();
    }

    /// <summary>Start menu: show the code made on this phone earlier.</summary>
    void ShowSavedInvitation()
    {
        string code = PlayerPrefs.GetString(LevelManager.SavedCodeKey, "");
        if (string.IsNullOrEmpty(code)) return;
        HideAll();
        ShowInvitationInternal(code, false);
    }

    void OnCopyClicked()
    {
        GUIUtility.systemCopyBuffer = shownCode;
        ShowToast("CODE COPIED!", 1.5f);
    }
}
