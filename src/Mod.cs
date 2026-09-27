using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppBrokenArrow.DataBase;
using Il2CppBrokenArrow.DataBase.Models;
using Il2CppBrokenArrow.Shared.Ecs;
using Il2CppBrokenArrow.Client.Ecs.Decks;
using Il2CppBrokenArrow.Client.Ecs.Decks.Models;
using Il2CppBrokenArrow.MissionEditor.MissionResolver;
using Il2CppBrokenArrow.MissionEditor.Inspector;
using Il2CppBrokenArrow.Shared.Ecs.MissionEditor;
using Il2CppBrokenArrow.Shared.Ecs.Localization;
using Il2CppBrokenArrow.Shared.Ecs.Services;
using Il2CppNetworkCommon.Enums;
using Il2CppNetworkCommon.Enums.Common;

[assembly: MelonInfo(typeof(BACustomNations.Core), "BA Custom Nations", "0.1.1", "BovineOverlord")]
[assembly: MelonGame(null, null)]
[assembly: HarmonyDontPatchAll]

namespace BACustomNations;

public sealed class Core : MelonMod
{
    internal static readonly List<NationPack> Packs = new();
    internal static readonly Dictionary<int, NationPack> Owners = new();
    internal static readonly Dictionary<IntPtr, LoadUnits> Applied = new();
    internal static readonly Dictionary<IntPtr, DataBaseSourceData> Sources = new();
    internal static readonly Dictionary<string, string> Labels = new();
    internal static bool Ready;
    private IntPtr checkedLoader;
    private int frames;
    internal static string Root => Path.Combine(MelonEnvironment.UserDataDirectory, "BACustomNations");
    public override void OnInitializeMelon()
    {
        try
        {
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                if (module.ModuleName?.Contains("EasyAntiCheat", StringComparison.OrdinalIgnoreCase) == true)
                    throw new InvalidOperationException("Use the existing offline modded launcher.");
            Directory.CreateDirectory(Path.Combine(Root, "packs"));
            CustomFlags.EnsureBuiltin();
            var files = Directory.GetFiles(Path.Combine(Root, "packs"), "*.json").OrderBy(x => x).ToArray();
            if (files.Length == 0)
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("zombies.json")!;
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();
                File.WriteAllText(Path.Combine(Root, "packs", "zombies.json"), text);
                Packs.Add(NationPack.Read(text));
            }
            else foreach (var file in files) Packs.Add(NationPack.Read(File.ReadAllText(file)));
            HarmonyInstance.CreateClassProcessor(typeof(RegisterNationPatch)).Patch();
            HarmonyInstance.CreateClassProcessor(typeof(StarterDeckPatch)).Patch();
            HarmonyInstance.CreateClassProcessor(typeof(LabelPatch)).Patch();
            HarmonyInstance.CreateClassProcessor(typeof(CustomFlagPatch)).Patch();
            HarmonyInstance.CreateClassProcessor(typeof(CustomFlagAsyncPatch)).Patch();
            LoggerInstance.Msg($"Custom Nations 0.1.1: {Packs.Count} pack(s), explicit patch registration.");
        }
        catch (Exception e) { LoggerInstance.Error("Custom Nations disabled: " + e); }
    }

    public override void OnLateInitializeMelon()
    {
        // The addon never rewrites or replaces the confirmed Local Skirmish DLL.
        var type = AccessTools.TypeByName("BALocalSkirmish.Core");
        var method = type == null ? null : AccessTools.Method(type, "PickSpawnSlot");
        if (method == null) { LoggerInstance.Warning("Local Skirmish ground-wave integration unavailable."); return; }
        try
        {
            HarmonyInstance.Patch(method, prefix: new HarmonyMethod(typeof(GroundWavePatch), nameof(GroundWavePatch.Prefix)));
            LoggerInstance.Msg("Local Skirmish ground-wave integration installed.");
        }
        catch (Exception e) { LoggerInstance.Error("Ground-wave integration failed: " + e); }
    }

    public override void OnUpdate()
    {
        if (!Ready || ++frames % 120 != 0) return;
        var db = DataBaseService._instance;
        if (db == null || !db.IsLoaded || checkedLoader == db.UnitsLoader.Pointer) return;
        DeckService ds = null!;
        if (!Session.TryGetService<DeckService>(out ds, false) || ds == null) return;
        checkedLoader = db.UnitsLoader.Pointer;
        try
        {
            var reports = new List<object>();
            foreach (var p in Packs)
            {
                var deck = StarterDeckPatch.MakeDeck(p);
                bool valid = ds.ValidateDeck(deck.Cast<IDeckDataModel>(), out bool full);
                var clone = deck.Clone(p.Name + " diagnostic clone");
                bool cloneValid = ds.ValidateDeck(clone.Cast<IDeckDataModel>(), out bool cloneFull);
                var roster = new List<object>();
                foreach (var d in p.Units)
                {
                    var fresh = db.GetUnitById(d.Id, true);
                    var stock = db.GetUnitById(d.TemplateId, false);
                    if (fresh == null || fresh.Id != d.Id || fresh.CountryId != p.CountryId ||
                        fresh.Cost != d.Cost || !fresh.SpecializationIds.Contains(d.SpecializationId))
                        throw new InvalidOperationException($"Native clone failed for {d.Id}.");
                    roster.Add(new { fresh.Id, fresh.Name, fresh.CountryId, fresh.Cost,
                        category = fresh.CategoryType.ToString(), squadMembers = fresh.SquadMembers?.Count,
                        weapons = fresh.Turrets?.Count, model = fresh.ModelFileName,
                        originalId = stock.Id, originalCountry = stock.CountryId, originalCategory = stock.CategoryType.ToString() });
                }
                var flag = CustomFlags.CheckNativeLoaders(db.GetCountryById(p.CountryId).FlagFileName);
                reports.Add(new { p.Name, valid, full, cloneValid, cloneFull, roster, flag });
                MelonLogger.Msg($"[CustomNations] Runtime check {p.Name}: native deck valid={valid}, full={full}, cloned deck valid={cloneValid}; all six identity/profile checks passed.");
            }
            File.WriteAllText(Path.Combine(Root, "runtime-checks.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) { MelonLogger.Error("[CustomNations] Runtime checks: " + e); }
    }

    internal static void Register(LoadUnits loader)
    {
        if (Applied.ContainsKey(loader.Pointer)) return;
        Ready = false;
        var src = loader._source;
        if (Sources.ContainsKey(src.Pointer))
        {
            foreach (var p in Packs) foreach (var d in p.Specializations)
                src.Specializations.GetById(d.Id).Availabilities.Clear();
            Applied.Add(loader.Pointer, loader);
            return;
        }
        var countries = src.Countries._rows;
        var specs = src.Specializations._rows;
        var units = src.Units._rows;
        var avail = src.SpecializationAvailabilities._rows;
        var cs = new List<Countries>(); foreach (var c in countries.Values) cs.Add(c);
        var ss = new List<Specializations>(); foreach (var s in specs.Values) ss.Add(s);
        var unitIds = new HashSet<int>(); foreach (var id in units.Keys) unitIds.Add(id);
        var availabilityIds = new HashSet<int>(); foreach (var id in avail.Keys) availabilityIds.Add(id);
        // Inventory includes hidden entries, before allocating any custom identity.
        var inventory = new {
            countries = cs.Select(c => new { c.Id, c.Name, c.UIName, c.Hidden, c.FlagFileName, c.MaxPoints }),
            specializations = ss.Select(s => new { s.Id, s.Name, s.CountryId, s.ShowInHangar, s.Icon, s.Illustration }),
            templates = Packs.SelectMany(p => p.Units).Select(d => d.TemplateId).Distinct().Select(id => {
                var u = loader.GetInitById(id, false);
                return new { id, name = u?.Name, country = u?.CountryId, category = u?.CategoryType.ToString(),
                    modifications = u?.Modifications?.Count, model = u?.ModelFileName };
            })
        };
        File.WriteAllText(Path.Combine(Root, "inventory.json"), JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }));
        DefinitionRules.Validate(Packs, cs.Select(c => c.Id).ToHashSet(), ss.Select(s => s.Id).ToHashSet(),
            unitIds, availabilityIds);
        var countryArt = cs.First(c => !c.Hidden && c.ContentMembership == ContentMembership.Vanilla);
        var specArt = ss.First(s => s.ShowInHangar && s.ContentMembership == ContentMembership.Vanilla);
        // Build everything before publishing. On failure the database remains usable.
        var newCountries = new List<Countries>();
        var newSpecs = new List<Specializations>();
        var newUnits = new List<Units>();
        var newAvail = new List<SpecializationAvailabilities>();
        foreach (var p in Packs)
        {
            var country = new Countries { Id = p.CountryId, Name = p.Name, UIName = Label(p.Key, p.Name),
                Hidden = false, MaxPoints = p.MaxPoints, FlagFileName = CustomFlags.Register(p, countryArt.FlagFileName),
                ContentMembership = ContentMembership.Vanilla };
            newCountries.Add(country);
            foreach (var d in p.Specializations)
            {
                var spec = new Specializations { Id = d.Id, CountryId = p.CountryId, Country = country,
                    Name = d.Name, UIName = Label(p.Key + "." + d.Id, d.Name),
                    UIDescription = Label(p.Key + "." + d.Id + ".description", p.Name + ": " + d.Name),
                    Icon = specArt.Icon, Illustration = specArt.Illustration, ShowInHangar = true,
                    ContentMembership = ContentMembership.Vanilla,
                    Availabilities = new Il2CppSystem.Collections.Generic.List<SpecializationAvailabilities>() };
                SetLimits(spec, p, d.Id);
                newSpecs.Add(spec);
            }
            foreach (var d in p.Units)
            {
                var original = loader.GetInitById(d.TemplateId, false)
                    ?? throw new InvalidDataException($"Missing loaded template {d.TemplateId}.");
                // Loadout replacement graphs need their own identity remapping. Reject them
                // explicitly in this first release instead of leaking stock variants.
                if (original.Modifications != null && original.Modifications.Count > 0)
                    throw new InvalidDataException($"Template {d.TemplateId} has loadouts; version 0.1 supports units without loadout modifications.");
                var u = loader.FullUnitClone(original);
                u.Id = d.Id; u.Name = d.Name; u.OriginalName = d.Name; u.HUDName = d.Name;
                u.CountryId = p.CountryId; u.Country = country;
                u.Cost = d.Cost; u.OriginalCost = d.Cost;
                u.CategoryType = Enum.Parse<UnitCategoryType>(d.Category);
                u.DisplayInArmory = true; u.IsUnitModification = false;
                u.BaseUnit = null; u.ReplaceUnit = null;
                u.SpecializationIds = new Il2CppSystem.Collections.Generic.HashSet<int>();
                // The native linker fills specialization membership on these exact records.
                newUnits.Add(u);
                newAvail.Add(new SpecializationAvailabilities { Id = d.AvailabilityId,
                    UnitId = d.Id, SpecializationId = d.SpecializationId, Name = d.Name,
                    MaxAvailabilityXp0 = d.Count, MaxAvailabilityXp1 = d.Count,
                    MaxAvailabilityXp2 = d.Count, MaxAvailabilityXp3 = d.Count,
                    Transport = new Il2CppSystem.Collections.Generic.List<TransportAvailabilities>() });
            }
        }
        var undo = new Stack<Action>();
        try
        {
            foreach (var c in newCountries) { countries.Add(c.Id, c); undo.Push(() => countries.Remove(c.Id)); }
            foreach (var s in newSpecs) { specs.Add(s.Id, s); undo.Push(() => specs.Remove(s.Id)); }
            foreach (var u in newUnits)
            {
                units.Add(u.Id, u); undo.Push(() => units.Remove(u.Id));
                loader._loadedUnits.Add(u.Id, u); undo.Push(() => loader._loadedUnits.Remove(u.Id));
            }
            foreach (var a in newAvail) { avail.Add(a.Id, a); undo.Push(() => avail.Remove(a.Id)); }
            Applied.Add(loader.Pointer, loader);
            Sources.Add(src.Pointer, src);
            foreach (var p in Packs) foreach (var u in p.Units) Owners[u.Id] = p;
            MelonLogger.Msg($"[CustomNations] Registered {newCountries.Count} nations, {newSpecs.Count} specializations, {newUnits.Count} units before native linking.");
        }
        catch { while (undo.Count > 0) undo.Pop()(); throw; }
    }
    internal static string Label(string key, string value)
    { var id = "bacn." + key; Labels[id] = value; return id; }
    private static void SetLimits(Specializations spec, NationPack p, int specId)
    {
        string[] slots = { "Recon", "Infantry", "Combat", "Support", "Logistics", "Helicopters", "Air" };
        for (int i = 0; i < slots.Length; i++)
        {
            var roster = p.Units.Where(u => u.SpecializationId == specId && u.Category == DefinitionRules.Categories[i]).ToArray();
            typeof(Specializations).GetProperty(slots[i] + "Slots")!.SetValue(spec, roster.Length);
            typeof(Specializations).GetProperty(slots[i] + "Points")!.SetValue(spec, roster.Sum(u => u.Cost * u.Count));
        }
    }
}

