# Create a nation for Custom Nations

For BA Custom Nations 0.1.1, Broken Arrow 1.2.0, and MelonLoader 0.7.3. No C# build is needed to author a nation pack.

## What a pack can create

A pack creates a selectable country, two specializations, and separate unit variants based on existing game units. The supported edits are the nation and unit names, nation flag, unit category, purchase cost, and availability. Models, squad composition, weapons, armor, mobility, sounds, and abilities come from each template.

The current implementation accepts templates without loadout modifications. It rejects units with loadout replacement graphs. Importing original models, creating weapons, editing damage or squad size, adding transports or loadouts, and scripting infection are separate features that this version does not implement.

## Quick start: a complete second nation

1. Install Custom Nations and launch the modded game once, then close it.
2. Find `UserData/BACustomNations/packs` under the game installation.
3. Copy this package's [outbreak-colony.json](../examples/outbreak-colony.json) into that folder.
4. Restart the modded game. Open the deck builder and look for **Outbreak Colony**, with **Shamblers** and **Hunters** specializations.
5. Add Colony Walkers and Colony Runners, save the deck, and select that saved deck in Local Skirmish.

The example uses IDs distinct from the shipped zombie pack and reuses its included flag. It is a template for learning, not an additional pack installed automatically. Its two units reuse zombie templates so the example does not depend on unverified loadout support.

The example validates alongside the shipped pack. Other installed packs could still occupy its IDs. Check your own inventory and pack files before publishing or changing IDs.

```json
{
  "schemaVersion": 1,
  "key": "outbreak-colony",
  "name": "Outbreak Colony",
  "countryId": 91,
  "flag": "assets/zombies-flag.png",
  "maxPoints": 640,
  "mapGroundWaves": true,
  "specializations": [
    { "id": 910, "name": "Shamblers" },
    { "id": 911, "name": "Hunters" }
  ],
  "units": [
    {
      "id": 9510, "templateId": 490,
      "specializationId": 910, "availabilityId": 91000,
      "name": "Colony Walkers", "category": "Infantry",
      "cost": 40, "count": 8
    },
    {
      "id": 9511, "templateId": 491,
      "specializationId": 911, "availabilityId": 91001,
      "name": "Colony Runners", "category": "Infantry",
      "cost": 40, "count": 8
    }
  ]
}
```

## Folder layout

```text
Broken Arrow/
  Mods/
    BACustomNations.dll
  UserData/
    BACustomNations/
      packs/
        zombies.json
        outbreak-colony.json
        assets/
          zombies-flag.png
          your-nation-flag.png
      inventory.json
      runtime-checks.json
```

Only JSON files directly inside `packs` are loaded. Asset subfolders are allowed, but JSON subfolders are not scanned. Keep disabled packs outside `packs`, or change their extension from `.json`. Restart after changes; there is no live reload.

## Nation fields

| Field | Required or default | Meaning and limits |
| --- | --- | --- |
| `schemaVersion` | Default `1` | Only version 1 is supported. |
| `key` | Required | Nonempty identifier unique across all packs, ignoring case. Prefer a short lowercase name such as `my-colony`. Used in localization and flag lookup. |
| `name` | Required | Visible nation name. |
| `countryId` | Required | Stable integer from 4 through 32767, unused in the live country table and other packs. ID 3 is reserved for the mission editor. |
| `flag` | Optional | PNG path relative to the `packs` directory. See flags below. |
| `maxPoints` | Default `2000` | Integer from 1 through 100000. The generated roster's sum of `cost * count` must fit this limit. |
| `mapGroundWaves` | Default `false` | With Local Skirmish 2.6.3, allows ground waves to choose across the nation's ground roster. |
| `specializations` | Required | Exactly two specialization objects, each with `id` and `name`. Both must have at least one unit. |
| `units` | Required | At least one unit object. At most eight unit entries per category. |

Specialization IDs must be unique integers from 1 through 32767 and unused in the live specialization table. Specialization names must be nonempty. The addon derives category slot and point limits from the units assigned to each specialization. Specialization icons and illustrations currently use stock placeholder artwork.

## Unit fields

| Field | Required or default | Meaning and limits |
| --- | --- | --- |
| `id` | Required | New stable integer from 1 through 32767. Must not exist in the live unit table or another loaded pack. |
| `templateId` | Required | Existing base-game unit to clone. Refer to its original ID, not another custom variant. |
| `specializationId` | Required | One of this pack's two specialization IDs. |
| `availabilityId` | Required | Positive 32-bit integer unique in the availability table and across packs. Identifies the relationship between the unit and its specialization. |
| `name` | Required | Visible unit name. |
| `category` | Default `Infantry` | One of the exact category names below. |
| `cost` | Default `40` | Integer from 1 through 10000. Replaces the cloned unit's base purchase cost. |
| `count` | Default `8` | Integer from 1 through 100. Sets availability at all four experience levels and the automatic starter-card count. |

The exact category names are `Recon`, `Infantry`, `Vehicles`, `Support`, `Logistic`, `Helicopters`, and `Aircrafts`. `Logistic` is singular. Category names are case-sensitive, even though JSON field names are case-insensitive.

Changing category changes deck placement. It does not change an infantry unit into a vehicle or provide flight, transport, or resupply abilities.

For a new variant, copy one unit object, assign unused `id` and `availabilityId` values, and edit its supported fields. Keep the referenced specialization in the same pack. To create a whole new country, change the pack key, country ID, both specialization IDs, all unit IDs, and all availability IDs. Retain the original template IDs.

## Custom flags

