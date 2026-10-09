# UI 提示词 v0.1 — 视觉资产生成契约

> 范围：本轮只覆盖两个组件：**Button**、**PopupDialog**（含标题栏 + 内容区 + 关闭按钮）。
> 用途：喂给图像生成模型（见 [AI生成模型清单 v0.1](docs/AI生成模型清单_v0.1.md)），产出可直接切图的 pixel-art 资产。
> 视觉语言：与现有 `scripts/Ui/Components/Jrpg*.cs` + `scripts/Ui/Theme/JrpgStyleFactory.cs` 一致——JRPG 像素、深色木纹/石纹边框、米黄羊皮纸底、冷金高光。

## 0. 全局约束（所有资产必须遵守）

- **风格**：16-bit JRPG pixel art，类似 SNES 时代《火焰之纹章》《皇家骑士团》UI。**禁止**平滑渐变、禁止矢量 UI 感、禁止现代拟物。
- **色板**（每项只允许以下色阶；不要自由色）：
  - 边框深棕 `#3a2418` / `#5a3a26` / `#8a5a32`
  - 木纹/石纹灰 `#2a2a2e` / `#3c3c44` / `#56565e`
  - 底纸米黄 `#e8d8a8` / `#d8c890` / `#b8a878`
  - 高光冷金 `#e8c878` / `#f0d898`
  - 危险红 `#a03828` / `#d04838`
  - 文字深 `#1a1410` / `#3a2818`
- **像素栅格**：所有元素对齐到 **2 像素栅格**（奇数尺寸会糊）。最小可点击单元 24×24，按钮高度建议 32。
- **描边**：所有元素外缘 1px 深棕硬描边，禁止阴影、禁止 anti-alias 边缘过渡。
- **状态集**：每个组件必须出 **4 个状态**：default / hover / pressed / disabled（缺一即视为不合格）。
- **交付格式**：PNG，**透明背景**，单组件多状态横向拼版（default → hover → pressed → disabled），整版宽度允许但单状态切片干净。
- **禁止**：UI 上出现任何汉字、英文、应用名（按钮文字由运行时 UI 控件绘制，不在贴图里）。
- **可访问性**：hover/disabled 状态除颜色外必须有形状差异（如按钮：hover 边框变金，pressed 内缩 1px，disabled 灰化 + 半透明 + 边框变虚线样）。

---

## 1. Button

### 1.1 用途
- 工具栏按钮（`ToolStrip`）
- 调色板按钮（`BuildPaletteSection`、`BuildingButton`）
- 对话框内按钮（OK / Cancel / Apply 等）

### 1.2 规格
- **尺寸档**（出 3 档，每档都要 4 状态）：
  - `S`：24×24（仅图标，工具栏用）
  - `M`：96×32（图标 + 短文本，工具条/对话框主按钮）
  - `L`：160×40（大对话框主操作按钮）
- **结构**：木纹边框 2px → 内描金线 1px → 米黄底纸 → 居中图标或图标+文字。
- **图标槽**：左边 24×24 像素区域留给图标；M/L 档右侧留 8px 文字区。
- **状态差异**：
  - default：边框 `#5a3a26`，底 `#e8d8a8`，内描金 `#e8c878`。
  - hover：边框 `#e8c878`（冷金高亮），底 `#f0d898`，**顶部加 1px 高光线**。
  - pressed：边框 `#3a2418`，**整个内容向内缩 1px**（模拟按下），底 `#b8a878`。
  - disabled：边框 `#56565e`，底 `#3c3c44`（灰化），**整体 50% 不透明度** + 内描金改为虚线样（每 4 像素断 2 像素）。
- **圆角**：4 像素圆角（pixel-step 阶梯式圆角，不是平滑）。

### 1.3 提示词

**英文版**（用于主流模型）：

