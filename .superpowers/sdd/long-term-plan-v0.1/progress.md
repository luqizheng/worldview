# SDD ledger — plan: docs/长期执行计划_v0.1.md

启动: m00002 用户「启动 docs/长期执行计划_v0.1.md」，按 §11 交接卡推进，单 active 铁律，提交落 deepseek-build。

## 基线自检 §0.5 B1–B4（实测）
- B1 `dotnet build WorldView.sln -c Debug`: 0 error / 165 警告 / EXIT=0（过渡条款 P0-C2 前只看 0 error）
- B2 裸跑 MTP exe `tests/WorldView.Tests/bin/Debug/net8.0/WorldView.Tests.exe`: 修前 344/25；`dotnet test` 与 R10 xunit.console 链双双过期（项目已切 xunit.v3 + Microsoft.Testing.Platform 2.4.0）
- B3 `Godot --headless --check-only` 挂起（autoload McpInteractionServer 拦住主循环）；加 `--quit-after 120` → EXIT=0
- B4 `git status --short`: 无新增产物噪音

## 25 失败四组根因
- 组1 SampleWorld cwd 定位 14 条 — MTP runner chdir 到 exe 目录 → 已修（TestPaths 从 AppContext.BaseDirectory 反推仓库根）
- 组2 Temp 沙箱拒写 5 条 — 子进程 Low label 不能写 Medium Temp → 已修（TestPaths.CreateScratchDir 落 workspace 内）
- 组3 真实断言 5 条 + 新暴露 1 条 — 待判产品 bug vs 断言过期（走规格化门，不在 P0 卫生豁免）
- 组4 校验器 severity 1 条 — 待读代码判定

## Task: P0 卫生 — 组1+组2 测试卫生修复
- status: complete (commit 5b970de; dotnet build 0 errors; full suite 25 → 7 failures, 337/344; 剩 7 = 组3 6 + 组4 1)
- 改动: 新增 tests/WorldView.Tests/Support/TestPaths.cs（RepoRoot 反推 / SampleWorldDir / CreateScratchDir）；ProjectLoaderTests / ProjectSaverTests / GenerationSpecYamlTests / TileSetStyleRegistryTests / GeneratedWorldPersistenceTests / ProjectValidatorTests / ProjectManagementTemp.cs / ProjectManagementServiceTests / MapValidatorTest / MapValidatorGenerationTests / MapValidatorExtraTests 共 12 个测试文件改用 TestPaths

## Task: P0-C0a 旧栈清理
- status: complete（实质由 2217fdd 完成 + 本次回写 §5 ST）
- 证据: `.trae/m1-cleanup-manifest.md` §7 记录 2026-10-12 执行「选项 A」，用户显式授权自动 commit；旧栈全部项（src/ src-tauri/ Cargo.toml 等）`git ls-files` 零命中，步骤 a/b/c 验证通过

