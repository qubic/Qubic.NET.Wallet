#!/usr/bin/env python3
"""Merge narration audio with screenshots into a final video.

Reads the .scenes timing file to know when each screenshot should appear,
matches scene names to screenshot files, and uses ffmpeg to produce a
synced video with narration audio and optional subtitles.

Requirements: ffmpeg (must be on PATH)

Usage:
    python3 merge.py --episode 9
    python3 merge.py --episode 9 --subtitles
    python3 merge.py --audio output/ep_msvault.mp3 --screenshots ../output/09_msvault --scenes output/ep_msvault.scenes
"""

import argparse
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

# Maps episode numbers to (narration slug, screenshot dir slug)
EPISODE_MAP = {
    1: ("ep1_getting_started",   "01_getting_started"),
    2: ("ep2_sending_receiving", "02_sending_receiving"),
    3: ("ep3_encrypted_vault",   "03_encrypted_vault"),
    4: ("ep4_assets_qx",         "04_assets_qx"),
    5: ("ep5_defi",              "05_defi"),
    6: ("ep6_governance",        "06_governance"),
    7: ("ep7_history",           "07_history"),
    8: ("ep8_tools_settings",    "08_tools_settings"),
    9: ("ep_msvault",            "09_msvault"),
}


def srt_to_seconds(ts: str) -> float:
    """Parse SRT timestamp 'HH:MM:SS,mmm' to seconds."""
    ts = ts.strip().replace(",", ".")
    parts = ts.split(":")
    return int(parts[0]) * 3600 + int(parts[1]) * 60 + float(parts[2])


def parse_scenes_file(scenes_path: Path) -> list[tuple[str, float]]:
    """Parse a .scenes file into [(scene_name, start_seconds), ...]."""
    scenes = []
    for line in scenes_path.read_text().splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        # Format: HH:MM:SS,mmm  scene_name
        parts = line.split(None, 1)
        if len(parts) == 2:
            scenes.append((parts[1].strip(), srt_to_seconds(parts[0])))
    return scenes


def find_screenshot(screenshots_dir: Path, scene_name: str) -> Path | None:
    """Find the screenshot PNG matching a scene name.

    Screenshots are named: {index}_{chapter}_{name}.png
    Scene names are just the 'name' part (e.g. 'overview', 'tab_register').
    For ep5-style scenes with slash (e.g. '12_msvault/tab_register'), the
    chapter prefix is already included.
    """
    # Normalize: if scene has a slash like "12_msvault/tab_register", match against that
    if "/" in scene_name:
        chapter, name = scene_name.rsplit("/", 1)
        pattern = f"*_{chapter}_{name}.png"
    else:
        pattern = f"*_{scene_name}.png"

    matches = sorted(screenshots_dir.glob(pattern))
    return matches[0] if matches else None


def get_audio_duration(mp3_path: Path) -> float:
    """Get audio duration in seconds using ffprobe."""
    result = subprocess.run(
        ["ffprobe", "-v", "quiet", "-show_entries", "format=duration",
         "-of", "default=noprint_wrappers=1:nokey=1", str(mp3_path)],
        capture_output=True, text=True
    )
    return float(result.stdout.strip())


def merge(audio_path: Path, scenes_path: Path, screenshots_dir: Path,
          output_path: Path, srt_path: Path | None = None,
          resolution: tuple[int, int] = (1920, 1080), fps: int = 1):
    """Build a video from screenshots synced to narration audio."""

    scenes = parse_scenes_file(scenes_path)
    if not scenes:
        print("Error: no scenes found in .scenes file", file=sys.stderr)
        sys.exit(1)

    audio_duration = get_audio_duration(audio_path)
    print(f"Audio duration: {audio_duration:.1f}s")
    print(f"Scenes: {len(scenes)}")
    print(f"Screenshots dir: {screenshots_dir}")
    print()

    # Match each scene to a screenshot and calculate durations
    entries = []
    for i, (scene_name, start_s) in enumerate(scenes):
        # Duration = time until next scene, or until end of audio for last scene
        if i + 1 < len(scenes):
            duration = scenes[i + 1][1] - start_s
        else:
            duration = audio_duration - start_s

        # Add 0.5s padding at end to avoid jarring cuts
        duration = max(duration, 0.5)

        screenshot = find_screenshot(screenshots_dir, scene_name)
        if screenshot:
            entries.append((scene_name, screenshot, duration))
            print(f"  {start_s:6.1f}s  {duration:5.1f}s  {scene_name} → {screenshot.name}")
        else:
            print(f"  {start_s:6.1f}s  {duration:5.1f}s  {scene_name} → MISSING (will hold previous)")

    if not entries:
        print("\nError: no screenshots matched any scenes", file=sys.stderr)
        sys.exit(1)

    # Handle missing screenshots by extending the previous entry's duration
    filled = []
    for scene_name, screenshot, duration in entries:
        filled.append((screenshot, duration))

    print(f"\n  {len(filled)} scenes matched to screenshots")

    # Build ffmpeg concat file
    with tempfile.NamedTemporaryFile(mode="w", suffix=".txt", delete=False) as f:
        concat_path = f.name
        for screenshot, duration in filled:
            # ffmpeg concat demuxer format
            f.write(f"file '{screenshot.resolve()}'\n")
            f.write(f"duration {duration:.3f}\n")
        # Repeat last frame (ffmpeg concat needs this to show the last image)
        if filled:
            f.write(f"file '{filled[-1][0].resolve()}'\n")

    try:
        # Build ffmpeg command
        cmd = [
            "ffmpeg", "-y",
            "-f", "concat", "-safe", "0", "-i", concat_path,
            "-i", str(audio_path),
        ]

        # Add subtitles if provided
        if srt_path and srt_path.exists():
            cmd.extend(["-i", str(srt_path)])

        cmd.extend([
            "-vf", f"scale={resolution[0]}:{resolution[1]}:force_original_aspect_ratio=decrease,"
                   f"pad={resolution[0]}:{resolution[1]}:(ow-iw)/2:(oh-ih)/2:color=black,"
                   "format=yuv420p",
            "-r", str(fps),
            "-c:v", "libx264",
            "-preset", "medium",
            "-crf", "23",
            "-c:a", "aac",
            "-b:a", "192k",
            "-shortest",
            "-movflags", "+faststart",
        ])

        # Burn in subtitles if provided
        if srt_path and srt_path.exists():
            # Replace the -vf with one that includes subtitles
            vf_idx = cmd.index("-vf") + 1
            srt_escaped = str(srt_path.resolve()).replace("\\", "/").replace(":", "\\:")
            cmd[vf_idx] = (
                f"scale={resolution[0]}:{resolution[1]}:force_original_aspect_ratio=decrease,"
                f"pad={resolution[0]}:{resolution[1]}:(ow-iw)/2:(oh-ih)/2:color=black,"
                f"subtitles='{srt_escaped}':force_style='FontSize=22,PrimaryColour=&HFFFFFF&,"
                f"OutlineColour=&H000000&,Outline=2,Shadow=1,MarginV=40',"
                "format=yuv420p"
            )

        cmd.append(str(output_path))

        print(f"\nRunning ffmpeg...")
        print(f"  Output: {output_path}")
        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode != 0:
            print(f"\nffmpeg failed (exit {result.returncode}):", file=sys.stderr)
            print(result.stderr[-2000:] if len(result.stderr) > 2000 else result.stderr, file=sys.stderr)
            sys.exit(1)

        size_mb = output_path.stat().st_size / (1024 * 1024)
        print(f"  Done! {size_mb:.1f} MB")

    finally:
        os.unlink(concat_path)