```
16-bit JRPG pixel art UI button set, dark fantasy theme.

Three size variants stacked vertically, each row showing 4 states
horizontally: default, hover, pressed, disabled.

Size S: 24x24 per cell (icon only). Size M: 96x32 per cell (icon + short
text area, no actual letters). Size L: 160x40 per cell (large action
button).

Strict palette only — no other colors allowed:
- border dark brown #3a2418, mid brown #5a3a26, light brown #8a5a32
- stone gray frame #2a2a2e, #3c3c44, #56565e
- parchment cream #e8d8a8, #d8c890, dim #b8a878
- highlight cold gold #e8c878, #f0d898
- danger red #a03828, #d04838
- text dark #1a1410, #3a2818

Pixel grid: 2px snap. 1px hard outline on every edge. No anti-alias.
No shadows. No gradients. No vectors. SNES-era Fire Emblem / Ogre
Battle look.

States:
- default: brown border #5a3a26, cream fill #e8d8a8, gold inner line.
- hover: gold border #e8c878, brighter fill #f0d898, +1px top highlight.
- pressed: dark border #3a2418, content shifted 1px inward, dim fill
  #b8a878.
- disabled: gray border #56565e, gray fill #3c3c44, 50% opacity, inner
  gold line changed to dashed (4px on, 2px off).

4px pixel-step rounded corners. Transparent background. Horizontal
sheets per size, each sheet is the 4 states side-by-side. No text, no
letters, no icon art — leave icon slot empty (flat parchment).

Output: PNG, transparent background, exact pixel sizes.
```

**中文版**（备用）：

```
16 位 JRPG 像素风 UI 按钮，深色奇幻主题。

三个尺寸档垂直排列，每档横向 4 个状态：default、hover、pressed、disabled。

S 档每格 24x24（仅图标）。M 档每格 96x32（图标+短文字区，不画具体文字）。
L 档每格 160x40（大操作按钮）。

严格色板，禁止其他色：边框深棕 #3a2418/中棕 #5a3a26/浅棕 #8a5a32；
石纹灰 #2a2a2e/#3c3c44/#56565e；底纸米黄 #e8d8a8/#d8c890/暗 #b8a878；
高光冷金 #e8c878/#f0d898；危险红 #a03828/#d04838；文字深 #1a1410/#3a2818。

2 像素栅格对齐，每条外缘 1px 硬描边，禁止抗锯齿、阴影、渐变、矢量感。
SNES 时代《火焰之纹章》《皇家骑士团》质感。

default 边框 #5a3a26 底 #e8d8a8 内描金；hover 边框变 #e8c878 底变 #f0d898
顶部加 1px 高光；pressed 边框 #3a2418 内容内缩 1px 底 #b8a878；disabled
边框 #56565e 底 #3c3c44 50% 不透明 内描金改 4 像素断 2 像素虚线样。

4 像素阶梯式圆角。透明背景。每档横向 4 状态拼版。不画任何文字或图标 —
图标位留空（纯米黄底）。输出 PNG 透明背景 严格像素尺寸。
```

---

## 2. PopupDialog

### 2.1 用途
- 通用弹出对话框（确认、错误、表单输入、设置、About）
- 替代目前 `MapSettingsDialog` 临时实现
- 警报与阻塞提示（必须用户响应才能继续）

### 2.2 规格
- **尺寸档**（出 3 档，每档都要 default 一态；hover/pressed 只对内部按钮生效）：
  - `S`：320×200（确认 / 简单消息）
  - `M`：480×320（表单 / 设置）
  - `L`：640×400（多区块 / 帮助）
- **结构分区**（自上而下）：
  1. **标题栏** 32px 高：左侧 16px 留空（不放图标占位），中部留标题文字位（米黄底，运行时画字），右侧 24×24 **关闭按钮槽**（独立资产，见 §2.3）。
  2. **分隔金线** 1px：`#e8c878`，横贯。
  3. **内容区**：米黄底纸 `#e8d8a8`，允许轻微做旧纹理（噪点 ≤ 5% 透明度），不允许内容纹理压字。
  4. **底栏** 48px 高：右对齐放置按钮（OK / Cancel），按钮用 §1 的 `M` 档。
- **边框**：外 4px 深棕 `#3a2418` 硬描边，再内 2px 木纹 `#5a3a26` → 再内 1px 冷金 `#e8c878` 三线包边。
- **装饰角**：四角各加一颗 4×4 冷金铆钉（不是装饰花纹，避免和标题文字抢视线）。
- **背景遮罩**：弹出时主画布需要半透明黑色遮罩 50% 不透明（**遮罩由代码绘制，不在贴图里**）。
- **状态差异**：对话框本体只有 default 一态；内部按钮按 §1 状态集。