1. Create a PNG with a bold design that remains readable at small sizes. A 3:2 rectangle such as 384 x 256 or 768 x 512 is a useful starting point.
2. Place it in `packs/assets`, for example `packs/assets/colony-flag.png`.
3. Set `"flag": "assets/colony-flag.png"` in your pack.
4. Restart the game. Check the country selector and saved-deck flag.

PNG dimensions must each be between 1 and 2048 pixels, and the file must be no larger than 8 MiB. The game must also be able to decode the PNG. Use actual PNG data, not a JPEG renamed to `.png`. Prefer forward slashes in JSON; a backslash must be written as `\\`.

Paths are relative to `packs`, not to the JSON file's name, the game root, or a web address. Absolute paths, network paths, URLs, `.` and `..` components, and other extensions are rejected. Different nations may deliberately share one flag file.

If the file is missing, malformed, or cannot load, the nation uses stock fallback artwork and logs a warning. Correct the file and restart to retry. A bad flag does not invalidate an otherwise valid nation.

Omitting `flag` uses stock fallback artwork, except that the built-in `zombies` key automatically uses the bundled zombie flag for compatibility with 0.1.0 packs. Existing customized pack JSON and existing flag files are preserved during installation. To change the zombie flag, replace its PNG while the game is closed or point `flag` to another PNG.

The loader caches textures and fills the game's shared sprite cache before synchronous and asynchronous icon requests. Both ordinary and outline requests use the supplied design. It does not modify stock countries' image addresses.

## Finding templates and avoiding ID collisions

Start with the known working zombie template IDs:

| Template ID | Original unit |
| --- | --- |
| 490 | ZOM Walkers |
| 491 | ZOM Runners |
| 492 | ZOM Spitters |
| 493 | ZOM Smokers |
| 494 | ZOM Boomer |
| 495 | ZOM Thrower |

After database loading, `inventory.json` lists existing countries and specializations, including hidden entries, plus the templates requested by your current packs. A template's `modifications` value must be zero in this version. If you use the separate Balance Mod, its `Units_full.json` export can help find other original IDs; its unit-option export can help identify loadouts. The Balance Mod is not required to run a nation pack.

The shipped zombie pack reserves country 90, specializations 900 and 901, units 9490 through 9495, and availabilities 90000 through 90005. The tutorial example uses country 91, specializations 910 and 911, units 9510 and 9511, and availabilities 91000 and 91001.

IDs are checked independently per table. A unit ID and a country ID can have the same number, but two units cannot. There is no central registry or automatic allocation, so authors should publish their ranges and coordinate compatible packs. A game update can also introduce a collision.

Never renumber a released pack casually. Saved decks reference those IDs and can break or resolve to different content. Keep IDs stable when changing names, artwork, cost, or availability. Back up saved decks before roster removals or ID changes.

## Creating and using decks

Create a deck through the game's normal deck builder, choose the new nation and its two specializations, add cards, and save. Select that saved deck for the human player or individual AI slots in Local Skirmish.

The addon also attempts an automatic starter deck, but version 0.1.0's starter failed native validation in the observed game session. Version 0.1.1 does not change that generator. A manually created zombie deck worked and produced custom-unit AI spawn logs. Use the normal deck builder if the automatic starter is absent. Do not treat the starter-validation warning as proof that the nation or your saved deck is broken.

With `mapGroundWaves: true`, a deck containing only units from this pack can replace ground requests from Recon, Infantry, Vehicles, Support, and Logistics with its available ground cards. Air requests still follow Local Skirmish's normal matching rules. Mixed-nation or stock decks retain the normal matching behavior.

The setting does not change player purchases, aircraft into walkers, map orders, timing, income, or bot strategy. Card counts weight random selection; they do not give scripted AI a finite inventory. Selected transport handling remains Local Skirmish's responsibility.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| No new country | Check `MelonLoader/Latest.log` for `Registration failed` or `Custom Nations disabled`; verify valid JSON, IDs, and both specialization memberships. |
| Every pack is missing | One invalid pack prevents the shared registration batch. Temporarily move the recently added JSON out of `packs`. |
| Template has loadouts | Choose a template with no modifications. Setting category or renaming the unit cannot remove that restriction. |
| Flag remains stock | Check `flag`, PNG signature, dimensions, and the warning in the log. Restart after fixing. |
| Flag is blurry | Simplify the design and use an adequate source resolution. The UI displays small sprites, so avoid tiny text and fine lines. |
| Flag edit has no effect | Restart; flags are cached. The installer deliberately does not overwrite existing edited PNGs. |
| Starter deck missing | Create and save a deck in the normal builder. The automatic starter is a known limitation. |
| Saved deck breaks after an update | Check removed or changed IDs, specialization links, and new collisions. Restore the compatible pack and deck backup. |
| Zombies do not infect victims | Infection belongs to the original mission graph and is not a pack feature. |

`runtime-checks.json` reports unit-clone checks, automatic starter validation, and custom-flag results. For a successfully loaded flag, `flag.sync`, `flag.outline`, and `flag.async` are all `true`. A unit/profile success does not prove every attack animation, special ability, or battle behavior works outside the original scenario. Test your new templates on an ordinary skirmish map.

## Sharing your nation

Distribute your JSON and the PNG files you created or have permission to redistribute. Include a short README with the required Custom Nations version, installation paths, your reserved ID ranges, template dependencies, balance choices, and known limitations. Tell users to copy your JSON into `packs` and preserve the referenced asset subfolders.

Do not bundle game DLLs, extracted game models, textures, audio, or the game's database. The addon loads existing game assets from each user's installation. Link to Custom Nations as a dependency rather than distributing a modified core DLL for a data-only pack.

Before release, test a fresh install and an update, nation selection, flag display, deck creation/save/reopen, player deployment, both AI teams, map changes, and coexistence with your intended packs. Offline modded play is the supported environment.
