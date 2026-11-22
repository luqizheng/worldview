# CD-02-B — Developer Handoff V0.1（CD-02-C 执行契约）

- **Date:** 2026-11-22
- **Baseline:** master `95f22e4`（CD-02-B 交付后；全量 268/268）
- **Companion:** [CD-02-B Implementation Report](CD-02-B_IMPLEMENTATION_REPORT_v0.1.md)、[CD-02-A 主文档 §7/§18](CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md)

目标：下一位开发不需要重新研究即可执行 **CD-02-C（Registry Runtime & Typed Schema）**。

## 1. 30 秒上下文

CD-02-B 已落地 Object 持久化（8 张表 + 服务）。CD-02-C 要建立 **Registry 运行时**：把 P2-09-26 V0.2 descriptor 目录从"测试时读 JSON 文档"升级为**代码内编译期权威 + 运行时加载/验证**，支撑 Bound pin 校验与 typed payload 校验入口（OD-1/OD-2 的依赖门），并保持十类型兼容与 fail-closed。

## 2. 现状（CD-02-B 交付后）

- pin 推导目前在 `BuiltinObjectTypes`（Domain 常量目录）：`SemanticsIdFor(typeKey)` + `DescriptorSchemaVersion=2`。**这是 CD-02-C 的替换点**：Registry runtime 就位后，pin 校验应走 registry（保留 `BuiltinObjectTypes` 作为种子目录数据源，语义权威迁移）。
- payload 校验现状：结构层（canonical JSON + hash + 成对 CHECK）。typed 字段校验**不存在**（OD-1/OD-2 有意留白）。
- P2-09-46 V0.2（Registry Format V2 JSON Schema）与 P2-09-47 V0.2（34 conformance vectors）是**设计态**文件；P2-09-26 V0.2 是现行 descriptor 工作目录（format_version=1）。

## 3. CD-02-C 实施建议（切片）

```
R1 Schema artifacts 盘点：逐字段核对 P2-09-26 V0.2 十 descriptor 与 P2-09-24
   字段目录；列出真实缺失（不得猜测字段含义）；产出盘点报告。
R2 Descriptor 加载器：解析 P2-09-26 V0.2 JSON（嵌入资源或编译期常量），
   结构验证 fail-closed + 稳定错误码。
R3 Registry runtime API：ObjectTypeRegistry——type_key→descriptor、pin 校验
   （schema_version+semantics_id）、payload 结构+已知字段校验入口；
   未知 type_key/未知字段 fail-closed（OT-01/OT-05/06）。
R4 兼容验证：十类型 descriptor 全部通过加载+校验；与 BuiltinObjectTypes
   种子行一致性测试；Conformance vectors 中可离线执行的子集。
R5 决策门：Registry V2 激活（P2-09-45 §8 十项验收门）需要的真实缺口清单
   ——未关门项明确报告，不得宣布 V2 已激活。
```

## 4. 红线（继承 + 新增）

1. 不把 P2-09-41 Building/Cave descriptor 加进 active registry（legacy allowlist 不含它们）。
2. 不发明 typed schema 字段含义（OD-1/OD-2 仍然开放；盘点优先于编写）。
3. Registry V2 激活不宣布——十项验收门未执行。
4. 不改 CD-02-B 已交付表/服务语义；registry 是**编译期权威**，不新增第二套运行时权威表。
5. payload 校验升级不得破坏既有 268 测试与已写入数据解释。

## 5. 开工前必读

| 文档 | 读什么 |
|---|---|
| P2-09-45 V0.3 §4/§5/§7/§8 | dual-profile、compiler 十二项职责、运行时只加载编译后 bundle、十项验收门 |
| P2-09-46 V0.2 | format V2 JSON schema（离线结构验证可用） |
| P2-09-26 V0.2 / V0.3 | 现行目录 vs V2 preview 差异 |
| P2-09-18 §5/§10 | descriptor 三域（definition/runtime/Initial State）与 pin 规则 |
| P2-09-47 V0.2 | 34 vectors 哪些可离线执行 |