### 2.3 关闭按钮（标题栏右上角 X）

独立切图，**必须**和按钮状态集对齐：
- 24×24，4 状态（default / hover / pressed / disabled-虽然弹窗关闭不该禁用，但保持 4 状态统一）。
- default：深棕底 `#3a2418`，白色 / 米黄 X 线 1px。
- hover：底变冷金 `#e8c878`，X 线变深棕 `#3a2418`。
- pressed：底变 `#a03828`（危险红），X 线变米黄。
- disabled：底 `#56565e`，X 线变深灰。

提示词片段（可拼到 Button 同一调用里）：

```
Popup dialog close button (X), 24x24, 4 states horizontal:
default dark brown #3a2418 with cream X stroke; hover gold #e8c878
with dark X; pressed danger red #a03828 with cream X; disabled gray
#56565e with dim X. 1px hard outline, pixel grid 2px snap, transparent
background.
```

### 2.4 提示词

**英文版**：

```
16-bit JRPG pixel art popup dialog set, dark fantasy theme.

Three size variants stacked vertically, each as a complete dialog
window with all regions visible:
- S: 320x200 (simple confirm)
- M: 480x320 (form / settings)
- L: 640x400 (multi-section)

Regions top-to-bottom:
1. Title bar 32px tall: parchment cream fill #e8d8a8, leaves 16px left
   padding and a 24x24 close button slot at top-right (close button
   delivered as a separate 4-state sprite).
2. 1px gold separator line #e8c878 across full width.
3. Content area: parchment cream #e8d8a8 with subtle aging noise
   (≤5% opacity dots, no patterns that fight text).
4. Bottom bar 48px tall: right-aligned button slots (2 buttons, use
   Button M spec from earlier).

Triple border: outer 4px dark brown #3a2418 hard stroke, then 2px wood
brown #5a3a26, then 1px cold gold #e8c878 inner line.

Four 4x4 gold rivets at the corners (#e8c878), no corner ornaments.

Strict palette only (same as Button spec). 2px pixel grid. 1px hard
outlines. No anti-alias. No shadows. No gradients. No text, no
letters, no icons inside the dialog — leave title area empty, leave
content area empty (just parchment), leave button slots as flat
parchment circles with the Button M border drawing around them.

Transparent background. PNG, exact pixel sizes.

Also deliver separately: close X button 24x24 in 4 states
(default/hover/pressed/disabled) as described.
```

**中文版**：

```
16 位 JRPG 像素风弹出对话框，深色奇幻主题。

三个尺寸档垂直堆叠，每档画出完整对话框（所有分区都可见）：
S 320x200（简单确认） / M 480x320（表单设置） / L 640x400（多区块）。

自上而下分区：
1. 标题栏 32px 高：米黄底 #e8d8a8，左侧留 16px 内边距，右侧留 24x24 关闭
   按钮槽（关闭按钮单独交付 4 状态切片）。
2. 1px 冷金分隔线 #e8c878 横贯。
3. 内容区：米黄底 #e8d8a8，允许极轻微做旧噪点（≤5% 不透明点状，不可有
   压字的纹理）。
4. 底栏 48px 高：右对齐两个按钮槽（按钮复用 Button M 档）。

三层包边：外 4px 深棕 #3a2418 硬描边 → 内 2px 木纹棕 #5a3a26 → 再内 1px
冷金 #e8c878。

四角各一颗 4x4 冷金铆钉 #e8c878，禁止角部装饰花纹。

严格色板（同 Button 规范）。2 像素栅格。1px 硬描边。禁止抗锯齿/阴影/
渐变。标题区、内容区、按钮槽都不画任何文字或图标 — 标题位留空米黄底，
按钮槽留空米黄圆（周围画 Button M 档的边框）。

透明背景 PNG 严格像素尺寸。

另单独交付：关闭 X 按钮 24x24 四状态（default/hover/pressed/disabled）。
```

---

## 3. 交付清单（agent 检查项）

生成完成后，agent 应在 [美术资源审计清单 v0.1](docs/美术资源审计清单_v0.1.md) 对应行打勾：

