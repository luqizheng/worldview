# AI 生成模型清单 v0.1

> **离线生成风格化美术资源**所需的 SDXL 模型下载清单。
>
> **用途边界**:本清单服务于"AI 离线生成 → 人工筛选 → 导入 `assets/`"工作流(参考 [美术指导借鉴方案 v0.1 §3](美术指导借鉴方案_v0.1.md) 的 CCGS 视觉总纲方法论);**非运行时依赖**——Godot 工程启动 / 加载世界工程 / 加载示例资源 **不**依赖这些模型。
>
> 适用阶段:v0.1(像素风高清 HD-2D 轻度风格化),目标产出**角色 / 物品图标 / 场景 tile** 三类素材。
>
> **关键纪律**:
> 1. 本清单**只登记外部模型仓库与版本**,具体生成参数(权重、采样器、CFG)属于生成侧工具配置,**不**写入数据契约字段
> 2. 仓库中**不**保存模型权重本体(模型文件 ≥ GB 级,且 AGENTS.md §6.1 明确 `.worldview/` 与 `assets/` 是工程内目录);模型统一放在**工程外的本地缓存目录**(详见 §2)
> 3. 仓库中**只**登记**用途 / 链接 / sha256 校验 / 下载脚本**;**禁止**把 `.safetensors` / `.ckpt` 入版本库
> 4. 国内/国外链接并列,默认走国内镜像(`hf-mirror.com`),海外 CI 走 `huggingface.co`

---

## 1. 模型清单

### 1.1 SDXL 1.0 基础模型

| 项 | 值 |
|---|---|
| 用途 | SDXL 文生图基底;所有 LoRA 与 ControlNet 的依赖根。**不**是 JRPG / HD-2D 风格本身——风格由 §1.1.2 Blue Pencil XL v7 或叠加 LoRA 实现 |
| 仓库 | stabilityai/stable-diffusion-xl-base-1.0 |
| 海外链接 | https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0 |
| 国内镜像 | https://hf-mirror.com/stabilityai/stable-diffusion-xl-base-1.0 |
| 直接文件 | `sd_xl_base_1.0.safetensors`(约 6.94 GB) |
| 国内文件直链 | https://hf-mirror.com/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0.safetensors |
| 推荐 VAE | `madebyollin/sdxl-vae-fp16-fix`(避免紫边),https://huggingface.co/madebyollin/sdxl-vae-fp16-fix |
| 本机实际命名 | `sdxl_vae.safetensors`(见 §2.5.5 文件名差异登记) |
| 必装 | **是**(所有 LoRA/ControlNet 链路的前置) |

#### 1.1.2 Blue Pencil XL v7.0.0(JRPG 风格主模型,**本工程已选**)

| 项 | 值 |
|---|---|
| 用途 | **JRPG 风格主模型**,与 [美术资源规范 §1.1](art_style_1_1) 默认 `art_style=jrpg` 一一对应;直接出 JRPG 美术稿,比 SDXL 基础模型 + 像素 LoRA 的组合更省心 |
| 仓库 | bluepen5805/blue_pencil-XL |
| 海外链接 | https://huggingface.co/bluepen5805/blue_pencil-XL |
| 国内镜像 | https://hf-mirror.com/bluepen5805/blue_pencil-XL |
| 直接文件 | `blue_pencil-XL-v7.0.0.safetensors`(约 6.8 GB) |
| 国内文件直链 | https://hf-mirror.com/bluepen5805/blue_pencil-XL/resolve/main/blue_pencil-XL-v7.0.0.safetensors |
| 安装脚本 | `D:\kudi-project\comfyui-models\install-bluepencil-xl.ps1`(本机已有,跑完自动重启 ComfyUI 容器) |
| 本机实际 | `D:\kudi-project\comfyui-models\checkpoints\blue_pencil_xl_v7.safetensors`(已落地) |
| 推荐用法 | 直接用此模型出图;**不再**需要叠加 `pixel-art-xl` LoRA,模型本身已含 JRPG 风格 |
| 必装 | **是**(JRPG 风格首选;SDXL 基础模型作为后备) |

> **决策记录**:原 §1 路径是 `SDXL 1.0 基础模型 + nerijs/pixel-art-xl LoRA`(需叠加轻风格化);实测发现 Blue Pencil XL v7 是**专用**JRPG 主模型,**一步到位**,更适合本工程 v0.1 默认 `art_style=jrpg`。建议:
> 1. **JRPG 美术稿** → Blue Pencil XL v7 直接出图
> 2. **HD-2D 风 / 轻度风格化(角色 / 物品 / tile)** → SDXL 1.0 + pixel-art-xl LoRA
> 3. **其他风格实验** → SDXL 1.0 + 其他 LoRA

### 1.2 SDXL ControlNet(轻风格化叠加用)

| 项 | 值 |
|---|---|
| 用途 | 控轮廓/深度/姿态,**不抢** LoRA 风格(weight 0.5–0.7) |
| Canny(边缘) | diffusers/controlnet-canny-sdxl-1.0 |
| Canny 海外 | https://huggingface.co/diffusers/controlnet-canny-sdxl-1.0 |
| Canny 国内 | https://hf-mirror.com/diffusers/controlnet-canny-sdxl-1.0 |
| Canny 直接文件 | https://hf-mirror.com/diffusers/controlnet-canny-sdxl-1.0/resolve/main/diffusion_pytorch_model.safetensors |
| Depth(深度图) | diffusers/controlnet-depth-sdxl-1.0 |
| Depth 海外 | https://huggingface.co/diffusers/controlnet-depth-sdxl-1.0 |
| Depth 国内 | https://hf-mirror.com/diffusers/controlnet-depth-sdxl-1.0 |
| Depth 直接文件 | https://hf-mirror.com/diffusers/controlnet-depth-sdxl-1.0/resolve/main/diffusion_pytorch_model.safetensors |
| 官方文档 | https://huggingface.co/docs/diffusers/en/api/pipelines/controlnet_sdxl |
| 必装 | 否(按需;Canny + Depth 二选一或全装) |

### 1.3 像素风 LoRA(HD-2D 轻度风格化)

