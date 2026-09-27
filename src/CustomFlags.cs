using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2CppBrokenArrow.Client.Ecs.UI;

namespace BACustomNations;

internal static class CustomFlags
{
    private sealed class Entry
    {
        public string FilePath = "";
        public string Fallback = "";
        public Sprite? Sprite;
        public Texture2D? Texture;
        public bool Failed;
    }
    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);
    private const string BuiltinFile = "assets/zombies-flag.png";

    internal static void EnsureBuiltin()
    {
        var path = FlagFiles.Resolve(Path.Combine(Core.Root, "packs"), BuiltinFile);
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var embedded = typeof(Core).Assembly.GetManifestResourceStream("zombies-flag.png")
            ?? throw new InvalidDataException("Bundled zombie flag is missing.");
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        embedded.CopyTo(output);
    }

    internal static string Register(NationPack pack, string fallback)
    {
        // Existing 0.1.0 zombie packs acquire the bundled flag without rewriting
        // their costs, IDs, roster, or JSON formatting during an upgrade.
        var file = pack.Flag;
        if (string.IsNullOrWhiteSpace(file) && pack.Key == "zombies") file = BuiltinFile;
        if (string.IsNullOrWhiteSpace(file)) return fallback;
        try
        {
            string path = FlagFiles.Resolve(Path.Combine(Core.Root, "packs"), file);
            string address = "bacn.flag." + pack.Key;
            if (!Entries.ContainsKey(address))
                Entries.Add(address, new Entry { FilePath = path, Fallback = fallback });
            return address;
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[CustomNations] Flag for {pack.Name}: {e.Message} Using stock fallback.");
            return fallback;
        }
    }

    internal static void Prepare(ref string address)
    {
        if (address == null || !Entries.TryGetValue(address, out var entry)) return;
        if (entry.Failed) { address = entry.Fallback; return; }
        try
        {
            if (entry.Sprite == null)
            {
                var bytes = FlagFiles.ReadPng(entry.FilePath);
                var dimensions = FlagFiles.InspectPng(bytes);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                entry.Texture = texture;
                texture.name = address;
                texture.hideFlags = HideFlags.HideAndDontSave;
                if (!ImageConversion.LoadImage(texture, bytes, true))
                    throw new InvalidDataException("Unity could not decode the PNG.");
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                entry.Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                entry.Sprite.name = address;
                entry.Sprite.hideFlags = HideFlags.HideAndDontSave;
                MelonLogger.Msg($"[CustomNations] Loaded flag {address}: {dimensions.Width}x{dimensions.Height}.");
            }
            // Populate the native cache before either sync or async loading starts.
            // Native code still assigns the image and completes its own async task.
            IconsLoader._sprites[address] = entry.Sprite;
            IconsLoader._sprites[IconsLoader.OUTLINE_PREFIX + address] = entry.Sprite;
        }
        catch (Exception e)
        {
            entry.Failed = true;
            if (entry.Sprite != null) UnityEngine.Object.Destroy(entry.Sprite);
            if (entry.Texture != null) UnityEngine.Object.Destroy(entry.Texture);
            entry.Sprite = null; entry.Texture = null;
            MelonLogger.Warning($"[CustomNations] Flag {address}: {e.Message} Using stock fallback until restart.");
            address = entry.Fallback;
        }
    }

    internal static object CheckNativeLoaders(string address)
    {
        if (!Entries.TryGetValue(address, out var entry)) return new { custom = false };
        var sync = IconsLoader.LoadSprite(address, false, null);
        var outline = IconsLoader.LoadSprite(address, true, null);
        var task = IconsLoader.LoadSpriteAsync(address, false, null);
        var awaiter = task.GetAwaiter();
        var asyncSprite = awaiter.IsCompleted ? awaiter.GetResult() : null;
        bool Matches(Sprite? sprite) => entry.Sprite != null && sprite != null && sprite.Pointer == entry.Sprite.Pointer;
        var result = new { custom = true, sync = Matches(sync), outline = Matches(outline), async = Matches(asyncSprite) };
        MelonLogger.Msg($"[CustomNations] Native flag checks {address}: sync={result.sync}, outline={result.outline}, async={result.async}.");
        return result;
    }
}

[HarmonyPatch(typeof(IconsLoader), nameof(IconsLoader.LoadSprite))]
internal static class CustomFlagPatch
{
    static void Prefix(ref string __0) => CustomFlags.Prepare(ref __0);
}

[HarmonyPatch(typeof(IconsLoader), nameof(IconsLoader.LoadSpriteAsync))]
internal static class CustomFlagAsyncPatch
{
    static void Prefix(ref string __0) => CustomFlags.Prepare(ref __0);
}