## Task: P0-C0b 新栈产物卫生
- status: complete (commit 481a32e)
- 改动: ①`git rm --cached` 16 个已跟踪产物（.worldview/*.png、TestResults/run3.trx、WorldView.csproj.old、_tools/_b64_check.txt、build.log、csc-build.log、csc-line.txt、csc2.log、csc3.log、probe-temp.ps1、screenshot.png.import、test-class.cs、test-class.cs.uid、tests/.../run2.trx、tests.trx）
- ②`.gitignore` 补 `*.log` `TestResults/` `*.trx` `screenshot.png.import` `test-class.cs(.uid)` `csc-line.txt` `probe-temp.ps1` `**/_b64_check.txt`
- ③负向规则 `!autoload/*.gd` + `!autoload/*.gd.uid` → `autoload/mcp_interaction_server.gd` 本体入库（此前 project.godot 引用了不入库的脚本）
- ④`project.godot` `run/main_scene` 复原为 `res://scenes/Main.tscn`（HEAD 原值 `res://scenes/_demo/DemoUiAtoms.tscn` 所在目录被 ignore；工作副本曾为 `res://scenes/ui/BottomPalette.tscn`）
- ⑤3 个新 `.uid` 入库（scripts/ProjectManagement/IFileSystem.cs.uid、SystemFileSystem.cs.uid、tests/.../FakeFileSystem.cs.uid）
- uid 策略 = 提交跟踪（Godot 4.7 惯例，197 个已跟踪）

## Task: P0-C0c README 重写
- status: complete (commit 0e3ae50)
- 改动: 根 README.md 全文重写为 Godot 4.7.2 mono + C#（.NET 8）口径；删全部 cargo/pnpm/tauri/PixiJS 指令；快速上手 = B1 `dotnet build WorldView.sln -c Debug` / B2 MTP exe 直跑 / B3 `--headless --check-only --quit-after 120` / B4 `git status --short` + `dotnet run --project tests\WorldView.Smoke`；补仓库布局、七条设计原则（抄 AGENTS.md:21-27）、已知约束 D-1/D-2/D-3、本机环境表；测试计数不入 README
- 同一 commit 含计划回写：§5 P0-C0a/b/c ST→done + HIS；§6.1 第 1/2/3/6/7 条标记收口 + 新增第 9 条（MarkerLayer.cs:83 / RouteLayer.cs:61 InvalidCastException）；§6.3 补 7 条 2026-10-12 计划外发现；新增 §6.4 七条基线失败判定表；§12 追加变更记录

## B2 剩余 7 条基线失败 — 判定定稿（2026-10-12）
- 过期断言 2: UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue、Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric（探针实测终值 RedoCount=1；UndoStack.cs:159 EnqueueUnit 内 `_redo.Clear()` 是 R-UNDO-02 标准语义；改测试期望，禁止改 Push）
- 产品偏离规格 2（改 scripts/，须过规格化门）: BrushShapesTests.Round_Radius_1（Expected 5 / Actual 9）、Round_Radius_2（13 / 21）；依据 `.trae/specs/map-editor-mvp/spec.md:335` R-BRUSH-02 明文欧几里得圆盘 `sqrt(dx²+dy²) ≤ r`；`scripts/Editing/BrushShapes.cs:33` 自创 `> r + 0.5f`，应改回 `> r`
- 测试笔误 1: YamlSchemasTest.Deserialize_GridCoord_As_Array（测试 yaml 写 `id: test` at tests/.../YamlSchemasTest.cs:15，断言写 `map_test` at :17）
- 示例数据违规 1: ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute；examples/sample_world 8 处 `../../layers/*.yaml`（maps/world/world_kael.yaml:38-41、maps/city/city_kael.yaml:38-41）违反 `docs/存储与目录规范_v0.1.md:90`、:216、`.trae/specs/map-editor-mvp/spec.md:299`
- 产品 bug 1（改 scripts/）: ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only；`scripts/Domain/Validation/ProjectValidator.cs:81` 构造 Severity.Warning 却 `errors.Add`，应 `warnings.Add`
- 修复统一排到 P0-C2 之后；豁免边界（§0.3 字面不改 tests/scripts/examples）留待用户拍板

## P0-C0b/C0c 后权威基线（2026-10-12 复测，提交 0e3ae50）
- B1 `dotnet build WorldView.sln -c Debug`: 184 警告 / 0 错误 / exit 0
- B2 `WorldView.Tests.exe`: 总计 344 / 失败 7 / 成功 337 / 跳过 0 / 1s195ms / exit 2（7 条见上节）
- B3 `--headless --path . --check-only --quit-after 120`: exit 0，stdout 打印 Listening→Stopped；stderr 两条 InvalidCastException（§0.6.1 不计红）
- B4 `git status --short`: 仅既有未跟踪 docs/superpowers/{specs,plans}/2026-10-12-geo-feature-cross-map-object.md

## 下一步
- P0-C1（verify，C 轮 F5 人工目检 6 项，需用户 GUI 配合）
- P0-C2 固化验收基线 docs/acceptance/baseline_v0.1.md（下一个可独立推进的 P0 项）
- AGENTS.md §6.1/§6.2 回写待做