[HarmonyPatch(typeof(LoadUnits), nameof(LoadUnits.LinkSpecializations))]
internal static class RegisterNationPatch
{
    static void Prefix(LoadUnits __instance)
    {
        try { Core.Register(__instance); }
        catch (Exception e) { Core.Ready = false; MelonLogger.Error("[CustomNations] Registration failed: " + e); }
    }
    static void Postfix(LoadUnits __instance)
    {
        if (!Core.Applied.ContainsKey(__instance.Pointer)) return;
        try
        {
            foreach (var p in Core.Packs) foreach (var d in p.Units)
            {
                var u = __instance.GetInitById(d.Id, false);
                var a = __instance._source.SpecializationAvailabilities.GetById(d.AvailabilityId);
                if (!u.SpecializationIds.Contains(d.SpecializationId) || a.Unit == null || a.Specialization == null)
                    throw new InvalidOperationException($"Incomplete links for unit {d.Id}.");
            }
            Core.Ready = true;
            MelonLogger.Msg("[CustomNations] Native unit and specialization links verified.");
        }
        catch (Exception e) { Core.Ready = false; MelonLogger.Error("[CustomNations] Link verification failed: " + e); }
    }
}

[HarmonyPatch(typeof(DeckService), nameof(DeckService.GetAvailableDecksForScenario))]
internal static class StarterDeckPatch
{
    static void Postfix(DeckService __instance, ScenarioSource __0, ref Il2CppReferenceArray<IDeckDataModel> __result)
    {
        if (!Core.Ready || __0?.MetaFile?.Type != ScenarioType.Skirmish) return;
        try
        {
            var result = __result == null ? new List<IDeckDataModel>() : __result.ToArray().ToList();
            foreach (var p in Core.Packs)
            {
                var deck = MakeDeck(p);
                bool full = false;
                if (!__instance.ValidateDeck(deck.Cast<IDeckDataModel>(), out full))
                { MelonLogger.Warning($"[CustomNations] Native validation rejected {p.Name} starter deck."); continue; }
                result.Add(deck.Cast<IDeckDataModel>());
            }
            __result = new Il2CppReferenceArray<IDeckDataModel>(result.ToArray());
        }
        catch (Exception e) { MelonLogger.Error("[CustomNations] Starter decks: " + e); }
    }
    internal static DeckDataModel MakeDeck(NationPack p)
    {
        var deck = new DeckDataModel { Name = p.Name + " - Starter", CountryID = p.CountryId,
            Spec1ID = p.Specializations[0].Id, Spec2ID = p.Specializations[1].Id, Version = 6 };
        foreach (var cat in DefinitionRules.Categories)
        {
            var category = Enum.Parse<UnitCategoryType>(cat);
            var cards = new Il2CppReferenceArray<DeckSlotModel>(8);
            var roster = p.Units.Where(u => u.Category == cat).ToArray();
            for (int i = 0; i < 8; i++)
            {
                var card = new DeckSlotModel { SlotIndex = i, Category = category,
                    ModList = new Il2CppSystem.Collections.Generic.List<DeckModData>(),
                    TransportModList = new Il2CppSystem.Collections.Generic.List<DeckModData>() };
                if (i < roster.Length) { card.UnitID = roster[i].Id; card.Count = roster[i].Count; }
                cards[i] = card;
            }
            deck.GetSlots[category] = cards;
        }
        return deck;
    }
}

