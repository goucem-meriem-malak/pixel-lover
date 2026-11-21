using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if FIREBASE_ENABLED
using Firebase;
using Firebase.Database;
#endif

/// <summary>
/// Room creation / verification on Firebase Realtime Database.
///
/// Database layout (one node per room, key = the 6-character code):
///   rooms/LOVE2D = { "roomCode": "LOVE2D", "boyFinished": true, "createdAt": <server timestamp>,
///                    "boyName": "ADAM", "ringId": 3 }
///
/// MODES
///  * FIREBASE_ENABLED defined (Player Settings > Scripting Define Symbols) + Firebase Unity SDK
///    imported  -> real cloud sync between two phones.
///  * Not defined -> OFFLINE DEMO MODE: rooms are kept in PlayerPrefs on the same device so the
///    project compiles and the whole flow can be tested in the Editor with no Firebase setup.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

#if FIREBASE_ENABLED
    public const bool Enabled = true;
#else
    public const bool Enabled = false;
#endif

    [Tooltip("Only needed if google-services.json has no firebase_url, e.g. https://YOUR-PROJECT-default-rtdb.firebaseio.com")]
    public string databaseUrl = "";

    [Tooltip("Seconds before a database call is treated as failed (no internet).")]
    public float timeoutSeconds = 12f;

    public const int CodeLength = 6;
    // No 0/O/1/I to avoid typos when the code is read out or typed.
    const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    const string RoomsNode = "rooms";

    public bool IsReady { get; private set; }
    public string StatusMessage { get; private set; }

    readonly System.Random rng = new System.Random(Guid.NewGuid().GetHashCode());

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ------------------------------------------------------------------ code generation

    public string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++) chars[i] = CodeAlphabet[rng.Next(CodeAlphabet.Length)];
        return new string(chars);
    }

    public static string NormalizeCode(string raw)
    {
        return string.IsNullOrEmpty(raw) ? "" : raw.Trim().ToUpperInvariant();
    }

    static bool IsWellFormed(string code)
    {
        if (code.Length != CodeLength) return false;
        foreach (char c in code)
            if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
        return true;
    }

    /// <summary>Waits for a Task with a timeout. done(true) = timed out.</summary>
    IEnumerator WaitFor(Task task, Action<bool> done)
    {
        float t = 0f;
        while (!task.IsCompleted && t < timeoutSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        done(!task.IsCompleted);
    }

    // ------------------------------------------------------------------ public API

    /// <summary>Boy finished level 10 and chose a ring: create rooms/{code}. callback(success, code, errorMessage)</summary>
    public void CreateRoom(string boyName, int ringId, Action<bool, string, string> callback)
    {
        StartCoroutine(CreateRoomRoutine(NameUtil.Clean(boyName, "KNIGHT"), ringId, callback));
    }

    /// <summary>Girl enters a code: callback(valid, messageForPlayer, roomInfoOrNull)</summary>
    public void VerifyRoom(string rawCode, Action<bool, string, RoomInfo> callback)
    {
        StartCoroutine(VerifyRoomRoutine(NormalizeCode(rawCode), callback));
    }

#if FIREBASE_ENABLED
    // =====================================================================================
    //  REAL FIREBASE IMPLEMENTATION
    // =====================================================================================
    DatabaseReference rooms;
    bool initStarted;

    IEnumerator EnsureInitialised(Action<string> onError)
    {
        if (IsReady) yield break;
        if (!initStarted)
        {
            initStarted = true;
            var depTask = FirebaseApp.CheckAndFixDependenciesAsync();
            while (!depTask.IsCompleted) yield return null;

            if (depTask.IsFaulted || depTask.Result != DependencyStatus.Available)
            {
                StatusMessage = "Firebase dependencies unavailable: " +
                                (depTask.IsFaulted ? depTask.Exception.GetBaseException().Message : depTask.Result.ToString());
                Debug.LogError("[Firebase] " + StatusMessage);
                initStarted = false;
                onError(StatusMessage);
                yield break;
            }

            string failure = null;
            try
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                FirebaseDatabase db = string.IsNullOrEmpty(databaseUrl)
                    ? FirebaseDatabase.DefaultInstance
                    : FirebaseDatabase.GetInstance(app, databaseUrl);
                rooms = db.RootReference.Child(RoomsNode);
                IsReady = true;
                StatusMessage = "Firebase ready";
            }
            catch (Exception e)
            {
                failure = "Firebase init failed: " + e.Message;
                Debug.LogError("[Firebase] " + e);
            }

            if (failure != null)
            {
                StatusMessage = failure;
                initStarted = false;
                onError(failure);
                yield break;
            }
        }
        else
        {
            // another call is already initialising
            float t = 0f;
            while (!IsReady && t < timeoutSeconds) { t += Time.unscaledDeltaTime; yield return null; }
            if (!IsReady) { onError("Firebase is not ready (check your internet and setup)."); yield break; }
        }
    }

    void Start() { StartCoroutine(EnsureInitialised(msg => { })); }

    IEnumerator CreateRoomRoutine(string boyName, int ringId, Action<bool, string, string> callback)
    {
        string initError = null;
        yield return EnsureInitialised(m => initError = m);
        if (!IsReady) { callback(false, null, initError ?? "Firebase is not ready."); yield break; }

        for (int attempt = 0; attempt < 6; attempt++)
        {
            string code = GenerateCode();

            // make sure the code is not taken
            var getTask = rooms.Child(code).GetValueAsync();
            bool timedOut = false;
            yield return WaitFor(getTask, t => timedOut = t);
            if (timedOut) { callback(false, null, "No connection to the server. Check your internet."); yield break; }
            if (getTask.IsFaulted)
            {
                callback(false, null, "Database error: " + getTask.Exception.GetBaseException().Message);
                yield break;
            }
            if (getTask.Result.Exists) continue; // collision, try another code

            var data = new Dictionary<string, object>
            {
                { "roomCode", code },
                { "boyFinished", true },
                { "createdAt", ServerValue.Timestamp },
                { "boyName", boyName },
                { "ringId", ringId }
            };
            var setTask = rooms.Child(code).SetValueAsync(data);
            yield return WaitFor(setTask, t => timedOut = t);
            if (timedOut) { callback(false, null, "No connection to the server. Check your internet."); yield break; }
            if (setTask.IsFaulted)
            {
                callback(false, null, "Could not save the room: " + setTask.Exception.GetBaseException().Message);
                yield break;
            }

            callback(true, code, null);
            yield break;
        }
        callback(false, null, "Could not find a free invitation code. Please retry.");
    }

    IEnumerator VerifyRoomRoutine(string code, Action<bool, string, RoomInfo> callback)
    {
        if (!IsWellFormed(code)) { callback(false, "A code has 6 letters/numbers.", null); yield break; }

        string initError = null;
        yield return EnsureInitialised(m => initError = m);
        if (!IsReady) { callback(false, initError ?? "Firebase is not ready.", null); yield break; }

        var getTask = rooms.Child(code).GetValueAsync();
        bool timedOut = false;
        yield return WaitFor(getTask, t => timedOut = t);
        if (timedOut) { callback(false, "No connection to the server. Check your internet.", null); yield break; }
        if (getTask.IsFaulted)
        {
            callback(false, "Database error: " + getTask.Exception.GetBaseException().Message, null);
            yield break;
        }

        DataSnapshot snap = getTask.Result;
        if (!snap.Exists) { callback(false, "Room not found. Check the code.", null); yield break; }

        object finished = snap.Child("boyFinished").Value;
        if (!(finished is bool && (bool)finished))
        {
            callback(false, "He hasn't finished his journey yet. Try again later.", null);
            yield break;
        }

        var info = new RoomInfo { code = code, boyName = "KNIGHT", ring = 0 };
        object nameVal = snap.Child("boyName").Value;
        if (nameVal is string) info.boyName = NameUtil.Clean((string)nameVal, "KNIGHT");
        object ringVal = snap.Child("ringId").Value;
        if (ringVal != null)
        {
            try { info.ring = Mathf.Clamp(Convert.ToInt32(ringVal), 0, Rings.Count - 1); } catch (Exception) { info.ring = 0; }
        }
        callback(true, "Room found!", info);
    }

