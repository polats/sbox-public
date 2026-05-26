# Community / cloud assets (sbox.game packages)

sbox.game hosts user-published packages — models, clothing, maps, sounds —
addressed by **ident `<org>.<name>`** (e.g. `lostick.kimono`). They are *not* in
the base install; s&box downloads them on demand, and only once a project
**mounts** the package do its files land on disk.

## Fetch & mount (pull a package to disk)

From a running editor, via `sbox-eval` (the `Package` API is async, so block on it):

```bash
sbox-eval '
var p = Package.FetchAsync("lostick.kimono", false).GetAwaiter().GetResult();
p.MountAsync().GetAwaiter().GetResult();
return p.Title + " | type=" + p.PackageType + " | primary=" + p.GetMeta<string>("PrimaryAsset","?");'
# → Kimono | type=0 | primary=kimono/body/kimono.clothing
```

- `Package.FetchAsync(ident, detailed)` → metadata only (`Title`, `FullIdent`,
  `PackageType`, `GetMeta<T>("PrimaryAsset")`, …). `IRevision` has **no** `.Version`.
- `MountAsync()` is what actually **downloads the files** and makes them loadable.
- `sbox-models --cloud` lists cloud *models* (≈2100) but they won't load until the
  owning package is mounted — that's what the "needs project access" caveat means.

## Where the files land — `<sbox>/download/assets/`

Two layouts in the wild (both seen):
- **Structured** (well-packaged): `<pkg>/<part>/{models,textures}/…`, e.g.
  `download/assets/kimono/body/models/citizen_kimono.<hash>.vmdl_c`.
- **Flat content-addressed**: everything in `download/assets/` as
  `<name>.<hash>.<ext>`, shared across *many* assets in one dir.

Files are **compiled** Source2 (`.vmdl_c`, `.vmat_c`, `.vtex_c`, `.clothing_c`) with
**content-hashed names** (`<stem>.<hash>.vmdl_c`). The source `.clothing` is **not**
shipped — but the **`.clothing_c` DATA block embeds the full source JSON as a
string** (the `Model`/`HumanAltModel`/`Category`/`Slots*` fields). Pull it out with a
regex over the bytes, or `strings <file>.clothing_c | grep HasHumanSkin`.

A mount can be **partial** — models + `.vmat` may arrive before texture `.vtex_c`
(the older flat-cached poncho had no colour map; the structured kimono had all).

## Decompiling for external use (Blender / three.js / a non-s&box tool)

Use **ValveResourceFormat / Source2Viewer-CLI** (see the kimodo skill's
`rules/sbox-integration.md` for the full recipe):
`Source2Viewer-CLI -i <file>_c -o out -d --gltf_export_format glb --gltf_export_animations`.
VRF has **no decompiler for the custom `.clothing` resource** (it only prints the
block summary) — read the embedded JSON instead. `.vtex_c` → PNG works; the `-o`
filename is ignored for textures (it writes its own name into the `-o` directory).

## Using community CLOTHING in the kimodo /kata viewer

The kimodo repo's `web/scripts/clothing_add.py` consumes these directly: point it at
a downloaded `.clothing_c` and it extracts the JSON, resolves the hashed
`<stem>.<hash>.vmdl_c` variants (citizen / human-m / human-f), decompiles + rigs each
onto the citizen, bakes the albedo, and registers it for the CLOTHING drawer.
**Verified end-to-end with `lostick.kimono` + `lostick.kimonotrousers`.**