- [ ] `ui/button_S_4states.png` — 4 切片 24×24，横向拼版
- [ ] `ui/button_M_4states.png` — 4 切片 96×32，横向拼版
- [ ] `ui/button_L_4states.png` — 4 切片 160×40，横向拼版
- [ ] `ui/dialog_S.png` — 320×200，标题栏含关闭槽
- [ ] `ui/dialog_M.png` — 480×320
- [ ] `ui/dialog_L.png` — 640×400
- [ ] `ui/dialog_close_4states.png` — 4 切片 24×24

资产落位：[美术资源规范 v0.1 §资源目录](docs/美术资源规范_v0.1.md) 中的 `assets/ui/`。

---

## 4. ComfyUI 否定提示词（negative prompt）

> 喂给 CLIP Text Encode (Negative Prompt) 节点，与 §1.3 / §2.4 的正向提示词**同强度**使用。
> 一份统一文案，Button 与 PopupDialog 共用；与正向提示词严格互补。

```
ugly, deformed, noisy, blurry, low contrast, washed out,
smooth gradients, anti-aliasing, soft shadows, drop shadow,
glow, bloom, lens flare, chromatic aberration,
modern UI, material design, flat design, neumorphism, glassmorphism,
web UI, mobile UI, iOS, Android, Windows 11, macOS,
vector art, svg, illustrator, 3d render, octane, blender, unreal engine,
photorealistic, photographic, painting, watercolor, oil painting,
anime, cartoon, chibi, kawaii, cute, mascot,
high saturation, neon, bright red, bright blue, bright green,
pastel colors, rainbow, multicolor,
text, letters, alphabet, chinese characters, japanese characters,
numbers, watermark, signature, logo, brand name,
borderless, no outline, thick outline, double outline,
rounded corners over 8px, perfect circle, ellipse,
busy background, detailed scenery, characters, people, creatures,
swords, shields, magic circles, fantasy ornaments,
christmas, halloween, holiday themed,
low resolution, jpeg artifacts, pixelated edges (blocky jagged),
transparent fill, white fill, black fill
```

### 4.1 分级与使用纪律

- **强否定**（绝不能出现，删整张重出）：
  `modern UI, material design, flat design, vector art, svg, 3d render, photorealistic, anime, cartoon, text, letters, watermark, smooth gradients, anti-aliasing, soft shadows`
- **弱否定**（出现 1–2 个可容忍，但建议重出）：
  `busy background, glow, bloom, neon, high saturation, low resolution`
- **风格锁定项**（与正向提示词中的「16-bit JRPG pixel art, SNES-era Fire Emblem」**互锁**——正向提了"SNES 时代"，负向必须封死"现代 / 矢量 / 拟物"，否则模型会模糊掉风格）：
  正向提 `pixel art` ↔ 负向封 `vector art, svg, 3d render`
  正向提 `16-bit` ↔ 负向封 `photorealistic, painting, watercolor`
  正向提 `hard outline` ↔ 负向封 `anti-aliasing, soft shadows, glow`
  正向提 `parchment cream` ↔ 负向封 `neon, pastel colors, white fill`
  正向提 `no text` ↔ 负向封 `text, letters, alphabet, chinese characters, watermark`

### 4.2 节点接入模板（ComfyUI）

```
[CLIP Text Encode (Positive Prompt)] → 喂 §1.3 或 §2.4 英文版
[CLIP Text Encode (Negative Prompt)] → 喂本节文案
                ↓
        [KSampler / CheckpointLoader 等]
                ↓
        输出 PNG → 人工核对 → 进 assets/ui/
```

### 4.3 检查项

生成完成后，逐张过 [美术资源审计清单 v0.1 §13 项](docs/美术资源审计清单_v0.1.md)，重点看：

- [ ] 是否出现文字 / 字母 / 数字（必须重出）
- [ ] 是否有平滑渐变（放大 400% 看边缘过渡）
- [ ] 是否被现代 UI 风格污染（拟态、玻璃、毛玻璃、阴影模糊）
- [ ] 是否出现图标 / 装饰花纹挤占图标位
- [ ] 色板是否在 §0 列出的 6 组色阶内（出现紫色 / 亮蓝 / 青色 / 荧光绿即重出）

---

## 5. 变更记录

- v0.1（2026-XX-XX）：首版，仅含 Button + PopupDialog；附 ComfyUI 否定提示词。
