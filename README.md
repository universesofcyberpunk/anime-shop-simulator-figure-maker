# Figure Maker for Anime Shop Simulator

Turn any 3D model (GLB, GLTF, Sketchfab zip, FBX, OBJ, BLEND) into a figure pack for the **FigureShop** mod of
Anime Shop Simulator. The figure is sold in your shop as a loose
figure and as a gift box figure. Windows only. Made by **Fabianens**.

This repository holds both halves: the **Figure Maker** app (Python) and the source of the **FigureShop** MelonLoader
mod that loads the packs in the game (`FigureShop/`, C#).

- [Screenshots](#screenshots)
- [Install the ready exe](#install-the-ready-exe)
- [Run from source](#run-from-source)
- [Build the exe yourself](#build-the-exe-yourself)
- [FigureShop mod (source)](#figureshop-mod-source)
- [How to use (full guide, English + Türkçe)](#english)

## Screenshots

**The form** - one model dragged onto FigureMaker.exe. Numbers explain every control.

![FigureMaker form](docs/screenshots/01_form_en.png)

**Parts** - invisible effect parts (light rings, music notes, background plates in game models) are left out automatically, so the figure gets its real size. Click any part to leave it out yourself.

![Parts list before and after](docs/screenshots/02_parts_en.png)

**Batch window** - many models at once, one after another.

![Batch window](docs/screenshots/03_batch_en.png)

**Results** - shop pictures from the batch above, default settings.

![Results](docs/screenshots/04_results_en.png)

<details>
<summary>Ekran görüntüleri (Türkçe)</summary>

![Form](docs/screenshots/01_form_tr.png)
![Parçalar](docs/screenshots/02_parts_tr.png)
![Toplu pencere](docs/screenshots/03_batch_tr.png)
![Sonuçlar](docs/screenshots/04_results_tr.png)

</details>

## Install the ready exe

1. Download **FigureMaker.exe** from the [Releases](../../releases) page (or from [Nexus Mods](https://www.nexusmods.com/animeshopsimulator/mods/82)).
2. Put it in a folder you can write to, e.g. `Desktop\FigureMaker` (results are written next to the exe; Program Files does not work).
3. Drag a model onto `FigureMaker.exe`, or double-click it for the batch window.
4. Copy the folder from `out\<name>` into the game's `UserData\FigureShop` folder and restart the game.

In the game you need MelonLoader 0.7.x, **[FigureShop](https://www.nexusmods.com/animeshopsimulator/mods/80) 1.3.0+** (source in [`FigureShop/`](FigureShop)) and **[Fabianens Mod Market](https://github.com/universesofcyberpunk/anime-shop-simulator-mod-market) 1.4.0+**.
FBX / OBJ / BLEND input also needs [Blender 4.x](https://www.blender.org/download/) installed (GLB, GLTF and zip need nothing).
If Windows SmartScreen warns, click *More info > Run anyway* (the exe is not signed).

## Run from source

Needs **Python 3.10 or newer (64-bit)** on Windows.

```bat
git clone https://github.com/universesofcyberpunk/anime-shop-simulator-figure-maker.git
cd anime-shop-simulator-figure-maker
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt

python figuremaker.py                          :: batch window (drag and drop, many models)
python figuremaker.py model.glb                :: one model, opens the form with the 3D preview
python figuremaker.py model.glb --no-form --install   :: no window, copies straight into UserData\FigureShop
```

`FigureMaker.bat model.glb` does the same as the second line (drag a model onto the .bat).
When run from source the tool finds the game in the usual Steam folder; `--install` copies the result there.

## Build the exe yourself

```bat
.venv\Scripts\activate
pip install pyinstaller
pyinstaller --noconfirm FigureMaker.spec
```

The single-file exe is written to `dist\FigureMaker.exe`. Use a clean venv (only `requirements.txt` + PyInstaller)
so the exe stays small. `tools\` is bundled inside the exe; `lang\`, `out\`, `work\` and `settings.json` are created
next to the exe on first use and are not part of the repository.

## FigureShop mod (source)

`FigureShop/` is the in-game half: a MelonLoader 0.7.x mod (net6.0, IL2CPP) that reads every pack folder in
`UserData\FigureShop`, registers each figure as a loose and a boxed product with its own licence on the
**[Fabianens Mod Market](https://github.com/universesofcyberpunk/anime-shop-simulator-mod-market)** MODS screen, and builds the 3D model from `model.skin` at the pose frame in `figure.json`.
Players normally get the ready `FigureShop.dll` from [Nexus Mods](https://www.nexusmods.com/animeshopsimulator/mods/80) or the [Releases](../../releases) page; build it yourself like this:

1. Install the [.NET 6 SDK or newer](https://dotnet.microsoft.com/download) and run the game once with MelonLoader 0.7.x
   (that generates `MelonLoader\Il2CppAssemblies`, which the project references).
2. Build:

```bat
cd FigureShop
dotnet build -c Release
:: game not in the default Steam folder?
dotnet build -c Release -p:GameDir="D:\Games\Anime Shop Simulator"
```

The DLL is copied to the game's `Mods` folder. Close the game first, the file is locked while it runs.
The game's own DLLs are not in this repository; they come from your install.

| File | What it is |
|---|---|
| `FigureShop/FigureShop.cs` | mod entry, pack loading, product and licence registration, shelf and box display |
| `FigureShop/Figures.cs` | `figure.json` reader and pack catalogue |
| `FigureShop/SkinModel.cs` | `model.skin` loader: mesh, skinning and the chosen pose frame |

## Repository layout

| Path | What it is |
|---|---|
| `figuremaker.py` | the app: batch window, form with 3D preview, conversion, icons, language files |
| `tools/conv.py` | glTF reader and converter to the game format (`model.skin`, textures) |
| `tools/standalone.py` | built-in icon renderer, facing detection, polygon reduction (no Blender needed) |
| `tools/orient.py` | point-cloud preview used by the form |
| `tools/fbx2glb.py` | runs inside Blender to turn FBX / OBJ / BLEND into GLB |
| `tools/gl.py`, `tools/rd.py` | glTF math helpers, `model.skin` reader |
| `FigureMaker.spec` | PyInstaller recipe for the exe |
| `FigureShop/` | source of the FigureShop mod that loads the packs in the game |

Only share figure packs of models whose licence allows it; the tool copies the model's credits into `license.txt`.
Code licence (Figure Maker and FigureShop): MIT, see `LICENSE`. If you reuse the code, credit **Fabianens**.

## More by Fabianens

| Project | What it does | Links |
|---|---|---|
| **Figure Maker + FigureShop** (this repository) | turn any 3D model into a shop figure (app) and the mod that sells it | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-figure-maker) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/82) |
| **Statue Maker + Statue Shop** | turn any 3D model into a life-size shop statue (app) and the mod that sells it | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-statue-maker-) |
| **Fabianens Mod Market** | MODS screen on the shop computer; API for other mods to list their items | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-mod-market) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/31) |
| **Mod Manager** | build / test / package / publish state of all your mods (tool for modders) | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-mod-manager) |
| **Video TV** | wall TVs that play your own videos | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-video-tv) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/90) |
| **Graphics Plus** | URP colour, anti-aliasing and SSAO presets, an ENB alternative | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-GraphicsPlus) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/95) |
| **Free Placement** | place, stack, resize and tilt items anywhere | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-FreePlacement) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/47) |
| **FBS Body** | play as your own VRoid character, with FBS Studio | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-FBSBody) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/45) |
| **Mod Settings Manager** | the F10 in-game settings menu used by most of these mods | [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/40) |
| **FTPV - First and Third Person View** | see your own body in first person (needed by FBS Body) | [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/32) |

All mods: [Fabianens on Nexus Mods](https://www.nexusmods.com/profile/Fabianens/mods?gameId=10172)

---

*Türkçe açıklama aşağıda.*

---

## ENGLISH

### What is this?

**Figure Maker** turns a 3D model into a figure pack for **FigureShop**. Put the pack folder into the game and your model is sold in your shop as a loose figure and a gift box figure.

### IMPORTANT

- **GLB, GLTF and Sketchfab .zip** need nothing - just FigureMaker.exe.
- **FBX, OBJ and .blend** need **Blender 4.x** installed on the computer (they are converted to GLB with Blender first). No Blender? Save the model as GLB.

### Requirements (in the game)

- MelonLoader 0.7.x
- **FigureShop 1.3.0** or newer
- **[Fabianens Mod Market](https://github.com/universesofcyberpunk/anime-shop-simulator-mod-market) 1.4.0** or newer (the licence and figures are sold on the MODS screen)

### How to use

1. Put FigureMaker.exe in a folder you can write to, e.g. **Desktop\FigureMaker** (the result is written next to the exe; it may not work inside Program Files).
1. Drag your model onto FigureMaker.exe.
1. A form opens: folder name, figure name (English / Turkish), pose, market value, up axis and the licence.
1. Check the **Preview** on the right: the side you see faces the customers. If the figure lies, faces the floor or shows its back, drag it with the mouse (left / right turns it, up / down tips it over, right button tips it sideways; it snaps to 90° when you let go) or use the **Tip over 90°**, **Tip sideways 90°** and **Turn 180°** buttons. **Automatic** goes back to the detected orientation. Animated model? Pick the moment of the animation with the frame slider under the picture.
1. Under **Parts** every part of the model is listed. Click a part to leave it out of the figure (a light effect, a background plate, a weapon you do not want); click again to take it back. Invisible and effect parts are left out automatically, parts far from the body are marked. **Automatic** goes back to that choice.
1. Click **Create**. After a few seconds a "DONE" window appears and the `out\<name>` folder opens.
1. Copy that folder into the game's **UserData\FigureShop** folder and restart the game.

### Many models at once (batch)

1. Double-click FigureMaker.exe (or drag several models / a folder onto it). The batch window opens.
1. Drag models or whole folders onto the window, or click **Add files...** / **Add folder...** and select many at once.
1. Click **Start**. The models are converted one after another; each row shows Waiting, Converting, Done or ERROR. You can keep adding models while it runs.
1. When you click Start the first time, the licence is asked once (Fabi Figures or your own licence) and every figure in the queue gets it. **Licence for all...** changes it later.
1. Every figure gets its own default name, the default pose (an idle animation if there is one) and the automatic parts choice. To change one before it starts, select it and click **Edit...** (the same form as above); a licence changed there applies only to that figure.
1. Tick **Also install into the game** to copy each finished figure into UserData\FigureShop. Double-click a finished row to open its folder; a failed row shows the error and can be retried.

### Language

- Pick the language at the top right of the window. The whole window switches at once and the choice is remembered.
- The texts live in the **lang** folder next to the exe (**en.json**, **tr.json**); they are created the first time the exe runs. Edit them to change any text.
- New language: copy en.json to e.g. **de.json**, set **"_name": "Deutsch"** and translate the texts. It appears in the language list right away (no restart needed). Missing texts fall back to English.

If Windows says "Windows protected your PC", click **More info > Run anyway** (the exe is not signed).

### What you get

- **model.skin** + textures - the model in the game's format
- **figure.json** - the in-game info: names, gift box names, size, rotation, pose, market value, licence, credits
- **icon.png** - the shop picture of the figure
- **box_icon.png** - the shop picture of the gift box (made when the game has created its box template; otherwise the game draws it)
- **license.txt** - the model's licence and author

### Licence

- Default: the figure is sold under the **Fabi Figures - Fabianens** licence.
- Your own licence: choose "My own licence" in the form and enter a name, price, level and an optional logo (png). The figure then appears under its own licence. Packs that use the same licence name share it.

### Done for you

- Models with more than 60,000 triangles are reduced automatically (many figures stand on a shelf).
- Lying models are stood up: models saved with Z as the up axis (common in Blender, 3ds Max and ZBrush OBJ exports) used to come out lying face down. The up axis is found from the skeleton, or from the model's shape and base when there is no skeleton.
- The direction the figure faces is found from its skeleton (toes / eyes) so it faces the customers. Models without a skeleton now face the customers by default too.
- The shop picture is drawn from the model.
- Invisible and effect parts are left out before anything is measured (see **Parts**), so a figure with a big hidden effect plate is no longer tiny.
- Animations that the exporter split bone by bone (names like *Body|Come|Base Layer*, *Bone_Bow01|Come|Base Layer*, common in Sketchfab game rips) are joined back into one animation (*Come*). Before, only one piece moved and e.g. the weapon stayed behind in the air.
- The default pose is an idle animation (a name with idle, stand, wait or breath) when the model has one; otherwise the first animation. Game models often start with an entrance animation where weapons fly in.
- A model that is already clearly taller than it is wide and deep is kept standing (flat backs and capes no longer make the automatic up axis lay it down).

### Choosing the pose (animated models)

A figure on a shelf is one still model, so an animated model has to be frozen in one moment. You choose that moment in the form.

- **When can you choose?** Only when the model file contains at least one animation. Rigged models from Sketchfab, Mixamo and game rips usually do; a plain statue or a 3D scan does not. A rig (skeleton) alone is not enough: without an animation the figure keeps the pose it was modelled in.
- **Which files carry animations?** GLB, GLTF and Sketchfab .zip (read directly), FBX and .blend (through Blender, all animations / actions are kept). OBJ never has animations.
- **1. Animation:** the **Pose (animation)** list shows every animation in the file (by default an idle animation if there is one, otherwise the first). Animations split bone by bone are shown as one. **-** means the rest pose: the model as it was built, usually a T-pose or A-pose for rigged models. Without animations the list is greyed out.
- **2. Frame:** under the preview picture there is a frame slider ("Frame 21 / 61 (0.67 s)"). Slide it and the preview jumps to that moment of the animation; the picture follows a moment after you stop. The figure is made exactly as you see it. Frames are counted at 30 per second. A one-frame animation or a model without animations shows the slider greyed out ("No animation: rest pose").
- **3. Turn it if needed:** the orientation you set with the mouse is kept when you change the frame. Choosing another animation puts the slider back to the first frame.
- **What follows the frame:** the figure model in the game, the shop picture (icon.png and box_icon.png) and the automatic facing (which way the figure looks at the customer).
- **figure.json:** **pose** = animation name ("-" = rest pose), **poseFrame** = frame number (0 = first), **poseTime** = the same in seconds. You can change them by hand and restart the game; FigureShop 1.3.3+ uses poseFrame, older versions poseTime. The whole animation stays in model.skin, so any frame can be chosen later without making the pack again (only the shop picture keeps the old frame).
- **Batch:** each model starts on frame 0 of its default animation; select a row and click **Edit...** to choose its pose and frame.
- **Command line:** --pose "Animation name" --pose-time 1.5 (seconds, turned into the nearest frame); --pose - for the rest pose.
- **Several poses of one model = several figures:** convert the same model again with another animation or frame and a **different folder name** (for example Veera_Idle, Veera_Fly) and different names. Every folder is its own figure in the game with its own IDs, shop picture and on/off switch, so they never mix up in saves. The same folder name overwrites the earlier pack instead of adding a new figure. In the batch window the same file can be added again after it is done (the folder gets _2, _3 ...); use **Edit...** to give each one its own name and pose. Every pose is a full figure on disk (its own model.skin and textures).
- **Tips:** the first and last frames of a walk or idle loop are often a plain standing pose; a moment in the middle of a move usually looks livelier. Check the feet in the preview: the lowest point of the chosen frame stands on the shelf, so a jump frame is shown standing, not floating.

### Parts: leaving parts out

Many models carry pieces you do not want on a figure: glow effects, light rings, background plates, a spare weapon, a sign at the feet. The **Parts** list in the form shows every part of the model and lets you leave any of them out.

- **What is a part?** One material of the model, together with every piece that uses it (the names come from the model file, e.g. *Body*, *Hair*, *Weapon*, *EF_...* for effects in game models). The list shows each part with its triangle count, the biggest first.
- **Leave a part out:** click it. The tick turns into a dash, the row turns grey and the note says *left out*. Click it again to take it back. The preview is redrawn at once without it.
- **What a left-out part changes:** it is not written into the figure at all (not hidden, really gone), it is not drawn on the shop picture, and the size, the centre and the stand on the floor are worked out without it. So leaving out a huge effect plate gives the figure its real size.
- **Left out automatically** (note: *invisible / effect*): transparent parts that cannot be seen (colour transparency almost 0); cut-out parts that are below their cut-out limit; transparent parts without a texture that are more than half see-through or still have the plain grey colour that a lost texture leaves behind (typical for effect cards in game rips: light rings, music notes, glow planes).
- **Never left out automatically:** transparent parts with a texture (lace, tulle, hair tips, eyelashes), glass, and every opaque part. If every part of a model is transparent, nothing is left out, so the model never ends up empty.
- **Marked "far from the body":** parts that do not touch the area around the main body at all (in the rest pose). This is only a hint: they stay in, because wings, weapons, earrings and bases are separate pieces too. Leave them out by hand if they are really unwanted (a background plate, a floating effect).
- **Automatic** (under the list) goes back to the automatic choice. At least one part always stays.
- **Where it is saved:** figure.json lists the left-out parts under **maker > hiddenParts**. To change the choice later, make the figure again.
- **Batch:** every model starts with the automatic choice; select a row and click **Edit...** to change its parts.
- **Command line:** **--hide "Weapon,EF_"** leaves out every part whose name contains one of the comma-separated words (upper / lower case does not matter); **--show "EF_09"** takes automatically left-out parts back. The output lists which parts were left out.

### Model rules

- Rigged (animated) or plain models both work. If the model has animations, one frame is used as the pose: choose the animation under Pose and the frame with the slider under the preview.
- Textures must be inside the model or next to it.
- Still lying or upside down? Fix it in the form's preview with the mouse, or pick the model's real up axis under **Up axis of the model** in the form (Automatic, Y = as it is, Z, -Z, X, -X, -Y) or use **--up Z** on the command line. Compact poses (kneeling, crouching) without a skeleton can't always be detected.
- Still showing its back? Open its **figure.json** and add or subtract 180 from **"rotationY"**.
- **Licence of the model:** only share packs of models that allow it (e.g. CC-BY: credit the author; CC-BY-NC: free sharing only). Keep license.txt in the pack. Sketchfab "Standard" / editorial licences usually do not allow sharing.

### Advanced (command line)

```bat
FigureMaker.exe model.glb --no-form --name Name --en "English" --tr "Türkçe" --pose Anim --pose-time 0 --up auto --budget 60000 --licence "Name" --licence-price 1000 --licence-level 1 --licence-logo logo.png --hide "Weapon,EF_" --install
FigureMaker.exe folder_or_model1 model2 ... --no-form --install
```

- **--no-form** - no window; the figure is made with the given / default settings.
- **--name** - folder name; **--en** / **--tr** - English / Turkish name.
- **--pose** - animation name (part of the name is enough; **-** = rest pose); **--pose-time** - moment in seconds (nearest frame, 30 per second).
- **--up** - up axis of the model: auto, Y, Z, -Z, X, -X, -Y.
- **--budget** - most triangles before the model is reduced (60000).
- **--licence**, **--licence-price**, **--licence-level**, **--licence-logo** - your own licence (see **Licence**).
- **--hide** / **--show** - leave parts out / take automatically left-out parts back (see **Parts**).
- **--install** - also copy the figure into the game's UserData\FigureShop.

### Changes

- **1.4.0**
   - **New - Parts list** in the form: every part of the model with its triangle count; click a part to leave it out of the figure, click again to take it back; the preview follows at once. Parts far from the body are marked, **Automatic** resets the choice. Also in the batch window (**Edit...**) and on the command line (**--hide**, **--show**). figure.json lists the left-out parts under maker > hiddenParts.
   - **New - invisible and effect parts are left out automatically**: fully transparent parts and texture-less effect cards (light rings, music notes and glow planes in game models). Before, they counted for the size, so the figure itself came out far too small on the shelf and the shop picture showed grey plates.
   - **Fixed - animations split bone by bone are joined** (e.g. *Bone_Bow01|Come|Base Layer* + *Body|Come|Base Layer* = *Come*). The pose list is shorter and weapons / hair no longer stay behind in the rest pose.
   - **Fixed - standing models laid down**: a tall model with a flat back or cape (e.g. Boa Hancock) was taken for a Z-up model and came out lying. A model clearly taller than wide and deep now stays standing.
   - **Changed - default pose** is an idle animation (idle / stand / wait / breath) when there is one, also in the batch window. Game models often start with an entrance animation where the weapon flies in.
   - **Your packs:** nothing changes for figures made with 1.3.x; the 52 models of the New Figure Pack were checked and come out the same (same up axis, nothing left out). FigureShop needs no update.
- 1.3.1 - models whose colours are stored as vertex colours (no texture, common for Sketchfab print models) no longer come out all white: the colours are baked into a texture (vertex_colors.png) after the polygon reduction.
- 1.3.0 - choose the animation frame: for animated models the preview has a frame slider under the picture. Pick the animation under Pose, slide to the moment you like and the figure, its shop picture and its facing are made in that frame. figure.json stores it as **poseFrame** (and poseTime); FigureShop 1.3.3+ uses that exact frame. On the command line: --pose Anim --pose-time 1.5 (seconds).
- 1.2.0 - fixed: models saved with Z as the up axis (Blender / 3ds Max / ZBrush OBJ and some Sketchfab models) came out lying face down; the up axis is now detected automatically (skeleton, or shape and base) and can be set by hand in the form (Up axis of the model) or with --up. New preview in the form: see the figure as the customers will, and turn or tip it with the mouse (or the 90° buttons) before creating it. Fixed: figures without a skeleton came out showing their back (they needed rotationY 180 by hand); they now face the customers. figure.json records the detected up axis and facing under "maker".
- 1.1.0 - batch window: drop or multi-select many models (or whole folders), they are converted one after another with a status for each; the licence is asked once for the whole batch. One file dragged onto the exe still opens the single form. New, tidier windows in one language at a time with a language picker (English / Türkçe, more via the lang folder). A progress window while a model is read and converted; the black console window is hidden. Fixed: figures made from large models came out with white / dark specks, cracks and wrong texture patches (the polygon reduction mixed up texture seams and split body parts); models whose colour is stored as a glow (emissive) texture no longer come out pitch black. Re-make figures made with 1.0.
- 1.0.0 - first release.

---

## TÜRKÇE

### Bu ne?

**Figure Maker**, bir 3D modeli **FigureShop** için figür paketine çevirir. Paket klasörünü oyuna koyduğunda modelin dükkânında açık figür ve kutulu hediye figür olarak satılır.

### ÖNEMLİ

- **GLB, GLTF ve Sketchfab .zip** hiçbir şey istemez - sadece FigureMaker.exe.
- **FBX, OBJ ve .blend** için bilgisayarda **Blender 4.x** kurulu olmalı (bu biçimler önce Blender ile GLB'ye çevrilir). Blender yoksa modeli GLB olarak kaydet.

### Gereksinimler (oyunda)

- MelonLoader 0.7.x
- **FigureShop 1.3.0** veya üstü
- **[Fabianens Mod Market](https://github.com/universesofcyberpunk/anime-shop-simulator-mod-market) 1.4.0** veya üstü (lisans ve figürler MODLAR ekranında satılır)

### Kullanım

1. FigureMaker.exe'yi yazabildiğin bir klasöre koy, ör. **Masaüstü\FigureMaker** (sonuç exe'nin yanına yazılır; Program Files içinde çalışmayabilir).
1. Modelini FigureMaker.exe'nin üzerine sürükle-bırak.
1. Bir form açılır: klasör adı, figür adı (İngilizce / Türkçe), poz, piyasa değeri, yukarı ekseni ve lisans.
1. Sağdaki **Önizleme**'ye bak: gördüğün yüz müşteriye bakar. Figür yatıyor, yere bakıyor ya da sırtını dönüyorsa fareyle sürükle (sola / sağa döndürür, yukarı / aşağı öne / arkaya yatırır, sağ tuş yana yatırır; bırakınca 90°'ye oturur) ya da **Öne yatır 90°**, **Yana yatır 90°** ve **180° çevir** düğmelerini kullan. **Otomatik** bulunan yöne geri döner. Animasyonlu modelde animasyonun hangi ânı olacağını resmin altındaki kare kaydırıcısıyla seç.
1. **Parçalar** altında modelin bütün parçaları listelenir. Bir parçaya tıklayınca figüre alınmaz (ışık efekti, arka plan levhası, istemediğin bir silah); tekrar tıklayınca geri gelir. Görünmeyen ve efekt parçaları kendiliğinden çıkarılır, gövdeden uzak parçalar işaretlenir. **Otomatik** bu seçime geri döner.
1. **Oluştur**'a bas. Birkaç saniye sonra "TAMAM" penceresi çıkar ve `out\<ad>` klasörü açılır.
1. O klasörü oyunun **UserData\FigureShop** klasörüne kopyala ve oyunu yeniden başlat.

### Çok sayıda model (toplu)

1. FigureMaker.exe'ye çift tıkla (ya da birden çok modeli / bir klasörü üzerine sürükle). Toplu pencere açılır.
1. Modelleri ya da klasörleri pencereye sürükle-bırak, veya **Dosya ekle...** / **Klasör ekle...** ile birden çok dosyayı birlikte seç.
1. **Başlat**'a bas. Modeller sırayla çevrilir; her satırda Bekliyor, Çevriliyor, Tamam ya da HATA yazar. Çalışırken yeni model eklemeye devam edebilirsin.
1. Başlat'a ilk basışta lisans bir kez sorulur (Fabi Figures ya da kendi lisansın) ve kuyruktaki bütün figürler onu alır. **Herkes için lisans...** ile sonradan değiştirilebilir.
1. Her figür kendi varsayılan adı, varsayılan pozu (varsa bekleme animasyonu) ve kendiliğinden yapılan parça seçimiyle çıkar. Başlamadan önce birini değiştirmek için seçip **Düzenle...**'ye bas (yukarıdaki formun aynısı); orada değiştirilen lisans sadece o figüre uygulanır.
1. **Oyuna da kur** işaretliyse biten her figür UserData\FigureShop içine de kopyalanır. Biten satıra çift tıklayınca klasörü açılır; hatalı satır hatayı gösterir ve tekrar denenebilir.

### Dil

- Dili pencerenin sağ üstünden seç. Bütün pencere anında o dile geçer ve seçim hatırlanır.
- Yazılar exe'nin yanındaki **lang** klasöründedir (**en.json**, **tr.json**); exe ilk çalıştığında oluşturulur. Herhangi bir yazıyı değiştirmek için bu dosyaları düzenle.
- Yeni dil: en.json'u ör. **de.json** olarak kopyala, **"_name": "Deutsch"** yaz ve yazıları çevir. Dil listesinde hemen görünür (yeniden başlatmak gerekmez). Eksik yazılar İngilizce çıkar.

Windows "Windows bilgisayarınızı korudu" derse **Ek bilgi > Yine de çalıştır** de (exe imzasız).

### Ne üretir?

- **model.skin** + dokular - modelin oyun biçimi
- **figure.json** - oyun içi bilgiler: adlar, kutulu adlar, boyut, dönüş, poz, piyasa değeri, lisans, emeği geçenler
- **icon.png** - figürün mağaza resmi
- **box_icon.png** - hediye kutusunun mağaza resmi (oyun kutu şablonunu ürettiyse yapılır; yoksa oyun kendisi çizer)
- **license.txt** - modelin lisansı ve yazarı

### Lisans

- Varsayılan: figür **Fabi Figures - Fabianens** lisansının altında satılır.
- Kendi lisansın: formda "Kendi lisansım"ı seç; ad, fiyat, seviye ve isteğe bağlı logo (png) gir. Figür oyunda kendi lisansının altında çıkar. Aynı lisans adını kullanan paketler aynı lisansı paylaşır.

### Kendiliğinden yapılanlar

- 60.000 üçgenden büyük modeller otomatik hafifletilir (rafta çok sayıda figür durur).
- Yatan modeller ayağa kaldırılır: yukarı ekseni Z olan modeller (Blender, 3ds Max ve ZBrush OBJ çıktılarında sık) yüzüstü yatarak, yere bakarak çıkıyordu. Yukarı ekseni iskeletten, iskelet yoksa modelin şeklinden ve kaidesinden bulunur.
- Figürün baktığı yön iskeletinden (ayak parmakları / gözler) bulunur, müşteriye bakacak şekilde ayarlanır. İskeletsiz modeller de artık varsayılan olarak müşteriye bakar.
- Mağaza resmi modelden çizilir.
- Görünmeyen ve efekt parçaları her ölçümden önce çıkarılır (bkz. **Parçalar**); büyük, görünmez bir efekt levhası olan figür artık minicik çıkmaz.
- Dışa aktarırken kemik kemik bölünmüş animasyonlar (*Body|Come|Base Layer*, *Bone_Bow01|Come|Base Layer* gibi adlar; Sketchfab'daki oyun modellerinde sık) tekrar tek animasyonda (*Come*) birleştirilir. Önceden sadece bir parça hareket ediyor, ör. silah havada kalıyordu.
- Modelde bekleme animasyonu (adında idle, stand, wait ya da breath geçen) varsa varsayılan poz odur; yoksa ilk animasyon. Oyun modelleri çoğu zaman silahların uçarak geldiği bir giriş animasyonuyla başlar.
- Eninden ve derinliğinden belirgin şekilde uzun olan model zaten ayakta sayılır (düz sırt ve pelerinler otomatik yukarı eksenini artık yanıltmaz).

### Poz seçimi (animasyonlu modeller)

Raftaki figür tek bir sabit modeldir; bu yüzden animasyonlu bir model tek bir anda dondurulur. O ânı formda sen seçersin.

- **Ne zaman seçilebilir?** Sadece model dosyasında en az bir animasyon varsa. Sketchfab'daki iskeletli modellerin, Mixamo ve oyun modellerinin çoğunda vardır; düz bir heykel ya da 3D taramada yoktur. Sadece iskelet (rig) olması yetmez: animasyon yoksa figür modellendiği pozda kalır.
- **Hangi dosyalarda animasyon olur?** GLB, GLTF ve Sketchfab .zip (doğrudan okunur), FBX ve .blend (Blender ile; bütün animasyonlar / action'lar korunur). OBJ'de hiç animasyon olmaz.
- **1. Animasyon:** **Poz (animasyon)** listesinde dosyadaki bütün animasyonlar çıkar (varsayılan: varsa bekleme animasyonu, yoksa ilki). Kemik kemik bölünmüş animasyonlar tek animasyon olarak görünür. **-** dinlenme pozudur: modelin yapıldığı hali, iskeletli modellerde genelde T ya da A pozu. Animasyon yoksa liste gri olur.
- **2. Kare:** önizleme resminin altında kare kaydırıcısı var ("Kare 21 / 61 (0.67 sn)"). Kaydırınca önizleme animasyonun o ânına geçer; resim, bıraktıktan bir an sonra yenilenir. Figür tam gördüğün gibi yapılır. Saniyede 30 kare sayılır. Tek kareli animasyonda ya da animasyonsuz modelde kaydırıcı gri olur ("Animasyon yok: dinlenme pozu").
- **3. Gerekirse çevir:** fareyle ayarladığın yön kare değişince korunur. Başka animasyon seçince kaydırıcı ilk kareye döner.
- **Kareye göre değişenler:** oyundaki figür modeli, mağaza resmi (icon.png ve box_icon.png) ve otomatik yön (figürün müşteriye hangi yönden baktığı).
- **figure.json:** **pose** = animasyon adı ("-" = dinlenme pozu), **poseFrame** = kare numarası (0 = ilk kare), **poseTime** = aynısı saniye olarak. Elle değiştirip oyunu yeniden başlatabilirsin; FigureShop 1.3.3+ poseFrame'i, eski sürümler poseTime'ı kullanır. Animasyonun tamamı model.skin içinde kalır, yani paketi yeniden üretmeden de başka kare seçilebilir (sadece mağaza resmi eski karede kalır).
- **Toplu:** her model varsayılan animasyonunun 0. karesiyle başlar; pozunu ve karesini seçmek için satırı seçip **Düzenle...**'ye bas.
- **Komut satırı:** --pose "Animasyon adı" --pose-time 1.5 (saniye, en yakın kareye çevrilir); dinlenme pozu için --pose -.
- **Bir modelin birkaç pozu = birkaç figür:** aynı modeli başka bir animasyon ya da kareyle, **farklı bir klasör adıyla** (örneğin Veera_Idle, Veera_Fly) ve farklı adlarla tekrar çevir. Her klasör oyunda kendi ID'leri, mağaza resmi ve aç/kapa anahtarı olan ayrı bir figürdür; kayıtlarda birbirine karışmaz. Aynı klasör adı yeni figür eklemez, önceki paketin üzerine yazar. Toplu pencerede aynı dosya bittikten sonra tekrar eklenebilir (klasöre _2, _3 ... eklenir); her birine kendi adını ve pozunu **Düzenle...** ile ver. Her poz diskte tam bir figür kadar yer kaplar (kendi model.skin ve dokuları).
- **İpuçları:** yürüme ya da bekleme döngülerinin ilk ve son kareleri genelde düz ayakta duruştur; bir hareketin ortasındaki an çoğu zaman daha canlı görünür. Önizlemede ayaklara bak: seçilen karenin en alt noktası rafa oturur, yani zıplama karesi havada değil, rafta duruyormuş gibi gösterilir.

### Parçalar: istemediğin parçaları çıkarmak

Birçok modelde figür için istemeyeceğin parçalar olur: parlama efektleri, ışık halkaları, arka plan levhaları, fazladan bir silah, ayakların dibinde bir tabela. Formdaki **Parçalar** listesi modelin bütün parçalarını gösterir ve istediğini çıkarmanı sağlar.

- **Parça nedir?** Modelin bir malzemesi ve onu kullanan bütün parçalar (adlar model dosyasından gelir, ör. *Body*, *Hair*, *Weapon*, oyun modellerinde efektler için *EF_...*). Liste her parçayı üçgen sayısıyla, en büyüğü üstte gösterir.
- **Parçayı çıkarmak:** üzerine tıkla. Tik işareti çizgiye döner, satır grileşir ve notta *çıkarıldı* yazar. Tekrar tıklayınca geri gelir. Önizleme hemen onsuz yeniden çizilir.
- **Çıkarılan parça neyi değiştirir:** figüre hiç yazılmaz (gizlenmez, gerçekten yoktur), mağaza resminde çizilmez ve boyuta, ortalamaya ve yere oturmaya katılmaz. Yani dev bir efekt levhasını çıkarınca figür gerçek boyunu alır.
- **Kendiliğinden çıkarılanlar** (not: *görünmez / efekt*): görünmeyen saydam parçalar (renk saydamlığı neredeyse 0); kesme sınırının altında kalan kesik (cut-out) parçalar; dokusu olmayan, yarıdan fazla saydam ya da dokusu kaybolunca kalan düz gri renkteki saydam parçalar (oyun modellerindeki efekt kartları: ışık halkaları, nota işaretleri, parlama yüzeyleri).
- **Asla kendiliğinden çıkarılmayanlar:** dokusu olan saydam parçalar (dantel, tül, saç uçları, kirpikler), cam ve bütün opak parçalar. Modelin bütün parçaları saydamsa hiçbir şey çıkarılmaz, model asla boş kalmaz.
- **"Gövdeden uzak" işareti:** ana gövdenin çevresine (dinlenme pozunda) hiç değmeyen parçalar. Bu sadece bir ipucudur, parça kalır; çünkü kanatlar, silahlar, küpeler ve kaideler de ayrı parçalardır. Gerçekten istenmiyorsa (arka plan levhası, havada duran efekt) elle çıkar.
- Listenin altındaki **Otomatik** kendiliğinden yapılan seçime geri döner. En az bir parça her zaman kalır.
- **Nereye kaydedilir:** figure.json çıkarılan parçaları **maker > hiddenParts** altında listeler. Seçimi sonradan değiştirmek için figürü yeniden yap.
- **Toplu:** her model kendiliğinden yapılan seçimle başlar; parçalarını değiştirmek için satırı seçip **Düzenle...**'ye bas.
- **Komut satırı:** **--hide "Weapon,EF_"** adında virgülle ayrılmış kelimelerden biri geçen bütün parçaları çıkarır (büyük / küçük harf fark etmez); **--show "EF_09"** kendiliğinden çıkarılan parçaları geri alır. Çıktı hangi parçaların çıkarıldığını yazar.

### Model kuralları

- İskeletli (animasyonlu) ya da düz model olabilir. Animasyon varsa bir karesi poz olur: animasyonu Poz kısmından, kareyi önizlemenin altındaki kaydırıcıdan seç.
- Dokular modelin içinde ya da yanında olmalı.
- Hâlâ yatıyor ya da ters mi? Formdaki önizlemede fareyle düzelt ya da formda **Modelin yukarı ekseni** kısmından modelin gerçek yukarı eksenini seç (Otomatik, Y = olduğu gibi, Z, -Z, X, -X, -Y) ya da komut satırında **--up Z** kullan. İskeletsiz toplu pozlar (diz çökmüş, çömelmiş) her zaman bulunamayabilir.
- Hâlâ sırtını mı dönüyor? **figure.json** dosyasını aç ve **"rotationY"** değerine 180 ekle ya da çıkar.
- **Modelin lisansı:** sadece izin veren modellerin paketlerini paylaş (ör. CC-BY: yazar adı verilir; CC-BY-NC: sadece ücretsiz paylaşım). license.txt pakette kalmalı. Sketchfab "Standard" / editoryal lisansları genelde paylaşıma izin vermez.

### İleri seviye (komut satırı)

```bat
FigureMaker.exe model.glb --no-form --name Ad --en "English" --tr "Türkçe" --pose Anim --pose-time 0 --up auto --budget 60000 --licence "Ad" --licence-price 1000 --licence-level 1 --licence-logo logo.png --hide "Weapon,EF_" --install
FigureMaker.exe klasor_ya_da_model1 model2 ... --no-form --install
```

- **--no-form** - pencere açılmaz; figür verilen / varsayılan ayarlarla yapılır.
- **--name** - klasör adı; **--en** / **--tr** - İngilizce / Türkçe ad.
- **--pose** - animasyon adı (adın bir kısmı yeter; **-** = dinlenme pozu); **--pose-time** - saniye olarak an (en yakın kare, saniyede 30 kare).
- **--up** - modelin yukarı ekseni: auto, Y, Z, -Z, X, -X, -Y.
- **--budget** - model hafifletilmeden önceki en çok üçgen (60000).
- **--licence**, **--licence-price**, **--licence-level**, **--licence-logo** - kendi lisansın (bkz. **Lisans**).
- **--hide** / **--show** - parça çıkar / kendiliğinden çıkarılanları geri al (bkz. **Parçalar**).
- **--install** - figürü oyunun UserData\FigureShop klasörüne de kopyala.

### Değişiklikler

- **1.4.0**
   - **Yeni - Parçalar listesi** formda: modelin bütün parçaları üçgen sayılarıyla; tıklanan parça figüre alınmaz, tekrar tıklayınca geri gelir; önizleme hemen güncellenir. Gövdeden uzak parçalar işaretlenir, **Otomatik** seçimi sıfırlar. Toplu pencerede (**Düzenle...**) ve komut satırında (**--hide**, **--show**) da var. figure.json çıkarılan parçaları maker > hiddenParts altında listeler.
   - **Yeni - görünmeyen ve efekt parçaları kendiliğinden çıkarılır**: tamamen saydam parçalar ve dokusuz efekt kartları (oyun modellerindeki ışık halkaları, nota işaretleri, parlama yüzeyleri). Önceden boyuta katılıyorlardı; figürün kendisi rafta çok küçük çıkıyor, mağaza resminde gri levhalar görünüyordu.
   - **Düzeltme - kemik kemik bölünmüş animasyonlar birleştirilir** (ör. *Bone_Bow01|Come|Base Layer* + *Body|Come|Base Layer* = *Come*). Poz listesi kısalır, silah / saç artık dinlenme pozunda geride kalmaz.
   - **Düzeltme - ayakta duran modeller yatırılıyordu**: düz sırtlı ya da pelerinli uzun bir model (ör. Boa Hancock) Z-up sanılıp yatık çıkıyordu. Eninden ve derinliğinden belirgin şekilde uzun model artık ayakta kalır.
   - **Değişiklik - varsayılan poz** varsa bekleme animasyonudur (idle / stand / wait / breath), toplu pencerede de. Oyun modelleri çoğu zaman silahın uçarak geldiği bir giriş animasyonuyla başlar.
   - **Paketlerin:** 1.3.x ile yapılan figürler için hiçbir şey değişmez; New Figure Pack'in 52 modeli kontrol edildi, aynı çıkıyor (aynı yukarı ekseni, çıkarılan parça yok). FigureShop'un güncellenmesi gerekmez.
- 1.3.1 - renkleri köşe renklerinde saklanan modeller (dokusuz, Sketchfab baskı modellerinde sık) artık bembeyaz çıkmıyor: renkler poligon azaltmadan sonra bir dokuya pişiriliyor (vertex_colors.png).
- 1.3.0 - animasyon karesi seçimi: animasyonlu modellerde önizlemenin altında kare kaydırıcısı var. Poz kısmından animasyonu seç, beğendiğin âna kaydır; figür, mağaza resmi ve baktığı yön o karede yapılır. figure.json bunu **poseFrame** (ve poseTime) olarak saklar; FigureShop 1.3.3+ tam o kareyi kullanır. Komut satırında: --pose Anim --pose-time 1.5 (saniye).
- 1.2.0 - düzeltme: yukarı ekseni Z olan modeller (Blender / 3ds Max / ZBrush OBJ ve bazı Sketchfab modelleri) yüzüstü yatarak, yere bakarak çıkıyordu; yukarı ekseni artık kendiliğinden bulunuyor (iskelet ya da şekil ve kaide) ve formdan (Modelin yukarı ekseni) ya da --up ile elle seçilebiliyor. Formda yeni önizleme: figürü müşterinin göreceği gibi gör, oluşturmadan önce fareyle (ya da 90° düğmeleriyle) döndür veya yatır. Düzeltme: iskeletsiz figürler sırtı dönük çıkıyordu (elle rotationY 180 gerekiyordu); artık müşteriye bakıyorlar. figure.json "maker" altında bulunan yukarı eksenini ve yönü yazar.
- 1.1.0 - toplu pencere: birden çok modeli (ya da klasörü) bırak veya seç, her biri durumu görünerek sırayla çevrilir; lisans bütün kuyruk için bir kez sorulur. Exe'nin üzerine tek dosya sürüklenirse yine tek form açılır. Yenilenmiş, tek dilde yazan pencereler ve dil seçici (English / Türkçe, lang klasörüyle başka diller). Model okunurken ve çevrilirken ilerleme penceresi; arkadaki siyah konsol penceresi gizlenir. Düzeltme: büyük modellerden yapılan figürlerde çıkan beyaz / koyu lekeler, çatlaklar ve yanlış doku parçaları giderildi (poligon azaltma doku dikişlerini ve gövde parçalarını karıştırıyordu); rengi parlama (emissive) dokusunda olan modeller artık simsiyah çıkmıyor. 1.0 ile yapılan figürleri yeniden üret.
- 1.0.0 - ilk sürüm.
