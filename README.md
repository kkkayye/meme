# Rune Arena 符文竞技场 — 原型 v0.1

回合制团队竞技 + 三种抽卡机制（符文三选一、装备商店刷新、宝箱开箱）的 Unity 原型。
人类玩家 + 机器人，默认 2v2，三个英雄（烈焰 Blaze / 铁壁 Vanguard / 影刃 Shade）。
设计文档见 [DESIGN.md](DESIGN.md)。

## 第一次运行

1. **激活 Unity license**：打开 Unity Hub → 右上角头像登录 → Manage licenses → Add → Get a free personal license。
   （这台机器上的 Unity 6000.4.5f1 还从未成功激活过，不激活编辑器打不开工程。）
2. Unity Hub → Projects → **Add** → 选择本文件夹（RuneArena）。首次打开会生成 Library，需要一两分钟。
3. 打开后直接按 **Play**（任何场景都行，包括空场景）：`Bootstrap` 会在运行时自动创建相机、灯光、UI 和所有系统，显示主菜单。
   想要一个保存好的场景：菜单 **RuneArena → Create Arena Scene** 会生成 `Assets/Scenes/Arena.unity` 并加入 Build Settings。
4. 测试：Window → General → Test Runner，EditMode 与 PlayMode 各跑一次。

## 操作

| 按键 | 作用 |
|---|---|
| W A S D | 移动（世界坐标轴） |
| 鼠标 | 朝向 / 瞄准 |
| 左键（按住） | 普攻 |
| Q E F R | 技能（快速施法，朝光标方向或光标点） |
| 1 2 3 | 符文抽选阶段选卡 |
| Enter / Space | 商店阶段准备 |
| Esc | 暂停 |

## 对局流程（v0.2：加入小兵与防御塔）

每回合是一条中路的推塔战：双方基地前各一座 **防御塔**（血量 2500，只吃普攻伤害，会优先打小兵、被偷袭时转火英雄并逐发加伤），**小兵**每 25 秒一波（3 近战 + 1 远程，逐回合加强）沿中路推进。补刀给 20/25 金（附近队友分 40%），推掉对面的塔全队 +250 金并 **直接赢下本回合**。团灭和超时规则不变，回合时长 120 秒。



`主菜单 → 选英雄 → 符文抽选(12s) → 商店(12s) → 倒计时(3s) → 战斗(75s) → 回合结算 → …`，先赢 3 回合者胜。
战斗中：中央控制点（占满给金币并刷宝箱）、每 20 秒随机刷宝箱（站 1 秒开箱，每第 4 个箱子保底史诗+）。
抽选：稀有度权重 60/28/10/2，落后方 40/35/20/5，连续 3 次没见到史诗则下次保底；同系符文集齐 3 个触发套装。
商店：5 件随机装备，T2 从第 2 回合、T3 从第 3 回合解锁；刷新 100 金起每次 +50；出售返还 70%。

## 代码结构（Assets/Scripts）

| 目录 | 内容 |
|---|---|
| Core | 枚举、定义（英雄/技能/符文/装备）、StatBlock、DamageInfo、EventBus、Rng、GameServices、GameConstants（所有数值） |
| Content | HeroCatalog / SkillBook（15 个技能）、MinionCatalog（小兵、防御塔）、RuneCatalog + RuneHooks（32 符文）、ItemCatalog + ItemHooks（18 装备）、LootTables |
| Combat | Unit、DamagePipeline、CombatWorld、SkillCaster（前摇/后摇/输入缓冲/充能）、SkillExecutor（10 种技能形状）、Projectile、Telegraphs、UnitMotor、UnitVisuals |
| Runes / Items / Loot | 抽选服务、符文背包与套装、商店与背包、宝箱与掉落 |
| Match | Bootstrap、GameRoot、MatchController + MatchPhaseRunner（状态机）、Arena、ControlPoint、LaneController（塔与兵线）、Economy、Scoring、TeamSpawner |
| Player / AI | 玩家输入与相机；英雄机器人（追击/战斗/撤退/占点/捡箱/推塔）、小兵 AI、防御塔 AI、共享避障 |
| UI | 纯代码 uGUI：HUD、技能栏、头顶血条、飘字、抽选面板、商店面板、菜单、开箱翻牌 |
| Juice | 顿帧、震屏、闪白、挤压拉伸、粒子、合成音效 |

