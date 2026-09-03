# Dark Geo Asset Structure Map

## 📊 Overall Statistics
- **Total files in pack**: 2,647
- **Sprite Component files**: 127 PNG files (Icons, Buttons, Frames, Sliders, UI elements)
- **Asset location**: `Assets/Folder_Assets/GUI Kit - Dark Geo/`
- **Status**: Not yet copied to Assets/Resources (needs manual copy)

---

## 📁 Folder Structure

```
GUI Kit - Dark Geo/
├── +Document README+/          (License & usage docs)
├── Prefab/                      (Legacy uGUI prefabs - can ignore for UI Toolkit)
│   ├── Prefabs_Component_ActionText
│   ├── Prefabs_Component_Buttons
│   ├── Prefabs_Component_Frames
│   ├── Prefabs_Component_Popups
│   ├── Prefabs_Component_Sliders
│   ├── Prefabs_Component_UI_Etc
│   ├── Prefabs_DemoScene
├── Preview/                     (Screenshot previews)
├── PSD/                         (Source PSD files)
├── ResourceData/
│   ├── Animation/               (Sprite animation assets)
│   ├── Font/                    (Custom fonts)
│   └── Sprite/
│       ├── Component/           ⭐ **Main UI sprite asset folder**
│       │   ├── ActionText/      (20 files) - Mission/Combo/Victory popups
│       │   ├── Button/          (19 files) - Button backgrounds (5 styles)
│       │   ├── Frame/           (11 files) - Panel/list frames
│       │   ├── Icon_ButtonIcon_(Origin)/  (64x64 base icons)
│       │   ├── Icon_ButtonIcon_(x2)/      (576 files) - Button icons (4 sizes)
│       │   │   ├── 64/          (144 icons) - Small button icons
│       │   │   ├── 128/         (144 icons)
│       │   │   ├── 256/         (144 icons)
│       │   │   └── 512/         (144 icons)
│       │   ├── Icon_ItemIcon_(x2)/        (144 files) - Item/reward icons (4 sizes)
│       │   │   ├── 64/
│       │   │   ├── 128/
│       │   │   ├── 256/
│       │   │   └── 512/
│       │   ├── Popup/           (5 files) - Popup decorations
│       │   ├── Slider/          (33 files) - Progress bars/sliders
│       │   └── UI_Etc/          (39 files) - Toggles, input fields, labels, etc.
│       └── Demo/                (Demo scene background/icon assets)
```

---

## 🎨 Sprite Components Breakdown

### 1️⃣ **Button Styles** (19 files)
**Path**: `ResourceData/Sprite/Component/Button/`

| Style | Files | Notes |
|-------|-------|-------|
| Button00 | `Button00_n.png`, `Button00_s.png` | Small button (normal/selected) |
| Button01 | `Button01_d.png`, `Button01_n.png`, `Button01_s.png`, `Button01_white.png` | Medium button (4 variants) |
| Button02 | `Button02_n.png`, `Button02_s.png`, `Button02_white.png` | Large button (3 variants) |
| Button03 | `Button03_d.png`, `Button03_n.png`, `Button03_s.png`, `Button03_white.png` | XL button (4 variants) |
| Button04 | `Button04_d.png`, `Button04_n.png`, `Button04_s.png`, `Button04_white.png` | Square button (4 variants) |
| Button05 | `Button05.png` | Special/icon button |
| Settings | `Frame_setting_language_bg.png` | Settings panel background |

**Usage**: Tab buttons, action buttons, navigation buttons
**Recommended**: Use Button02 (large) for main actions, Button01 for secondary

---

### 2️⃣ **Frame/Panel Backgrounds** (11 files)
**Path**: `ResourceData/Sprite/Component/Frame/`

| File | Use Case |
|------|----------|
| `Frame00.png` | Main panel background |
| `TopFrame.png` | Header/title area frame |
| `ItemFrame_n.png` | Item slot frame (normal) |
| `ItemFrame_s.png` | Item slot frame (selected) |
| `ListFrame00.png` | List container frame |
| `ListFrame01_n.png` | List item frame (normal) |
| `ListFrame01_s.png` | List item frame (selected) |
| `StageFrame_n.png` | Stage/level frame (normal) |
| `StageFrame_s.png` | Stage/level frame (selected) |
| `LineFrame00_Left.png` | Left border decoration |
| `LineFrame00_Right.png` | Right border decoration |

