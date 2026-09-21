# FarmMotion artwork

`farmmotion.png` is the generated source artwork. `farmmotion.ico` contains 16, 20, 24, 32, 40, 48, 64, 128 and 256-pixel Windows icon frames. Both executables embed the icon; no external image is needed at runtime.

Generated with the built-in imagegen tool. Final edit prompt:

> Edit this FM icon for Windows. Keep the bold italic WHITE letters FM, but place them smaller, occupying about 70% of canvas width, optically centered. Replace EVERYTHING behind the white letters with a perfectly uniform solid red #D92332 rectangle filling the ENTIRE SQUARE canvas edge-to-edge, all four corners red. Remove all transparent or black areas, all shadows, all tilted banner shape, all glow and distressed edges. Output a flat 1024x1024 square red app tile with only the centered white bold italic FM text. Not a slanted red plaque. No border.

Repackage the artwork using `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Convert-Icon.ps1`. No Adobe artwork or font file is included.
