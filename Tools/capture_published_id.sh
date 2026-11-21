#!/usr/bin/env bash
set -euo pipefail
# After the first in-game Workshop upload, copy the new item ID back into the
# repo. Never replace an existing ID: that is what prevents duplicate items.
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage_root="${PA_WORKSHOP_STAGING:-$(dirname "$project_root")/PsychicAugmentics-WorkshopUpload}"
source_id="$stage_root/About/PublishedFileId.txt"
target_id="$project_root/About/PublishedFileId.txt"
[[ -f "$source_id" ]] || { echo "No PublishedFileId.txt in $stage_root/About yet - upload first." >&2; exit 1; }
id="$(tr -d '[:space:]' < "$source_id")"
[[ "$id" =~ ^[0-9]+$ ]] || { echo "Invalid Workshop ID: $id" >&2; exit 1; }
if [[ -f "$target_id" && "$(tr -d '[:space:]' < "$target_id")" != "$id" ]]; then
    echo "Refusing to replace existing Workshop ID $(cat "$target_id") with $id" >&2; exit 1
fi
cp "$source_id" "$target_id"
echo "Recorded Workshop item ID $id in About/PublishedFileId.txt"