**Usage**: Panel backgrounds, list containers, item slots
**Recommended**: `Frame00.png` for main panels, `ListFrame00.png` for scrollable lists

---

### 3️⃣ **Button Icons** (576 files across 4 sizes)
**Path**: `ResourceData/Sprite/Component/Icon_ButtonIcon_(x2)/`

**Available sizes**: 64px, 128px, 256px, 512px (x2 resolution)

**Icon categories** (sample names):
- Game controls: `backward`, `forward`, `play`, `pause`, `stop`
- Gameplay: `bomb`, `shield`, `sword`, `armor`, `key`, `lock`
- UI: `menu`, `settings`, `close`, `check`, `x`, `arrow`
- Items: `coin`, `gem`, `potion`, `scroll`, `crown`, `medal`
- Status: `target`, `alert`, `warning`, `info`, `question`
- Buildings: `castle`, `shop`, `house`, `tower`, `gate`
- Calendar/Time: `calendar`, `clock`, `timer`, `hourglass`
- Misc: `camera`, `message`, `bell`, `flag`, `star`

**Pattern**: `{name}.png` (normal) + `{name}_f.png` (focus/highlighted)

**Recommended**: Use 128px for HUD/TabBar, 64px for status icons, 256px for detailed dialogs

---

### 4️⃣ **Item/Reward Icons** (144 files across 4 sizes)
**Path**: `ResourceData/Sprite/Component/Icon_ItemIcon_(x2)/`

**Available sizes**: 64px, 128px, 256px, 512px

**Icon list** (sample):
- Resources: `icon_coin`, `icon_gem_pink`, `icon_gem_white`, `icon_energy_*` (4 colors)
- Equipment: `icon_boots`, `icon_key`, `icon_crown`, `icon_medal_gold/silver/bronze`
- Combat: `icon_bomb`, `icon_life_red`, `icon_life_gray`
- Misc: `icon_feather`, `icon_calendar`, `icon_message`

**Pattern**: `icon_{name}.png` (single file per icon, not variants)

**Recommended**: Use 128px for inventory/rewards, 256px for detail view, 64px for status bar icons

---

### 5️⃣ **Slider/Progress Bar** (33 files)
**Path**: `ResourceData/Sprite/Component/Slider/`

| Component | Files |
|-----------|-------|
| Slider00 (Simple horizontal) | Fill, Fill_Area, Frame, White variants |
| Slider01 (Bordered) | Fill, FillArea, Frame, InnerFrame, White variants |
| Slider02 (Colored fills) | 6 colors (Blue/Brown/Green/Orange/Red/Yellow), Frame, Icons, White variants |
| Slider03 (Dotted/segmented) | Frame, Point_Off, Point_On, White variants |

**Color fill variants**: Blue, Brown, Green, Orange, Red, Yellow
**Special icons**: Bomb, Boots, Energy, Key, Potion (blue/red), Time

**Recommended**: Slider02 for HP/MP/exp (Red for HP), Slider01 for progress bars, Slider03 for level indicators

---

### 6️⃣ **UI Elements** (39 files)
**Path**: `ResourceData/Sprite/Component/UI_Etc/`

| Category | Files |
|----------|-------|
| **Input Field** | `InputField_n.png` (normal), `InputField_s.png` (selected) |
| **Labels** | `Label_00.png`, `Label_01.png`, `Label_02.png`, `Label_03.png` (4 styles) |
| **Toggles** | Check/Radio boxes: Diamond, Hexagon, Octagon, Square (on/off pairs) = 16 files |
| **Toggle Switch** | `Toggle_Switch_Frame.png`, `Handle_Off.png`, `Handle_On.png` |
| **Pagination** | `PageNavi_on.png`, `PageNavi_off.png`, `PageNavi_Line.png` |
| **Status Icons** | `Status_Icon_Star.png`, `Status_Icon_Coin.png`, `Status_Icon_Gem.png`, `Status_Icon_Life*` |
| **Status Frames** | `Status_Frame_Large.png`, `Status_Frame_Small.png`, `Status_Btn_Add_n.png`, `Status_Btn_Add_f.png` |
| **Notifications** | `Notify_count_bg.png` (badge background) |

**Recommended**: Use toggle variants for equipment equip buttons, `Notify_count_bg.png` for red notification badges

---

