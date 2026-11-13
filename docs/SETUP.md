# Pixel Love – 2D Platformer (Unity + Firebase)

A retro pixel-art, Super-Mario-style 2D platformer for Android/iOS with **10 levels** and a two-player "love story":

| Step | What happens |
|------|--------------|
| 1 | Start screen: **Player 1 (Boy)** or **Player 2 (Girl)** |
| 2 | The boy plays Level 1 → 10 (touch controls: Left / Right / Jump) |
| 3 | Clearing Level 10: the boy walks to the flag, **sits down and waits**. The game writes a room to **Firebase Realtime Database** and shows a 6-character **Invitation Code** |
| 4 | The girl taps **Player 2**, types the code. The game checks the code on Firebase; if it exists and `boyFinished == true`, she plays Levels 1 → 10 |
| 5 | On her Level 10 she finds the boy sitting at the flag. As she walks up, an automatic cutscene plays: he stands up, offers a bouquet, hearts appear, they kiss, and **"Together Forever ❤"** is shown |

Everything runs **out of the box with Unity's built-in primitives/generated pixel sprites** – no art import needed. Levels are generated procedurally (deterministic, difficulty ramps from level 1 to 10), the UI is built in code, and the camera/player are created at runtime. You only need **one empty GameObject** in the scene.

---

## 1. Project layout

```
PixelLovePlatformer/
├── README.md
├── .gitignore
├── Firebase/
│   └── database.rules.json        ← paste into Firebase console (Realtime Database → Rules)
└── Assets/
    └── Scripts/
        ├── GameBootstrap.cs       ← put on ONE empty GameObject (entry point)
        ├── PlayerController2D.cs  ← movement, jump, ground-check linecasts, touch/keyboard input
        ├── LevelManager.cs        ← builds levels, level transitions, Level-10 finale / trigger
        ├── LevelGenerator.cs      ← procedural level data (10 levels, rising difficulty)
        ├── LevelObjects.cs        ← Coin, Hazard (spikes), Enemy, LevelGoal, ReunionTrigger
        ├── FirebaseManager.cs     ← room creation, 6-char code generation, join verification
        ├── EndingCutscene.cs      ← stand up, bouquet, hearts, kiss, "Together Forever"
        ├── UIManager.cs           ← start menu, code entry, touch buttons, invitation code UI
        ├── CameraFollow2D.cs      ← smooth side-scroll camera
        └── SpriteFactory.cs       ← generates all fallback pixel sprites at runtime
```

Required files for the five scripts you asked for: `PlayerController2D`, `LevelManager`, `FirebaseManager`, `EndingCutscene`, `UIManager`. The other files are small helpers they depend on – **copy the whole `Scripts` folder**.

**Written for:** Unity 2021.3 LTS, 2022.3 LTS and Unity 6 (6000.x) and Firebase Unity SDK 11+. Both the old Input Manager and the new Input System are handled (the scripts detect which is active). Note: the code was written without access to a Unity editor, so the first thing to do is open the project and check the Console for compile messages.

---

## 2. Create the Unity project and paste the files

1. Install **Unity Hub** and a Unity LTS editor. In the installer add the **Android Build Support** module (including *OpenJDK* and *Android SDK & NDK Tools*).
2. In Unity Hub: **New project → "2D (Built-in Render Pipeline)"** (or *2D Core*). Name it `PixelLovePlatformer`.
   *(If you pick "Universal 2D" (URP) it also works – just keep the **Global Light 2D** object that the template puts in the scene.)*