所有美术都是运行时用基础几何体生成的，工程里没有任何手工资源。

## 不用 license 的编译检查

`tools/compile_check.sh all` 用 Unity 自带的 Roslyn 和真实引擎 DLL 编译全部程序集（Runtime 的编辑器版和 Player 版、Editor、两个测试程序集），报错和编辑器里一致。

## 已知限制 / 下一步

- 尚未在编辑器里实际运行过（本机 license 未激活），只做了编译验证和代码走查；第一次 Play 后可能需要微调数值和 UI 布局。
- 没有联网对战；架构上所有逻辑都走 `Unit` 和服务，不依赖"本地玩家"单例，方便后续接 Netcode。
- 机器人是简单状态机；美术为占位几何体。

## 角色模型流水线（Tripo 3D，v0.3）

基于官方 `tripo` CLI（V3 接口）。前置：`npm install -g tripo-cli`（需要 Node 20+），API key 放在 `tools/tripo.key`（一行，`tsk_` 开头，已 gitignore）或环境变量 `TRIPO_API_KEY`。账号 API 积分不足时所有任务都会被拒（exit 4），充值入口是 `tripo topup` 或 developers.tripo3d.ai 的 Billing 页。

1. `tools/.venv/bin/python tools/tripo_pipeline.py --dry-run` 看计划和预估积分（当前方案约 565 积分）。
2. `tools/.venv/bin/python tools/tripo_pipeline.py` 正式跑：每个角色 文字生成（tripo-v3.1）→ 免费绑骨检查 → 绑骨（v1.0 人形骨骼、Mixamo 骨骼命名、FBX）→ 预设动作重定向（idle / run / hurt / fall / dive + 普攻 + 技能，原地播放）→ 复制到 `Assets/Art/Tripo/<id>/`；塔只生成静态模型并转 FBX。每一步的结果记在 `tools/tripo_manifest.json`，中断重跑会跳过已完成的步骤，不重复扣费；每个角色的 `preview.png` 在 `tools/tripo_out/<id>/` 里，不满意可以删掉该角色的 manifest 条目重跑。
3. Unity 里菜单 **RuneArena → Build Character Prefabs**（或批处理 `-executeMethod RuneArena.Editor.CharacterPrefabBuilder.BuildAll`）：为每个角色生成 Animator Controller（移动混合树 + 普攻 / 技能 / 冲刺 / 受击 / 死亡状态）和 `Assets/Resources/Characters/<id>.prefab`。
4. 运行时 `UnitVisuals` 发现同名预制体就用模型替换胶囊体，按 `BodyHeight` 自动缩放并把脚放在地面；`UnitAnimator` 把移动速度、施法、冲刺、受击、死亡翻译成动画参数。模型朝向不对时改预制体上 `CharacterVisualConfig.FacingYawOffset`。

`tools/tripo_pipeline_v2_legacy.py` 是基于 Python SDK 的旧版（V2 接口，2026-11-01 停用），仅作参考。

## 已导入的模型与程序化动画（v0.3）

- `Assets/Art/Tripo/blaze/`：猫咪毛绒（Tripo Studio 导出，Blender 减面到 1.5 万面，保留 30 根人形骨骼）。
- `Assets/Art/Tripo/vanguard/`：柴犬战士（无骨骼静态模型，减面到 1.5 万面）。
- 每个文件夹里的 `visual.json` 存朝向 / 缩放 / 离地修正（两个模型都是 `facingYawOffset: 180`），Build Character Prefabs 时写进预制体。
- 减面与骨骼动画剥离：`Blender -b --python tools/blender_prepare_character.py -- <in.fbx> <out.fbx> 15000 [--anim-only]`。
- 动画优先级：有动画片段的模型走 `UnitAnimator`（Animator 状态机）；有骨骼没片段的模型走 `ProceduralRigAnimator`（按骨骼名驱动跑步摆腿摆臂、普攻挥臂、施法举手、受击后仰、冲刺前倾、死亡倒地，兼容 Tripo / Mixamo 命名）；没骨骼的模型走 `ProceduralBodyMotion`（颠簸、前倾、扑击、后仰、倒地）。
