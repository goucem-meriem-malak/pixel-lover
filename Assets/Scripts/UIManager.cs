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

/// <summary>Press-and-hold UI button (works with multi-touch: hold Right and tap Jump together).</summary>
public class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action onDown;
    public Action onUp;
    public Image image;
    public Color idleColor = new Color(1f, 1f, 1f, 0.30f);
    public Color activeColor = new Color(1f, 1f, 1f, 0.65f);
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
/// Builds the whole UI in code (no prefabs needed): start menu (Player 1 / Player 2), invitation-code
/// entry, HUD, on-screen touch buttons (Left / Right / Jump), invitation-code display, ending banner.
/// Uses the legacy UnityEngine.UI so nothing extra has to be imported.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    Canvas canvas;
    Font font;
    readonly List<RectTransform> safeRects = new List<RectTransform>();
    Rect lastSafeArea;

    GameObject startPanel, codePanel, hudPanel, touchPanel, invitePanel, endPanel, toastBox, endBanner;
    Button myCodeButton, joinButton, retryButton, copyButton, skipButton, endMenuButton;
    Text hudLabel, joinStatus, inviteTitle, inviteCode, inviteInfo, toastLabel, endingLabel;
    InputField codeInput;
    CanvasGroup endBannerGroup;
    RectTransform heartL, heartR;
    Coroutine toastRoutine, bannerRoutine;
    string shownCode = "";
    bool pulseHearts;

    static readonly Color BtnBlue = new Color(0.15f, 0.42f, 0.92f);
    static readonly Color BtnPink = new Color(0.95f, 0.35f, 0.62f);
    static readonly Color BtnGold = new Color(0.95f, 0.70f, 0.15f);
    static readonly Color BtnGray = new Color(0.35f, 0.38f, 0.48f);
    static readonly Color BtnGreen = new Color(0.20f, 0.65f, 0.35f);

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        font = LoadFont();
        BuildCanvas();
        BuildStartPanel();
        BuildCodePanel();
        BuildHud();
        BuildTouchControls();
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
        if (pulseHearts && heartL != null)
        {
            float s = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);
            heartL.localScale = heartR.localScale = new Vector3(s, s, 1f);
        }
    }

    static Font LoadFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
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

    // ------------------------------------------------------------------ public API (used by other scripts)

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
        bool skip = LevelManager.Instance != null && LevelManager.Instance.debugSkipButton;
        skipButton.gameObject.SetActive(skip);
    }

    public void HideTouchControls() { touchPanel.SetActive(false); TouchInput.Reset(); }
    public void ShowTouchControls() { touchPanel.SetActive(true); }

    public void SetHud(PlayerRole role, int level, int total, int coins)
    {
        string who = role == PlayerRole.Boy ? "PLAYER 1" : "PLAYER 2";
        hudLabel.text = who + "   LEVEL " + level + "/" + total + "   COINS " + coins;
    }

    public void ShowToast(string text, float seconds)
    {
        toastLabel.text = text;
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

    /// <summary>Boy finished: show the 6-character invitation code.</summary>
    public void ShowInvitation(string code)
    {
        shownCode = code;
        hudPanel.SetActive(false);
        touchPanel.SetActive(false);
        invitePanel.SetActive(true);
        inviteTitle.text = "YOU DID IT!";
        inviteCode.fontSize = 120;
        inviteCode.text = Spaced(code);
        inviteInfo.text = "Send this invitation code to Player 2.\nShe enters it on her start screen. I'll be waiting right here.";
        copyButton.gameObject.SetActive(true);
        retryButton.gameObject.SetActive(false);
    }

    public void ShowInvitationError(string message)
    {
        hudPanel.SetActive(false);
        touchPanel.SetActive(false);
        invitePanel.SetActive(true);
        inviteTitle.text = "COULD NOT CREATE ROOM";
        inviteCode.fontSize = 38;
        inviteCode.text = message;
        inviteInfo.text = "Check your internet connection and tap RETRY.";
        copyButton.gameObject.SetActive(false);
        retryButton.gameObject.SetActive(true);
    }

    /// <summary>Show the saved code again (start menu button).</summary>
    void ShowSavedInvitation()
    {
        string code = PlayerPrefs.GetString(LevelManager.SavedCodeKey, "");
        if (string.IsNullOrEmpty(code)) return;
        HideAll();
        ShowInvitation(code);
    }

    public void ShowEndingMessage(string message, bool showHeartSprites)
    {
        hudPanel.SetActive(false);
        touchPanel.SetActive(false);
        endPanel.SetActive(true);
        endMenuButton.gameObject.SetActive(false);
        endingLabel.text = message;
        heartL.gameObject.SetActive(showHeartSprites);
        heartR.gameObject.SetActive(showHeartSprites);
        pulseHearts = showHeartSprites;
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
        codePanel.SetActive(false);
        hudPanel.SetActive(false);
        touchPanel.SetActive(false);
        invitePanel.SetActive(false);
        endPanel.SetActive(false);
        endBanner.SetActive(false);
        toastBox.SetActive(false);
        pulseHearts = false;
    }

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

    // ------------------------------------------------------------------ button handlers

    void OnBoyClicked() { LevelManager.Instance.StartGame(PlayerRole.Boy); }

    void OnGirlClicked()
    {
        HideAll();
        codePanel.SetActive(true);
        codeInput.text = "";
        SetJoinStatus("Ask Player 1 for the 6-character code.", Color.white);
        joinButton.interactable = true;
    }

    void OnJoinClicked()
    {
        string code = FirebaseManager.NormalizeCode(codeInput.text);
        joinButton.interactable = false;
        SetJoinStatus("Checking room...", Color.white);
        FirebaseManager.Instance.VerifyRoom(code, (ok, msg) =>
        {
            joinButton.interactable = true;
            if (ok)
            {
                SetJoinStatus(msg + " Get ready!", new Color(0.6f, 1f, 0.6f));
                StartCoroutine(DelayThen(0.9f, () => LevelManager.Instance.StartGame(PlayerRole.Girl)));
            }
            else
            {
                SetJoinStatus(msg, new Color(1f, 0.55f, 0.55f));
            }
        });
    }

    void SetJoinStatus(string text, Color c) { joinStatus.text = text; joinStatus.color = c; }

    IEnumerator DelayThen(float seconds, Action a)
    {
        yield return new WaitForSeconds(seconds);
        a();
    }

    void OnCopyClicked()
    {
        GUIUtility.systemCopyBuffer = shownCode;
        ShowToast("Code copied!", 1.5f);
    }

    // ------------------------------------------------------------------ UI construction helpers

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

    /// <summary>Anchor == pivot, so pos is the margin from that corner/edge/centre.</summary>
    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>Full-screen panel with a background; returns the safe-area content rect.</summary>
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

    Text MakeText(Transform parent, string s, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var rt = NewRect("Text", parent);
        Place(rt, anchor, pos, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = s;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyle.Bold;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = rt.gameObject.AddComponent<Shadow>();
        sh.effectDistance = new Vector2(2f, -2f);
        sh.effectColor = new Color(0f, 0f, 0f, 0.5f);
        return t;
    }

    Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, int fontSize, UnityAction onClick)
    {
        var rt = NewRect(label, parent);
        Place(rt, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bg;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        var t = MakeText(rt, label, fontSize, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Stretch(t.rectTransform);
        return b;
    }

    Image MakeSpriteImage(Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = NewRect("Sprite", parent);
        Place(rt, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    void Bar(Transform parent, Color c, float y, float h)
    {
        var rt = NewRect("Bar", parent);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
    }

    TouchHoldButton MakeHold(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
    {
        var rt = NewRect(label, parent);
        Place(rt, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        var hold = rt.gameObject.AddComponent<TouchHoldButton>();
        hold.image = img;
        img.color = hold.idleColor;
        var t = MakeText(rt, label, fontSize, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Stretch(t.rectTransform);
        return hold;
    }

    // ------------------------------------------------------------------ panels

    void BuildStartPanel()
    {
        var safe = MakePanel("StartPanel", new Color(0.36f, 0.62f, 0.95f), out startPanel);
        Bar(startPanel.transform, new Color(0.55f, 0.35f, 0.20f), 0f, 70f);
        Bar(startPanel.transform, new Color(0.35f, 0.75f, 0.25f), 70f, 24f);

        MakeText(safe, "PIXEL LOVE", 100, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 130f));
        MakeText(safe, "a tiny adventure for two", 36, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(800f, 50f));

        MakeSpriteImage(safe, SpriteFactory.BoyStand(), new Vector2(0.5f, 0f), new Vector2(-400f, 94f), new Vector2(144f, 192f));
        MakeSpriteImage(safe, SpriteFactory.GirlStand(), new Vector2(0.5f, 0f), new Vector2(400f, 94f), new Vector2(144f, 192f));

        var center = new Vector2(0.5f, 0.5f);
        MakeButton(safe, "PLAYER 1  (BOY)", center, new Vector2(0f, 30f), new Vector2(520f, 110f), BtnBlue, 44, OnBoyClicked);
        MakeButton(safe, "PLAYER 2  (GIRL)", center, new Vector2(0f, -100f), new Vector2(520f, 110f), BtnPink, 44, OnGirlClicked);
        myCodeButton = MakeButton(safe, "MY INVITATION CODE", center, new Vector2(0f, -215f), new Vector2(520f, 70f), BtnGold, 30, ShowSavedInvitation);

        if (!FirebaseManager.Enabled)
        {
            MakeText(safe, "OFFLINE DEMO MODE - Firebase not enabled (see README). Codes only work on this device.",
                     22, new Color(1f, 1f, 0.6f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1100f, 34f));
        }
    }

    void BuildCodePanel()
    {
        var safe = MakePanel("CodePanel", new Color(0.12f, 0.14f, 0.25f, 0.97f), out codePanel);
        var center = new Vector2(0.5f, 0.5f);
        MakeText(safe, "ENTER INVITATION CODE", 60, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1000f, 90f));

        // input field
        var rt = NewRect("CodeInput", safe);
        Place(rt, center, new Vector2(0f, 60f), new Vector2(560f, 110f));
        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = Color.white;
        codeInput = rt.gameObject.AddComponent<InputField>();
        var text = MakeText(rt, "", 60, new Color(0.1f, 0.1f, 0.2f), TextAnchor.MiddleCenter, center, Vector2.zero, Vector2.zero);
        var ph = MakeText(rt, "ABC123", 50, new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleCenter, center, Vector2.zero, Vector2.zero);
        Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(10f, 6f); text.rectTransform.offsetMax = new Vector2(-10f, -6f);
        Stretch(ph.rectTransform); ph.rectTransform.offsetMin = new Vector2(10f, 6f); ph.rectTransform.offsetMax = new Vector2(-10f, -6f);
        text.GetComponent<Shadow>().enabled = false;
        ph.GetComponent<Shadow>().enabled = false;
        codeInput.targetGraphic = bg;
        codeInput.textComponent = text;
        codeInput.placeholder = ph;
        codeInput.characterLimit = FirebaseManager.CodeLength;
        codeInput.contentType = InputField.ContentType.Alphanumeric;
        codeInput.onValidateInput = (s, i, c) => char.ToUpperInvariant(c);

        joinStatus = MakeText(safe, "", 32, Color.white, TextAnchor.MiddleCenter, center, new Vector2(0f, -35f), new Vector2(1000f, 60f));
        joinButton = MakeButton(safe, "JOIN", center, new Vector2(0f, -120f), new Vector2(360f, 90f), BtnGreen, 44, OnJoinClicked);
        MakeButton(safe, "BACK", center, new Vector2(0f, -225f), new Vector2(240f, 70f), BtnGray, 32, () => ShowStartMenu());
    }

    void BuildHud()
    {
        var safe = MakePanel("HudPanel", new Color(0f, 0f, 0f, 0f), out hudPanel);
        var tl = new Vector2(0f, 1f);
        var tr = new Vector2(1f, 1f);
        var bgRt = NewRect("HudBg", safe);
        Place(bgRt, tl, new Vector2(20f, -15f), new Vector2(640f, 56f));
        var bg = bgRt.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.4f);
        bg.raycastTarget = false;
        hudLabel = MakeText(bgRt, "", 30, Color.white, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 56f));
        Stretch(hudLabel.rectTransform);
        hudLabel.rectTransform.offsetMin = new Vector2(14f, 0f);

        MakeButton(safe, "MENU", tr, new Vector2(-20f, -15f), new Vector2(150f, 56f), BtnGray, 26, () => LevelManager.Instance.ReturnToMenu());
        skipButton = MakeButton(safe, "SKIP >>", tr, new Vector2(-20f, -85f), new Vector2(150f, 56f), BtnGold, 26, () => LevelManager.Instance.DebugSkipLevel());
    }

    void BuildTouchControls()
    {
        var safe = MakePanel("TouchPanel", new Color(0f, 0f, 0f, 0f), out touchPanel);
        var bl = new Vector2(0f, 0f);
        var br = new Vector2(1f, 0f);

        var left = MakeHold(safe, "<", bl, new Vector2(30f, 30f), new Vector2(170f, 170f), 90);
        left.onDown = () => TouchInput.Left = true;
        left.onUp = () => TouchInput.Left = false;

        var right = MakeHold(safe, ">", bl, new Vector2(225f, 30f), new Vector2(170f, 170f), 90);
        right.onDown = () => TouchInput.Right = true;
        right.onUp = () => TouchInput.Right = false;

        var jump = MakeHold(safe, "JUMP", br, new Vector2(-30f, 30f), new Vector2(230f, 230f), 52);
        jump.onDown = TouchInput.PressJump;
        jump.onUp = TouchInput.ReleaseJump;
    }

    void BuildInvitePanel()
    {
        var safe = MakePanel("InvitePanel", new Color(0.05f, 0.06f, 0.15f, 0.88f), out invitePanel);
        var center = new Vector2(0.5f, 0.5f);
        inviteTitle = MakeText(safe, "YOU DID IT!", 72, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1100f, 100f));
        MakeText(safe, "Your invitation code", 34, Color.white, TextAnchor.MiddleCenter, center, new Vector2(0f, 120f), new Vector2(800f, 50f));

        var boxRt = NewRect("CodeBox", safe);
        Place(boxRt, center, new Vector2(0f, 20f), new Vector2(980f, 170f));
        var box = boxRt.gameObject.AddComponent<Image>();
        box.color = new Color(1f, 1f, 1f, 0.12f);
        box.raycastTarget = false;
        inviteCode = MakeText(boxRt, "", 120, Color.white, TextAnchor.MiddleCenter, center, Vector2.zero, new Vector2(960f, 170f));
        Stretch(inviteCode.rectTransform);

        inviteInfo = MakeText(safe, "", 30, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter, center, new Vector2(0f, -110f), new Vector2(1000f, 100f));

        copyButton = MakeButton(safe, "COPY CODE", center, new Vector2(-190f, -230f), new Vector2(340f, 80f), BtnGreen, 34, OnCopyClicked);
        retryButton = MakeButton(safe, "RETRY", center, new Vector2(-190f, -230f), new Vector2(340f, 80f), BtnGold, 34, () =>
        {
            invitePanel.SetActive(false);
            LevelManager.Instance.RetryRegisterRoom();
        });
        MakeButton(safe, "MENU", center, new Vector2(190f, -230f), new Vector2(340f, 80f), BtnGray, 34, () => LevelManager.Instance.ReturnToMenu());
    }

    void BuildEndPanel()
    {
        var safe = MakePanel("EndPanel", new Color(0f, 0f, 0f, 0f), out endPanel);

        var bannerRt = NewRect("Banner", safe);
        Place(bannerRt, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1200f, 150f));
        var bannerBg = bannerRt.gameObject.AddComponent<Image>();
        bannerBg.color = new Color(0f, 0f, 0f, 0.35f);
        bannerBg.raycastTarget = false;
        endBannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
        endBannerGroup.alpha = 0f;
        endBanner = bannerRt.gameObject;

        endingLabel = MakeText(bannerRt, "", 84, new Color(1f, 0.8f, 0.9f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 150f));
        Stretch(endingLabel.rectTransform);

        var heart = SpriteFactory.Heart();
        heartL = MakeSpriteImage(bannerRt, heart, new Vector2(0.5f, 0.5f), new Vector2(-440f, 0f), new Vector2(90f, 80f)).rectTransform;
        heartR = MakeSpriteImage(bannerRt, heart, new Vector2(0.5f, 0.5f), new Vector2(440f, 0f), new Vector2(90f, 80f)).rectTransform;

        endMenuButton = MakeButton(safe, "BACK TO MENU", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(420f, 90f), BtnPink, 38, () => LevelManager.Instance.ReturnToMenu());
    }

    void BuildToast()
    {
        var rt = NewRect("Toast", canvas.transform);
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(900f, 90f));
        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;
        toastBox = rt.gameObject;
        toastLabel = MakeText(rt, "", 44, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 90f));
        Stretch(toastLabel.rectTransform);
    }
}