[HarmonyPatch(typeof(LocalizationService), nameof(LocalizationService.GetLocalizedStringPart))]
internal static class LabelPatch
{
    static bool Prefix(string __0, ref string __result)
    {
        if (__0 == null || !Core.Labels.TryGetValue(__0, out var label)) return true;
        __result = label; return false;
    }
}

internal static class GroundWavePatch
{
    private static readonly Random Random = new();
    public static bool Prefix(UnitModData __0, List<DeckSlotModel> __1, ref DeckSlotModel? __result)
    {
        if (!Core.Ready || __0 == null || __1 == null || __1.Count == 0) return true;
        if (!Core.Owners.TryGetValue(__1[0].UnitID, out var pack)) return true;
        bool all = __1.All(s => s != null && Core.Owners.TryGetValue(s.UnitID, out var owner) && owner == pack);
        if (!DefinitionRules.UseGroundRoster(__0.Category.ToString(), pack.MapGroundWaves, all)) return true;
        var valid = __1.Where(s => s.Count > 0 && (int)s.Category >= 0 && (int)s.Category <= 4).ToArray();
        int sum = valid.Sum(s => s.Count);
        __result = null;
        if (sum == 0) return false;
        int choice = Random.Next(sum);
        foreach (var s in valid) { choice -= s.Count; if (choice < 0) { __result = s; break; } }
        return false;
    }
}
