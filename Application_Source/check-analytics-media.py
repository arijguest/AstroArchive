"""Decode real exported files independently of the Windows encoder (CI only)."""
import pathlib
import subprocess
import sys

import imageio_ffmpeg
from PIL import Image, ImageChops, ImageStat


def verify(folder):
    for reference in sorted(folder.glob("media-*-reference.png")):
        image = Image.open(reference).convert("RGB")
        prefix = str(reference).removesuffix("-reference.png")
        frame_bytes = image.width * image.height * 3
        movie = subprocess.run(
            [imageio_ffmpeg.get_ffmpeg_exe(), "-v", "error", "-i", prefix + ".mp4",
             "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
            check=True, capture_output=True,
        ).stdout
        assert len(movie) == frame_bytes * 8, f"Wrong MP4 frame count or ratio: {prefix}"
        final = Image.frombytes("RGB", image.size, movie[-frame_bytes:])
        error = sum(ImageStat.Stat(ImageChops.difference(image, final)).mean) / 3
        assert error < 7, f"MP4 colours, orientation or final chart differ ({error:.2f}): {prefix}"
        with Image.open(prefix + ".gif") as gif:
            assert gif.size == image.size and gif.n_frames == 8, f"GIF frames/ratio: {prefix}"
            assert gif.info.get("loop") == 0, f"GIF does not loop: {prefix}"
            duration = 0
            for frame in range(gif.n_frames):
                gif.seek(frame)
                gif.load()
                duration += gif.info["duration"]
            assert duration == 2000, f"Wrong GIF timing: {prefix}"
            error = sum(ImageStat.Stat(ImageChops.difference(image, gif.convert("RGB"))).mean) / 3
            assert error < 9, f"GIF palette lost the branded chart ({error:.2f}): {prefix}"
        first = Image.frombytes("RGB", image.size, movie[:frame_bytes])
        change = sum(ImageStat.Stat(ImageChops.difference(first, final)).mean) / 3
        assert change > .5, f"Chart export is static: {prefix}"
        print(f"PASS {reference.stem}: MP4/GIF decode, ratio, timing, orientation, palette and animation")
    for suffix in ("mp4", "gif"):
        path = folder / f"media-story.{suffix}"
        if suffix == "gif":
            with Image.open(path) as gif:
                assert gif.n_frames == 36 and gif.size == (180, 320)
                for frame in range(gif.n_frames):
                    gif.seek(frame)
                    gif.load()
        else:
            raw = subprocess.run(
                [imageio_ffmpeg.get_ffmpeg_exe(), "-v", "error", "-i", str(path),
                 "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
                check=True, capture_output=True,
            ).stdout
            assert len(raw) == 36 * 180 * 320 * 3
        print(f"PASS six-chart {suffix.upper()} story: all 36 frames decode")
    for suffix in ("mp4", "gif"):
        path = folder / f"media-custom-length.{suffix}"
        if suffix == "gif":
            with Image.open(path) as gif:
                assert gif.n_frames == 12 and gif.size == (180, 320)
                duration = 0
                for frame in range(gif.n_frames):
                    gif.seek(frame)
                    gif.load()
                    duration += gif.info["duration"]
                assert duration == 3000, "Custom length drifted across scenes"
        else:
            raw = subprocess.run(
                [imageio_ffmpeg.get_ffmpeg_exe(), "-v", "error", "-i", str(path),
                 "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
                check=True, capture_output=True,
            ).stdout
            assert len(raw) == 12 * 180 * 320 * 3
        print(f"PASS custom-length {suffix.upper()} story: six scenes in exactly 3 seconds")
    sizes = {"Landscape": (1200, 800), "Vertical": (1080, 1920),
             "Portrait": (1080, 1350), "Square": (1080, 1080),
             "Widescreen": (1920, 1080), "Pinterest": (1000, 1500)}
    for layout, size in sizes.items():
        path = folder / f"media-native-{layout}.mp4"
        raw = subprocess.run(
            [imageio_ffmpeg.get_ffmpeg_exe(), "-v", "error", "-i", str(path),
             "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
            check=True, capture_output=True,
        ).stdout
        assert len(raw) == 3 * size[0] * size[1] * 3, f"Full-size H.264 export failed: {layout}"
        print(f"PASS full-size {layout} MP4: {size[0]} × {size[1]}, 24 fps")


if __name__ == "__main__":
    folder = pathlib.Path(sys.argv[1])
    assert len(list(folder.glob("media-*-reference.png"))) == 12, "Missing ratio/theme exports"
    verify(folder)
