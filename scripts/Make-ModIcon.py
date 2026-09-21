"""Deterministic geometric placeholder. Not approved ModHub submission artwork."""
from pathlib import Path
from PIL import Image, ImageDraw
import sys
out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
im = Image.new("RGB", (512,512), (32,32,32))
d = ImageDraw.Draw(im)
d.rectangle((64,112,108,360), fill="white")
d.rectangle((108,112,240,156), fill="white")
d.rectangle((108,214,212,258), fill="white")
d.polygon([(276,360),(276,112),(314,112),(360,214),(406,112),(444,112),(444,360),(404,360),(404,208),(360,304),(316,208),(316,360)], fill="white")
d.line([(64,416),(148,416),(176,386),(208,446),(244,396),(272,416),(444,416)],fill=(100,190,255),width=10)
im.save(out/"icon_farmMotion.dds",pixel_format="DXT1")
im.save(out/"icon_farmMotion-preview.png")