### 7️⃣ **Action Text** (20 files)
**Path**: `ResourceData/Sprite/Component/ActionText/`

**Combat feedback popups**:
- `ActionText_Victory.png` + `ActionText_Victory_Icon.png`
- `ActionText_Defeat.png`
- `ActionText_Good.png`, `ActionText_Great.png`, `ActionText_Wow.png`
- `ActionText_Go.png`, `ActionText_Ready.png`
- `ActionText_Combo.png`
- `ActionText_LevelUp.png` + 2 icon variants
- `ActionText_MissionClear.png` + icon
- `ActionText_BonusTime.png` + icon
- `ActionText_GameOver.png`, `ActionText_Skull.png`
- `Deco_Large.png`, `Deco_Small.png` (border decorations)

**Usage**: Splash text notifications during combat/missions

---

### 8️⃣ **Popup Backgrounds** (5 files)
**Path**: `ResourceData/Sprite/Component/Popup/`

Generic popup/modal backgrounds for dialogs

---

## 🔍 Icon Name Reference (Key Assets)

### Most Useful Button Icons (for HUD/TabBar)
```
Category icons:
- category, menu, settings, close, back, forward
- info, question, warning, alert

Character/Game:
- person, team, guild, crown, ranking
- star, heart, shield, sword, armor
- map, dungeon, shop, castle, tower

Resources:
- coin, gem, key, hourglass, bell
- bag, inventory, backpack

Actions:
- play, pause, stop, refresh, reload
- check, x, arrow_right, arrow_left
- plus, minus, settings, close

Combat/Status:
- target, lock, bomb, fire, ice
- heal, potion, energy, buff, debuff
```

### Most Useful Item Icons
```
Resources: icon_coin, icon_gem_pink, icon_gem_white
Energy: icon_energy_blue, icon_energy_green, icon_energy_purple, icon_energy_yellow
Rewards: icon_medal_gold, icon_medal_silver, icon_medal_bronze, icon_crown
Essences: icon_feather, icon_key, icon_boots
```

---

## 📌 Recommended Resource Structure (for mkLike)

```
Assets/Resources/
├── DarkGeo/           ⭐ New folder (copy here)
│   ├── Icons/
│   │   ├── ButtonIcons/       (Copy 128px size)
│   │   └── ItemIcons/         (Copy 128px size)
│   ├── Frames/                (Copy all Frame/*.png)
│   ├── Buttons/               (Copy all Button/*.png)
│   ├── Sliders/               (Copy all Slider/*.png)
│   ├── UIElements/            (Copy all UI_Etc/*.png)
│   └── ActionText/            (Copy all ActionText/*.png)
```

---

## 🚀 Implementation Priority

### Phase 1 (Quick Wins)
- [ ] Copy ButtonIcons (128px) to Resources for HUD icons
- [ ] Copy ItemIcons (128px) for inventory/rewards display
- [ ] Update TabBar icons using dark geo button icons

### Phase 2 (UI Polish)
- [ ] Copy Frame assets for panel backgrounds (USS background-image)
- [ ] Copy Button styles for all buttons (experimental, may need USS tweaks)
- [ ] Copy Slider styles for HP/MP/exp bars

### Phase 3 (Complete UI Overhaul)
- [ ] Copy all UI_Etc (toggles, input fields) for remaining uGUI panels
- [ ] Replace ActionText sprites in combat system
- [ ] Integrate popup backgrounds

---

## ⚠️ Important Notes

1. **No Resources copy yet** — Dark Geo is only in `Folder_Assets/`; needs manual copy to `Assets/Resources/DarkGeo/`
2. **Icon naming**: All button icons have normal + focused variants (`name.png` vs `name_f.png`)
3. **Size flexibility**: Icons available in 4 sizes; choose appropriate resolution
4. **uGUI vs UI Toolkit**: Dark Geo includes uGUI prefabs (can ignore), focus on raw sprites
5. **Sprite slicing**: Some sprites (buttons, frames) may need slice/border settings in import

---

## 🔗 Paths for Direct Use

**Most frequently needed paths**:
```
Button Icons (128px):
Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Icon_ButtonIcon_(x2)/128/

Item Icons (128px):
Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Icon_ItemIcon_(x2)/128/

Frames:
Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Frame/

Buttons:
Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Button/

Sliders:
Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Slider/

UI Elements:
Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/UI_Etc/
```

