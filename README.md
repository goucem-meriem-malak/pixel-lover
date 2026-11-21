<p align="center">
  <img src="docs/images/logo.png" alt="Pixel Love logo" width="120">
</p>

<h1 align="center">Pixel Love</h1>

<p align="center">
  A retro pixel-art platformer for two.<br>
  He clears ten levels and waits at the flag. She follows his invitation code, finds him at the end, and the game says the rest.
</p>

<p align="center">
  <img alt="Unity" src="https://img.shields.io/badge/Unity-6000.0%20LTS-000000?logo=unity&logoColor=white">
  <img alt="C#" src="https://img.shields.io/badge/C%23-2D%20Platformer-239120?logo=csharp&logoColor=white">
  <img alt="Firebase" src="https://img.shields.io/badge/Firebase-Realtime%20Database-FFCA28?logo=firebase&logoColor=black">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Android-3DDC84?logo=android&logoColor=white">
  <img alt="License" src="https://img.shields.io/badge/License-MIT-green">
</p>

---

## Screenshots

<table>
  <tr>
    <td align="center"><img src="docs/screenshots/start.jpg" width="200" alt="Start screen"><br><sub>Choose Player 1 or Player 2</sub></td>
    <td align="center"><img src="docs/screenshots/level_boy.jpg" width="200" alt="Gameplay"><br><sub>Ten pixel-art levels</sub></td>
    <td align="center"><img src="docs/screenshots/shop.jpg" width="200" alt="Shop"><br><sub>Shop menu</sub></td>
    <td align="center"><img src="docs/screenshots/invitation.jpg" width="200" alt="Invitation code"><br><sub>The boy's invitation code</sub></td>
  </tr>
  <tr>
    <td align="center"><img src="docs/screenshots/code-entry.png" width="200" alt="Enter code"><br><sub>She enters the code</sub></td>
    <td align="center"><img src="docs/screenshots/level_girl.jpg" width="200" alt="Gameplay"><br><sub>Ten pixel-art levels</sub></td>
    <td align="center"><img src="docs/screenshots/reunion.jpg" width="200" alt="Reunion"><br><sub>She finds him at the flag</sub></td>
    <td align="center"><img src="docs/screenshots/ending.jpg" width="200" alt="Ending"><br><sub>Together Forever</sub></td>
  </tr>
</table>

## How the story works

1. **Player 1 (Boy)** plays Level 1 to Level 10.
2. After the last flag, he walks to it and **sits down to wait**. The game saves a room on Firebase and shows a **6-character invitation code**.
3. **Player 2 (Girl)** types that code on her start screen. The game checks it online, and if the room exists she plays her own run of ten levels.
4. On her Level 10 she finds him sitting at the end. When she walks up, a short cutscene plays: he stands, offers a bouquet, hearts appear, a kiss, and **"Together Forever"**.

## Features

- **Ten levels.** Procedurally generated from a fixed seed, so each level is always the same. Difficulty rises from gentle hills to wide gaps, spikes and patrolling enemies.
- **Touch controls.** On-screen Left, Right and Jump buttons that work with multi-touch.
- **Two-device story.** A room code on Firebase connects two phones.
- **Ending cutscene.** Scripted standing, bouquet, heart effects and kiss.
- **Runs out of the box.** All sprites are generated in code. Drop in your own art any time.
- **Offline demo mode.** Test the whole flow on one device without Firebase.

## Tech stack

| Part | Choice |
|---|---|
| Engine | Unity 6.0 LTS (6000.0.x), 2D |
| Language | C# |
| Backend | Firebase Realtime Database |
| Physics | Rigidbody2D, BoxCollider2D, ground-check linecasts |
| Target | Android (iOS possible with a Mac) |

Everything runs on the free Firebase tier. No server code is needed.

## Quick start

You need [Unity Hub](https://unity.com/download) and the Unity editor with Android Build Support.

1. **Get the code**
   ```bash
   git clone https://github.com/goucem-meriem-malak/pixel-love.git
   ```
2. **Open it.** In Unity Hub choose **Add → Add project from disk**, select the cloned folder, and open it with Unity 6000.0.x.
3. **Install Unity UI** if prompted: *Window → Package Management → Package Manager → Unity Registry → Unity UI*.
4. **Play.** Open the scene, make sure there is an empty GameObject with the `GameBootstrap` component, and press **Play**. Keyboard: A/D or arrows, Space to jump.

The game starts in **offline demo mode**, so this works with no Firebase setup. The Boy and the Girl can both be tested on the same machine.

### Two phones with Firebase

Follow [docs/SETUP.md](docs/SETUP.md), section 4. In short:

1. Create a Firebase project and a Realtime Database, then paste `Firebase/database.rules.json` into the **Rules** tab.
2. Download `google-services.json` into `Assets/` (it is ignored by Git, so it is never uploaded).
3. Import the Firebase Unity SDK (`FirebaseDatabase.unitypackage`).
4. Add the scripting define `FIREBASE_ENABLED` in *Player Settings → Other Settings*.

### Build an APK

*File → Build Profiles → Android → Build And Run*, with your phone connected and USB debugging on. Full steps are in [docs/SETUP.md](docs/SETUP.md), section 5.

## Project structure

```
Assets/Scripts/
  GameBootstrap.cs       entry point, creates the managers
  PlayerController2D.cs  movement, jump, ground check, touch input
  LevelManager.cs        builds levels, transitions, finale and reunion
  LevelGenerator.cs      procedural level data for levels 1 to 10
  LevelObjects.cs        coins, spikes, enemies, goal and trigger
  FirebaseManager.cs     room creation, code generation, join check
  EndingCutscene.cs      stand up, bouquet, hearts, kiss, final text
  UIManager.cs           menus, code entry, touch buttons, invitation code
  CameraFollow2D.cs      side-scrolling camera
  SpriteFactory.cs       generated fallback pixel sprites
Firebase/
  database.rules.json    Realtime Database security rules
docs/                    setup guide, logo, screenshots
```

## Database structure

```json
rooms
  └── "LOVE2D"
        ├── roomCode:    "LOVE2D"
        ├── boyFinished: true
        └── createdAt:   <server timestamp>
```

## Status

Played and tested in the Unity editor. Android device testing is in progress.

## License

[MIT](LICENSE). The license covers the code in this repository.

## Credits

Made by Meriem Malak Goucem. Pixel art, level layouts and the story flow are original to this project.
