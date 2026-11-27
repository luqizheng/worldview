# WorldView 项目长期笔记

## 工作流规则（用户明确要求）
- **多会话并发规则（2026-11-25）**：若已有另一个 session 正在本仓库执行任务，当前 session 必须创建独立 git worktree（`git worktree add ../worldview-wt-<任务名> <branch>`）在隔离目录内工作，禁止直接在原工作目录并发操作；合并回主工作目录前须经用户裁决。已写入 `WorldView/AGENTS.md` G 节。
- `WorldView/AGENTS.md` 修改必须经用户明确批准；docs/ 仓库根为正式历史文档，未经批准不得修改。
