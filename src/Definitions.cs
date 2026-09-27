using System.Text.Json;

namespace BACustomNations;

public sealed class NationPack
{
    public int SchemaVersion { get; set; } = 1;
    public string Key { get; set; } = "";
    public int CountryId { get; set; }
    public string Name { get; set; } = "";
    public string? Flag { get; set; }
    public int MaxPoints { get; set; } = 2000;
    public bool MapGroundWaves { get; set; }
    public List<SpecializationDef> Specializations { get; set; } = new();
    public List<UnitDef> Units { get; set; } = new();
    public static NationPack Read(string text) => JsonSerializer.Deserialize<NationPack>(text,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Empty nation pack.");
}
public sealed class SpecializationDef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class UnitDef
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int SpecializationId { get; set; }
    public int AvailabilityId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Infantry";
    public int Cost { get; set; } = 40;
    public int Count { get; set; } = 8;
}
public static class DefinitionRules
{
    public static readonly string[] Categories = { "Recon", "Infantry", "Vehicles", "Support", "Logistic", "Helicopters", "Aircrafts" };
    public static void Validate(IReadOnlyList<NationPack> packs, HashSet<int> countries,
        HashSet<int> specializations, HashSet<int> units, HashSet<int> availabilities)
    {
        // Work on copies: validation cannot reserve half a pack on failure.
        countries = new(countries); specializations = new(specializations);
        units = new(units); availabilities = new(availabilities);
        var templates = new HashSet<int>(units);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in packs)
        {
            Require(p.SchemaVersion == 1, "Unsupported schema version.");
            Require(!string.IsNullOrWhiteSpace(p.Key) && keys.Add(p.Key), "Pack keys must be unique.");
            Require(!string.IsNullOrWhiteSpace(p.Name), "Nation name is required.");
            Require(p.CountryId > 3 && p.CountryId <= 32767 && countries.Add(p.CountryId), $"Country ID {p.CountryId} is reserved or occupied.");
            Require(p.MaxPoints > 0 && p.MaxPoints <= 100000, "Invalid nation point limit.");
            Require(p.Specializations != null && p.Specializations.Count == 2, "Each initial pack requires two specializations.");
            var ownSpecs = new HashSet<int>();
            foreach (var s in p.Specializations!)
            {
                Require(s.Id > 0 && s.Id <= 32767 && specializations.Add(s.Id), $"Specialization ID {s.Id} is occupied or invalid.");
                Require(!string.IsNullOrWhiteSpace(s.Name), "Specialization name is required.");
                ownSpecs.Add(s.Id);
            }
            Require(p.Units != null && p.Units.Count > 0, "A roster is required.");
            foreach (var u in p.Units!)
            {
                Require(u.Id > 0 && u.Id <= 32767 && units.Add(u.Id), $"Unit ID {u.Id} is occupied or invalid.");
                Require(templates.Contains(u.TemplateId), $"Template unit {u.TemplateId} does not exist in the base database.");
                Require(u.AvailabilityId > 0 && availabilities.Add(u.AvailabilityId), $"Availability ID {u.AvailabilityId} is occupied or invalid.");
                Require(ownSpecs.Contains(u.SpecializationId), "Unit specialization belongs to another nation.");
                Require(!string.IsNullOrWhiteSpace(u.Name) && Categories.Contains(u.Category), "Invalid unit name or category.");
                Require(u.Cost > 0 && u.Cost <= 10000 && u.Count > 0 && u.Count <= 100, "Invalid cost or availability.");
            }
            Require(p.Units!.GroupBy(u => u.Category).All(g => g.Count() <= 8), "A category supports at most eight starter cards.");
            Require(p.Units.Sum(u => (long)u.Cost * u.Count) <= p.MaxPoints, "Starter deck exceeds the nation point limit.");
            Require(ownSpecs.All(id => p.Units.Any(u => u.SpecializationId == id)), "Both specializations need units.");
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }
    public static bool UseGroundRoster(string category, bool enabled, bool allCardsBelongToNation)
        => enabled && allCardsBelongToNation && Categories.Take(5).Contains(category);
}