3. In your file manager, copy the folder **`Assets/Scripts`** from this archive into your new project's `Assets` folder (so you get `Assets/Scripts/*.cs`). Unity compiles them automatically.
4. Open **Window → Package Manager** and make sure **Unity UI** (`com.unity.ugui`) is installed (it is by default in 2D templates; in Unity 6 it's part of the template).
5. Create/open an empty scene (**File → New Scene → Basic 2D**). Save it as `Assets/Scenes/Main.unity`.

---

## 3. Attach the scripts to GameObjects in the Editor

### The one required step (recommended)
1. In the Hierarchy: **right-click → Create Empty**, rename it `Game`.
2. Select `Game`, **Add Component → `GameBootstrap`**.
3. Press **Play**. You'll see the start screen. Done – everything else is created at runtime:
   - `UIManager` (canvas, buttons, touch controls)
   - `LevelManager` (+ `EndingCutscene` is added to it automatically)
   - `FirebaseManager`
   - `Main Camera` gets `CameraFollow2D` automatically
   - The `Player` object (with `Rigidbody2D`, `BoxCollider2D`, `PlayerController2D`) is spawned per level.

### Optional: put the managers in the scene yourself (to edit Inspector fields)
`GameBootstrap` reuses managers that already exist in the scene. Create empty GameObjects and add:

| GameObject | Component | Useful Inspector fields |
|------------|-----------|-------------------------|
| `FirebaseManager` | `FirebaseManager` | **Database Url** (only if your `google-services.json` has no `firebase_url`), Timeout Seconds |
| `LevelManager` | `LevelManager` | **Player Prefab**, **Custom Boy/Girl/Boy Sitting Sprite** (drop in your own pixel art), Camera Size, **Debug Skip Button** |
| `LevelManager` (same object) | `EndingCutscene` | Ending Message, Use Emoji Glyph, Stand Distance, End Hold Seconds |
| `UIManager` | `UIManager` | – (UI is built in code) |
| `Game` | `GameBootstrap` | – |

### Using your own player prefab (optional)
Make a prefab with a `SpriteRenderer` (child named `Visual` is fine). Drag it onto **LevelManager → Player Prefab**. `PlayerController2D`, `Rigidbody2D` and `BoxCollider2D` are added automatically if missing. Untick `Auto Configure Collider` on `PlayerController2D` if you want to keep your own collider size.

### Testing in the Editor (no phone)
- Keyboard: **A / D** or **← / →** to move, **Space / W / ↑** to jump. On a phone the on-screen buttons are used (they also work with the mouse in the Editor).
- Tick **Debug Skip Button** on `LevelManager` (see optional table above) to get a **SKIP >>** button in the HUD, so you can reach Level 10 in seconds.
- Without Firebase the game runs in **OFFLINE DEMO MODE** (yellow note on the start screen): the boy gets a code, the same device can then use "Player 2" with that code. Great for testing the full flow and the cutscene before touching Firebase.

---

## 4. Set up Firebase Realtime Database for Unity

> Needed so two *different phones* can share a room. Skip it while you test locally.

### 4.1 Create the Firebase project
1. Go to <https://console.firebase.google.com> → **Add project** (Analytics is optional → you can disable it).
2. **Build → Realtime Database → Create Database**. Choose a location, start in **locked mode** (we'll paste proper rules next).
3. Open the **Rules** tab and paste the contents of `Firebase/database.rules.json`, then **Publish**. These rules let anyone create a *new* room with exactly the 3 fields below and read a room by its code, and nothing else (rooms can't be edited or deleted from the client).
4. Copy the database URL shown at the top of the **Data** tab (looks like `https://YOUR-PROJECT-default-rtdb.firebaseio.com`).

### 4.2 Register your Android app (and iOS if you need it)
1. Firebase console → ⚙ **Project settings → Your apps → Add app → Android**.
2. **Android package name** must exactly match Unity's *Player Settings → Other Settings → Package Name* (e.g. `com.yourname.pixellove`). Set that in Unity **first**.
3. Download **`google-services.json`** and drop it into Unity's **`Assets/`** folder (project root of Assets, not in a subfolder).
   - Create the Realtime Database **before** downloading the file so it contains `firebase_url`. If you created the database afterwards, download the file again (or set **Database Url** on `FirebaseManager`).
4. iOS (needs a Mac for the build): add an iOS app with your iOS Bundle ID, download **`GoogleService-Info.plist`** and put it into `Assets/` too.

### 4.3 Import the Firebase Unity SDK
1. Download the **Firebase Unity SDK** from <https://firebase.google.com/docs/unity/setup> (or use the Firebase packages from OpenUPM/Package Manager if you prefer UPM).
2. Unzip it. In Unity: **Assets → Import Package → Custom Package…** and import:
   - `FirebaseDatabase.unitypackage`  (this also pulls in `FirebaseApp`)
3. When the **External Dependency Manager** dialog appears, accept the Android dependency resolution (**Assets → External Dependency Manager → Android Resolver → Force Resolve** if it doesn't).
4. Set Android minimum API level to **23 or higher** (Player Settings → Other Settings → Minimum API Level).

### 4.4 Turn the Firebase code on
`FirebaseManager.cs` ships with a safe offline fallback so the project compiles even before the SDK is imported. Once the SDK is imported:

1. **Edit → Project Settings → Player → Android tab → Other Settings → Scripting Define Symbols**
2. Add: `FIREBASE_ENABLED` (click **+**, then **Apply**). Do the same on the iOS tab if needed.
3. Wait for recompilation. The yellow "OFFLINE DEMO MODE" label on the start screen disappears.

### 4.5 Data structure written by the game
```json
rooms
  └── "LOVE2D"
        ├── roomCode:    "LOVE2D"
        ├── boyFinished: true
        └── createdAt:   1767225600000   (server timestamp, ms)
```
- The 6-character code uses `A-Z` and `2-9` (no `0/O/1/I`, so it's easy to read aloud).
- The boy's phone also stores the last code locally, so the start screen shows **MY INVITATION CODE** if he closes the app.

---

## 5. Build and run on an Android device

1. On the phone: **Settings → About phone → tap "Build number" 7×** → enable **Developer options → USB debugging**. Plug in USB and accept the prompt.
2. In Unity: **File → Build Settings** (Unity 6: **File → Build Profiles**).
   - Select **Android → Switch Platform**.
   - Click **Add Open Scenes** so `Main` is ticked (index 0).
3. **Player Settings** (button in Build Settings):
   - *Company / Product Name* – anything.
   - *Other Settings → Package Name* – the same one registered in Firebase.
   - *Minimum API Level* – **23+**; *Target API* – automatic/latest.
   - *Scripting Backend* – **IL2CPP** and tick **ARM64** (needed for Google Play; for plain sideloading Mono/ARMv7 also works).
   - *Resolution and Presentation → Default Orientation* – **Landscape Left** (the game also forces it at runtime).
   - *Scripting Define Symbols* – contains `FIREBASE_ENABLED` (step 4.4).
4. **Build And Run** with your phone connected → Unity builds the APK and launches it on the phone.
   Or **Build** to produce `PixelLove.apk`, then copy it to the phone (or `adb install -r PixelLove.apk`) and open it. Allow "install from unknown sources" if asked.
5. For two-player testing install the APK on **two phones** (or a phone + a second phone/tablet), both with internet:
   - Phone A: Player 1 (use the SKIP button on a debug build to reach level 10 quickly) → code appears.
   - Phone B: Player 2 → enter the code → play → on level 10 walk right to the sitting boy → cutscene.

---

## 6. Customizing

- **Your own sprites:** `LevelManager → Custom Boy / Girl / Boy Sitting Sprite`. Sprites should face **right**, with the **pivot at bottom-centre**, roughly 0.75 × 1 world units tall (e.g. 12×16 px at 16 PPU).
- **Level design:** `LevelGenerator.cs` – tweak gap sizes, enemy/spike probabilities, level length (`targetLength`). Same level number → same layout. Jump limits (height ≈ 2.7, distance ≈ 5 units) are documented at the top of that file.
- **Player feel:** `PlayerController2D` Inspector fields (speed, jump velocity, gravity, coyote time, jump buffer).
- **Ending text:** `EndingCutscene → Ending Message`. By default the heart is drawn as a *sprite* next to the text, because Unity's built-in font doesn't contain the ❤️ emoji on every device. If your font supports it, tick **Use Emoji Glyph**.
- **Progress saving:** not included (each run starts at Level 1). Add `PlayerPrefs.SetInt` in `LevelManager.LoadLevel` if you need it.

---

## 7. Troubleshooting

| Problem | Fix |
|---------|-----|
| `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class…` | Shouldn't occur (scripts detect the Input System). If it does: *Project Settings → Player → Active Input Handling → **Both*** |
| Buttons don't react | Make sure the scene has no *second* EventSystem with the wrong module; delete any manually-created EventSystem – the game creates the right one |
| Sprites are black / invisible (URP) | Keep the **Global Light 2D** in the scene, or use the *2D Built-in* template |
| `The type or namespace name 'Firebase' could not be found` | You defined `FIREBASE_ENABLED` before importing the SDK. Import `FirebaseDatabase.unitypackage` or remove the define |
| "Firebase dependencies unavailable" | Update Google Play services on the phone; re-run Android Resolver (Force Resolve) |
| "Database error: Permission denied" | Rules not published, or you edited them. Paste `Firebase/database.rules.json` again |
| "No connection to the server" | No internet, or wrong database URL – set **Database Url** on `FirebaseManager` |
| Android build fails on Gradle/JDK | Reinstall Android Build Support (incl. OpenJDK/SDK/NDK) via Unity Hub → Installs → Add modules |
| Room not found even though the code is right | The boy's code was created in *offline demo mode* (no `FIREBASE_ENABLED`) – the two phones must both run a Firebase build |

---

## 8. Security note

The rules intentionally need no login to keep the project simple. Codes are 32⁶ ≈ 1 billion combinations and rooms are write-once, which is fine for a personal gift project. For anything public, enable **Firebase Authentication → Anonymous** and add `auth != null` to the `.write`/`.read` rules.
