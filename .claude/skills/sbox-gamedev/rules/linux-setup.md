# Linux Setup (Ubuntu + Steam Proton)

s&box has no native Linux build. The editor runs under **Proton** with several
manual fixes. If `sbox-doctor` fails, walk through these steps.

## Two Steam apps, two prefixes

| App ID | Name | Prefix |
|---|---|---|
| `590830` | s&box (game) | `~/.local/share/Steam/steamapps/compatdata/590830/` |
| `2129370` | **s&box editor** | `~/.local/share/Steam/steamapps/compatdata/2129370/` |

**Always target `2129370` for editor work.** The game appid is unrelated.

## Required state in the editor prefix

1. **Proton Experimental forced** for app 2129370 (Steam → Properties →
   Compatibility → "Force the use of a specific Steam Play compatibility tool").
   Without this, `protontricks-launch` can't find the active Proton.
2. **.NET 10 Desktop Runtime installed inside the prefix** at
   `pfx/drive_c/Program Files/dotnet/`. Editor binaries are Windows-native and
   need the Windows .NET runtime, not the Linux one.
3. **Material Icons font** at `pfx/drive_c/windows/Fonts/MaterialIcons-Regular.ttf`
   and registered in `pfx/system.reg` under
   `[Software\\Microsoft\\Windows NT\\CurrentVersion\\Fonts]`.

## Recovery commands

When `sbox-doctor` reports the .NET runtime missing:

```bash
# Editor must be CLOSED before installing into the prefix
protontricks-launch --appid 2129370 \
  ~/Downloads/windowsdesktop-runtime-10-win-x64.exe \
  /install /quiet /norestart
```

Download the .exe (not the Linux deb) from
<https://dotnet.microsoft.com/en-us/download/dotnet/10.0>.

When Material Icons is missing:

```bash
curl -fL -o /tmp/MaterialIcons-Regular.ttf \
  "https://github.com/google/material-design-icons/raw/master/font/MaterialIcons-Regular.ttf"
cp /tmp/MaterialIcons-Regular.ttf \
  ~/.local/share/Steam/steamapps/compatdata/2129370/pfx/drive_c/windows/Fonts/

# Add registry entry (editor must be closed; Wine rewrites system.reg on shutdown)
python3 -c "
from pathlib import Path
p = Path('$HOME/.local/share/Steam/steamapps/compatdata/2129370/pfx/system.reg')
c = p.read_text(encoding='utf-8')
hdr = '[Software\\\\\\\\Microsoft\\\\\\\\Windows NT\\\\\\\\CurrentVersion\\\\\\\\Fonts]'
i = c.find(hdr); j = c.find('\n\n', i)
entry = '\"Material Icons (TrueType)\"=\"MaterialIcons-Regular.ttf\"'
if entry not in c[i:j]: c = c[:j] + '\n' + entry + c[j:]
p.write_text(c, encoding='utf-8')
print('Material Icons registered')
"
```

## Apt gotcha: `qpm3` blocks protontricks/flatpak

If `apt install protontricks` (or `apt install flatpak`) fails complaining
about `policykit-1`, you have Qualcomm Package Manager 3 installed with an
obsolete dependency. Remove it:

```bash
sudo apt remove qpm3
sudo apt install protontricks
```

## Case-sensitivity scan for projects

Some upstream sample projects (`Facepunch/sbox-bombroyale`) ship both
`Code/` and `code/` as separate Git tree entries — invisible on Windows, fatal
on Linux. To detect:

```bash
find <project-dir> -maxdepth 2 -type d | awk -F/ '{print tolower($0), $0}' \
  | sort | awk '{ if ($1 == prev1 && $2 != prev2) print "CASE CONFLICT:", prev2, "<->", $2; prev1=$1; prev2=$2 }'
```

Merge with `git mv code/* Code/`. Verify no filename collisions inside any
subdir first.

## What does NOT work on Linux

- Source 2's asset compiler runs but a subset of paths fail — most visibly the
  on-demand SVG→texture cache (`textures/generated/imagefile/<hash>.vtex_c`)
  and model thumbnail generation (`thumb:` URLs in the spawn menu). This is
  the missing-`libassetsystem.so` issue ([upstream #10368](https://github.com/Facepunch/sbox-public/issues/10368)).
  Not patchable from outside — affects scene thumbnails in the spawn menu and
  some editor icons.
- Emoji codepoints in `[Icon("…")]` attributes — Wine's DirectWrite COLR/font
  fallback is broken. Use Material Icons names instead; see
  [ui-icons-linux.md](ui-icons-linux.md).
