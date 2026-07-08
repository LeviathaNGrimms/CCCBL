# CCCBL Content Pack Author Guide

**LeviathaN's Custom Community Center Bundles Loader**
Mod ID: `LeviathaN.CCCBL`

This guide explains how to create a content pack that adds your own custom Community Center bundles to CCCBL, making them selectable by users through the in-game GMCM menu.

---

## Folder Structure

A CCCBL content pack is a folder placed inside the player's `Mods` directory. It requires exactly two files:

```
[CCCBL] Your Pack Name/
├── manifest.json
└── content.json
```

The `[CCCBL]` prefix in the folder name is a convention that helps users identify what the pack is for. The name of the folder itself doesn't matter to CCCBL.

Optionally, you may also include sprite assets:

```
[CCCBL] Your Pack Name/
├── manifest.json
├── content.json
└── assets/
    └── bundleicons.png (Your custom bundle icon sprite sheet)
```

---

## manifest.json

Standard SMAPI manifest. The only required field specific to CCCBL is `ContentPackFor`.

```json
{
    "Name": "My Custom Bundles",
    "Author": "YourName",
    "Version": "1.0.0",
    "Description": "A short description of what your bundle pack does.",
    "UniqueID": "YourName.MyCCCBLPack",
    "ContentPackFor": {
        "UniqueID": "LeviathaN.CCCBL"
    },
    "UpdateKeys": []
}
```

The `Name` field is what appears in the GMCM dropdown for users. Keep it clear and descriptive.

---

## content.json

This file defines your bundles and options.

### Minimal example

```json
{
    "RequireCompletionistMode": false,
    "Bundles": {
        "Pantry/0": "Spring Crops/O 465 20/24 1 0 188 1 0 190 1 0 192 1 0/0",
        "Crafts Room/13": "Spring Foraging/O 495 30/16 1 0 18 1 0 20 1 0 22 1 0/0"
    }
}
```

### Fields

**`RequireCompletionistMode`** *(boolean, default `false`)*

Set to `true` if your pack uses extra bundle slots beyond the vanilla 30, specifically the additional ones that Completionist Mode enables: Pantry/37-39, Crafts Room/43-44-52, Fish Tank/12-48-49, Boiler Room/45-46-50-51, and Bulletin Board/40-42-53. When `true`, CCCBL automatically enables Completionist Mode while your pack is selected, and the GMCM tooltip explains to the user that it cannot be disabled.

**`Bundles`** *(object, required)*

A dictionary of bundle entries to apply. You only need to include the bundles you want to change. Anything you leave out keeps its existing value.

---

## Bundle Keys

Keys can be written in two formats.

**Room/ID format**: Directly targets a specific bundle slot by room name and number. This is the recommended format as it is unambiguous. Using this format will work in 100% of cases.

```
"Pantry/0"              "Pantry/1"              "Pantry/2"
"Pantry/3"              "Pantry/4"              "Pantry/5"
"Crafts Room/13"        "Crafts Room/14"        "Crafts Room/15"
"Crafts Room/16"        "Crafts Room/17"        "Crafts Room/19"
"Fish Tank/6"           "Fish Tank/7"           "Fish Tank/8"
"Fish Tank/9"           "Fish Tank/10"          "Fish Tank/11"
"Boiler Room/20"        "Boiler Room/21"        "Boiler Room/22"
"Vault/23"              "Vault/24"              "Vault/25"           "Vault/26"
"Bulletin Board/31"     "Bulletin Board/32"     "Bulletin Board/33"
"Bulletin Board/34"     "Bulletin Board/35"
"Abandoned Joja Mart/36"
```

**Bundle name format**: Matches whichever slot currently has that display name. Useful when you want to replace a bundle regardless of its exact ID.

**(I had mixed results while using this format, and not all custom bundles worked while using it, so I STRONGLY recommend using the Room/ID format over this one.)**

```json
"Spring Crops": "...",
"River Fish": "..."
```

---

## Valid Bundle IDs

### Vanilla slots (Always available)

