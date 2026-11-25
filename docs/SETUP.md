# Setup guide

Everything you need to go from the files in this repository to the game running on two phones.

## 1. Unity project

1. Install **Unity Hub** and a Unity **LTS** editor (2021.3, 2022.3 or Unity 6). In the installer add the **Android Build Support** module, including *OpenJDK* and *Android SDK & NDK Tools*.
2. In Unity Hub choose **New project > 2D (Built-in Render Pipeline)** (or any 2D template) and name it `PixelLove`.
3. Copy these folders from the repository into the project's `Assets` folder:
   - `Assets/Scripts`
   - `Assets/Editor`
   - `Assets/Icons`
4. Wait for Unity to compile. The crown is applied as the app icon automatically (menu **Pixel Love > Apply App Icon** re-applies it).
5. Open or create a scene, **right-click in the Hierarchy > Create Empty**, name it `Game`, and **Add Component > GameBootstrap**.
6. Save the scene (**Ctrl+S**), then press **Play**. You should see the start screen.

Keyboard controls in the Editor: **A / D** or arrow keys to move, **Space / W / Up** to jump, and the on-screen buttons also work with the mouse.

### Optional: managers in the scene

`GameBootstrap` creates the managers for you. If you want to change their Inspector fields, create empty GameObjects yourself and add the component to each. Do this with Play mode **stopped**, because Unity throws away every change made during Play mode.

| GameObject | Component | Fields worth knowing |
|---|---|---|
| `FirebaseManager` | `FirebaseManager` | **Database Url**, Timeout Seconds |
| `LevelManager` | `LevelManager` | **Debug Skip Button**, Rows (how zoomed in the camera is) |

## 2. Firebase (only needed for two phones)

Without it the game runs in **offline demo mode** (a yellow note on the start screen): the boy gets a code and the same device can use it as Player 2. Great for testing.

### 2.1 Create the database

1. Go to <https://console.firebase.google.com> and **Add project**.
2. **Build > Realtime Database > Create Database**. Pick a location and start in **locked mode**.
3. Open the **Rules** tab, paste the whole content of `Firebase/database.rules.json`, and click **Publish**.
   If you published an older version of these rules, replace them: the current rules also accept the boy's `boyName` and `ringId`, and writes fail without them.
4. Copy the database URL from the **Data** tab, for example `https://my-game-default-rtdb.europe-west1.firebasedatabase.app/`.

### 2.2 Register the Android app

1. In Unity set **Edit > Project Settings > Player > Other Settings > Package Name** (for example `com.yourname.pixellove`).
2. Firebase console > Project settings > **Add app > Android**, using exactly that package name.
3. Download `google-services.json` and put it in Unity's **`Assets/`** folder (not in a subfolder). Download it *after* creating the database so it contains the database URL.

### 2.3 Import the SDK

1. Download the **Firebase Unity SDK** from <https://firebase.google.com/docs/unity/setup> and unzip it.
2. In Unity: **Assets > Import Package > Custom Package** and import `FirebaseDatabase.unitypackage` (this brings in `FirebaseApp` too).
3. Accept the External Dependency Manager prompts. If it does not offer them: **Assets > External Dependency Manager > Android Resolver > Force Resolve**.
4. The Unity console will show many yellow "PluginImporter" warnings. They are harmless.

### 2.4 Turn Firebase on

1. **Edit > Project Settings > Player > Android tab > Other Settings > Script Compilation > Scripting Define Symbols**.
2. Click **+**, type `FIREBASE_ENABLED`, press Enter, click **Apply**. In Unity 6 also check **File > Build Profiles > Android > Player Settings** and add it there if the list is separate.
3. Wait for the compiling spinner (bottom right) to finish, then press Play. The yellow offline note is gone.

### 2.5 The database URL

If the game reports a database URL error, put the URL from step 2.4 into the `FirebaseManager` component's **Database Url** field (see "Optional: managers in the scene"), and save the scene while Play mode is stopped.

### 2.6 What the game stores

```
rooms
  LOVE2D
    roomCode:    "LOVE2D"
    boyFinished: true
    createdAt:   1767225600000     (server time, milliseconds)
    boyName:     "ADAM"
    ringId:      3
```

Codes use `A-Z` and `2-9` only, so they are easy to read aloud. Rooms can be created and read, but not edited or deleted from the game.

## 3. Build the APK

1. On the phone: **Settings > About phone**, tap **Build number** seven times, then enable **Developer options > USB debugging**. Connect the phone with a cable and accept the prompt.
2. In Unity: **File > Build Profiles** (older versions: **Build Settings**), select **Android > Switch Platform**, and make sure your scene is listed.
3. **Player Settings**:
   - *Minimum API Level*: **23 or higher**.
   - *Default Orientation*: **Landscape Left**.
   - *Scripting Backend*: **IL2CPP** with **ARM64** ticked.
4. Click **Build And Run**. The first build can take a long time (10 minutes or more with Firebase).
5. To share the app, click **Build** instead, then copy the `.apk` to the other phone and open it. Allow installs from unknown sources if asked.

## 4. Playing a full test

1. Tick **Debug Skip Button** on the `LevelManager` component (Play mode stopped), rebuild, and install on both phones.
2. **Phone A:** Player 1, type a name, press **SKIP >>** until level 10. The knight walks to the flag and sits. Pick a ring in the shop. A 6-character code appears.
3. Check the Firebase **Data** tab: a `rooms` entry with that code should be there.
4. **Phone B:** Player 2, type a name and the code, tap **JOIN HIM**, skip to level 10, and walk right. The surprise plays.

## 5. Troubleshooting

| Problem | Fix |
|---|---|
| Yellow "OFFLINE DEMO MODE" note is still there | The define is missing or Unity is still compiling. Check step 2.4, wait for the spinner, press Play again. |
| `The type or namespace 'Firebase' could not be found` | You added `FIREBASE_ENABLED` before importing the SDK. Import `FirebaseDatabase.unitypackage`, or remove the define. |
| "Permission denied" on the phone | The rules are not published, or an old version is still active. Paste `Firebase/database.rules.json` again and click Publish. |
| "No connection to the server" | No internet, or a wrong **Database Url**. |
| Room not found with the right code | The boy's code was made in offline demo mode. Both phones must run a Firebase build. |
| Firebase dependency errors in the build | Run **Android Resolver > Force Resolve**, and set the minimum API level to 23 or higher. |
| The app icon is still the Unity logo | Run **Pixel Love > Apply App Icon**, then rebuild. |
| Changes I made in the Inspector vanish | You changed them during Play mode. Stop first, then edit, then save the scene. |
| Sprites look black (URP project) | Keep the **Global Light 2D** object in the scene, or use the 2D Built-in template. |
