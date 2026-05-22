# Character customization (clothing, bodygroups)

Learnings from `examples/character-customizer/` (citizen on a turntable
cycling 5 outfit presets, slot-based clothing pickers, bodygroup
toggles for head/chest). Read this when authoring clothing variants,
character creators, NPC visual variety, or any avatar-driven content.

## The pipeline

```
.clothing GameResource (model + slot + tint)  --+
                                                |
                                                v
 ClothingContainer  ──── .Add() / .Toggle() / .Remove()
                         .Apply( smr )         |
                                                v
                              SkinnedModelRenderer's bones get the clothing model spawned as a child
```

## `.clothing` file format

The `Clothing` resource type exists in engine. Author by hand under
`Assets/clothing/<name>.clothing`:

```json
{
  "HasHumanSkin": false,
  "Title": "Cowboy Hat",
  "Subtitle": "Yeehaw",
  "Category": "HatCostume",
  "ConditionalModels": {},
  "Tags": "hat",
  "Model": "models/citizen_clothes/hat/cowboy_hat/models/cowboy_hat.vmdl",
  "HumanAltModel": "models/citizen_clothes/hat/cowboy_hat/models/cowboy_hat_m_human.vmdl",
  "HumanAltFemaleModel": null,
  "SkinMaterial": null,
  "EyesMaterial": null,
  "MaterialGroup": null,
  "SlotsUnder": "HeadTop",
  "SlotsOver": 0,
  "HideBody": 0,
  "AllowTintSelect": false,
  "TintDefault": 0.5,
  "__references": [],
  "__version": 0
}
```

Field map:
- **Title / Subtitle** — display name + UI hint
- **Category** — enum-ish string like `HatCostume`, `GlassesCostume`,
  `ChestCostume`, `LegsCostume`, `FeetCostume`, `WristCostume`. Determines
  inventory slot.
- **Model** — the clothing prop's `.vmdl` path. Always probe with
  `sbox-model-info` to confirm it loads.
- **HumanAltModel** — alt model for the standard citizen skeleton (often
  named `_m_human.vmdl`). The engine picks between Model and
  HumanAltModel based on what skeleton the wearer has.
- **HumanAltFemaleModel** — alt for a female-skeleton variant.
- **SlotsUnder / SlotsOver** — bitfield-style slot flags. `"HeadTop"` for
  hats; `0` if not applicable.
- **HideBody** — what bodygroup bits to hide when this item is worn (e.g.
  a full-helmet might set head bits).
- **TintDefault / AllowTintSelect** — runtime recoloring.
- **__references / __version** — standard GameResource footer.

The engine ships *many* `models/citizen_clothes/*` for clothing model
paths. `sbox-models clothing` lists them.

## `ClothingContainer` runtime API

The verified pattern (from `examples/character-customizer/Code/CustomizerManager.cs`
and `sbox-bombroyale/Code/PlayerDresser.cs`):

```csharp
[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
public ClothingContainer Container { get; private set; } = new();

void ApplyOutfit( IEnumerable<Clothing> items )
{
    Container = new ClothingContainer();
    foreach ( var c in items )
        Container.Add( c );           // or .Toggle(c) to add-or-remove
    Container.Apply( BodyRenderer );  // spawns the clothing models as children of the SMR
}
```

Other useful members (discover via `sbox-eval` on the type):
- `Container.Toggle( clothing )` — add if absent, remove if present
- `Container.Remove( clothing )`
- `Container.Randomize()` — picks a random combination from a registry
- `Container.Clear()`
- `Container.Serialize()` / `Container.Deserialize(string)` — JSON
  roundtrip. **`Connection.GetUserData("avatar")` returns the JSON the
  user picked in the s&box menu** — that's how multiplayer games show
  each player's chosen outfit (see `sbox-bombroyale/Code/PlayerDresser.cs`).

## Bodygroups

Citizen + most character models have named bodygroups for showing/hiding
body parts. Discover available names:

```bash
sbox-eval '
  var smr = Game.ActiveScene.GetAllComponents<SkinnedModelRenderer>().First();
  return new {
    count = smr.Model.BodyGroupCount,
    names = Enumerable.Range(0, smr.Model.BodyGroupCount)
                .Select(i => smr.Model.GetBodyGroupName(i)).ToList(),
  };
'
```

Standard citizen names include: `head`, `chest`, `legs`, `hands`, `feet`,
`hair`. Set:

```csharp
smr.SetBodyGroup( "head", 1 );   // 0 = visible, 1+ = hidden / variants
```

Use case: first-person view hides `head` so the camera doesn't see the
inside of your own head model.

## Morphs

Citizens may ship facial morphs (smile, eye_wide, etc.). Discover:

```bash
sbox-eval '
  var smr = Game.ActiveScene.GetAllComponents<SkinnedModelRenderer>().First();
  return smr.Morphs?.Names?.ToList() ?? new();
'
```

If non-empty, drive at runtime:

```csharp
smr.Morphs.Set( "smile", 0.7f );    // 0..1
```

Many engine citizens ship without morphs in the public asset depot — if
the list is empty, the model just doesn't have morph targets. Don't
expect to find them on the default `citizen_human_male.vmdl`.

## Multiplayer avatar from Steam

The s&box menu lets users dress their avatar; the JSON saves to
`Connection.UserData["avatar"]`. To apply on spawn:

```csharp
public sealed class PlayerDresser : Component, Component.INetworkSpawn
{
    [Property] public SkinnedModelRenderer BodyRenderer { get; set; }
    public void OnNetworkSpawn( Connection owner )
    {
        var clothing = new ClothingContainer();
        clothing.Deserialize( owner.GetUserData( "avatar" ) );
        clothing.Apply( BodyRenderer );
    }
}
```

This is what makes every player look like their Steam-menu avatar in
multiplayer.

## Gotchas

- **`Clothing.Apply()` re-creates child GameObjects** for the clothing
  models on every call. Don't call it every frame — only on change.
- **Slot conflicts**: two `Clothing` items in the same `Category` will
  fight unless you Remove the old one first. `.Add` doesn't auto-evict.
- **`SetBodyGroup(string name, int)` uses name lookup at runtime** —
  if the name is wrong (typo / model doesn't have it), the call is a
  no-op with no error. Verify names exist via `sbox-eval` first.
- **HumanAltModel matters** — if you forget to set it on a `.clothing`,
  the clothing model may not attach correctly to the standard citizen
  skeleton, leaving you with a floating hat or off-center jacket.
