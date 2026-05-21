# Running s&box on Ubuntu (via Steam + Proton)

s&box has no native Linux build. The official engine runs on Windows; under
Linux you launch it through **Steam Proton**. Out of the box, the editor fails
with a "**.NET Desktop Runtime is missing**" dialog because Steam's bundled
.NET installer script doesn't execute correctly under Proton.

This document records the steps that got the editor running on Ubuntu (tested
on Ubuntu Questing / 25.10). Based on the workaround from
[Facepunch/sbox-public#10781](https://github.com/Facepunch/sbox-public/issues/10781).

## Prerequisites

- Steam installed.
- s&box installed via Steam. Note there are **two** Steam apps:
  - **`590830`** — s&box (the game/runtime).
  - **`2129370`** — s&box editor. **This is the one you launch to edit/develop**,
    and the one that needs .NET installed into its Proton prefix.
- Launched the editor from Steam at least once so Proton creates its prefix
  (`~/.local/share/Steam/steamapps/compatdata/2129370/`).

## Steps

### 1. Force Proton Experimental for the s&box editor

In Steam: right-click **s&box editor** → **Properties** → **Compatibility** →
tick **Force the use of a specific Steam Play compatibility tool** →
select **Proton Experimental**.

Without this, `protontricks` can't auto-detect which Proton tool the app uses
and exits with `Could not find configured Proton installation!`.

### 2. Install `protontricks`

```bash
sudo apt install protontricks
```

If apt complains about an unmet `policykit-1` dependency from `qpm3` (Qualcomm
Package Manager), remove it first:

```bash
sudo apt remove qpm3
sudo apt install protontricks
```

Verify protontricks sees the editor:

```bash
protontricks --list | grep -iE 'sbox|2129370'
# -> s&box editor (2129370)
# -> s&box (590830)
```

### 3. Download the Windows .NET 10 Desktop Runtime

Get the **Windows x64** `.exe` installer (not the Linux package) from
<https://dotnet.microsoft.com/en-us/download/dotnet/10.0>:

```bash
curl -fL -o ~/Downloads/windowsdesktop-runtime-10-win-x64.exe \
  https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe
```

### 4. Install .NET into the s&box editor Proton prefix

The editor is appid **2129370** (separate from the game's `590830`):

```bash
protontricks-launch --appid 2129370 \
  ~/Downloads/windowsdesktop-runtime-10-win-x64.exe \
  /install /quiet /norestart
```

The installer runs through Wine inside the prefix. It produces a lot of
`fixme:` chatter from Wine — that's normal.

### 5. Verify the install

```bash
protontricks-launch --appid 2129370 \
  ~/.local/share/Steam/steamapps/compatdata/2129370/pfx/drive_c/Program\ Files/dotnet/dotnet.exe \
  --list-runtimes
```

Expected output (versions may differ):

```
Microsoft.NETCore.App 10.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
Microsoft.NETCore.App 10.0.8 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
Microsoft.WindowsDesktop.App 10.0.8 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
```

### 6. Launch from Steam

Hit **Play** on **s&box editor** in Steam. The missing-runtime dialog should
be gone and the editor should open.

> Common gotcha: if you installed .NET into the `590830` prefix (the game),
> launching the editor will *still* show the missing-runtime dialog because
> the editor uses a different prefix (`2129370`). Make sure step 4 used
> `--appid 2129370`.

## Notes

- Re-run step 4 if Steam ever recreates the prefix (e.g. after "Delete local
  content" + reinstall, or switching Proton versions).
- The editor under Proton has rough edges per the upstream issue thread; some
  projects may need an extra "Linux fix library" added. Not required for the
  editor to launch.
- This does **not** build the engine from source — Bootstrap.bat is still
  Windows-only. For source-build attempts see
  [Saladin1812/sbox-public-linux](https://github.com/Saladin1812/sbox-public-linux).
