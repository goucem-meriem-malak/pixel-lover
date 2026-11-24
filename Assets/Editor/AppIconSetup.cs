#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Makes the crown logo the app icon. Runs once automatically when the project opens
/// (version 2: also fills the Android Adaptive + Round icons, which is what modern phones use,
/// so the crown is no longer cropped or zoomed). Re-run any time from Pixel Love > Apply App Icon.
/// </summary>
[InitializeOnLoad]
public static class AppIconSetup
{
    const string Dir = "Assets/Icons/";
    const string DoneKey = "PixelLove.AppIconApplied.v2";

    static AppIconSetup()
    {
        if (EditorPrefs.GetBool(DoneKey, false)) return;
        EditorApplication.delayCall += () => { if (Apply(false)) EditorPrefs.SetBool(DoneKey, true); };
    }

    [MenuItem("Pixel Love/Apply App Icon")]
    static void ApplyFromMenu() { Apply(true); }

    static Texture2D Load(string file)
    {
        string path = Dir + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool dirty = importer.filterMode != FilterMode.Point || importer.mipmapEnabled
                         || importer.textureCompression != TextureImporterCompression.Uncompressed
                         || importer.npotScale != TextureImporterNPOTScale.None || !importer.isReadable;
            if (dirty)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static bool Apply(bool verbose)
    {
        var icon = Load("AppIcon.png");
        var fg = Load("AppIcon_Foreground.png");
        var bg = Load("AppIcon_Background.png");
        if (icon == null)
        {
            if (verbose) Debug.LogError("[Pixel Love] Assets/Icons/AppIcon.png not found. Copy the whole Assets/Icons folder into your project.");
            return false;
        }

        // classic square icon for every platform
        SetLegacy(NamedBuildTarget.Android, icon);
        SetLegacy(NamedBuildTarget.iOS, icon);
        SetLegacy(NamedBuildTarget.Standalone, icon);

        // Android 8+ adaptive icon (background + foreground layers) and the round icon
        if (fg != null && bg != null) SetAndroidKind("Adaptive", bg, fg);
        SetAndroidKind("Round", icon);
        SetAndroidKind("Legacy", icon);

        AssetDatabase.SaveAssets();
        if (verbose) Debug.Log("[Pixel Love] App icon applied. Rebuild the APK, and uninstall the old app first so the phone refreshes its icon.");
        return true;
    }

    static void SetLegacy(NamedBuildTarget target, Texture2D icon)
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

    /// <summary>Fills one Android icon kind (Adaptive takes background then foreground; the others one texture).</summary>
    static void SetAndroidKind(string kindName, params Texture2D[] textures)
    {
        try
        {
            var kindType = typeof(PlayerSettings).Assembly.GetType("UnityEditor.Android.AndroidPlatformIconKind");
            if (kindType == null) return;
            var prop = kindType.GetProperty(kindName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop == null) return;
            var kind = prop.GetValue(null, null) as PlatformIconKind;
            if (kind == null) return;
            var target = NamedBuildTarget.Android;
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            if (icons == null) return;
            foreach (var pi in icons)
            {
                var layers = new Texture2D[pi.maxLayerCount];
                for (int i = 0; i < layers.Length; i++) layers[i] = textures[Mathf.Min(i, textures.Length - 1)];
                pi.SetTextures(layers);
            }
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Pixel Love] Android " + kindName + " icon not set: " + e.Message);
        }
    }
}
#endif