> 三选一作为**主 LoRA**,其余作为风格对照与备选。
> 共同使用要点(参考各仓库 README):
> - LoRA weight:**0.6–0.8**(高于 1.0 过像素化,违反 [美术资源规范 §1.2 #11](美术资源规范_v0.1.md) 饱和度纪律)
> - ControlNet scale:**0.5–0.7**(同时使用时不压垮 LoRA 风格)
> - 采样:`LCM LoRA + DPM++ SDE / 8 步` 或 `Euler a / 25–30 步`
> - 出图后**下采样 8× + Nearest Neighbor** 得真正像素成品

#### 1.3.1 nerijs/pixel-art-xl(主推,覆盖角色 / 物品 / 场景)

| 项 | 值 |
|---|---|
| 用途 | **轻度风格化像素 LoRA**,覆盖角色 / 物品图标 / 场景 tile 三类 |
| 仓库 | nerijs/pixel-art-xl |
| 国内镜像 | https://hf-mirror.com/nerijs/pixel-art-xl |
| 直接文件 | https://hf-mirror.com/nerijs/pixel-art-xl/resolve/main/pixel-art-xl.safetensors |
| 推荐用法 | 配合 LCM LoRA,8 步,guidance scale 1.5,LoRA strength 1.2 |
| 必装 | **是**(三选一主 LoRA,推荐) |

#### 1.3.2 Pixel Art XL v1.1(Civitai,同源备选)

| 项 | 值 |
|---|---|
| 用途 | nerijs 模型的同源 Civitai 镜像,便于通过 Civitai Web 预览样张再决定权重 |
| 仓库 | https://civitai.com/models/120096/pixel-art-xl |
| 直链 | https://civitai.com/api/download/models/144035?type=Model&format=SafeTensor |
| 国内镜像 | https://civitai.work/models/120096(若主站不可达) |
| 必装 | 否(与 1.3.1 二选一) |

#### 1.3.3 artificialguybr/PixelArtRedmond(MS Paint 风,作为风格对照)

| 项 | 值 |
|---|---|
| 用途 | MS Paint 风像素(粗描边、8-bit 复古感),与本工程 HD-2D / JRPG 主路线**不同**,仅作风格对照与备选 |
| 仓库 | https://huggingface.co/artificialguybr/PixelArtRedmond |
| 国内镜像 | https://hf-mirror.com/artificialguybr/PixelArtRedmond |
| 必装 | 否(风格对照备选) |

> **2026-10-09 用户确认**:Microverse LoRA 用途不明,**删除不留底**。§1.4 原占位段已移除;若日后需要该风格,另起工单。

### 1.4 美术风格决议(2026-10-09)

> **背景**:本工程的地图内容美术走 `Map.presentation.art_style` 字段(参考 [数据模型规格 §2.8](数据模型规格_v0.1.md)),共三套(`jrpg` / `western_fantasy` / `ink_wash`)。本节登记与 [美术资源规范 v0.1](美术资源规范_v0.1.md) 对齐的 **JRPG 风格** 实现路径——HD-2D / ComfyUI / dsh-image-gen 边界。

#### 1.4.1 JRPG Mapping 风格子定位:HD-2D

| 项 | 值 |
|---|---|
| 美术子风格 | **HD-2D**(代表作品:八方旅人 Octopath / 三角战略 Triangle Strategy / Live A Live 重制版) |
| 视觉关键词 | 2.5D 透视 + 等距视角 + 水彩纸面底纹 + 厚涂层次光 + 笔触感 + 颗粒纹理 + 1-2px 暗角描边 |
| 与 jrpg 主调关系 | jrpg 是 v0.1 **内容美术风格顶层**(`art_style=jrpg`);HD-2D 是 jrpg 在 Mapping 场景下的 **子风格实现**——把"JRPG 二次元 + 像素"的传统搭配,改成"JRPG 二次元 + HD-2D 厚涂水彩",**不**走严格 16/32px 像素 |
| 与[美术资源规范 §1.1](#11-风格定位)关系 | §1.1 已默认 `art_style=jrpg`;HD-2D 是 `jrpg` 内部分支,**不**新增 art_style 字段,改由 `jrpg_palette.yaml` 与 `assets/textures/tiles/jrpg/` 下的资源决定 |
| 与§1.1.2 Blue Pencil XL 关系 | Blue Pencil XL v7.0.0 **本身**就是按 HD-2D / JRPG 厚涂水彩训练的(bluepen5805 公开训练集以 Octopath Traveler 风为主),所以**选 Blue Pencil XL v7 就是选 HD-2D 子风格**——无需额外 LoRA |

#### 1.4.2 出图工具:ComfyUI(本机已有实例)

| 项 | 值 |
|---|---|
| **本轮选择** | **ComfyUI**(本机已有实例,模型就位) |
| **不**选 dsh-image-gen / gml-image | 用户原话:"不依赖 gml-image,价格有点高"——ComfyUI 一次部署、长期使用、零边际成本;`dsh-image-gen` 仅用于 §4.5 组件契约的 7 张审稿图与本清单 §6.2 工单 T-AI-3 的少量样张。**走法详见 §3.5.9 dsh-image-gen 集成方案** |
| **ComfyUI 实例** | 用户已确认本机有 ComfyUI 实例;模型目录已实测存在,见 [§2.6.1](#261-本机实际目录d-kudi-projectcomfyui-models) 与 `D:\kudi-project\comfyui-models\`(8 个文件,见 [§2.6.5](#265-本机文件命名差异登记2026-01-xx-实测)) |
| **工作流文件** | `D:\kudi-project\comfyui-models\workflows\test-txt2img.json`(本机已有,通用 txt2img 工作流);HD-2D 风格工单 T-AI-5 见 §6.2 |
| **统一入口约定** | 出图 → `D:\ai-models\sdxl\outputs\raw\`,通过 §2.6.3 符号链接到 ComfyUI 输出目录 |
| **资源需求** | 单张 1024×1024 约需 8-12 GB VRAM(RTX 4070 Ti 及以上);批量出图建议用 fp8 量化版或 tile 拼接 |

#### 1.4.3 HD-2D 出图参数基线(ComfyUI 节点配置起点)

| 参数 | 推荐值 | 备注 |
|---|---|---|
| 采样器 | `euler_ancestral` / `dpmpp_2m` | HD-2D 水彩感走 ancestral 系 |
| 步数 | 30-40 | 30 起步,40 封顶(再高无明显提升) |
| CFG | 7-9 | 过高会过饱和,过低会失 HD-2D 关键词响应 |
| 尺寸 | 1024×1024(主) / 1024×576(横版场景) | HD-2D 偏好正方形或 16:9 |
| **正提示词骨架** | `hd-2d, octopath traveler style, watercolor texture, paper texture, thick layered lighting, 2.5d perspective, isometric, jRPG, painterly, soft vignette, cel-shaded characters, 1px dark outline, octopath traveler, JRPG hero, JRPG village` | 关键词 12-16 个封顶 |
| **负提示词骨架** | `low quality, worst quality, blurry, jpeg artifacts, watermark, 3d render, photorealistic, anime screenshot, modern ui, chibi, deformed hands, extra fingers, text, signature` | 与 jrpg_palette.yaml 4 色封顶纪律**不**冲突(模型输出色自由,后续人工选 sprite 时筛选) |
| LoRA 叠加 | **不叠加** | Blue Pencil XL v7 已含 JRPG 风格;叠 pixel-art-xl 会破坏 HD-2D 厚涂感(实验证伪) |
| ControlNet | 复杂建筑 / 地标可加 canny 控形 | prompt 内不控形时,出图自由度高但 sprite 难以切 |
| 输出后处理 | `D:\kudi-project\comfyui-models\_tools\crop_*.py` 系列(本机棋盘格 keying + NEAREST 缩放工具) | 详见 §6.2 工单 T-AI-5 |

> **2026-10-09 用户三选一确认**:
> 1. **HD-2D 模型选型**:Blue Pencil XL v7 主出图(✓)
> 2. **ComfyUI 接入**:本机已有 ComfyUI 实例(✓)
> 3. **色彩纪律**:出图后由 Blue Pencil XL 调色,**不**改 jrpg_palette.yaml;现有 4 色封顶纪律不破坏(✓)

#### 1.4.4 三套内容美术风格现状索引

> 引用 [美术资源规范 §1.1](美术资源规范_v0.1.md#11-风格定位)。本清单是**资源下载侧**,不重复定义风格字段;只在出图时按本节决策表执行。

| `art_style` | 状态 | 资源出图路径 | 调色板 |
|---|---|---|---|
| **`jrpg`** | ✅ **实装**(`assets/textures/tiles/styles.yaml:4`) | Blue Pencil XL v7 → ComfyUI(HD-2D 子风格) | `jrpg_palette.yaml`(27 hex) |
| `western_fantasy` | 🟡 占位(styles.yaml 登记 `path: _placeholder`,fallback → `jrpg` + 纯色) | 推迟到 T-ART-7 | `western_fantasy_palette.yaml`(色值全 `[待标定]`) |
| `ink_wash` | 🟡 占位(同上) | 推迟到 T-ART-8 | `ink_wash_palette.yaml`(色值全 `[待标定]`) |
| `_fallback_solid_color` | 兜底 | 纯色填色 | — |

> **同步原则**:本工程"软件地图都是 jrpg 风格"——这是 2026-10-09 用户的整体定调,与 [美术资源规范 §1.1](美术资源规范_v0.1.md#11-风格定位) 默认 `art_style=jrpg` 一致。

#### 1.4.5 软件 UI 与内容美术关系(同步自 §4.5)

> **背景**:本工程软件 UI 走"羊皮卷工坊"气质 + 7 件套组件契约(见 [美术资源规范 §4.5](美术资源规范_v0.1.md#45-基础-ui-组件视觉契约jrpg-增补));**UI 不依赖 SDXL 模型**——UI 走程序化 `_draw()` + 9-slice sprite,**不**走 `dsh-image-gen` 出图(详见 [美术资源规范 §4.5](#45-基础-ui-组件视觉契约v0-1-增补jrpg) 开篇说明)。
>
> 因此本清单的 SDXL 模型**只服务于"地图内容美术"**(建筑 / 地形 / 地标 / 室内 / marker icon),**不**服务于软件 UI。**软件 UI 7 件套已审稿通过(2026-10-09),契约表见 [美术资源规范 §4.5.2](#452-组件契约表七件套)**,本轮**不**调整。
>
> **2026-10-09 用户二选一确认**:**完全沿用 §4.5,不调整**(✓)。

#### 1.4.6 实测参考图(2026-10-09,出图样张)

> **本节是 §1.4.3 出图参数基线的实测背书**——下面这张图是按基线跑 Blue Pencil XL v7(无 ControlNet 控形、无 LoRA 叠加)的样张,证明 §1.4.3 的提示词骨架**确实**能出 HD-2D 子风格,不必调 LoRA。

**样张特征(实测)**:

| 项 | 值 | 与 §1.4.3 基线对应 |
|---|---|---|
| **视角** | 2.5D 俯视斜角 | ✓ `2.5d perspective, isometric` |
| **色调** | 4 主色封顶(米白裙 / 橙背心 / 棕发 / 灰蓝屋顶)+ 黑描边 + 草丛黄绿 / 石径灰白 | ✓ jrpg_palette 4 色封顶纪律 |
| **描边** | 1px dark outline | ✓ `1px dark outline, cel-shaded` |
| **厚涂** | 厚涂水彩 + 干刷笔触 | ✓ `watercolor texture, paper texture, painterly` |
| **光照** | 左上→右下方向暖光 + 阴影 | ✓ `thick layered lighting, soft vignette` |
| **角色姿态** | 正面 T-pose(参考图常用) | ✓ 角色 marker 图标骨架 |
| **场景元素** | 鹅卵石径 + 茅草屋顶 + 民居 + 灌木 + 灯笼 + 石围栏 | ✓ JRPG village 场景元素 |

**结论**:
- **§1.4.3 基线出图参数可用** —— 跑出图与基线描述高度吻合
- **不叠 LoRA 是对的** —— Blue Pencil XL v7 自身 JRPG 风格完整
- **载入 ControlNet 后可加细节控形** —— 这张图作为基线图,载入 §3.5.3 LoadImage(档 C:风格锚图)可复刻同一构图

**作为后续参考图**:
- 用作 LoadImage 上传**档 C:风格锚图**——保持同一视觉风格统一输出系列图
- 文件落地路径:`D:\kudi-project\comfyui-models\reference_maps\char_girl_back_refmap_v1.png`(与 §3.5.8.4 推荐存放一致)

---

## 2. 本地缓存目录约定

> 模型权重**不进**版本库(理由见顶部"关键纪律"第 2 条)。

### 2.1 根路径

| OS | 推荐路径 | 说明 |
|---|---|---|
| Windows | `D:\ai-models\sdxl\` | 与工程盘分开,避免占工程仓库磁盘 |
| Linux | `~/.cache/huggingface/hub/` | 与 `huggingface-cli` 默认一致 |
| macOS | `~/.cache/huggingface/hub/` | 同上 |

> **环境变量**(Windows 推荐):`$env:WORLDVIEW_AI_MODELS = "D:\ai-models\sdxl"`,下载脚本默认读它,缺省回退到上表根路径。

### 2.2 目录结构(Windows 完整布局)

```
D:\ai-models\sdxl\
│
├── manifest.yaml                        # 模型清单(仓库 + 版本 + sha256 + 用途);唯一真源
├── manifest.lock                        # 哈希校验锁文件(可选,自动生成)
│
├── base\                                # SDXL 基础模型
│   ├── sd_xl_base_1.0.safetensors       # 必须,所有 LoRA/ControlNet 链路的前置
│   └── README.md                        # 仓库来源 / 下载日期 / 文件大小记录
│
├── vae\                                 # VAE(影响出图色准)
│   ├── sdxl-vae-fp16-fix.safetensors    # 推荐(避免紫边)
│   └── README.md
│
├── controlnet\                          # ControlNet 模型
│   ├── controlnet-canny-sdxl-1.0.safetensors
│   ├── controlnet-depth-sdxl-1.0.safetensors
│   └── README.md
│
├── lora\                                # 风格 LoRA
│   ├── pixel-art-xl.safetensors         # 主推(HD-2D 轻度风格化)
│   ├── pixelart-redmond.safetensors     # 对照(MS Paint 风)
│   └── README.md
│
├── lcm\                                 # LCM 加速 LoRA(可选)
│   ├── lcm-lora-sdxl.safetensors
│   └── README.md
│
├── prompts\                             # 出图 prompt 模板(版本控制,入版本库后供工程复用)
│   ├── character_v1.yaml                # 角色类:marker icon 单体
│   ├── item_icon_v1.yaml                # 物品图标:16×16 / 32×32
│   ├── tile_topdown_v1.yaml             # 场景 tile:64×64 top-down
│   └── tile_iso_v1.yaml                 # 等距 tile:64×64 等距投影
│
├── outputs\                             # 出图成品(可清空,不入版本库)
│   ├── raw\                             # AI 原图(1024×1024+ 高清)
│   ├── downscaled\                      # 下采样 + Nearest Neighbor 后的像素成品
│   └── rejected\                        # 筛掉的废图(留底,便于复盘)
│
└── tools\                               # 下载 / 校验 / 出图辅助脚本
    ├── download_ai_models.ps1           # 一键下载(§3)
    ├── verify_ai_models.ps1             # sha256 校验
    └── downscale_pixel.py               # 下采样 + Nearest 转换脚本
```

### 2.3 路径约定要点

| 项 | 约定 | 理由 |
|---|---|---|
| 根目录 | `D:\ai-models\sdxl\` | 与工程盘分开;避免 `assets/` 内误落权重 |
| 模型权重 | 一律 `.safetensors` | 安全性 + 加载速度;**不**接受 `.ckpt` / `.pt` |
| 文件名 | 与仓库 `model_index` 默认一致 | 与 `huggingface-cli` 默认命名一致,便于脚本识别 |
| 元数据 | 每个模型目录内放 `README.md` | 人工记录仓库来源 / 下载日期 / 文件大小;**不**放版本库 |
| manifest.yaml | 仓库根 | 记录**所有**模型仓库 / 版本 / sha256(如有)/ 用途,作为环境重建唯一真源 |
| manifest.lock | 自动生成 | sha256 校验锁,与 manifest.yaml 对照 |
| prompts/ | 入版本库 | prompt 模板是工程资产,与模型权重解耦 |
| outputs/ | **不入**版本库;按需 `.gitignore` | 出图成品是临时产物,可随时清空 |
| tools/ | 入版本库(`scripts/download_ai_models.ps1` 镜像) | 脚本是工程可复用资产 |

### 2.4 模型存放方式(四种)

> **核心约束**:模型文件**只在磁盘上存在一份**,通过**符号链接**让 ComfyUI / A1111 / 脚本 都能读到;**不**复制不**双份**存放。
> 单份存放的额外好处:删除/升级/迁移只需改一处,不会出现"忘了删旧的目录"导致的几 GB 残留。

#### 2.4.1 方式一:单一主目录 + 符号链接(本机**默认采用**)

```
[一份权重,多处访问]
D:\ai-models\sdxl\base\sd_xl_base_1.0.safetensors      ← 实际文件(only copy)
       ↓ symlink
D:\ComfyUI\models\checkpoints\sd_xl_base_1.0.safetensors ← 链接(零字节占位)
       ↓ symlink
D:\stable-diffusion-webui\models\Stable-diffusion\sd_xl_base_1.0.safetensors  ← 另一工具
```

**适用场景**:
- 同时使用 ComfyUI + A1111(Automatic1111 / SD WebUI)
- 想用 `huggingface-cli` 拉取,又想直接被 ComfyUI 识别
- 模型升级时只换源文件,链接无需改

**PowerShell 创建**(节选,详见 §2.5):

```powershell
# 真源在 D:\ai-models\sdxl\base\,链接到 ComfyUI 的 models/checkpoints/
New-Item -ItemType SymbolicLink `
    -Path "D:\ComfyUI\models\checkpoints\sd_xl_base_1.0.safetensors" `
    -Target "D:\ai-models\sdxl\base\sd_xl_base_1.0.safetensors"
```

#### 2.4.2 方式二:直接放 ComfyUI 标准目录(最简,无链接)

```
D:\ComfyUI\
└── models\
    ├── checkpoints\
    │   ├── sd_xl_base_1.0.safetensors          ← 实际文件
    │   └── blue_pencil_xl_v7.safetensors
    ├── vae\
    │   └── sdxl_vae.safetensors
    ├── controlnet\
    │   ├── controlnet-canny-sdxl-1.0.safetensors
    │   └── controlnet_depth_sdxl_1_0.safetensors
    └── loras\
        ├── pixel-art-xl.safetensors
        └── lcm-lora-sdxl.safetensors
```

**适用场景**:
- **只用** ComfyUI 一个工具
- 不在乎模型被多个工具共享
- 想要"零配置"——ComfyUI 自动识别 `models/<类型>/` 下的 `.safetensors`

**缺点**:升级/迁移时需要逐个目录处理,容易遗漏

#### 2.4.3 方式三:`huggingface-cli` 默认缓存(适合 CLI 化工作流)

```
~/.cache/huggingface/hub/         (或 HF_HOME 环境变量指定路径)
├── models--stabilityai--stable-diffusion-xl-base-1/
│   └── blobs/  refs/
├── models--nerijs--pixel-art-xl/
│   └── blobs/  refs/
└── models--latent-consistency--lcm-lora-sdxl/
    └── blobs/  refs/
```

**适用场景**:
- 主要用 `huggingface-cli download` / `diffusers` 库 / Python 脚本加载
- 自动去重(同一模型多个项目共享)
- 不直接用 ComfyUI / A1111

**缺点**:目录层级深(`blobs/` 下才是真实文件);ComfyUI 不自动识别,**需要** §2.4.1 的符号链接桥接

#### 2.4.4 方式四:工程内嵌(本工程**禁止**)

```
world-view/
└── assets/
    └── ai_models/        ← ❌ 禁止
        ├── sd_xl_base_1.0.safetensors   (6.3 GB)
        └── pixel-art-xl.safetensors     (200 MB)
```

**禁止原因**:
- 单个仓库 6+ GB,**Git 完全无法管理**(每次 clone 6 GB)
- AGENTS.md §6.1 规定 `assets/` 仅放运行时美术资源(PNG/YAML),不放大模型
- 多工程复用同一份模型时无法共享
- 备份/同步成本极高

> **结论**:本工程**强制**采用 **方式 2** 作为基线(本机已经是这种结构,见 §2.5.1);若日后多工具共享再升级到方式 1 的符号链接方案。

### 2.5 ComfyUI 标准目录(若同时用 ComfyUI 出图)

> **约定**:ComfyUI 把模型统一放在 `<ComfyUI根>/models/<类型>/` 下,**必须**遵守,否则 ComfyUI 加载器找不到模型(它只扫这个目录树)。
> 推荐**软链接 / 硬链接**到 §2.1 的模型根,避免双份存储(模型权重单文件 6.94 GB / 2.5 GB,复制浪费)。

#### 2.5.1 本机实际目录(`D:\kudi-project\comfyui-models\`)

> **已落地状态**(2026-01-XX 实测存在):

```
D:\kudi-project\comfyui-models\
├── checkpoints\                          # 主模型
│   ├── sd_xl_base_1.0.safetensors        # SDXL 1.0 基础模型(仓库名一致)
│   └── blue_pencil_xl_v7.safetensors     # Blue Pencil XL v7.0.0(本机改名,原: blue_pencil-XL-v7.0.0.safetensors)
├── vae\                                  # VAE
│   └── sdxl_vae.safetensors              # SDXL VAE(本机简化,原: sdxl-vae-fp16-fix.safetensors)
├── controlnet\                           # ControlNet
│   ├── controlnet-canny-sdxl-1.0.safetensors   # Canny(原仓库名一致)
│   └── controlnet_depth_sdxl_1_0.safetensors   # Depth(本机下划线变体,原: controlnet-depth-sdxl-1.0.safetensors)
├── models\                               # LoRA(目录名直接叫 models,与 ComfyUI 标准不同)
│   └── pixel-art-xl.safetensors          # 像素风主 LoRA(原仓库名一致)
├── workflows\                            # ComfyUI 工作流
│   └── test-txt2img.json
├── install-bluepencil-xl.ps1             # Blue Pencil XL 安装脚本
```

> **结构特点**:与 ComfyUI 标准布局(`<根>/models/<类型>/`)不同——本工程把每类模型放成一级子目录,LoRA 单放在 `models/` 子目录(扁平化便于脚本批量管理)。**仍然有效**,因为 §2.5.3 给出**符号链接 / 软链接**到标准 ComfyUI 目录的方法。
>
> **建议升级路径**:把扁平结构改为标准 `<根>/models/<类型>/` 布局,以便直接被 ComfyUI 识别,**无需**符号链接。详见 §2.5.3。

#### 2.5.2 两种 ComfyUI 安装路径二选一

| 方案 | ComfyUI 根 | 模型符号链接源 |
|---|---|---|
| **A. 标准便携版(推荐)** | `D:\ComfyUI\` | `D:\ComfyUI\models\checkpoints\   → D:\ai-models\sdxl\base\`<br>`D:\ComfyUI\models\vae\            → D:\ai-models\sdxl\vae\`<br>`D:\ComfyUI\models\controlnet\     → D:\ai-models\sdxl\controlnet\`<br>`D:\ComfyUI\models\loras\          → D:\ai-models\sdxl\lora\`<br>`D:\ComfyUI\models\loras\          (再链) → D:\ai-models\sdxl\lcm\` |
| **B. 与 sdxl 同根并列** | `D:\ai-models\ComfyUI\` | `D:\ai-models\ComfyUI\models\checkpoints\   → D:\ai-models\sdxl\base\`<br>`D:\ai-models\ComfyUI\models\vae\            → D:\ai-models\sdxl\vae\`<br>`D:\ai-models\ComfyUI\models\controlnet\     → D:\ai-models\sdxl\controlnet\`<br>`D:\ai-models\ComfyUI\models\loras\          → D:\ai-models\sdxl\lora\`<br>`D:\ai-models\ComfyUI\models\loras\          (再链) → D:\ai-models\sdxl\lcm\` |

#### 2.5.3 创建符号链接(PowerShell,需开发者模式)

```powershell
# 方案 A 示例(D:\ComfyUI 已存在)
$ComfyRoot = "D:\ComfyUI\models"
$SDXLRoot  = "D:\ai-models\sdxl"

# 创建 ComfyUI 标准目录(若不存在)
New-Item -ItemType Directory -Force -Path "$ComfyRoot\checkpoints"
New-Item -ItemType Directory -Force -Path "$ComfyRoot\controlnet"
New-Item -ItemType Directory -Force -Path "$ComfyRoot\vae"
New-Item -ItemType Directory -Force -Path "$ComfyRoot\loras"

# 符号链接(symlink 类型 = Directory)
New-Item -ItemType SymbolicLink -Path "$ComfyRoot\checkpoints"   -Target "$SDXLRoot\base"
New-Item -ItemType SymbolicLink -Path "$ComfyRoot\vae"            -Target "$SDXLRoot\vae"
New-Item -ItemType SymbolicLink -Path "$ComfyRoot\controlnet"     -Target "$SDXLRoot\controlnet"
New-Item -ItemType SymbolicLink -Path "$ComfyRoot\loras"          -Target "$SDXLRoot\lora"
New-Item -ItemType SymbolicLink -Path "$ComfyRoot\loras\lcm-lora-sdxl.safetensors" -Target "$SDXLRoot\lcm\lcm-lora-sdxl.safetensors"
```

#### 2.5.4 ComfyUI 加载器 → 目录 → 我们的模型(完整对照)

| ComfyUI 节点 / 加载器 | 扫描目录 | 我们的模型 |
|---|---|---|
| `CheckpointLoaderSimple` / `CheckpointLoader` | `models/checkpoints/` | `sd_xl_base_1.0.safetensors` / `blue_pencil_xl_v7.safetensors` |
| `VAELoader` | `models/vae/` | `sdxl-vae-fp16-fix.safetensors` / `sdxl_vae.safetensors` |
| `ControlNetLoader` | `models/controlnet/` | `controlnet-canny-sdxl-1.0.safetensors` / `controlnet_depth_sdxl_1_0.safetensors`(注意下划线命名) |
| `LoraLoader` | `models/loras/` | `pixel-art-xl.safetensors` / `pixelart-redmond.safetensors` / `lcm-lora-sdxl.safetensors` |

> **不放进 `assets/`**:ComfyUI 的 `output/` 目录(`D:\ComfyUI\output\`)是 ComfyUI 自己的输出区,与本工程的 `D:\ai-models\sdxl\outputs\` 同义但不互通;建议**手动重定向**到 `D:\ai-models\sdxl\outputs\raw\`,便于 §5 出图 → 导入工作流统一管理。

#### 2.5.5 本机文件命名差异登记(2026-01-XX 实测)

| 标准命名 | 本机实际命名 | 备注 |
|---|---|---|
| `sdxl-vae-fp16-fix.safetensors` | `sdxl_vae.safetensors` | 下载时简化名;脚本统一使用 `madebyollin/sdxl-vae-fp16-fix` 仓库源 |
| `controlnet-depth-sdxl-1.0.safetensors` | `controlnet_depth_sdxl_1_0.safetensors` | 下划线变体;ComfyUI 按文件名加载,只要 ComfyUI 节点选的就是这个文件名即可 |
| `pixel-art-xl.safetensors` | `pixel-art-xl.safetensors`(一致) | 来自 `nerijs/pixel-art-xl` 仓库 |
| (缺失) `sd_xl_base_1.0.safetensors` | ✅ 已落 | SDXL 1.0 基础模型 |
| (缺失) `blue_pencil_xl_v7.safetensors` | ✅ 已落 | Blue Pencil XL v7.0.0;**新增主模型**,见 §1.1.2 |

### 2.6 Linux / macOS 路径映射

```
~/.cache/huggingface/hub/
├── models--stabilityai--stable-diffusion-xl-base-1/
├── models--diffusers--controlnet-canny-sdxl-1.0/
├── models--diffusers--controlnet-depth-sdxl-1.0/
├── models--nerijs--pixel-art-xl/
├── models--artificialguybr--pixelart-redmond/
└── models--latent-consistency--lcm-lora-sdxl/
```

> `huggingface-cli download` 默认落到此结构;`prompts/` `outputs/` `tools/` 与 Windows 同构,放在 `~/projects/world-view/ai-work/` 之类工程子目录。
>
> **本工程现状(2026-10-09)**:本机 ComfyUI 实例已运行于 Windows(见 §2.5),未规划 Linux / macOS 部署。本节作交叉平台备查,工单见 §6.2 T-AI-2。

---

## 3. 一键下载脚本(PowerShell)

> 用途:在 Windows + 国内网络环境下,**一键**把 §1 全部推荐模型拉到 §2 目录。
>
> 纪律:脚本**只**写模型目录,**不**接触工程目录;支持 `-SkipInstalled`(文件已存在则跳过)。

```powershell
# scripts\download_ai_models.ps1
# 用法:
#   pwsh scripts\download_ai_models.ps1                  # 全部下载
#   pwsh scripts\download_ai_models.ps1 -SkipInstalled   # 跳过已存在
#   pwsh scripts\download_ai_models.ps1 -Only "base,lcm" # 只下载指定组

[CmdletBinding()]
param(
    [switch]$SkipInstalled = $true,
    [string[]]$Only = @()
)

$ErrorActionPreference = "Stop"
$HF_MIRROR = "https://hf-mirror.com"
$ROOT = "D:\ai-models\sdxl"

$GROUPS = @{
    base        = @(
        @{ Name = "sd_xl_base_1.0.safetensors";   Url = "$HF_MIRROR/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0.safetensors" },
        @{ Name = "sdxl_vae.safetensors";          Url = "$HF_MIRROR/madebyollin/sdxl-vae-fp16-fix/resolve/main/diffusion_pytorch_model.safetensors" }
    )
    controlnet  = @(
        @{ Name = "controlnet-canny-sdxl-1.0.safetensors";  Url = "$HF_MIRROR/diffusers/controlnet-canny-sdxl-1.0/resolve/main/diffusion_pytorch_model.safetensors" },
        @{ Name = "controlnet_depth_sdxl_1_0.safetensors";  Url = "$HF_MIRROR/diffusers/controlnet-depth-sdxl-1.0/resolve/main/diffusion_pytorch_model.safetensors" }
    )
    lora        = @(
        @{ Name = "pixel-art-xl.safetensors"; Url = "$HF_MIRROR/nerijs/pixel-art-xl/resolve/main/pixel-art-xl.safetensors" }
    )
    bluepencil  = @(
        @{ Name = "blue_pencil_xl_v7.safetensors"; Url = "$HF_MIRROR/bluepen5805/blue_pencil-XL/resolve/main/blue_pencil-XL-v7.0.0.safetensors" }
    )
    lcm         = @(
        @{ Name = "lcm-lora-sdxl.safetensors"; Url = "$HF_MIRROR/latent-consistency/lcm-lora-sdxl/resolve/main/pytorch_lora_weights.safetensors" }
    )
}

# 过滤组
if ($Only.Count -gt 0) {
    $GROUPS = $GROUPS.GetEnumerator() | Where-Object { $Only -contains $_.Key } | ConvertTo-Hashtable
    if ($GROUPS.Count -eq 0) { throw "No valid group in -Only. Available: base, controlnet, lora, bluepencil, lcm" }
}

# 创建目录
$groupPaths = @{
    base        = "base"
    controlnet  = "controlnet"
    lora        = "lora"
    bluepencil  = "checkpoints"
    lcm         = "lcm"
}
foreach ($k in $groupPaths.Keys) {
    New-Item -ItemType Directory -Force -Path (Join-Path $ROOT $groupPaths[$k]) | Out-Null
}

# 下载
foreach ($g in $GROUPS.GetEnumerator()) {
    $dir = Join-Path $ROOT $groupPaths[$g.Key]
    Write-Host "=== Group: $($g.Key) -> $dir ===" -ForegroundColor Cyan
    foreach ($f in $g.Value) {
        $dest = Join-Path $dir $f.Name
        if ($SkipInstalled -and (Test-Path $dest)) {
            Write-Host "  [SKIP] $($f.Name) already exists" -ForegroundColor DarkGray
            continue
        }
        Write-Host "  [GET ] $($f.Name)" -ForegroundColor Yellow
        try {
            Invoke-WebRequest -Uri $f.Url -OutFile $dest -UseBasicParsing -TimeoutSec 600
            $size = (Get-Item $dest).Length
            Write-Host "  [OK  ] $($f.Name) ($([math]::Round($size/1MB,2)) MB)" -ForegroundColor Green
        } catch {
            Write-Host "  [FAIL] $($f.Name) -- $_" -ForegroundColor Red
            if (Test-Path $dest) { Remove-Item $dest -Force }
        }
    }
}

Write-Host ""
Write-Host "Done. Models under $ROOT" -ForegroundColor Green
```

---

## 3.5 ComfyUI HD-2D 工作流配置(2026-10-09 新增)

> **本节解决什么**:§1.4.3 给出了 HD-2D 出图参数基线,**但**只列了字段;**没有**完整可加载的 ComfyUI 工作流文件。本节给出**完整工作流 JSON** + **节点结构图** + **加载步骤** + **验证清单**——把 §1.4 决策落到可执行物。
>
> **决策回查**:Q1=B(全新建 `hd2d_txt2img.json`)+ Q2=D(双 ControlNet:Canny + Depth)+ Q3=A(写进本清单 §3.5)。

### 3.5.1 节点结构总览(16 节点 / 3 分组)

```
┌─────────────────────────────────────┐  ┌──────────────────────────────────┐  ┌────────────────────────────┐
│  分组 1:HD-2D 基础加载 (5 节点)      │  │  分组 2:ControlNet 控形 (6 节点) │  │  分组 3:采样 / 输出 (5 节点) │
├─────────────────────────────────────┤  ├──────────────────────────────────┤  ├────────────────────────────┤
│                                     │  │                                  │  │                            │
│  ① CheckpointLoaderSimple           │  │  ⑤ ControlNetLoader (Canny)      │  │  ⑫ KSampler                │
│     ↓ blue_pencil_xl_v7.safetensors │  │  ⑥ ControlNetLoader (Depth)      │  │     seed 12345 / 35 步       │
│     输出 MODEL/CLIP/VAE             │  │  ⑦ LoadImage (参考图)            │  │     cfg 8 / euler_ancestral │
│                                     │  │  ⑧ CannyEdgePreprocessor         │  │     ↓                       │
│  ② CLIPTextEncode (Positive)        │  │  ⑨ DepthAnythingV2Preprocessor   │  │  ⑬ VAEDecode                │
│     ↓ hd-2d / octopath 风骨架       │  │  ⑩ ApplyControlNet (Canny 0.65)  │  │  ⑭ SaveImage (主)           │
│                                     │  │  ⑪ ApplyControlNet (Depth 0.45)  │  │  ⑮ SaveImage (对比)         │
│  ③ CLIPTextEncode (Negative)        │  │                                  │  │  ⑯ PreviewImage (Live)      │
│     ↓ 质量 + 3D/动漫/像素反词       │  │                                  │  │                            │
│                                     │  │                                  │  │                            │
│  ④ EmptyLatentImage                 │  │                                  │  │                            │
│     ↓ 1024×1024 / batch 1           │  │                                  │  │                            │
│                                     │  │                                  │  │                            │
└─────────────────────────────────────┘  └──────────────────────────────────┘  └────────────────────────────┘
```

### 3.5.2 节点字段表(完整 JSON 结构)

| # | 节点类型 | 关键字段 | 值 | 备注 |
|---|---|---|---|---|
| 1 | `CheckpointLoaderSimple` | `ckpt_name` | `blue_pencil_xl_v7.safetensors` | 见 §2.5.1 本机路径 |
| 2 | `CLIPTextEncode` (P) | `text` | hd-2d + octopath 骨架(见 §1.4.3) | 12-16 词封顶 |
| 3 | `CLIPTextEncode` (N) | `text` | 质量 + 反词(见 §1.4.3) | — |
| 4 | `EmptyLatentImage` | width / height / batch_size | 1024 / 1024 / 1 | HD-2D 严格正方形 |
| 5 | `ControlNetLoader` | `control_net_name` | `controlnet-canny-sdxl-1.0.safetensors` | 见 §2.5.1 |
| 6 | `ControlNetLoader` | `control_net_name` | `controlnet_depth_sdxl_1_0.safetensors` | **下划线命名** |
| 7 | `LoadImage` | `image` / `upload` | 上传参考图(map sketch) | 可选;无参考图时 Preprocessor 输入空图 → 控形 = 0;具体见 §3.5.8 LoadImage 上传规范(三档用法 / 自画草图 / 不传时怎么办 / 存放位置) |
| 8 | `CannyEdgePreprocessor` | low_threshold / high_threshold | 默认 0.1 / 0.2 | 留默认 |
| 9 | `DepthAnythingV2Preprocessor` | (无字段) | — | 自动下采到 latent 尺寸 |
| 10 | `ApplyControlNet` | strength / start_percent / end_percent | **0.65** / 0.0 / 1.0 | Canny 强控形 |
| 11 | `ApplyControlNet` | strength / start_percent / end_percent | **0.45** / 0.0 / 1.0 | Depth 弱控形(过强会变黑白) |
| 12 | `KSampler` | seed / control_after_generate / steps / cfg / sampler_name / scheduler / denoise | 12345 / fixed / 35 / 8 / euler_ancestral / normal / 1.0 | 见 §1.4.3 基线 |
| 13 | `VAEDecode` | samples / vae | 来自 KSampler / 来自 Checkpoint | — |
| 14 | `SaveImage` | `filename_prefix` | `hd2d_village` | 主输出 |
| 15 | `SaveImage` | `filename_prefix` | `hd2d_village_cmp` | 对比(可选) |
| 16 | `PreviewImage` | (无字段) | — | Live 预览,辅助调参 |

### 3.5.3 节点清单 + 连线说明(文字版,可在 ComfyUI 手动搭工作流)

> **历史说明**:本节早期版本为完整工作流 JSON(用户实测发现**部分 ComfyUI 节点类名因版本 / 汉化包 / 预处理器包而异,JSON 加载失败**),现改为**节点清单 + 连线表**——按本节在 ComfyUI 画布上**手动加节点 + 拉线**,5-10 分钟搭好,版本无关。

#### 3.5.3.1 节点清单(16 个,按加节点顺序)

> **加节点方法**:ComfyUI 画布上**双击空白处** → 搜节点名 → 加到画布。按下表顺序加,节点自然分布在画布上,便于拉线。

| # | 节点显示名(右键搜这个) | 节点类型 | 加完后做什么 |
|---|---|---|---|
| ① | `CheckpointLoaderSimple` | 模型加载 | ckpt_name 选 `blue_pencil_xl_v7.safetensors` |
| ② | `CLIP Text Encode (Positive Prompt)` | P 提示词 | 改 title 为 "CLIPTextEncode (P)";粘贴 §1.4.3 正提示词骨架 |
| ③ | `CLIP Text Encode (Negative Prompt)` | N 提示词 | 改 title 为 "CLIPTextEncode (N)";粘贴 §1.4.3 负提示词骨架 |
| ④ | `Empty Latent Image` | 潜空间 | width=1024, height=1024, batch_size=1 |
| ⑤ | `ControlNet Loader` | CN 模型加载(1) | control_net_name 选 `controlnet-canny-sdxl-1.0.safetensors` |
| ⑥ | `ControlNet Loader` | CN 模型加载(2) | control_net_name 选 `controlnet_depth_sdxl_1_0.safetensors` |
| ⑦ | `Load Image` | 参考图(Canny) | 不上传,出 1024×1024 黑边(若想空跑,可**不**加此节点,跳过 ⑨⑪) |
| ⑧ | `Load Image` | 参考图(Depth) | 不上传,出 1024×1024 黑边(若想空跑,可**不**加此节点,跳过 ⑩⑫) |
| ⑨ | `Canny Edge` 或 `CannyEdgePreprocessor` | Canny 预处理 | low_threshold=0.1, high_threshold=0.5(无参考图时输出全黑,可跳过) |
| ⑩ | `MiDaS Depth Map` 或 `DepthAnythingV2Preprocessor` | Depth 预处理 | 默认参数(无参考图时输出全黑,可跳过) |
| ⑪ | **`ControlNet Apply Advanced`**(新版) | CN 控形(1,控 P) | **新版节点**:无 conditioning 输入,只接 positive / negative / control_net / image;strength=0.65, start_percent=0.0, end_percent=1.0;**接 P 提示词**(控边缘) |
| ⑫ | **`ControlNet Apply Advanced`**(新版) | CN 控形(2,控 N) | 同上;strength=0.45;**接 N 提示词**(排除非等距视角) |
| ⑬ | `KSampler` | 采样 | sampler_name=euler_ancestral, steps=35, cfg=8, denoise=1.0, seed=12345, control_after_generate=fixed;positive 接 ⑪.POSITIVE 输出,negative 接 ⑫.NEGATIVE 输出 |
| ⑭ | `VAE Decode` | 解码 | 默认 |
| ⑮ | `Save Image` | 保存 | filename_prefix=hd2d |
| ⑯ | `Preview Image` | 实时预览 | 默认 |

> **关键设计变更(2026-10-09)**:旧版 `ApplyControlNet` 接受 `conditioning` 输入(再走 `ConditioningCombine` 合并);新版 `ControlNetApplyAdvanced` **直接接受 positive 和 negative 输入,内部完成叠加**,**不再需要** `ConditioningCombine` 节点。详见 §3.5.7.1 主表「`ControlNetApplyAdvanced`」行。

#### 3.5.3.2 连线表(17 条,按顺序拉)

> **拉线方法**:每个节点**右侧小圆点**(输出)→ 拖到下一个节点**左侧小圆点**(输入)上。鼠标拖动即可。
>
> **节点编号说明**:每行的"节点名"列都标注了节点**类型**和**显示名**——避免画布上 ⑨⑩ 这类小数字节点找错。例如「⑨ 类型:Canny Edge / 显示名:`Canny Edge`」。

| # | 链路分组 | 从(输出) | 到(输入) | 数据类型 | 备注 |
|---|---|---|---|---|---|
| 1 | 基础加载 | ① 类型:CheckpointLoaderSimple / 显示名:`CheckpointLoaderSimple` — 输出口 `MODEL` | ⑬ 类型:KSampler / 显示名:`KSampler` — 输入口 `model` | MODEL | 主模型 |
| 2 | 基础加载 | ① 类型:CheckpointLoaderSimple / 显示名:`CheckpointLoaderSimple` — 输出口 `CLIP` | ② 类型:CLIPTextEncode / 显示名:`CLIP Text Encode (Positive Prompt)`(改名 `CLIPTextEncode (P)`)— 输入口 `clip` | CLIP | 给 P 提示词喂 CLIP |
| 3 | 基础加载 | ① 类型:CheckpointLoaderSimple / 显示名:`CheckpointLoaderSimple` — 输出口 `CLIP` | ③ 类型:CLIPTextEncode / 显示名:`CLIP Text Encode (Negative Prompt)`(改名 `CLIPTextEncode (N)`)— 输入口 `clip` | CLIP | 给 N 提示词喂 CLIP |
| 4 | 基础加载 | ① 类型:CheckpointLoaderSimple / 显示名:`CheckpointLoaderSimple` — 输出口 `VAE` | ⑭ 类型:VAE Decode / 显示名:`VAE Decode` — 输入口 `vae` | VAE | 给解码器喂 VAE |
| 5 | 提示词控形 | ② 类型:CLIPTextEncode / 显示名:`CLIPTextEncode (P)` — 输出口 `conditioning` | ⑪ 类型:ControlNetApplyAdvanced / 显示名:`ControlNet Apply Advanced (1, Canny)` — 输入口 `positive` | CONDITIONING | P 提示词 → Canny 控形(走 positive 输入) |
| 6 | 提示词控形 | ③ 类型:CLIPTextEncode / 显示名:`CLIPTextEncode (N)` — 输出口 `conditioning` | ⑫ 类型:ControlNetApplyAdvanced / 显示名:`ControlNet Apply Advanced (2, Depth)` — 输入口 `negative` | CONDITIONING | N 提示词 → Depth 控 N(走 negative 输入) |
| 7 | 潜空间 | ④ 类型:EmptyLatentImage / 显示名:`Empty Latent Image` — 输出口 `LATENT` | ⑬ 类型:KSampler / 显示名:`KSampler` — 输入口 `latent_image` | LATENT | 1024×1024 潜空间 |
| 8 | Canny 链路 | ⑤ 类型:ControlNetLoader / 显示名:`ControlNet Loader (1, Canny)` — 输出口 `CONTROL_NET` | ⑪ 类型:ControlNetApplyAdvanced / 显示名:`ControlNet Apply Advanced (1, Canny)` — 输入口 `control_net` | CONTROL_NET | Canny 模型 |
| 9 | Canny 链路 | ⑦ 类型:LoadImage / 显示名:`Load Image (Canny)` — 输出口 `IMAGE` | ⑨ 类型:CannyEdge / 显示名:`Canny Edge`(或 `CannyEdgePreprocessor` / `Canny` 别名) — 输入口 `image` | IMAGE | (若 ⑦ 不加,跳过此条 + 连线 11) |
| 10 | Canny 链路 | ⑨ 类型:CannyEdge / 显示名:`Canny Edge` — 输出口 `IMAGE` | ⑪ 类型:ControlNetApplyAdvanced / 显示名:`ControlNet Apply Advanced (1, Canny)` — 输入口 `image` | IMAGE | (若 ⑨ 跳过,⑪ image 空着,控形 = 0) |
| 11 | Depth 链路 | ⑥ 类型:ControlNetLoader / 显示名:`ControlNet Loader (2, Depth)` — 输出口 `CONTROL_NET` | ⑫ 类型:ControlNetApplyAdvanced / 显示名:`ControlNet Apply Advanced (2, Depth)` — 输入口 `control_net` | CONTROL_NET | Depth 模型 |
| 12 | Depth 链路 | ⑧ 类型:LoadImage / 显示名:`Load Image (Depth)` — 输出口 `IMAGE` | ⑩ 类型:MiDaSDepthMap / 显示名:`MiDaS Depth Map`(或 `DepthAnythingV2Preprocessor` 别名) — 输入口 `image` | IMAGE | (若 ⑧ 不加,跳过此条 + 连线 13) |
| 13 | Depth 链路 | ⑩ 类型:MiDaSDepthMap / 显示名:`MiDaS Depth Map` — 输出口 `IMAGE` | ⑫ 类型:ControlNetApplyAdvanced / 显示名:`ControlNet Apply Advanced (2, Depth)` — 输入口 `image` | IMAGE | (若 ⑩ 跳过,⑫ image 空着,控形 = 0) |
| 14 | 控形回 KSampler | ⑪ 类型:ControlNetApplyAdvanced / 显示名:`ControlNet Apply Advanced (1, Canny)` — 输出口 `POSITIVE` | ⑬ 类型:KSampler / 显示名:`KSampler` — 输入口 `positive` | CONDITIONING | Canny 控 P 后输出(已叠加边缘) |
| 15 | 控形回 KSampler | ⑫ 类型:ControlNetApplyAdvanced / 显示名:`ControlNet Apply Advanced (2, Depth)` — 输出口 `NEGATIVE` | ⑬ 类型:KSampler / 显示名:`KSampler` — 输入口 `negative` | CONDITIONING | Depth 控 N 后输出(已过滤非等距) |
| 16 | 采样→解码 | ⑬ 类型:KSampler / 显示名:`KSampler` — 输出口 `LATENT` | ⑭ 类型:VAE Decode / 显示名:`VAE Decode` — 输入口 `samples` | LATENT | 采样结果 |
| 17 | 解码→输出 | ⑭ 类型:VAE Decode / 显示名:`VAE Decode` — 输出口 `IMAGE` | ⑮ 类型:SaveImage / 显示名:`Save Image`(filename_prefix=`hd2d`)和 ⑯ 类型:PreviewImage / 显示名:`Preview Image` — 输入口 `images` | IMAGE | 出图分叉到 2 个节点 |

> **方案选择**:本表是 **方案 1:分层控形(Canny→P,Depth→N)**。Canny 让 P 提示词边缘精准;Depth 让 N 提示词排除非等距视角。若想用方案 2(都控 P,删 Depth 一支),见 §3.5.3.3 简化版。

#### 3.5.3.3 简化版(只跑 Canny,12 节点 / 11 条连线)

> **何时用**:Depth 节点没装 / 不想分层控形 / 想快速看风格——删 ⑥⑧⑩⑫ + 4 条连线,工作流变 12 节点 / 11 条连线。

简化版节点清单(去掉 # ⑥⑧⑩⑫,保留 ①-⑤⑦⑨⑪⑬⑭⑮⑯):

| # | 节点 | 备注 |
|---|---|---|
| ① | CheckpointLoaderSimple | 同上 |
| ② | CLIPTextEncode (P) | 同上 |
| ③ | CLIPTextEncode (N) | 同上 |
| ④ | Empty Latent Image | 同上 |
| ⑤ | ControlNet Loader (Canny) | 同上 |
| ⑦ | Load Image (Canny) | 同上(可省略,见 §3.5.4 步骤 5) |
| ⑨ | Canny Edge | 同上(若 ⑦ 省略则跳过) |
| ⑪ | **ControlNet Apply Advanced** | strength=0.65;**只接 P 提示词**(控边缘),不接 N |
| ⑬ | KSampler | positive 接 ⑪.POSITIVE,negative 直接接 ③ |
| ⑭ | VAE Decode | 同上 |
| ⑮ | Save Image | 同上 |
| ⑯ | Preview Image | 同上 |

简化版连线(7 条,基本链路):

```
# 基础加载(4 条,同 §3.5.3.2 #1-4)
1. ①.MODEL → ⑬.model
2. ①.CLIP → ②.clip
3. ①.CLIP → ③.clip
4. ①.VAE → ⑭.vae

# Canny 控 P(3 条,替代 §3.5.3.2 #5+10+11+14)
5. ②.conditioning → ⑪.positive
6. ⑦.IMAGE → ⑨.image
7. ⑨.IMAGE → ⑪.image

# N 提示词直接接 KSampler(1 条,替代 §3.5.3.2 #6+15)
8. ③.conditioning → ⑬.negative

# ⑪.POSITIVE → ⑬.positive(1 条,替代 §3.5.3.2 #14)
9. ⑪.POSITIVE → ⑬.positive

# 采样输出(2 条,同 §3.5.3.2 #16+17)
10. ⑬.LATENT → ⑭.samples
11. ⑭.IMAGE → ⑮.images + ⑯.images
```

合计 11 条连线(注:上面分 6 段但实际拉 11 条;#11 ⑭.IMAGE 是分叉到 ⑮⑯ 两个节点,算 1 拉线动作但 ComfyUI 记 1 条线)。节点共 **12 个**(去掉 ⑥⑧⑩⑂ 的 4 个节点,剩 ① ② ③ ④ ⑤ ⑦ ⑨ ⑪ ⑬ ⑭ ⑮ ⑯)。

---

> **本节历史(2026-10-09 改)**:早期 §3.5.3 是一段 JSON 模板,实测在用户 ComfyUI 中**无法 Load**——JSON 字段名(类名 / widgets_values / 端口类型)在不同 ComfyUI 版本 / 预处理器包下差异较大,任何一项不对就报错。现改为**文字版 + 手动搭**——节点名 / 端口类型随 ComfyUI 版本变化时,§3.5.7 节点兼容性表会列出回退方案。

### 3.5.4 手动搭工作流步骤(6 步走)

> **不再用 Load JSON**——按 §3.5.3 节点清单手动加 + 按连线表手动拉。

| 步骤 | 操作 | 期望 |
|---|---|---|
| 1 | 启动 ComfyUI(默认 `http://localhost:8188`);本机实例见 §2.5.1 | 浏览器打开 ComfyUI 画布 |
| 2 | 按 §3.5.3.1 节点清单,**双击画布** → 搜节点名 → 加到画布 | 画布出现 16 个节点(简化版 12 个) |
| 3 | 按 §3.5.3.2 连线表,**从输出拖到输入**,逐条拉 17 条连线(简化版 11 条) | 所有节点都有输入(除 ⑦⑧⑨⑩⑮⑯ 的外露输入) |
| 4 | 按 §3.5.1 / §3.5.2 填各节点的字段值(ckpt_name / prompt 文本 / ControlNet 名称 / sampler 参数) | 字段值正确 |
| 5 | (可选)上传 ⑦⑧ Load Image 参考图;不传则 ⑨⑩ 输出黑图,ControlNetApplyAdvanced 走 0 控形,近似 txt2img 自由出图 | 选填 |
| 6 | 点 **Queue Prompt**(或 `Ctrl+Enter`) | 进度条走 35 步,出图 |

### 3.5.5 验证清单(出图后 6 项)

| # | 检查项 | 期望 | 不符怎么办 |
|---|---|---|---|
| 1 | 节点加载无红框报错 | 16 节点全部绿色(简化版 12 节点) | 检查模型路径;看 ComfyUI 控制台日志 |
| 2 | Checkpoint 加载时间 | 首次 10-30 s(读盘 6 GB) | 正常;若 > 60 s 检查磁盘 |
| 3 | 采样时间(35 步) | RTX 4070 Ti:约 15-30 s;RTX 3060:约 60-90 s | 若 > 180 s 检查 VRAM |
| 4 | 出图分辨率 | 1024×1024 | 与 §4 验证项 #5 一致 |
| 5 | 厚涂水彩 + 2.5D 透视 | 一眼看出是 HD-2D 而非 3D 渲染 | 加 `impasto, thick brushstrokes` |
| 6 | 4 色封顶 | 肉眼 ≤ 4 主色 | 负提示词加 `oversaturated, vivid, neon` |

### 3.5.6 与 §1.4.3 出图参数基线对应

| §1.4.3 字段 | §3.5.2 节点字段 | 值 |
|---|---|---|
| 采样器 | ⑬ KSampler.sampler_name | `euler_ancestral` |
| 步数 | ⑬ KSampler.steps | 35(30-40 区间) |
| CFG | ⑬ KSampler.cfg | 8(7-9 区间) |
| 尺寸 | ④ Empty Latent Image | 1024×1024 |
| 正提示词 | ② CLIPTextEncode (P) | §1.4.3 骨架 |
| 负提示词 | ③ CLIPTextEncode (N) | §1.4.3 骨架 |
| LoRA 叠加 | (无) | **不**叠加 pixel-art-xl |
| ControlNet | ⑪⑫ ControlNetApplyAdvanced ×2 | Canny 0.65 控 P / Depth 0.45 控 N(分层) |

> **关键设计**:本工作流**不**包含 `LoraLoader` 节点——Blue Pencil XL v7 本身已含 JRPG 风格;若日后需要叠加 pixel-art-xl,在 ⑬ KSampler 之前插入 `LoraLoader` 节点(连接 MODEL/CLIP),**不**直接改本工作流。

### 3.5.7 节点名兼容性与回退方案(2026-10-09 新增)

> **背景**:本工作流中 4 类节点(⑨ Canny / ⑩ Depth / ⑪⑫ ControlNet Apply Advanced / ① CheckpointLoaderSimple)的**显示名 / 类名**跟随 ComfyUI 版本、`comfyui_controlnet_aux` 版本、ComfyUI Manager 自定义节点包、汉化包变化。**节点名错一个,整条工作流崩溃**——故必须把回退方案写进文档,作为 M2 启动前的检查项。

#### 3.5.7.1 主表(按节点类型)

| 节点号 | 用途 | 标准名(本节用) | 常见别名 | 缺失时回退 | 备注 |
|---|---|---|---|---|---|
| ⑨ | Canny 预处理 | `CannyEdge` / `CannyEdgePreprocessor` | `Canny 线稿`(汉化)/ `Canny`(极简) | 用 `Canny Edge Mask` 节点(老版)或安装 `comfyui_controlnet_aux` | 旧版 ≥ 0.6 都有 |
| ⑩ | Depth 预处理 | `DepthAnythingV2Preprocessor` | `DepthAnythingV2` / `MiDaSDepthMapPreprocessor` / `MiDaS Depth Map` / `深度图 Midas` | **优先用 `MiDaSDepthMapPreprocessor`**(老牌,稳定) | DepthAnythingV2 需新版 `comfyui_controlnet_aux` ≥ 0.4 |
| ⑪⑫ | ControlNet 控形(新版) | **`ControlNetApplyAdvanced`** | `ControlNet Apply Advanced` / `ControlNetApply`(老版)/ `ApplyControlNet`(极旧) | **老版 `ApplyControlNet` 仍可用**(走 conditioning 输入,需补 ⑬⑭ ConditioningCombine);新版无 conditioning 输入,只接 positive/negative/control_net/image,内部完成叠加 | 2026-10-09 实测用户 ComfyUI 是新版 |
| ① | 模型加载 | `CheckpointLoaderSimple` | `Load Checkpoint` / `Checkpoint Loader` | 必需;无回退 | 内置 |

#### 3.5.7.2 风险等级总结

| 风险等级 | 节点 | 缺失时影响 | 应对 |
|---|---|---|---|
| **高(必装)** | ① CheckpointLoaderSimple / ⑤⑥ ControlNetLoader / ⑬ KSampler / ⑭ VAE Decode | 整条工作流瘫痪 | ComfyUI 内置,极少缺失;若缺失,**ComfyUI 装包损坏**,重装 |
| **中(版本敏感)** | ⑨ Canny / ⑩ Depth | Preprocessor 节点不存在 → ⑪⑫ 没输入 | 装新版 `comfyui_controlnet_aux` |
| **中(版本敏感)** | ⑪⑫ ControlNetApplyAdvanced | 控形节点不存在 | 必装;若缺失,降级为单 ControlNet 或纯 txt2img;**老版 ApplyControlNet 也可**(需 ⑬⑭ 合并节点) |
| **低(基本无风险)** | ②③④⑦⑧⑨⑩⑮⑯ | 部分输入缺失,工作流仍能跑 | 跳过缺失节点对应的连线即可 |

#### 3.5.7.3 缺失节点排错 4 步

1. **右键画布 → 搜节点名**(用 §3.5.7.1 标准名 / 别名挨个试)
2. **若搜不到,看 ComfyUI Manager 状态**:
   - 启动 ComfyUI → 点 **Manager** 按钮
   - **Install Custom Nodes** → 搜 `ControlNet Preprocessors`
   - 安装 `comfyui_controlnet_aux`(覆盖旧版)
   - 重启 ComfyUI
3. **若仍搜不到,用 pip 命令装**(在 ComfyUI 根目录):
   ```bash
   cd D:\ComfyUI\custom_nodes\comfyui_controlnet_aux
   pip install -r requirements.txt
   ```
4. **若 pip 失败,降级为简化版**——按 §3.5.3.3 删 ⑥⑧⑩⑫ 节点 + 4 条连线,只用 Canny 单 ControlNet(11 节点 / 11 条连线)

#### 3.5.7.4 字段值兼容性(节点字段名 vs 显示名)

| 节点 | 字段(代码) | 显示名(右键) | 备注 |
|---|---|---|---|
| CheckpointLoaderSimple | `ckpt_name` | "ckpt_name" | 内置字段,稳定 |
| CLIPTextEncode | `text` | "text" | 内置,稳定 |
| EmptyLatentImage | `width` / `height` / `batch_size` | 同 | 内置,稳定 |
| ControlNetLoader | `control_net_name` | "control_net_name" | 内置,稳定 |
| ApplyControlNet | `strength` / `start_percent` / `end_percent` | 同 | 内置,**顺序可能略变**(新版可能加 `resolution` 字段) |
| **ControlNetApplyAdvanced**(新版) | `strength` / `start_percent` / `end_percent` | 同 | 新版,输入端口改了:无 `conditioning`,只接 `positive` / `negative` / `control_net` / `image`;**接口不兼容老版**——老版 conditioning 接线法无效 |
| KSampler | `seed` / `sampler_name` / `steps` / `cfg` / `denoise` | 同 | 内置,稳定 |
| Canny Edge | `low_threshold` / `high_threshold` | "low_threshold" / "high_threshold" | 字段名稳定,但**顺序可能换**——按 ComfyUI 当前 UI 顺序填 |
| MiDaSDepthMap | (无字段) | — | 默认即可 |

> **历史经验**:实测 2026-10-09,用户**未找到** `DepthAnythingV2Preprocessor` 节点,降级为 `MiDaSDepthMapPreprocessor` 后跑通(详见 T-AI-7 工单)。

### 3.5.8 LoadImage 上传规范(参考图选什么图,怎么用)

> **这一节回答**:`LoadImage` 节点(⑦⑧,各工作流一份)`upload` 按钮到底传什么图、不传会怎样、可省略吗。

#### 3.5.8.1 三档使用方式(从省事到精细)

| 档位 | 是否上传 | 出图结果 | 适用 |
|---|---|---|---|
| **档 A:不传** | 不点 upload 按钮 | Preprocessor 输入空图 → Canny 输出**全黑边图** → ControlNet 控形强度 ≈ 0 → 近似纯 txt2img 自由出图 | 看主模型自身风格(Blue Pencil XL v7 的 JRPG 基线);批量试提示词时省事 |
| **档 B:传草图** | upload 一张手绘/简笔 sketch(线稿、方块图、甚至截图) | Preprocessor 把它转成 Canny 边缘图 → ControlNet 用 Canny 边缘 + P 提示词双重控形;**出图轮廓贴合草图** | 同一个场景批量试色 / 试光影;固定构图,变体风格 |
| **档 C:传风格锚图** | upload 一张已出但满意的真图(其他 SDXL 出过的、网上的) | Canny 直接抄这张图的边缘 → ControlNet 0.65 控形 → 新出图**接近这张图的轮廓与构图**,但提示词决定内容 | 复刻特定景观 / 角色姿态;做素材库时风格统一化 |

#### 3.5.8.2 推荐:档 B,自画草图(本工程首选)

**自画草图参考图** 具体指南(给场景 tile / 角色图标 / 物品图标三类):

| 内容类型 | 草图画法 | 推荐尺寸 |
|---|---|---|
| **场景 tile**(HD-2D 厚涂水彩) | 三段式天/地/中景,大致勾出建筑轮廓 + 主光源;不要细节 | 1024×576 或 1024×1024 |
| **角色 marker 图标**(战斗场景头标) | 单人正面站姿,头身体简化几何;不要手臂细节 | 512×512 |
| **物品图标**(背包 / 任务物品) | 单物件正面描线,无背景 | 512×512 |

**画风口径**:**画的草图不需要好看,只需要边缘清晰**——Canny Edge 是边缘检测,**白底黑笔 / 黑底白笔**两种都能跑;**线条越清楚**,Canny 提取的边缘越准,ControlNet 控形越稳。

#### 3.5.8.3 不传参考图时的注意事项

**① 不上传图也能跑**(档 A),但要确认 Preprocessor 不崩溃。验证方法:

```
步骤:① 双击 LoadImage(upload 按钮不点)→ ② 点 Queue Prompt
     → ③ 看 Terminal / 控制台日志:
       - 正常:Canny 输出黑图、ControlNet 控形 ≈ 0、出图无 Reference
       - 异常:Preprocessor 报 "No image input" → ComfyUI 不出图
        ↑ 后者需要 fallback:在 LoadImage 前加 Reroute 节点强喂一张占位图
```

**② 不传时,工作流的"控形链路"还是连着**——只是输入空,不代表链路断开。这就是 §3.5.4 步骤 5 写"选填"的原因。

**③ 简化版 §3.5.3.3(12 节点)只跑 Canny**:LoadImage(⑦)+ Canny Edge(⑨)+ ApplyAdvanced(⑪)是 Canny 主链路;**不传图也是档 A,不会中断主链路**——出图照样跑。

#### 3.5.8.4 参考图存放位置(避免硬编码)

**不要把参考图放到工程 `assets/` 内**(AGENTS.md §6.1 规定 `assets/` 仅放 PNG/YAML 出图成品)。三种推荐放法:

| 放法 | 路径 | 适用 |
|---|---|---|
| **工程外(本机默认)** | `D:\kudi-project\comfyui-models\reference_maps\` | 单机工作;不进版本库,ComfyUI 可读 |
| **ComfyUI input 标准目录** | `D:\ComfyUI\input\reference_maps\` | 标准便携版;ComfyUI 自动识别 |
| **工程内临时** | `D:\kudi-project\world-view\.dsh_tmp\reference_maps\` | DSH 一次性截图;**不进版本库**,用完清理 |

> **本工程默认**:`D:\kudi-project\comfyui-models\reference_maps\`,本机实测目录扁平化的扩展,与 §2.5.1 配置一致。

#### 3.5.8.5 文件格式 / 命名规范

| 项 | 推荐 |
|---|---|
| **格式** | PNG(无损,推荐)/ JPG(可,但 Canny 边缘会因压缩变粗糙)|
| **命名** | `<内容类型>_<目标>_<用途>_<版本>.png`(例:`scene_village_layout_refmap_v1.png` / `char_knight_pose_refmap_v2.png`)|
| **色彩空间** | RGB(灰度也可,白底黑笔最佳)|
| **尺寸** | 长宽按 §3.5.8.2 表选;不要超过 2048×2048(ControlNet 输入限制)|
| **是否标 alpha 通道** | 不需要;LoadImage 会按 RGB 处理 |

#### 3.5.8.6 实测工作流的当前 widgets_values(参考)

**简化版** `docs/comfyui/HD2D_simplified.json` 里 `LoadImage` 节点的 `widgets_values`:

```
[
  "reference_map.png",   # 默认文件名(用户上传时改这个名,或上传时直接选别的图)
  "image"                # widget type(固定不动)
]
```

**实际用法**:
1. ComfyUI 画布上**双击 LoadImage** 节点 → 弹出 upload 弹窗
2. 点 `choose file to upload` 选图(我本机把要传的图就先放进 `D:\kudi-project\comfyui-models\reference_maps\`)
3. `image` COMBO 显示选中的文件名 → 点 Queue Prompt,ComfyUI 自动从该路径读图

> **关键**:`widgets_values[0]` 的字符串("reference_map.png")**只是显示文件名**,真正读图走 widget 第 2 个参数 `upload` 选的文件路径。**两个解耦**——改了第 1 个字符串不会重新加载图,只有重选 upload 才生效。

### 3.5.9 dsh-image-gen 集成方案(2026-10-09 落,走法 A)

> **目标**:让 DeepSeek Harness 的 dsh-image-gen 插件帮你出 HD-2D 风格的图——DSH 用提示词驱动(纯 txt2img,无图上传口),替代"出图都得手动开 ComfyUI"。

#### 3.5.9.1 集成核心矛盾

| 维度 | §3.5 ComfyUI 工作流 | dsh-image-gen |
|---|---|---|
| **驱动** | 参考图(图生图) + ControlNet 控形 + 提示词 | **纯提示词**(txt2img) |
| **模型** | Blue Pencil XL v7 + SDXL 1.0(本地 GPU) | 云端 API(google/openai/xai/seadream 等) |
| **控形** | Canny 0.65 控 P / Depth 0.45 控 N | 无控形 |
| **可调节** | seed/CFG/steps/Canny strength 全可调 | 仅 prompt + aspect ratio + 几个 model 选项 |
| **出图时间** | 35 步 × GPU 算 15-90 s | 几秒(云端) |

**结论**:DSH 不能直接复刻 §3.5 工作流(它没法接 ComfyUI),但可以走"提示词驱动 + 近似风格"。

#### 3.5.9.2 走法 A:让 DSH 跑 §1.4.3 提示词骨架 → 出"近似风格"图

**步骤**(在 DSH 窗口或命令行):

1. **复制 §1.4.3 正提示词骨架**(去掉 `1px dark outline` 这种像素专用,改成 `JRPG style, painterly`):

```
hd-2d, octopath traveler style, watercolor texture, paper texture,
thick layered lighting, 2.5d perspective, isometric, jRPG,
painterly, soft vignette, cel-shaded characters,
JRPG village, stone house, thatched roof, cobblestone path,
daytime, sunny
```

2. **DSH 用 `generate_image` 工具 / GUI 入口**(等价接口):

```python
generate_image(
    prompt=<上面提示词>,
    aspect_ratio="1:1",          # §1.4.3 默认 1024×1024
    # 其他参数随 provider 选
)
```

3. **DSH 出图后,图落到 `D:\kudi-project\world-view\dsh-image-gen\image-*.png`**(本机实测目录)→ 复制到 `D:\kudi-project\comfyui-models\outputs\dsh_raw\` 作为草稿库。

4. **挑满意的图**(通常 1 张就够)→ 作为 **§3.5.3 LoadImage 档 C 风格锚图**(`comfyui-models/reference_maps/`),再到 ComfyUI 加载 `HD2D_simplified.json` 跑精细版。

#### 3.5.9.3 走法 A 的预期效果

| 项 | 期望 | 实际 |
|---|---|---|
| **风格统一性** | DSH 出图与 §1.4.6 实测图"近似"(80% 同气质) | 主色、构图、视角大致吻合;**笔触可能变**(因为模型不同) |
| **构图** | 镜头布局相近 | ✓ 提示词锁住大部分 |
| **单图成本** | 几秒 + 几美分 | 真出 |
| **批量出图成本** | 10 张 ≈ 几毛钱 | 真出 |
| **能用作最终 sprite?** | **不**能用 — 需 ComfyUI 精细版做最后把关 | 草稿级 |

#### 3.5.9.4 注意事项

- **DSH 模型** ≠ Blue Pencil XL v7:DSH 调的是云端模型(如 google 的 Imagen / openai 的 DALL-E / xai 的 Aurora),与 §3.5 用 Blue Pencil XL v7 出图**不可能 100% 一致**。**DSH 出图适合做"前期草稿 + 风格参考"**,最终地图 sprite 必须经 ComfyUI 精细化。
- **DSH 出图可能违反 jrpg_palette 4 色封顶**:云端模型调色自由,出图饱和度可能突破 4 色。**预计超 4 色封顶 20-50%**;选图时人工筛选,弃饱和度过高的。
- **不要把 DSH 出图直接 commit 进工程**:`D:\kudi-project\world-view\dsh-image-gen\` 是 DSH 私有缓冲目录,已 untracked,本工程 `assets/` 不放 AI 原图。

#### 3.5.9.5 工作流备忘(典型顺序)

```
1. DSH(本对话窗口)→ 提示词出图 → 10 张草稿
   ↓ 挑 1 张满意的
2. 复制到 D:\kudi-project\comfyui-models\reference_maps\<name>_refmap.png
   ↓ 打开 ComfyUI(浏览器 → localhost:8188)
3. 拖 HD2D_simplified.json 进去(或新建工作流,按 §3.5.3 手动搭)
   ↓ LoadImage 选这张参考图
4. Queue Prompt → 35 步采样 → 出最终 sprite
   ↓ 人工筛选 + 切 atlas
5. 复制到 D:\kudi-project\world-view\assets\textures\tiles\jrpg\<name>.png
   ↓ 写 index.yaml status: v1 → v2
6. commit
```

#### 3.5.9.6 与 §3.5.3 工作流节点字段值同步

DSH 出图时,提示词 = §3.5.2 表行 2 (CLIPTextEncode P)。其他字段(ControlNet 强度 / 采样器 / 步数)在 DSH 不可调,**都靠模型自身默认 + 提示词手调**。

> **为什么这不是 bug**:DSH 出图本就是"草稿级",模型默认参数对**草稿**足够;精细化在 ComfyUI 阶段做。

---

## 4. 验证清单(下载后回归)

| # | 检查项 | 期望 | 工具 |
|---|---|---|---|
| 1 | `sd_xl_base_1.0.safetensors` 文件大小 | 约 6.94 GB | `Get-Item` |
| 2 | `pixel-art-xl.safetensors` 文件大小 | 约 24–250 MB(随版本) | `Get-Item` |
| 3 | `controlnet-canny-sdxl-1.0.safetensors` 文件大小 | 约 2.5 GB | `Get-Item` |
| 4 | sha256 与 HuggingFace 仓库页面对比 | 一致(可选,非强制) | `Get-FileHash -Algorithm SHA256` |
| 5 | 任意一张样张试跑:提示词 `pixel art village, hd-2d, single layer, 1px outline, parchment bg` | 出图非黑/非纯色 | ComfyUI / A1111 |

> sha256 不强校验:每个仓库 README 偶有变动,字节级一致维护成本高;**优先**用文件大小 + 试跑出图判断。

---

## 5. 出图 → 导入工作流(简版)

> 详细规范见 [美术资源规范 v0.1 §6 生成器](美术资源规范_v0.1.md) 与 §7 v1_draft 替换计划。

1. **AI 出图**(本清单模型 + ComfyUI,**本工程实例已就位**;出图参数基线见 [§1.4.3](#143-hd-2d-出图参数基线comfyui-节点配置起点))
2. **人工筛选**(审美 + jrpg 4 色封顶纪律,见 [美术资源规范 §1.2 #13](美术资源规范_v0.1.md))
3. **后处理**(按子风格分):
   - **HD-2D 子风格**(本工程默认):**不**做 NN 下采样,保留 1024×1024 大图;人工用 PS / Aseprite 切 64×64 / 32×32 sprite 时用 bilinear + lanczos,保留厚涂感
   - **严格像素子风格**(对比备选):用 `_tools/crop_*.py` 系列做棋盘格 keying 透明化 + NEAREST 缩放,见 [§1.4.2](#142-出图工具comfyui本机已有实例) 备注
4. **导入**:
 - 角色 / 物品图标 → `assets/icons/icons_atlas.png` 替换帧
 - 场景 tile → `assets/textures/tiles/<style_id>/` 替换 atlas
5. **更新元数据**:`[index.yaml] status: v1_draft` → `v1` / `v2`
6. **跑审计**:`docs/美术资源审计清单_v0.1.md` 13 项

---

## 6. 待办(v0.1+)

- [ ] **T-AI-2**:加 Linux / macOS 版下载脚本(本节只给 PowerShell 版)
- [ ] **T-AI-3**:为 §1.3 三个 LoRA 各出 5 张样张,登记到 `docs/UI-jrpg-preview/` 作为参考
- [ ] **T-AI-4**:验证 `madebyollin/sdxl-vae-fp16-fix` 是否是当前最佳 VAE(SimDTI-A3,可推迟)
- [ ] **T-AI-5**:**HD-2D 风格工单**——在 ComfyUI 加载 Blue Pencil XL v7,按 §1.4.3 出图参数基线出 5 张样张(`1village_2d_house / town_stone_house / capital_castle / forest_grove / mountain_peak`),登记到 `docs/UI-jrpg-preview/hd2d_samples/`;验证"不叠 LoRA 也能出 HD-2D"的可行性(预计无需 pixel-art-xl)。**M2 启动前完成**。
- [ ] **T-AI-6**:把 §1.4.3 的正/负提示词骨架整理到 `assets/prompts/hd2d/positive.yaml` / `negative.yaml`,作为 ComfyUI 工作流的默认提示词来源(避免每次重写)。**M2 启动前完成**。
- [ ] **T-AI-7**:**双 ControlNet 配置验证**——按 §3.5 在 ComfyUI 加载 `hd2d_txt2img.json` 出 5 张样张(2d_house / town_stone_house / capital_castle / forest_grove / mountain_peak),验证 Canny 0.65 + Depth 0.45 的控形强度;**特别关注**:`LoadImage` 不上传参考图时,Preprocessor 节点是否崩溃。**M2 启动前完成**。

---

## 7. 变更记录

| 日期 | 版本 | 变更 |
|---|---|---|
| 2026-10-09 | v0.1 初版 | 新建。登记 SDXL 1.0 基础模型 + 2 个 ControlNet + 3 个像素 LoRA(主推 nerijs/pixel-art-xl);附国内镜像下载脚本与本地缓存目录约定 |
| 2026-10-09 | v0.1 补充 | §1.2 ControlNet 海外/国内双链接并列;Depth 补直接文件直链;§2 缓存目录重写为完整树形结构(增 `manifest.yaml`/`manifest.lock`/`prompts/`/`outputs/`/`tools/`/各子目录 `README.md`)与路径约定要点;新增 Linux/macOS 路径映射 |
| 2026-10-09 | v0.1 补充 2 | §1.1.2 新增 **Blue Pencil XL v7.0.0**(JRPG 风格主模型);§2.5 新增本机实际目录 `D:\kudi-project\comfyui-models\` + §2.5.5 文件命名差异登记(VAE / Depth / Blue Pencil);§3 下载脚本目标文件名改为本机实测名(`sdxl_vae` / `controlnet_depth_sdxl_1_0` / `blue_pencil_xl_v7`);新增 `bluepencil` 下载组(目标目录 `checkpoints/`) |
| 2026-10-09 | v0.1 补充 3 | **删除** §1.4 Microverse LoRA 整段(用户确认"不知道有什么用",不留底);同步 §6.2 删除 T-AI-1 工单 |
| 2026-10-09 | v0.1 补充 4 | **新增 §1.4 美术风格决议**(5 小节):HD-2D 子风格定位(Blue Pencil XL v7 一步到位)/ ComfyUI 出图(本机实例已就位,不使用 dsh-image-gen)/ 出图参数基线 / 三套 art_style 现状索引 / 软件 UI 不走 SDXL(沿用 §4.5);§1.1 用途字段去掉"HD-2D"错误归属(HD-2D 是 Mapping 子风格,非 SDXL 基础模型自身);§5 出图 → 导入流程重写第 1/3 步(子风格分叉 HD-2D 不做 NN vs 严格像素做 NN);新增 §6.2 工单 T-AI-5(HD-2D 5 张样张验证)+ T-AI-6(正/负提示词骨架整理到 `assets/prompts/hd2d/`) |
| 2026-10-09 | v0.1 补充 5 | **修复 §2 编号错乱**(§2.5 Linux/macOS 与 §2.6 ComfyUI 互换位置,统一为 §2.5=ComfyUI / §2.6=Linux/macOS);**新增 §3.5 ComfyUI HD-2D 工作流配置**(16 节点 / 3 分组:CheckpointLoaderSimple / CLIPTextEncode×2 / EmptyLatentImage / ControlNetLoader×2 / LoadImage / CannyEdgePreprocessor / DepthAnythingV2Preprocessor / ApplyControlNet×2 / KSampler / VAEDecode / SaveImage×2 / PreviewImage),含完整工作流 JSON 模板 + 节点字段表 + 5 步加载 + 6 项验证清单 + 与 §1.4.3 出图参数基线对照表(Canny 0.65 / Depth 0.45,**不**叠加 pixel-art-xl LoRA);新增 §6.2 工单 T-AI-7(双 ControlNet 配置验证)。**用户决策回查**:Q1=B(全新建 `hd2d_txt2img.json`)+ Q2=D(双 ControlNet)+ Q3=A(写进本清单 §3.5) |
| 2026-10-09 | v0.1 补充 6 | **§3.5.3 改写为文字版**——原 JSON 模板实测无法在用户 ComfyUI 中 Load(节点类名因版本 / 汉化 / 预处理器包而异),改为 18 节点清单 + 17 条连线表 + 简化版(12 节点 9 连线,删 Depth 一支);**§3.5.4 改写为 6 步手动搭工作流**——双击画布加节点 + 拖线 + 填字段;**新增 §3.5.7 节点名兼容性与回退方案**(4 子节:主表 5 节点名别名 / 风险等级 5 类 / 缺失排错 4 步 / 字段值兼容性 4 节点);**关键修正**:`ConditioningCombine` 节点必须加(原 JSON 没加,导致 Canny/Depth 控形直接接 KSampler.positive 错位);`LoadImage` 不上传参考图时 `CannyEdge` / `MiDaSDepthMap` 输出黑图,ApplyControlNet 走 0 控形,近似 txt2img 自由出图 |
| 2026-10-09 | v0.1 补充 7 | **`ControlNetApplyAdvanced` 节点替换**——用户实测 ComfyUI 已升级,`ApplyControlNet` 被新版 `ControlNetApplyAdvanced` 取代,**无 `conditioning` 输入,只接 `positive` / `negative` / `control_net` / `image`**,**内部完成叠加**;**§3.5.3 节点清单 18 节点 → 16 节点**(删 ⑬⑭ ConditioningCombine,加 ⑪⑫ 新版节点);**§3.5.3.2 连线表 17 条 → 17 条(重写 5/12/13/14/15)**——方案 1 分层控形(Canny 控 P,strength=0.65;Depth 控 N,strength=0.45;N 排除非等距视角);**§3.5.3.3 简化版 12 节点 → 11 节点 / 9 连线 → 11 连线**(无 N 控形,③ 直接接 ⑬.negative);**§3.5.7.1 主表 `ControlNetApplyAdvanced` 行**——加新旧版接口对比,标注缺失回退到老版 ApplyControlNet(需补 ConditioningCombine);**§3.5.7.4 字段值兼容性表**——加 `ControlNetApplyAdvanced` 行,标注"接口不兼容老版";**残留旧 JSON 代码块(行 688-881)整体删除**——早已被新版文字版取代;**用户决策回查**:分层控形(方案 1) |
| 2026-10-09 | v0.1 补充 8 | **`docs/comfyui/HD2D_simplified.json` 落地**——删除 Depth 一支 + ID 与 §3.5.3.3 一一对应(① ② ③ ④ ⑤ ⑦ ⑨ ⑪ ⑬ ⑭ ⑮ ⑯),12 节点 / 11 条连线,新增 `③ → ⑬.negative` 直连线(link=50);§3.5.3.3 表头"11 节点 / 7 条连线"勘误为"12 节点 / 11 条连线";§3.5.3.3 表格末"11 条"加节点数 12 备注;自检脚本 `.dsh_tmp/verify_hd2d.py` 输出 ✅ 每条 link 两端都对应实际节点 |
| 2026-10-09 | v0.1 补充 9 | **新增 §3.5.8 LoadImage 上传规范**(6 小节:三档用法 A/B/C / 自画草图三类型 / 不传时怎么验证 / 三种推荐存放位置 / 文件格式 + 命名 + 尺寸规范 / widgets_values 实测结构);§3.5.2 表行 7 同步指向 §3.5.8 |
| 2026-10-09 | v0.1 补充 10 | **新增 §1.4.6 实测参考图**——`image.png`(1024×1024,sha256:bb19de8a3c0f40da18db9698a8c4aae85181fb20e1a386e5b42c9757c6e72ceb)按 §1.4.3 基线出图,验证 7 项视觉特征与基线吻合;作为 LoadImage 档 C 风格锚图,文件落地路径 `D:\kudi-project\comfyui-models\reference_maps\char_girl_back_refmap_v1.png`(与 §3.5.8.4 推荐存放一致) |
| 2026-10-09 | v0.1 补充 11 | **新增 §3.5.9 dsh-image-gen 集成方案**——走法 A(纯提示词 txt2img,接近 §3.5 风格的草稿级),含 6 子节:核心矛盾(列 DSH vs ComfyUI 5 维对比)/ 走法 A 4 步操作(复制提示词 → DSH 出图 → 复制到 refmap → ComfyUI 精细化)/ 预期效果 5 项 / 注意事项 3 条(模型不同 / 超 4 色封顶 / 不 commit)/ 工作流备忘 6 步 / 与 §3.5.3 字段值同步说明 |