| Room | Slot IDs |
|---|---|
| Pantry | 0, 1, 2, 3, 4, 5 |
| Crafts Room | 13, 14, 15, 16, 17, 19 |
| Fish Tank | 6, 7, 8, 9, 10, 11 |
| Boiler Room | 20, 21, 22 |
| Vault | 23, 24, 25, 26 |
| Bulletin Board | 31, 32, 33, 34, 35 |
| Abandoned Joja Mart | 36 |

### Extra slots (Completionist Mode only)

Only use these if `RequireCompletionistMode` is `true`. These IDs are fixed, so do not invent new ones. There are no other valid IDs beyond these.

| Room | Extra Slot IDs |
|---|---|
| Pantry | 37, 38, 39 |
| Crafts Room | 43, 44, 52 |
| Fish Tank | 12, 48, 49 |
| Boiler Room | 45, 46, 50, 51 |
| Bulletin Board | 40, 42, 53 |

---

## Bundle Data String Format

Each value in `Bundles` follows this format:

```
Name/Reward/Items/Color
Name/Reward/Items/Color/RequiredCount
Name/Reward/Items/Color/RequiredCount/SpriteReference
```

Fields after `Color` are optional. If you skip `RequiredCount` but want to specify a `SpriteReference`, use an empty field: `Name/Reward/Items/Color//SpriteReference`.

### Name

The display name shown in the Community Center UI.

### Reward

What the player receives for completing the bundle. Format: `TypeCode ItemId Quantity`.

| Code | Type | Example |
|---|---|---|
| `O` | Object / item | `O 465 20` = 20 Speed-Gro |
| `BO` | Big craftable | `BO 25 1` = 1 Preserves Jar |
| `R` | Ring | `R 517 1` = 1 Glow Ring |
| `F` | Furniture | `F 1675 1` |
| `C` | Clothing | `C 1157 1` |
| `-1` | Gold | `-1 2500 2500` = 2,500g (amount written twice) |

Item IDs can be numeric (legacy) or string names from SDV 1.6 (`Carrot`, `Broccoli`, `SummerSquash`).

### Items

Space-separated groups of three values per item: `ItemId Quantity Quality`

Quality: `0` = Normal, `1` = Silver, `2` = Gold, `4` = Iridium.

```
24 1 0 188 1 0 190 1 0 192 1 0
```

For modded items, use their qualified item ID:
```
FlashShifter.StardewValleyExpandedCP_Cucumber 1 0
Morghoula.AlchemistryCP_Hemlock 5 2
```

### Color

A number 0 - 6 controlling the bundle's color in the UI.

| Number | Color |
|---|---|
| 0 | Green |
| 1 | Purple |
| 2 | Orange |
| 3 | Yellow |
| 4 | Red |
| 5 | Blue |
| 6 | Teal |

### RequiredCount *(optional)*

How many items from the list the player must donate. Omit or leave empty to require all of them.

```
"Pantry/3": "Quality Crops/BO 15 1/24 5 2 188 5 2 254 5 2 260 5 2 276 5 2/6/4"
```

The `/4` at the end means any 4 of the 5 listed items are required.

### SpriteReference *(optional)*

The icon shown for this bundle in the Community Center. See the Icons section below for full details. If omitted, CCCBL automatically uses its built-in `bundleicon_default.png`.

---

## Icons

### How icons work

CCCBL handles bundle icons in the following priority order:

1. **Pack-specified icon**: If your bundle data string includes a sprite reference as field 6, that icon is used.
2. **CCCBL default icon**: If no sprite reference is present, CCCBL automatically shows its own `bundleicon_default.png` for that bundle.

You do not need to provide icons at all if you are happy with the default. Icons are purely optional.

### Providing custom icons

Place your icon sprite sheet at `assets/bundleicons.png` inside your content pack folder. The sheet must use **32×32 pixels per frame**, arranged left to right. Multiple rows are supported, as the game calculates columns as `sheetWidth / 32`.

```
assets/bundleicons.png  (e.g. 96×32 for 3 icons in one row)

┌────────┬────────┬────────┐
│ frame0 │ frame1 │ frame2 │   each cell = 32×32 px
└────────┴────────┴────────┘
```

To use a custom icon, add the sprite reference as the last field of your bundle data string:

```
Mods\{YourPackUniqueId}\bundleicons:0
```

where `:0` is the zero-based frame index.

