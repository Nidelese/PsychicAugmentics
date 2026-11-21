#!/usr/bin/env bash
set -euo pipefail

# Build a lean Steam upload folder: runtime files only (no Source/, Tools/,
# .git/ or docs). An existing staging folder is moved to a timestamped
# backup beside it, never deleted. Upload from RimWorld with the Mods/ link
# pointed at the staging copy, then run Tools/capture_published_id.sh.

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage_root="${PA_WORKSHOP_STAGING:-$(dirname "$project_root")/PsychicAugmentics-WorkshopUpload}"
case "$stage_root" in
    /|"$project_root"|"$project_root"/*)
        echo "Refusing unsafe staging target: $stage_root" >&2; exit 1 ;;
esac

python3 "$project_root/Tools/validate_release.py"

temp_root="$(mktemp -d "$(dirname "$stage_root")/.PsychicAugmentics-WorkshopUpload.XXXXXX")"
for item in About Common 1.6 1.7 Anomaly Odyssey Languages Textures LoadFolders.xml; do
    [[ -e "$project_root/$item" ]] || { echo "Missing runtime item: $item" >&2; exit 1; }
    cp -a "$project_root/$item" "$temp_root/"
done

# RimWorld writes PublishedFileId.txt into the uploaded folder; keep it
# across rebuilds even before it has been captured into git.
if [[ -f "$stage_root/About/PublishedFileId.txt" && ! -f "$temp_root/About/PublishedFileId.txt" ]]; then
    cp "$stage_root/About/PublishedFileId.txt" "$temp_root/About/"
fi

preview="$temp_root/About/Preview.png"
[[ -f "$preview" ]] || { echo "Missing About/Preview.png" >&2; exit 1; }
(( $(stat -c %s "$preview") < 1000000 )) || { echo "About/Preview.png exceeds Steam's 1 MB limit" >&2; exit 1; }
for version in 1.6 1.7; do
    [[ -s "$temp_root/$version/Assemblies/PsychicAugmentics.dll" ]] \
        || { echo "Missing $version/Assemblies/PsychicAugmentics.dll" >&2; exit 1; }
done

if [[ -e "$stage_root" ]]; then
    backup="$stage_root.previous-$(date +%Y%m%d-%H%M%S)"
    mv "$stage_root" "$backup"
    echo "Previous staging copy preserved at: $backup"
fi
mv "$temp_root" "$stage_root"
echo "Steam upload staging copy ready: $stage_root"
du -sh "$stage_root"
