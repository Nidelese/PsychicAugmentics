#!/usr/bin/env python3
"""Repeatable on-paper release validation for this RimWorld mod."""

import argparse
import re
import struct
import subprocess
import sys
from pathlib import Path
from xml.etree import ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
PROJECTS = list((ROOT / "Source").glob("*/*.csproj"))
PREFIX = "DS_" if ROOT.name == "Dragonship" else "PA_"
NAMESPACE = "Dragonship" if ROOT.name == "Dragonship" else "PsychicAugmentics"


def xml_files():
    paths = [ROOT / "About" / "About.xml", ROOT / "LoadFolders.xml"]
    for folder in ("1.6", "Anomaly", "Odyssey"):
        base = ROOT / folder
        if base.exists():
            paths.extend(base.rglob("*.xml"))
    return sorted(set(paths))


def texture_exists(path):
    base = ROOT / "Textures" / path
    return base.with_suffix(".png").is_file() or any(
        base.parent.glob(base.name + "_*.png")
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--no-build", action="store_true")
    args = parser.parse_args()
    errors = []

    if len(PROJECTS) != 1:
        errors.append(f"expected one csproj, found {len(PROJECTS)}")
    elif not args.no_build:
        result = subprocess.run(
            ["dotnet", "build", str(PROJECTS[0]), "-c", "Release", "--no-restore"],
            cwd=ROOT,
        )
        if result.returncode:
            errors.append("Release build failed")

    trees = {}
    def_names = {}
    for path in xml_files():
        if not path.is_file():
            errors.append(f"missing required XML: {path.relative_to(ROOT)}")
            continue
        try:
            tree = ET.parse(path)
        except ET.ParseError as exc:
            errors.append(f"{path.relative_to(ROOT)}: XML parse error: {exc}")
            continue
        trees[path] = tree
        if path.parts[-2:] in [("About", "About.xml")]:
            continue
        for node in tree.getroot():
            name = node.findtext("defName")
            if not name:
                continue
            key = (node.tag, name.strip())
            if key in def_names:
                errors.append(
                    f"duplicate {node.tag} {key[1]} in "
                    f"{def_names[key].relative_to(ROOT)} and {path.relative_to(ROOT)}"
                )
            else:
                def_names[key] = path

    source_text = "\n".join(
        path.read_text(encoding="utf-8") for path in (ROOT / "Source").rglob("*.cs")
    )
    declared_types = set(
        re.findall(r"\b(?:class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", source_text)
    )
    custom_refs = set()
    texture_refs = set()
    for tree in trees.values():
        for node in tree.iter():
            cls = node.attrib.get("Class")
            if cls and cls.startswith(NAMESPACE + "."):
                custom_refs.add(cls)
            if node.tag in {
                "thingClass", "workerClass", "hediffClass", "driverClass",
                "giverClass", "verbClass", "compClass",
            } and node.text and node.text.strip().startswith(NAMESPACE + "."):
                custom_refs.add(node.text.strip())
            if node.tag in {"texPath", "texturePath", "iconPath", "uiIconPath"} and node.text:
                texture_refs.add(node.text.strip())

    for reference in sorted(custom_refs):
        if reference.rsplit(".", 1)[-1] not in declared_types:
            errors.append(f"XML references missing source type {reference}")

    for reference in sorted(texture_refs):
        is_owned = (
            reference.startswith("PA/")
            or "DS_" in reference
            or reference.startswith("UI_Dragonship/")
        )
        if is_owned and not texture_exists(reference):
            errors.append(f"missing owned texture for {reference}")

    for path in (ROOT / "Textures").rglob("*.png"):
        try:
            with path.open("rb") as fh:
                header = fh.read(24)
            if header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
                raise ValueError("invalid PNG signature/IHDR")
            width, height = struct.unpack(">II", header[16:24])
            if width < 1 or height < 1:
                raise ValueError(f"invalid dimensions {width}x{height}")
        except (OSError, ValueError, struct.error) as exc:
            errors.append(f"{path.relative_to(ROOT)}: {exc}")

    load_folders = ROOT / "LoadFolders.xml"
    if load_folders in trees:
        for node in trees[load_folders].getroot().findall("./v1.6/li"):
            folder = (node.text or "").strip()
            if folder and folder != "/" and not (ROOT / folder).is_dir():
                errors.append(f"LoadFolders.xml references missing folder {folder}")

    keyed = {}
    language = ROOT / "Languages" / "English" / "Keyed"
    for path in language.glob("*.xml"):
        try:
            for node in ET.parse(path).getroot():
                if node.tag in keyed:
                    errors.append(
                        f"duplicate keyed translation {node.tag} in "
                        f"{keyed[node.tag].relative_to(ROOT)} and {path.relative_to(ROOT)}"
                    )
                keyed[node.tag] = path
        except ET.ParseError:
            pass
    used_keys = set(
        re.findall(r'"(' + re.escape(PREFIX) + r'[A-Za-z0-9_]+)"\.Translate\s*\(', source_text)
    )
    for key in sorted(used_keys - set(keyed)):
        errors.append(f"missing English keyed translation {key}")

    about = ROOT / "About" / "About.xml"
    if about in trees:
        metadata = trees[about].getroot()
        if metadata.findtext("packageId") is None:
            errors.append("About.xml has no packageId")
        versions = [n.text for n in metadata.findall("./supportedVersions/li")]
        if "1.6" not in versions:
            errors.append("About.xml does not declare RimWorld 1.6")
        dependency_ids = {
            (node.text or "").strip().lower()
            for node in metadata.findall("./modDependencies/li/packageId")
        }
        expected = (
            {"ludeon.rimworld.odyssey", "brrainz.harmony",
             "oskarpotocki.vanillafactionsexpanded.core", "ancot.ancotlibrary"}
            if ROOT.name == "Dragonship"
            else {"vanillaexpanded.vpsycastse"}
        )
        for missing in sorted(expected - dependency_ids):
            errors.append(f"About.xml is missing hard dependency {missing}")

    assemblies = list((ROOT / "1.6" / "Assemblies").glob("*.dll"))
    if len(assemblies) != 1 or assemblies[0].stat().st_size == 0:
        errors.append(f"expected one non-empty release DLL, found {len(assemblies)}")

    if errors:
        print(f"FAIL: {len(errors)} release validation error(s)")
        for error in errors:
            print(f"  - {error}")
        return 1
    print(
        f"PASS: {ROOT.name}: Release build, {len(trees)} XML files, "
        f"{len(def_names)} defs, {len(texture_refs)} texture references, "
        f"{len(list((ROOT / 'Textures').rglob('*.png')))} PNGs"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