Full example with a custom icon (frame 1 from the sheet):

```json
"Crafts Room/13": "Spring Crops/O 486 30/24 1 0 188 1 0/0//Mods\\YourName.MyCCCBLPack\\bundleicons:1"
```

Note the `//` before the sprite reference. The `RequiredCount` field is empty (all items required), so two consecutive slashes are needed.

### Notes on the sprite path

- The path after `Mods\` must exactly match your pack's `UniqueID` in `manifest.json`
- The filename (`bundleicons`) must match the PNG file name without the extension
- You can use any filename you like, not just `bundleicons`, as long as the file exists at `assets/{filename}.png` in your pack folder
- Frame index `:0` is the leftmost frame in the first row; counting continues left to right, then top to bottom

---

## Complete Example

**Folder layout:**
```
[CCCBL] Metalworker's Pantry/
├── manifest.json
├── content.json
└── assets/
    └── bundleicons.png    (96×32 - Three icons)
```

**manifest.json:**
```json
{
    "Name": "Metalworker's Pantry",
    "Author": "YourName",
    "Version": "1.0.0",
    "Description": "Replaces Pantry bundles with metal and mining themed requirements.",
    "UniqueID": "YourName.MetalworkersPantry",
    "ContentPackFor": {
        "UniqueID": "LeviathaN.CCCBL"
    },
    "UpdateKeys": []
}
```

**content.json:**
```json
{
    "RequireCompletionistMode": false,
    "Bundles": {
        "Pantry/0": "Copper Bundle/BO 13 1/334 10 0/4//Mods\\YourName.MetalworkersPantry\\bundleicons:0",
        "Pantry/1": "Iron Bundle/BO 13 1/335 10 0/3//Mods\\YourName.MetalworkersPantry\\bundleicons:1",
        "Pantry/2": "Gold Bundle/BO 13 1/336 5 0/2//Mods\\YourName.MetalworkersPantry\\bundleicons:2",
        "Pantry/3": "Quality Metal/BO 15 1/334 5 2 335 5 2 336 5 2/6/2",
        "Pantry/4": "Iridium Bundle/BO 13 1/337 1 0/1",
        "Pantry/5": "Refined Bundle/BO 12 1/334 5 0 335 5 0 336 5 0 337 1 0/5"
    }
}
```

In this example, the first three bundles use custom icons from frames 0, 1, and 2 of `bundleicons.png`. The last three bundles have no sprite reference, so they automatically use CCCBL's `bundleicon_default.png`.

---

## Tips and Common Mistakes

**Only include bundles you are changing.** Anything you leave out of the `Bundles` dictionary keeps its existing value.

**Do not invent bundle IDs.** Only use the IDs listed in the Valid Bundle IDs section. Any ID not in that list has no physical slot in the room layout and will cause the Community Center to fail to load.

**RequiredCount and the double slash.** If `RequiredCount` is empty but you want to specify a `SpriteReference`, you need two consecutive slashes: `Name/Reward/Items/Color//SpriteReference`. Missing the double slash will shift fields and cause unexpected behaviour.

**Do not end every item string with a quantity.** Each item entry is `ItemId Quantity Quality` — three values. A common mistake is writing `ItemId Quantity` and forgetting the quality, which causes items to be misread.

**Set `RequireCompletionistMode: true` if you use any extra slot IDs.** Slots 37 and above only exist when Completionist Mode is on. Referencing them without the flag will cause a crash at Community Center load time.

**Preferably, test on a new save.** Community Center room layouts are determined when the save is first created by default. Extra bundle slots from Completionist Mode are appearing correctly in a save started before that mode was enabled, and I didn't have trouble completing the community center, *but just to be sure, test it on a new save*.

**Item IDs.** Numeric IDs still work but SDV 1.6 string names (`Carrot`, `Broccoli`, `SummerSquash`) are preferred for vanilla items. For modded items, use the format `Author.ModId_ItemName` as the item's mod provides it.

**The sprite path must match your UniqueID exactly.** `Mods\YourName.MyPack\bundleicons:0` will only work if your manifest `UniqueID` is `YourName.MyPack`. A mismatch means the game requests a path CCCBL doesn't intercept, and the icon will appear blank.
