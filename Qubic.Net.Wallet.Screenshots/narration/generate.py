#!/usr/bin/env python3
"""Generate voiceover MP3 files for each episode using edge-tts (Microsoft Neural TTS).

Supports [SCENE: name] markers in narration scripts for screenshot synchronization.
Scene markers are stripped before TTS and output as a .scenes timing file."""

import asyncio
import argparse
import os
import re
import sys
from pathlib import Path

VOICE = "en-US-JennyNeural"
RATE = "+0%"       # speech rate adjustment (e.g. "+10%", "-5%")
VOLUME = "+0%"

EPISODES = [
    ("ep1_getting_started",   "Episode 1 — Getting Started"),
    ("ep2_sending_receiving", "Episode 2 — Sending & Receiving"),
    ("ep3_encrypted_vault",   "Episode 3 — Encrypted Vault"),
    ("ep4_assets_qx",         "Episode 4 — Assets & QX Trading"),
    ("ep5_defi",              "Episode 5 — DeFi Suite"),
    ("ep6_governance",        "Episode 6 — Governance & Auctions"),
    ("ep7_history",           "Episode 7 — History & Monitoring"),
    ("ep8_tools_settings",    "Episode 8 — Tools & Settings"),
    ("ep_msvault",            "MSVault Deep Dive"),
]

SCENE_RE = re.compile(r"^\[SCENE:\s*(.+?)\]\s*$", re.MULTILINE)


def parse_scenes(text: str) -> tuple[str, list[tuple[str, int]]]:
    """Extract [SCENE: name] markers and map each to a character offset in the cleaned text.

    Returns (clean_text, [(scene_name, char_offset), ...]).
    """
    scenes = []
    clean_parts = []
    char_offset = 0

    for line in text.splitlines(keepends=True):
        m = re.match(r"^\[SCENE:\s*(.+?)\]\s*$", line)
        if m:
            scenes.append((m.group(1), char_offset))
        else:
            clean_parts.append(line)
            char_offset += len(line)

    return "".join(clean_parts).strip(), scenes


async def generate_episode(script_dir: Path, output_dir: Path, slug: str, title: str,
                           voice: str, rate: str, volume: str):
    """Generate a single episode MP3 from its text file."""
    import edge_tts

    txt_path = script_dir / f"{slug}.txt"
    if not txt_path.exists():
        print(f"  SKIP  {slug} — {txt_path} not found")
        return

    raw_text = txt_path.read_text(encoding="utf-8").strip()
    if not raw_text:
        print(f"  SKIP  {slug} — empty script")
        return

    # Parse scene markers
    text, scenes = parse_scenes(raw_text)
    has_scenes = len(scenes) > 0

    mp3_path = output_dir / f"{slug}.mp3"
    srt_path = output_dir / f"{slug}.srt"
    scenes_path = output_dir / f"{slug}.scenes"

    print(f"  GEN   {title}")
    print(f"        Voice: {voice}  Rate: {rate}")
    if has_scenes:
        print(f"        Scenes: {len(scenes)} markers found")

    communicate = edge_tts.Communicate(text, voice, rate=rate, volume=volume)
    sentences = []
    sub_idx = 1
    with open(str(mp3_path), "wb") as mp3_file:
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                mp3_file.write(chunk["data"])
            elif chunk["type"] == "SentenceBoundary":
                offset_ms = chunk["offset"] / 10_000  # 100-ns units to ms
                duration_ms = chunk["duration"] / 10_000
                sentences.append((sub_idx, offset_ms, offset_ms + duration_ms,
                                  chunk["text"], chunk.get("offset", 0)))
                sub_idx += 1

    # Write SRT subtitles
    with open(srt_path, "w", encoding="utf-8") as f:
        for idx, start, end, sentence, _ in sentences:
            f.write(f"{idx}\n")
            f.write(f"{ms_to_srt(start)} --> {ms_to_srt(end)}\n")
            f.write(f"{sentence}\n\n")

    # Write scene timing file — maps each [SCENE: name] to the timestamp of
    # the first sentence that follows it in the text
    if has_scenes:
        # Build a map: for each scene, find the first sentence whose text
        # appears at or after the scene's character offset in the clean text
        scene_timings = []
        for scene_name, scene_char_pos in scenes:
            # Find text starting at scene_char_pos
            scene_text_after = text[scene_char_pos:].lstrip()
            matched_ms = 0.0
            for _, start_ms, _, sentence, _ in sentences:
                # Match if the sentence text appears at the start of the scene's block
                clean_sentence = sentence.strip()
                if clean_sentence and scene_text_after.startswith(clean_sentence):
                    matched_ms = start_ms
                    break
            scene_timings.append((scene_name, matched_ms))

        with open(scenes_path, "w", encoding="utf-8") as f:
            f.write("# Scene timing — use these timestamps to sync screenshots with narration\n")
            f.write("# Format: TIMESTAMP  SCENE_NAME\n\n")
            for name, ms in scene_timings:
                f.write(f"{ms_to_srt(ms)}  {name}\n")

        print(f"        → {scenes_path.name} ({len(scene_timings)} scene timings)")

    size_kb = mp3_path.stat().st_size / 1024
    print(f"        → {mp3_path.name} ({size_kb:.0f} KB)")
    print(f"        → {srt_path.name} ({len(sentences)} subtitle lines)")


