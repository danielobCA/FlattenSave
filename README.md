# <img src="icon_new.png" width="32" align="center"> FlattenSave for Paint.NET

**One-click "flatten and save a copy" for Paint.NET 5.x.**

FlattenSave adds a new window that lets you export the open image to a folder you choose, flattened, in the format you choose, without touching your working file. Press one button instead of going through File > Save As, picking a folder, typing a name, choosing a type and confirming dialogs every time.

![FlattenSave panel](https://i.imgur.com/jOBhkLX.png) ![FlattenSave settings](https://i.imgur.com/driZaYu.png)

## Who it's for

Anyone who repeatedly exports the same image to the same place while still editing it in Paint.NET:

- **Texture and asset work for 3D / game tools** (Blender, Unity, Godot, etc.): edit a texture in layers, press **Flatten & Save**, and the engine's watched folder picks up the new PNG.
- **Iterating on an image** where you want a fresh flattened copy after each round of edits.
- **Layered working files**: your layered `.pdn` stays untouched and open; only the exported copy is flattened.
- **Quick variants**: export rotated or mirrored copies, or copies with helper layers (guides, notes, backgrounds) left out.

## Features

- **Output directory**: paste a path or browse for a folder. It is remembered between sessions.
- **Export file name**: optional. If left blank, the open file's name is used. If the image hasn't been saved and no name is typed, FlattenSave won't export and tells you why.
- **File type dropdown**: lists the single-layer formats Paint.NET can save (PNG, JPEG, BMP, etc.).
- **Live preview**: a line under the button shows exactly what will be written, e.g. `Texture.png`.
- **Export options** (toggle buttons):
  - Rotate 90 clockwise, 90 counter-clockwise or 180
  - Mirror horizontally and/or vertically
  - **Omit layers** whose names match a comma-separated list (e.g. `guide, notes`)
- **Number exports**: never overwrite; repeated exports become image.png, image_1.png, image_2.png.
- **File name templates**: {name}, {date}, {time} and {n} / {n:3} (auto-incrementing counter).
- **Launcher button** on the top right that opens or closes the panel.

## Install

1. Download `FlattenSave_x.x.x.zip` from the Releases.
2. Extract `FlattenSave.dll` into `Documents\paint.net App Files\Effects\`.
3. Restart Paint.NET. The panel opens automatically; reopen it any time from **Effects > Tools > FlattenSave Panel** (marked with the FlattenSave icon).

## Usage

1. Set the **Output Directory**.
2. (Optional) type an **Export file name**, pick a **File type**, and toggle any rotate / mirror / omit options.
3. Press **Flatten & Save**.
4. Voila! :)