def main():
    parser = argparse.ArgumentParser(
        description="Merge narration audio with screenshots into a synced video")
    parser.add_argument("--episode", "-e", type=int,
                        help="Episode number (auto-resolves paths)")
    parser.add_argument("--audio", help="Path to narration MP3")
    parser.add_argument("--scenes", help="Path to .scenes timing file")
    parser.add_argument("--screenshots", help="Path to screenshots directory")
    parser.add_argument("--subtitles", action="store_true",
                        help="Burn subtitles from .srt file into the video")
    parser.add_argument("--output", "-o", help="Output video path (default: auto)")
    parser.add_argument("--narration-dir", default="output",
                        help="Narration output dir (default: output)")
    parser.add_argument("--screenshot-dir", default="../output",
                        help="Screenshot output base dir (default: ../output)")
    parser.add_argument("--resolution", default="1920x1080",
                        help="Output resolution (default: 1920x1080)")
    parser.add_argument("--fps", type=int, default=1,
                        help="Output frame rate (default: 1 — slideshow)")
    args = parser.parse_args()

    script_dir = Path(__file__).parent

    # Resolve paths from --episode shortcut
    if args.episode:
        if args.episode not in EPISODE_MAP:
            print(f"Error: unknown episode {args.episode}. "
                  f"Available: {sorted(EPISODE_MAP.keys())}", file=sys.stderr)
            sys.exit(1)

        narr_slug, screen_slug = EPISODE_MAP[args.episode]
        narr_dir = script_dir / args.narration_dir
        screen_dir = script_dir / args.screenshot_dir / screen_slug

        args.audio = args.audio or str(narr_dir / f"{narr_slug}.mp3")
        args.scenes = args.scenes or str(narr_dir / f"{narr_slug}.scenes")
        args.screenshots = args.screenshots or str(screen_dir)
        if not args.output:
            args.output = str(narr_dir / f"{narr_slug}.mp4")

    # Validate required args
    if not all([args.audio, args.scenes, args.screenshots]):
        print("Error: provide --episode or all of --audio, --scenes, --screenshots",
              file=sys.stderr)
        sys.exit(1)

    audio_path = Path(args.audio)
    scenes_path = Path(args.scenes)
    screenshots_dir = Path(args.screenshots)
    output_path = Path(args.output or "output.mp4")

    if not audio_path.exists():
        print(f"Error: audio not found: {audio_path}", file=sys.stderr)
        sys.exit(1)
    if not scenes_path.exists():
        print(f"Error: scenes file not found: {scenes_path}", file=sys.stderr)
        sys.exit(1)
    if not screenshots_dir.is_dir():
        print(f"Error: screenshots dir not found: {screenshots_dir}", file=sys.stderr)
        sys.exit(1)

    # Subtitles
    srt_path = None
    if args.subtitles:
        srt_path = scenes_path.with_suffix(".srt")
        if not srt_path.exists():
            print(f"Warning: SRT not found at {srt_path}, skipping subtitles")
            srt_path = None

    # Parse resolution
    w, h = args.resolution.split("x")
    resolution = (int(w), int(h))

    # Check ffmpeg
    try:
        subprocess.run(["ffmpeg", "-version"], capture_output=True, check=True)
    except FileNotFoundError:
        print("Error: ffmpeg not found. Install it: apt install ffmpeg", file=sys.stderr)
        sys.exit(1)

    merge(audio_path, scenes_path, screenshots_dir, output_path,
          srt_path=srt_path, resolution=resolution, fps=args.fps)


if __name__ == "__main__":
    main()
