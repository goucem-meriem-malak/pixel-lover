using UnityEngine;

/// <summary>
/// Put this on ONE empty GameObject in an empty scene and press Play.
/// It creates any manager that is not already in the scene (FirebaseManager, UIManager,
/// LevelManager) and opens the start menu. Everything else (camera, UI, level, player)
/// is built at runtime from built-in sprites.
///
/// If you add the manager components to the scene yourself (to edit their Inspector fields,
/// e.g. the Firebase database URL or custom sprites), this script reuses them.
/// </summary>
[DefaultExecutionOrder(100)]
public class GameBootstrap : MonoBehaviour
{
    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (FirebaseManager.Instance == null) new GameObject("FirebaseManager").AddComponent<FirebaseManager>();
        if (UIManager.Instance == null) new GameObject("UIManager").AddComponent<UIManager>();
        if (LevelManager.Instance == null) new GameObject("LevelManager").AddComponent<LevelManager>();

        UIManager.Instance.ShowStartMenu();
    }
}