#else
    // =====================================================================================
    //  OFFLINE DEMO FALLBACK (no Firebase SDK needed; same-device only)
    // =====================================================================================
    void Start()
    {
        IsReady = true;
        StatusMessage = "OFFLINE DEMO MODE (FIREBASE_ENABLED not defined)";
        Debug.LogWarning("[Firebase] " + StatusMessage + ". Room codes only work on this device. See README step 4.");
    }

    IEnumerator CreateRoomRoutine(string boyName, int ringId, Action<bool, string, string> callback)
    {
        yield return new WaitForSeconds(0.6f);
        string code;
        int guard = 0;
        do { code = GenerateCode(); } while (PlayerPrefs.HasKey("demoRoom_" + code) && ++guard < 50);
        PlayerPrefs.SetString("demoRoom_" + code, boyName + "|" + ringId);
        PlayerPrefs.Save();
        callback(true, code, null);
    }

    IEnumerator VerifyRoomRoutine(string code, Action<bool, string, RoomInfo> callback)
    {
        yield return new WaitForSeconds(0.5f);
        if (!IsWellFormed(code)) { callback(false, "A code has 6 letters/numbers.", null); yield break; }
        if (!PlayerPrefs.HasKey("demoRoom_" + code))
        {
            callback(false, "Room not found (offline demo: only codes made on this device work).", null);
            yield break;
        }
        string[] parts = PlayerPrefs.GetString("demoRoom_" + code).Split('|');
        var info = new RoomInfo { code = code, boyName = parts.Length > 0 ? parts[0] : "KNIGHT", ring = 0 };
        int r;
        if (parts.Length > 1 && int.TryParse(parts[1], out r)) info.ring = Mathf.Clamp(r, 0, Rings.Count - 1);
        callback(true, "Room found!", info);
    }
#endif
}