def ms_to_srt(ms: float) -> str:
    """Convert milliseconds to SRT timestamp format HH:MM:SS,mmm."""
    total_s = ms / 1000
    h = int(total_s // 3600)
    m = int((total_s % 3600) // 60)
    s = int(total_s % 60)
    remainder = int(ms % 1000)
    return f"{h:02d}:{m:02d}:{s:02d},{remainder:03d}"


async def main():
    parser = argparse.ArgumentParser(description="Generate voiceover MP3s for wallet tutorial episodes")
    parser.add_argument("--output", "-o", default="output",
                        help="Output directory for MP3 files (default: output)")
    parser.add_argument("--voice", default=VOICE,
                        help=f"TTS voice name (default: {VOICE})")
    parser.add_argument("--rate", default=RATE,
                        help=f"Speech rate adjustment (default: {RATE})")
    parser.add_argument("--volume", default=VOLUME,
                        help=f"Volume adjustment (default: {VOLUME})")
    parser.add_argument("--episode", "-e",
                        help="Episode number(s) to generate, comma-separated (default: all)")
    parser.add_argument("--list-voices", action="store_true",
                        help="List available English voices and exit")
    args = parser.parse_args()

    if args.list_voices:
        import edge_tts
        voices = await edge_tts.list_voices()
        en_voices = [v for v in voices if v["Locale"].startswith("en-")]
        print(f"{'Name':<35} {'Gender':<8} {'Locale':<8} {'Keywords'}")
        print("-" * 90)
        for v in sorted(en_voices, key=lambda x: x["ShortName"]):
            keywords = ", ".join(v.get("VoiceTag", {}).values())
            print(f"{v['ShortName']:<35} {v['Gender']:<8} {v['Locale']:<8} {keywords}")
        return

    script_dir = Path(__file__).parent
    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Select episodes
    if args.episode:
        selected = []
        for part in args.episode.split(","):
            num = int(part.strip())
            if 1 <= num <= len(EPISODES):
                selected.append(EPISODES[num - 1])
            else:
                print(f"Error: episode {num} out of range (1-{len(EPISODES)})", file=sys.stderr)
                sys.exit(1)
    else:
        selected = EPISODES

    print(f"Generating {len(selected)} episode(s) with voice: {args.voice}")
    print(f"Output: {output_dir.resolve()}")
    print()

    for slug, title in selected:
        await generate_episode(script_dir, output_dir, slug, title,
                               args.voice, args.rate, args.volume)
        print()

    print("Done!")


if __name__ == "__main__":
    asyncio.run(main())
