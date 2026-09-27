using BACustomNations;
using System.Text.Json;

var json = File.ReadAllText(args[0]);
var checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
NationPack Fresh() => NationPack.Read(json);
void Validate(List<NationPack> packs, HashSet<int>? countries = null) => DefinitionRules.Validate(packs,
    countries ?? new() { 1, 2, 3 }, new() { 1, 2 }, new() { 490, 491, 492, 493, 494, 495 }, new() { 1 });
void Reject(Action<NationPack> mutate, string label)
{
    var p = Fresh(); mutate(p); bool rejected = false;
    try { Validate(new() { p }); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, label);
}
Validate(new() { Fresh() }); Check(true, "Shipped zombie pack is valid");
Reject(p => p.CountryId = 3, "Mission-editor country ID rejected");
Reject(p => p.Units[0].Id = 490, "Stock unit collision rejected");
Reject(p => p.Units[1].Id = p.Units[0].Id, "Duplicate custom unit rejected");
Reject(p => p.Units[0].TemplateId = 99999, "Missing template rejected");
Reject(p => p.Units[0].SpecializationId = 1, "Foreign specialization rejected");
Reject(p => p.Units[1].AvailabilityId = p.Units[0].AvailabilityId, "Availability collision rejected");
Reject(p => p.Units[0].Category = "FlyingZombies", "Unknown category rejected");
Reject(p => p.Units[0].Count = 0, "Empty card rejected");
Reject(p => p.MaxPoints = 100, "Over-budget starter rejected");
Reject(p => p.SchemaVersion = 2, "Unsupported schema rejected");
var ids = new HashSet<int> { 1, 2, 3, 90 };
try { Validate(new() { Fresh() }, ids); throw new Exception("Hidden collision accepted"); }
catch (InvalidDataException) { Check(ids.SetEquals(new[] { 1, 2, 3, 90 }), "Hidden-country collision rejected without mutating inventory"); }
var p2 = Fresh(); p2.Key = "another"; p2.CountryId = 91;
try { Validate(new() { Fresh(), p2 }); throw new Exception("Cross-pack collision accepted"); }
catch (InvalidDataException) { Check(true, "Cross-pack specialization collision rejected"); }
foreach (var cat in new[] { "Recon", "Infantry", "Vehicles", "Support", "Logistic" })
    Check(DefinitionRules.UseGroundRoster(cat, true, true), cat + " wave maps to ground roster");
foreach (var cat in new[] { "Helicopters", "Aircrafts", "None" })
    Check(!DefinitionRules.UseGroundRoster(cat, true, true), cat + " wave excluded from ground mapping");
Check(!DefinitionRules.UseGroundRoster("Vehicles", true, false), "Stock and mixed rosters unchanged");
Check(!DefinitionRules.UseGroundRoster("Vehicles", false, true), "Pack can disable ground mapping");
Console.WriteLine($"{checks} checks passed.");

var packsRoot = Path.Combine(Path.GetTempPath(), "bacn-flag-tests", "packs");
Check(FlagFiles.Resolve(packsRoot, "assets/my-flag.png") == Path.Combine(packsRoot, "assets", "my-flag.png"), "Flag path resolves inside packs");
foreach (var bad in new[] { "../flag.png", "assets/../../flag.png", "C:\\flag.png", "/flag.png", "\\\\server\\share\\flag.png", "assets/flag.jpg", "https://site/flag.png", "assets//flag.png" })
{
    bool rejected = false;
    try { FlagFiles.Resolve(packsRoot, bad); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Unsafe or unsupported flag path rejected: " + bad);
}
Check(NationPack.Read(json).Flag == "assets/zombies-flag.png", "Shipped flag property deserializes");
var oldPack = JsonSerializer.Serialize(Fresh()).Replace("\"Flag\":\"assets/zombies-flag.png\",", "");
Check(NationPack.Read(oldPack).Flag == null, "Old packs remain readable without a flag property");
var flagBytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(args[0])!, "assets", "zombies-flag.png"));
var dimensions = FlagFiles.InspectPng(flagBytes);
Check(dimensions.Width > 0 && dimensions.Height > 0, "Bundled PNG dimensions and file size accepted");
foreach (var badBytes in new[] { Array.Empty<byte>(), new byte[40], flagBytes.Take(20).ToArray() })
{
    bool rejected = false;
    try { FlagFiles.InspectPng(badBytes); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Invalid or truncated PNG rejected");
}
var huge = (byte[])flagBytes.Clone();
System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(huge.AsSpan(16, 4), 4096);
try { FlagFiles.InspectPng(huge); throw new Exception("Oversized PNG accepted"); }
catch (InvalidDataException) { Check(true, "Oversized texture rejected before Unity allocation"); }
var examplePath = Path.Combine(Path.GetDirectoryName(args[0])!, "..", "examples", "outbreak-colony.json");
Validate(new() { Fresh(), NationPack.Read(File.ReadAllText(examplePath)) });
Check(true, "Documented example coexists with the zombie pack");
Console.WriteLine($"{checks} total checks passed.");
