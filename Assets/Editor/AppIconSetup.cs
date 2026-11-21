#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Makes the crown logo (Assets/Icons/AppIcon.png) the app icon.
/// Runs once automatically when the project opens; you can re-run it any time from the menu
/// Pixel Love > Apply App Icon.
/// </summary>
[InitializeOnLoad]
public static class AppIconSetup
{
    const string IconPath = "Assets/Icons/AppIcon.png";
    const string DoneKey = "PixelLove.AppIconApplied.v1";

    static AppIconSetup()
    {
        if (EditorPrefs.GetBool(DoneKey, false)) return;
        EditorApplication.delayCall += () => { if (Apply(false)) EditorPrefs.SetBool(DoneKey, true); };
    }

    [MenuItem("Pixel Love/Apply App Icon")]
    static void ApplyFromMenu() { Apply(true); }

    static bool Apply(bool verbose)
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            if (verbose) Debug.LogError("[Pixel Love] " + IconPath + " not found. Copy the whole Assets/Icons folder into your project.");
            return false;
        }

        // pixel art must stay sharp: no filtering, no compression artefacts
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null && (importer.filterMode != FilterMode.Point || importer.mipmapEnabled))
        {
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        SetFor(NamedBuildTarget.Android, icon);
        SetFor(NamedBuildTarget.iOS, icon);
        SetFor(NamedBuildTarget.Standalone, icon);
        AssetDatabase.SaveAssets();
        if (verbose) Debug.Log("[Pixel Love] App icon applied. Rebuild the APK to see it on the phone.");
        return true;
    }

    static void SetFor(NamedBuildTarget target, Texture2D icon)
    {
        try
        {
            int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
            if (sizes == null || sizes.Length == 0) return;
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIcons(target, icons, IconKind.Application);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Pixel Love] Could not set the icon for " + target.TargetName + ": " + e.Message);
        }
    }
}
#endif
