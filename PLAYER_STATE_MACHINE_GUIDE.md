# 玩家状态机脚本筛选与代码解读

> 针对 `D:\ugame\gamejam`（Unity 2022.3.62f2c1，2D URP/内置管线 2D 项目）
> 目标：从导入的旧资源中筛出可作新项目基础的**玩家状态机**脚本，并逐段解释代码。

---

## 0. 结论速览

旧工程源码位于 `Assets\Scripts`（共 **40 个 .cs**，其中 `Assets\Scripts\Player` 下 **14 个**）。
`gamejam_OLD_删除我\` 下**只有 `.git` 目录，没有任何 `.cs` 源码**，因此可复用代码只有 `Assets\Scripts` 这一份。

玩家状态机是一套**教科书式的分层有限状态机（Hierarchical FSM）**，架构清晰、职责分离良好，**值得作为新项目基础**。但它目前**无法编译**，原因有 3 处硬错误（与状态机设计无关，属于导入残留）。

| 分类 | 文件数 | 处理建议 |
|---|---|---|
| A. 状态机核心骨架 | 3 | **必选**，直接复用 |
| B. 玩家具体状态类 | 11 | **必选**，按玩法裁剪 |
| C. 宿主与基建 | 7 | **必选**，缺一不可编译 |
| D. 可选增强 | 4 | 按需启用 |
| E. 与本主题无关 | 5 | 不纳入 |
| F. 敌人状态机（可参考） | 10 | 本次不纳入，仅作模板参考 |

---

## 1. 筛选清单

### A. 状态机核心骨架（必选 · 零依赖，可最先移植）

| 文件 | 职责 |
|---|---|
| `Assets\Scripts\Player\PlayerStateMachine.cs` | 状态机本体：持有 `currentState`，负责 `Enter/Exit` 切换 |
| `Assets\Scripts\Player\PlayerState.cs` | 所有状态的抽象基类：统一持有引用、计时器、动画参数、输入 |
| `Assets\Scripts\Entity.cs` | 玩家/敌人的公共基类：碰撞检测、翻转、击退、速度写入 |

### B. 玩家具体状态类（必选 · 11 个）

| 文件 | 继承自 | 语义 |
|---|---|---|
| `PlayerGroundedState.cs` | `PlayerState` | **空中/地面分界层**，含土狼时间与跳跃、反击入口 |
| `PlayerIdleState.cs` | `PlayerGroundedState` | 待机 |
| `PlayerMoveState.cs` | `PlayerGroundedState` | 地面移动 |
| `PlayerJumpState.cs` | `PlayerState` | 起跳上升 |
| `PlayerDoubleJumpState.cs` | `PlayerState` | 二段跳 |
| `PlayerAirState.cs` | `PlayerState` | 空中下落/滞空 |
| `PlayerDashState.cs` | `PlayerState` | 冲刺（无重力、留残影） |
| `PlayerWallSlideState.cs` | `PlayerState` | 贴墙下滑 |
| `PlayerWallJumpState.cs` | `PlayerState` | 蹬墙跳 |
| `PlayerPrimaryAttackState.cs` | `PlayerState` | 三段连击 |
| `PlayerCounterAttackState.cs` | `PlayerState` | 反击架势（弹反窗口） |
| `PlayerSuccessfulCounterAttackState.cs` | `PlayerState` | 弹反成功后的顿帧+震屏 |

> 注：B 类实为 12 个文件（含基类 `PlayerGroundedState`）。若只要"能跑起来的最小集"，保留
> `PlayerGroundedState` + `Idle` + `Move` + `Jump` + `Air` 即可，其余按玩法增量添加。

### C. 宿主与运行基建（必选 · 玩家状态机直接调用）

| 文件 | 为什么必需 |
|---|---|
| `Assets\Scripts\Player\Player.cs` | 状态机宿主：实例化全部状态、每帧轮询输入、暴露 `xInput` 等 |
| `Assets\Scripts\Player\PlayerAnimationTriggers.cs` | 动画事件桥：把 `Animation Event` 转发给当前状态与碰撞箱 |
| `Assets\Scripts\AttackHitbox.cs` | 攻击判定盒 + 拼刀/弹反判定 + 顿帧震屏 |
| `Assets\Scripts\Manager\TimeManager.cs` | `FrameFreeze` 顿帧，被 `AttackHitbox` 与弹反成功状态调用 |
| `Assets\Scripts\Camera\CameraShaker.cs` | 震屏，被 `AttackHitbox` 与弹反成功状态调用 |
| `Assets\Scripts\Manager\PlayerManager.cs` | 全局玩家单例（敌人 AI 靠它找玩家） |
| `Assets\Scripts\SpecialEffects\EntityFx.cs` | 受击闪白特效，`Entity.Damage()` 调用 |

### D. 可选增强（按需）

| 文件 | 说明 |
|---|---|
| `Assets\Scripts\Skill\Skill.cs` | 技能冷却/缓冲基类 |
| `Assets\Scripts\Skill\SkillManager.cs` | 技能聚合单例（`Player.cs` 强依赖 `SkillManager.instance`） |
| `Assets\Scripts\Skill\CloneSkill.cs` / `CloneSkillController.cs` | 冲刺残影，被 `PlayerDashState.Enter()` 调用 |
| `Assets\Scripts\Skill\DashSkill.cs` | 空壳，仅打日志 |
| `Assets\Scripts\Singleton.cs` | 泛型单例基类，当前**没人继承**，可留作规范 |

### E. 不纳入本次范围

`BackGrounds\ParallaxBackGround.cs`、`EndLessBackGrounds.cs`（视差背景，与状态机无关）；
`Enemy\*`、`Enemy\Skeleton\*`（敌人状态机，见第 6 节作参考模板）。

---

## 2. 架构总览

```
                        ┌──────────────────────┐
                        │  PlayerStateMachine  │  纯 C# 类，无 MonoBehaviour
                        │  currentState        │
                        │  Initialize/ChangeState
                        └──────────┬───────────┘
                                   │ 持有 1 个
                                   ▼
   ┌───────────────────────────────────────────────────────┐
   │  PlayerState（基类）                                  │
   │  protected PlayerStateMachine stateMachine            │
   │  protected Player player / Rigidbody2D rb             │
   │  static float xInput, yInput   ← 全局输入缓存         │
   │  protected float stateTimer    ← 状态存活计时         │
   │  protected bool  triggerCalled ← 动画播完信号         │
   │  virtual Enter/Update/Exit/AnimationFinishTrigger     │
   └──────────┬────────────────────────────┬───────────────┘
              │                            │
   ┌──────────▼──────────┐      ┌──────────▼───────────┐
   │ PlayerGroundedState │      │  其它空中系状态       │
   │ (地面/空中分界层)    │      │  Jump / Air / Dash   │
   │  土狼时间 / 跳跃入口 │      │  DoubleJump / Wall*  │
   └──────────┬──────────┘      │  Attack / Counter*   │
              │                 └──────────────────────┘
      ┌───────┴────────┐
      ▼                ▼
  PlayerIdleState  PlayerMoveState
```

**核心设计要点**

1. **引入"地面层"中间基类**：`PlayerIdleState` / `PlayerMoveState` 继承 `PlayerGroundedState`，
   把"离地则转 Air""按跳跃键则转 Jump""按反击键则转 Counter"三条公共转移规则**上提到父类统一实现**，
   子类只需处理自己的专属逻辑。这是本套代码最值得保留的设计。
2. **状态持有宿主引用**：每个状态构造时拿到 `Player` 实例，而非自己去 `GetComponent`，
   避免运行期查找开销。
3. **转移决策分散在状态内部**：`Update()` 里自检条件并调用 `ChangeState`，属于典型的
   "状态自驱动"写法，简单直观；代价是转移关系散落各处，规模变大后不易总览。
4. **动画与逻辑解耦**：逻辑通过 `anim.SetBool(animBoolName, true/false)` 驱动动画；
   反向靠 `Animation Event` → `AnimationFinishTrigger()` 回传"动作播完"。

---

## 3. 逐文件代码解读

### 3.1 `PlayerStateMachine.cs` —— 状态机本体

```csharp
public class PlayerStateMachine
{
    public PlayerState currentState { get; private set; }   // 外部只读，防篡改

    public void Initialize(PlayerState _startState)
    {
        currentState = _startState;
        currentState.Enter();          // 初始状态也要走 Enter，保证动画/计时器初始化
    }

    public void ChangeState(PlayerState _newState)
    {
        currentState.Exit();           // 1. 旧状态收尾（关动画 bool、清碰撞箱）
        currentState = _newState;      // 2. 换引用
        currentState.Enter();          // 3. 新状态就绪
    }
}
```

**要点**
- 它**不是** `MonoBehaviour`，只是一个普通 C# 对象——所以它不参与 Unity 生命周期，
  必须由宿主 `Player.Update()` 手动调用 `stateMachine.currentState.Update()`。
- `ChangeState` 的三步顺序（Exit → 赋值 → Enter）是标准写法。**注意**：`Exit()` 在赋值前调用，
  所以旧状态的 `Exit()` 里若访问 `stateMachine.currentState`，拿到的仍是**旧的自己**。
  本套代码正是利用这点（`Player.cs` 的 `CanClash` 判断 `currentState == primaryAttackState`）。
- **潜在风险**：如果在一个状态的 `Exit()` 里再次调用 `ChangeState`，会导致递归切换。
  本套代码没有出现这种情况，扩展时需注意。

---

### 3.2 `PlayerState.cs` —— 状态基类

```csharp
public class PlayerState
{
    protected PlayerStateMachine stateMachine;
    protected Player player;
    protected Rigidbody2D rb;

    static public float xInput;        // ⚠ static：所有状态共享同一份输入
    static public float yInput;
    private string animBoolName;       // 该状态对应的 Animator Bool 名
    protected float stateTimer;        // 通用倒计时，各状态复用
    protected bool triggerCalled;      // 动画事件回填

    public PlayerState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
    {
        this.player = _player;
        this.stateMachine = _stateMachine;
        this.animBoolName = _animBoolName;
    }

    public virtual void Enter()
    {
        player.anim.SetBool(animBoolName, true);   // 进入即打开对应动画
        rb = player.rb;                            // 缓存刚体引用
        triggerCalled = false;                     // 重置动画信号
    }

    public virtual void Update()
    {
        if (stateTimer > 0)
            stateTimer -= Time.deltaTime;          // 统一倒计时

        xInput = Input.GetAxisRaw("Horizontal");   // 每帧采一次输入，供全状态读取
        yInput = Input.GetAxisRaw("Vertical");

        player.anim.SetFloat("yVelocity", rb.velocity.y);  // 供跳跃/下落混合树使用
    }

    public virtual void Exit()
    {
        player.anim.SetBool(animBoolName, false);  // 离开即关闭动画 bool
    }

    public virtual void AnimationFinishTrigger()
    {
        triggerCalled = true;                      // 动画事件回调
    }
}
```

**要点**
- **`stateTimer` 的复用技巧**：一个字段在不同状态里被赋予不同含义——
  `PlayerGroundedState` 用它做**土狼时间**，`PlayerDashState` 用它做**冲刺持续时间**，
  `PlayerWallJumpState` 用它做**蹬墙跳锁定时长**。省字段，但可读性下降，
  新项目建议改名为 `timer` 或按语义拆分。
- **`triggerCalled` 是动画→逻辑的关键通道**：`Enter()` 时清零，动画播完由
  `Animation Event` 调用 → 置 `true`；状态在 `Update()` 里检测它来决定"动作做完了，可以退出"。
- ⚠ **`static xInput/yInput` 是一个设计缺陷**：静态字段意味着**所有 Player 实例共享输入**，
  多人/多角色场景会互相干扰。且 `Player.cs` 里用 `PlayerState.xInput` 直接访问静态字段，
  形成隐式耦合。**新项目建议改为实例字段**（见第 5 节修复建议）。
- `rb` 在 `Enter()` 才赋值，因此**不能在构造函数里使用 `rb`**。

---

### 3.3 `PlayerGroundedState.cs` —— 地面层（设计精华）

```csharp
public class PlayerGroundedState : PlayerState
{
    public override void Enter()
    {
        base.Enter();
        stateTimer = player.coyoteTime;   // 落地/在地面时重置土狼时间
        player.hasDashedInAir = false;    // 落地恢复：空中冲刺次数
        player.hasDoubleJumped = false;   // 落地恢复：二段跳次数
    }

    protected bool IsCurrentState() => stateMachine.currentState == this;

    public override void Update()
    {
        base.Update();

        // ① 土狼时间耗尽且确实离地 → 进入空中状态
        if (stateTimer <= 0 && !player.IsGroundDetected())
        {
            stateMachine.ChangeState(player.airState);
            return;                       // ⚠ 必须 return，防止后续逻辑用已失效的状态继续决策
        }

        // ② 地面反击：有缓冲输入 且 冷却已好
        if (player.counterAttackBufferTimer > 0f && player.counterAttackCoolDownTimer < 0)
        {
            player.counterAttackBufferTimer = 0f;
            player.counterAttackCoolDownTimer = player.counterAttackCoolDownTime;
            stateMachine.ChangeState(player.counterAttackState);
            return;
        }

        // ③ 跳跃：只要缓冲区还有值就起跳
        if (player.jumpBufferTimer > 0)
        {
            player.jumpBufferTimer = 0;   // 消费掉缓冲，避免重复触发
            stateMachine.ChangeState(player.jumpState);
        }
    }
}
```

**要点**
- **土狼时间（Coyote Time）**：`stateTimer` 被设为 `coyoteTime`（默认 0.1s）。
  刚离开平台边缘时，只要这 0.1s 内按跳，仍能起跳——手感优化的关键。
  实现方式很巧：不检测"是否刚离地"，而是**离地后还在倒计时就仍算在地面状态**。
- **`IsCurrentState()` 的意义**：子类 `Update()` 会先 `base.Update()`，而父类 `Update()`
  可能已经切换了状态。子类随后若继续执行 `HorizontalMoveController()`，
  就会在错误的时机改速度。所以子类统一在 `base.Update()` 后先 `if(!IsCurrentState()) return;`。
- **`return` 的重要性**：每个转移后立即 `return`，避免"一帧内连续转移"导致的怪异表现。
- **输入缓冲**：`jumpBufferTimer` / `counterAttackBufferTimer` 由 `Player.Update()` 里的
  `CheckXxxInput()` 在按键瞬间置位，状态机消费它。这实现了"落地瞬间按跳也能跳"的缓冲手感。

---

### 3.4 `PlayerIdleState.cs` / `PlayerMoveState.cs`

```csharp
// Idle
public override void Enter()
{
    base.Enter();
    player.SetZeroVelocity();          // 待机必须完全静止
}

public override void Update()
{
    base.Update();
    if (!IsCurrentState()) return;     // 父类可能已切到 Air/Jump/Counter

    if (player.attackBufferTimer > 0)  // 攻击优先级高于移动
    {
        player.attackBufferTimer = 0;
        stateMachine.ChangeState(player.primaryAttackState);
    }

    if (xInput != 0 && !player.isBusy) // 有输入且不在"忙碌"中 → 移动
        stateMachine.ChangeState(player.moveState);
}
```

```csharp
// Move
public override void Update()
{
    base.Update();
    if (!IsCurrentState()) return;
    player.HorizontalMoveController();  // 每帧写水平速度（含自动翻转）

    if (player.attackBufferTimer > 0)
    {
        player.attackBufferTimer = 0;
        stateMachine.ChangeState(player.primaryAttackState);
    }

    if (xInput == 0)                    // 松手回待机
        stateMachine.ChangeState(player.idleState);
}
```

**要点**
- **优先级顺序即代码顺序**：先判攻击，再判移动/待机。要调整手感，改这里的顺序即可。
- `player.isBusy` 由 `Player.BusyFor(seconds)` 协程控制（`PlayerPrimaryAttackState.Exit()` 里启动
  `BusyFor(0.05f)`），作用是**攻击结束后的极短硬直**，防止玩家用移动取消攻击后摇。
- `Move` 状态**不做地面检测**——它继承 `PlayerGroundedState`，父类的 `Update()` 已经处理了离地转移。

---

### 3.5 `PlayerJumpState.cs` / `PlayerAirState.cs` / `PlayerDoubleJumpState.cs`

```csharp
// Jump
public override void Enter()
{
    base.Enter();
    player.SetVelocity(rb.velocity.x, player.jumpSpeed);  // 保留水平速度，只改 Y
}

public override void Update()
{
    base.Update();
    player.HorizontalMoveController();   // 空中可控制水平移动
    player.JumpHeightController();       // 可变跳跃高度

    if (rb.velocity.y <= 0)              // 到达最高点 → 转下落
        stateMachine.ChangeState(player.airState);
    else if (player.canDoubleJump && player.jumpBufferTimer > 0 && !player.hasDoubleJumped)
    {
        player.jumpBufferTimer = 0;
        stateMachine.ChangeState(player.doubleJumpState);
    }
}
```

**要点**
- **可变跳跃高度**（`Player.JumpHeightController`）：上升中若松开跳跃键，就把
  `gravityScale` 乘以 `fallMultiplier`（默认 3）→ 上升被截断。**短按=小跳，长按=大跳**。
- `rb.velocity.y <= 0` 作为"到达顶点"的判据，简单可靠，无需额外计时器。
- **二段跳的两道锁**：`canDoubleJump`（功能开关）+ `hasDoubleJumped`（本次滞空是否已用）。
  `hasDoubleJumped` 在 `PlayerGroundedState.Enter()`（落地）和 `PlayerWallSlideState.Enter()`
  （贴墙）时被重置——**贴墙就能刷新二段跳**，这是有意为之的跑酷手感设计。

```csharp
// Air
public override void Update()
{
    base.Update();
    player.HorizontalMoveController();          // 空中横向控制

    if (player.IsWallDetected())                // ① 贴墙 → 下滑
    { stateMachine.ChangeState(player.wallSlideState); return; }

    if (player.IsGroundDetected())              // ② 落地 → 待机
    { stateMachine.ChangeState(player.idleState); return; }

    if (player.canDoubleJump && player.jumpBufferTimer > 0 && !player.hasDoubleJumped)
    {
        player.jumpBufferTimer = 0;
        stateMachine.ChangeState(player.doubleJumpState); return;  // ③ 二段跳
    }
}
```

**要点**
- `Air` 是空中状态的**汇合点**：`Jump`/`DoubleJump`/`WallJump`/`Dash` 结束后都回到这里。
- 条件判断顺序 = 优先级：**贴墙 > 落地 > 二段跳**。
- ⚠ **小瑕疵**：落地直接切 `idleState`，但落地瞬间玩家可能仍按着方向键，
  下一帧 `Idle.Update()` 才会切到 `Move`。会有 1 帧的观感停顿。
  更平滑的做法是落地时根据 `xInput` 直接决定进 `Idle` 还是 `Move`。

```csharp
// DoubleJump
public override void Enter()
{
    base.Enter();
    player.hasDoubleJumped = true;                              // 立刻置位，防连按重置
    player.SetVelocity(rb.velocity.x, player.doubleJumpSpeed);  // 覆盖 Y 速度
}
// Update 与 Jump 相同：水平控制 + 高度控制，落回 Air
```

**要点**：`hasDoubleJumped = true` 放在 `Enter()` 而非 `Update()`，
**确保进入即锁定**，避免同帧内被重复触发。

---

### 3.6 `PlayerDashState.cs`

```csharp
public override void Enter()
{
    base.Enter();
    player.skill.clone.CreateClone(player.transform);   // 生成残影（可选技能）

    rb.gravityScale = 0;                                 // 冲刺期间不受重力
    player.SetVelocity(player.dashSpeed * player.dashDir, 0);  // 锁定方向与速度
    stateTimer = player.dashDuration;                    // 用 stateTimer 计时

    if (!player.IsGroundDetected())
        player.hasDashedInAir = true;                    // 空中冲刺标记
}

public override void Exit()
{
    base.Exit();
    rb.gravityScale = 5f;                                // ⚠ 恢复重力（硬编码 5）
    player.SetVelocity(0, rb.velocity.y);                // 清水平速度，保留下落
}

public override void Update()
{
    base.Update();
    if (!player.IsGroundDetected() && player.IsWallDetected())  // 冲进墙 → 转下滑
    { stateMachine.ChangeState(player.wallSlideState); return; }

    if (stateTimer < 0)                                  // 冲刺结束
    {
        stateMachine.ChangeState(player.IsGroundDetected()
            ? player.idleState : player.airState);
    }
}
```

**要点**
- **冲刺期间 `gravityScale = 0`**，严格水平位移；`Exit()` 再恢复。
- **`dashDir` 的决策在 `Player.CheckDashInput()` 里**（不在本状态内）：
  有横向输入就用输入方向，否则用 `facingDir`——所以**原地按冲刺会朝面向方向冲**。
- ⚠ **两个可改进点**：
  1. `rb.gravityScale = 5f` 是**硬编码**，应改为 `player.gravityScale`，
     否则玩家在 Inspector 调过重力后，冲刺一次就会被重置回 5。
  2. `player.skill.clone.CreateClone(...)` 让**冲刺强依赖技能系统**。
     若新项目不要残影，这行必须删或加空判，否则 `skill` 为 null 时冲刺直接报错。
- 冷却与缓冲在 `Player.CheckDashInput()`：`dashCoolDownTimer < 0 && !hasDashedInAir` 才允许，
  成功后重置冷却并 `ChangeState(dashState)`。

---

### 3.7 `PlayerWallSlideState.cs` / `PlayerWallJumpState.cs`

```csharp
// WallSlide
public override void Enter()
{
    player.hasDashedInAir = false;    // 贴墙 = 刷新空中冲刺
    player.hasDoubleJumped = false;   // 贴墙 = 刷新二段跳
    base.Enter();
}

public override void Update()
{
    base.Update();
    player.SetVelocity(0, -player.freeWallSlideSpeed);  // 恒速下滑（非重力加速）

    // 朝远离墙面方向推 → 脱离墙面
    if (xInput != 0 && player.facingDir == -xInput)
    { stateMachine.ChangeState(player.airState); return; }

    if (player.wallJumpBufferTimer > 0)                 // 蹬墙跳
    { player.wallJumpBufferTimer = 0;
      stateMachine.ChangeState(player.wallJumpState); return; }

    if (player.IsGroundDetected())                      // 滑到地面
    { stateMachine.ChangeState(player.idleState); return; }
}
```

**要点**
- **`SetVelocity(0, -freeWallSlideSpeed)`**：下滑速度恒定，不受重力影响，手感可控。
- **脱离判据 `facingDir == -xInput`**：面向墙（`facingDir` 指向墙）时，
  输入反方向才算"推离"。因为贴墙时角色朝向墙，`facingDir` 与输入方向相反。
- **贴墙刷新能力**是刻意的连招设计：可"墙滑→二段跳→冲刺→墙滑"循环攀爬。
- ⚠ **本文件有两行无效 using**：`System.Net.NetworkInformation`（网络接口，状态机完全用不到）
  和 `Unity.VisualScripting`（可视化脚本插件）。应删除。

```csharp
// WallJump
public override void Enter()
{
    base.Enter();
    // 朝离开墙的方向弹射：-facingDir（背离墙）
    player.SetVelocity(-player.fromWallSpeedx * player.facingDir, player.fromWallSpeedy);
    player.jumpBufferTimer = 0;                     // 清掉跳跃缓冲，防立刻接普跳
    stateTimer = player.wallJumpDurationTime;       // 锁定时长（默认 0.12s）
}

public override void Update()
{
    base.Update();
    if (stateTimer < 0)                             // 锁定结束 → 交还控制权给 Air
        stateMachine.ChangeState(player.airState);
}
```

**要点**
- `stateTimer` 在这里作用 = **锁定蹬墙跳的水平弹射方向**，期间玩家输入不生效，
  之后转入 `Air` 恢复控制。这就是"蹭墙跳不会立刻被拉回墙面"的原因。
- ⚠ **`Update()` 里没有水平控制**，`SetVelocity` 设置的水平速度会**保持整个锁定期**，
  这正是设计意图；但 `rb.velocity.y` 仍受重力影响，所以表现为"斜向弹射后下落"。
- 蹬墙跳的输入检测在 `Player.CheckWallJumpInput()`：**要求 `!IsGroundDetected()`**，
  且缓冲时间 `wallJumpBuffer`（0.15s）比普通跳跃（0.1s）更宽松。

---

### 3.8 `PlayerPrimaryAttackState.cs` —— 三段连击

```csharp
public override void Enter()
{
    base.Enter();
    stateTimer = .1f;                        // 攻击前摇期间保持位移

    // 连击窗口判定：超过 2 段 或 距上次攻击超过 comboWindow → 重置为第 1 段
    if (player.comboCounter > 2 || Time.time > player.lastTimeAttack + player.comboWindow)
        player.comboCounter = 0;

    player.SetAttackHitBox(player.comboCounter);   // 按当前段数启用对应碰撞箱

    // 攻击方向：有输入用输入，否则用朝向
    player.attackDir = xInput != 0 ? xInput : player.facingDir;

    // 每段攻击自带位移（前冲感），数据驱动自 Inspector 数组
    player.SetVelocity(
        player.attackMovement[player.comboCounter].x * player.attackDir,
        player.attackMovement[player.comboCounter].y);

    player.anim.SetInteger("ComboCounter", player.comboCounter);  // 驱动动画
}

public override void Exit()
{
    base.Exit();
    player.CloseClashWindow();
    player.ClearAttackHitBox();               // 关碰撞箱，防残留判定

    player.StartCoroutine(player.BusyFor(0.05f));  // 攻击后硬直
    player.comboCounter++;                    // 段数自增（在 Exit 而非 Enter）
    player.lastTimeAttack = Time.time;        // 记录时间，用于 comboWindow
}

public override void Update()
{
    base.Update();
    if (stateTimer <= 0)
        player.SetZeroVelocity();             // 前摇结束，停住

    if (triggerCalled)                        // 动画播完
        stateMachine.ChangeState(player.idleState);
}
```

**要点**
- **连击计数的生命周期很关键**：`comboCounter` 在 `Enter()` 里读取（决定用第几个碰撞箱/位移），
  在 `Exit()` 里自增。配合 `comboWindow`（默认 0.5s）：**超时未接下一段就重置回第 1 段**。
- **数据驱动**：`attackMovement[]`（每段位移向量）和 `attackHitboxes[]`（每段判定盒）
  都是 Inspector 数组，策划可直接调数值而不用改代码——很好的设计。
- **`stateTimer = .1f` 的用途**：攻击最初 0.1s 内保留 `Enter()` 设置的位移速度
  （前冲），之后才 `SetZeroVelocity()`。这是"攻击带小幅前冲"的实现。
- **退出条件 `triggerCalled`**：完全交给动画长度决定，而非硬编码时间，
  动画师调整时长后逻辑自动跟随。这是本套代码**动画驱动逻辑**思路的体现。
- **拼刀窗口**通过 `PlayerAnimationTriggers.OpenClashWindow()`（动画事件）在攻击动画的特定帧打开，
  `Player.CanClash()` 再校验 `clashWindowOpen && currentState == primaryAttackState && opponent is Enemy`。

---

### 3.9 `PlayerCounterAttackState.cs` / `PlayerSuccessfulCounterAttackState.cs`

```csharp
// CounterAttack —— 弹反架势
public override void Enter()
{
    base.Enter();
    stateTimer = player.counterAttackDuration;          // 架势持续窗口
    player.anim.SetBool("SuccessfulCounterAttack", false);  // 重置成功标记
    player.SetZeroVelocity();
    player.ClearAttackHitBox();                          // 架势期间无攻击判定
}

public override void Update()
{
    base.Update();
    if (triggerCalled || stateTimer <= 0f)               // 动画播完 或 超时
    { stateMachine.ChangeState(player.idleState); return; }
}
```

```csharp
// SuccessfulCounterAttack —— 弹反成功
public override void Enter()
{
    base.Enter();
    stateTimer = player.counterAttackDuration;
    player.SetZeroVelocity();
    CameraShaker.Instance?.RequestShake(1f, 0.08f);       // 震屏
    TimeManager.FrameFreeze(0.08f);                       // 顿帧
}

public override void Update()
{
    base.Update();
    player.SetZeroVelocity();                             // 全程锁死
    if (triggerCalled || stateTimer <= 0f)
    { stateMachine.ChangeState(player.idleState); return; }
}
```

**要点**
- **双退出条件**：`triggerCalled`（动画事件）**或** `stateTimer <= 0`。
  用 `||` 而非 `&&` 是为了**容错**——万一动画事件漏配，状态仍能靠超时退出，不会卡死。
  这是很实用的防御式写法。
- **`?.` 空条件运算符**：`CameraShaker.Instance?.RequestShake(...)` 保证单例不存在时静默跳过，
  不会抛 `NullReferenceException`。新项目接入基建前不会因此崩溃。
- **弹反成功的触发链路**（跨文件，值得理解）：
  1. `AttackHitbox.TryHit()` 检测到 `canBeCountered && target.CanCounter(this)`
  2. 调用 `target.OnCounterSuccess(ownerEntity)` → `Player.OnCounterSuccess()`
  3. 启动协程 `CompleteCounterAttack()`，**先等待顿帧结束**（`while (TimeManager.IsFrameFrozen) yield return null;`）
  4. 若仍在 `counterAttackState`，则 `ChangeState(successFullCounterAttackState)`，并回调 `attacker.OnCountered(this)`
  5. 敌人 `EnemySkeleton.OnCountered()` 切入 `stunnedState`（硬直）
- **为什么要等顿帧**：`TimeManager.FrameFreeze` 把 `Time.timeScale` 设为 0。
  若在顿帧期间切状态，动画会被冻结在错误的帧。等 `IsFrameFrozen` 为 false 再切，才能让
  顿帧画面对应"弹反瞬间"的定格。这个细节处理得很到位。

---

### 3.10 `Player.cs` —— 状态机宿主

这是整个系统的**中枢**，职责有四：

```csharp
protected override void Awake()
{
    base.Awake();
    stateMachine = new PlayerStateMachine();

    // ① 实例化全部状态（构造时注入 this 和 animBoolName）
    idleState    = new PlayerIdleState(this, stateMachine, "Idle");
    moveState    = new PlayerMoveState(this, stateMachine, "Move");
    jumpState    = new PlayerJumpState(this, stateMachine, "Jump");
    doubleJumpState = new PlayerDoubleJumpState(this, stateMachine, "Jump");  // 复用 Jump 动画
    airState     = new PlayerAirState(this, stateMachine, "Jump");            // 复用 Jump 动画
    dashState    = new PlayerDashState(this, stateMachine, "Dash");
    wallSlideState = new PlayerWallSlideState(this, stateMachine, "WallSlide");
    wallJumpState  = new PlayerWallJumpState(this, stateMachine, "Jump");
    primaryAttackState = new PlayerPrimaryAttackState(this, stateMachine, "Attack");
    counterAttackState = new PlayerCounterAttackState(this, stateMachine, "CounterAttack");
    successFullCounterAttackState = new PlayerSuccessfulCounterAttackState(this, stateMachine, "SuccessfulCounterAttack");
}

protected override void Start()
{
    base.Start();
    skill = SkillManager.instance;          // 依赖技能单例
    rb.gravityScale = gravityScale;         // 应用 Inspector 的重力
    stateMachine.Initialize(idleState);     // ② 启动状态机
}

protected override void Update()
{
    base.Update();                          // Entity.Update：碰撞检测 + 最大下落速度
    CheckJumpInput();                       // ③ 五个输入轮询（只置缓冲，不切状态）
    CheckDashInput();                       //    ↑ 注意：Dash 是唯一直切状态的
    CheckWallJumpInput();
    CheckAttackInput();
    CheckCounterAttackInput();
    stateMachine.currentState.Update();     // ④ 驱动当前状态
}
```

**要点**
- **状态在 `Awake()` 创建、`Start()` 启动**：`Awake` 阶段所有单例已就绪，`Start` 里才取
  `SkillManager.instance` 并调用 `Initialize`。
- **动画复用**：`doubleJumpState` / `airState` / `wallJumpState` 都传入 `"Jump"`，
  即共用同一个 Animator Bool。因为 `PlayerState.Enter/Exit` 会 `SetBool("Jump", true/false)`，
  多次设置同一 bool 不会互相干扰（幂等）。
- **输入轮询 vs 状态决策分离**：
  `CheckXxxInput()` 只负责**把按键写进缓冲计时器**（`jumpBufferTimer` 等），
  真正的状态切换由各状态在 `Update()` 里读取缓冲后决定。
  这种"输入层—决策层"分离让缓冲手感统一实现。
  **唯一例外是 `CheckDashInput()`**，它自己直接 `ChangeState(dashState)`——
  因为冲刺可由任意状态触发（地面/空中），放在输入层更省事。这是不一致之处，
  但功能上没问题（后续 `CheckXxxInput` 不会覆盖它）。
- **`CheckDashInput()` 的完整条件**：
  ```csharp
  if (dashBufferTimer > 0 && dashCoolDownTimer < 0 && !hasDashedInAir)
  {
      dashCoolDownTimer = dashCoolDown;
      dashBufferTimer = 0;
      dashDir = PlayerState.xInput;        // ⚠ 静态访问
      if (dashDir == 0) dashDir = facingDir;   // 原地冲刺 → 朝面向方向
      stateMachine.ChangeState(dashState);
  }
  ```
- **`HorizontalMoveController()`** 是各移动状态的统一出口：
  ```csharp
  public void HorizontalMoveController()
      => SetVelocity(PlayerState.xInput * moveSpeed, rb.velocity.y);
  ```
  只改 X、保留 Y，所以空中/地面都能用。
- **`BusyFor` 协程**实现攻击后硬直：
  ```csharp
  public IEnumerator BusyFor(float _seconds)
  {
      isBusy = true;
      yield return new WaitForSeconds(_seconds);
      isBusy = false;
  }
  ```
  `Idle` 状态检查 `!player.isBusy` 才允许转向 `Move`。

---

### 3.11 `Entity.cs` —— 玩家/敌人公共基类

```csharp
#region 碰撞&墙体检测
protected virtual void CollisionCheck()
{
    Vector2 pos = groundCheck.position;
    // 三点射线检测：中、左、右 —— 解决站在平台边缘时的误判
    centerCheck = Physics2D.Raycast(pos, Vector2.down, groundCheckDistance, whatIsGround);
    leftCheck   = Physics2D.Raycast(pos + Vector2.left  * groundCheckDeviate, Vector2.down, groundCheckDistance, whatIsGround);
    rightCheck  = Physics2D.Raycast(pos + Vector2.right * groundCheckDeviate, Vector2.down, groundCheckDistance, whatIsGround);
}

public virtual bool IsGroundDetected() => centerCheck || leftCheck || rightCheck;
public virtual bool IsWallDetected()   => Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround);
```

**要点**
- **三点地面检测**：只用一根射线时，角色站在两块平台的接缝处会误判为悬空。
  中/左/右三点中任一命中即算在地面（`||` 逻辑），显著提升容错。
- **检测结果在 `Update()` 中缓存**（`CollisionCheck()` 每帧调一次），
  状态里可反复读 `IsGroundDetected()` 而不重复射线开销。墙检测则每次实时射线（因 `facingDir` 会变）。
- **`OnDrawGizmos()`** 把检测线画在 Scene 视图：地面红线（三条）、墙体绿线。
  调试角色卡墙/浮空问题时非常直观。

```csharp
#region 速度设置
public virtual void SetVelocity(float _xVelocity, float _yVelocity)
{
    if (isKnocked) return;                  // 击退期间屏蔽一切速度写入
    rb.velocity = new Vector2(_xVelocity, _yVelocity);
    FlipController(_xVelocity);             // 由速度方向自动决定翻转
}
```

**要点**
- **`isKnocked` 守卫**：受击击退的 `knockBackDuration` 内，所有 `SetVelocity` 调用被忽略，
  保证击退位移不被玩家输入打断。这是状态机与击退协程协同的关键。
- **自动翻转**：写入 X 速度时顺带 `FlipController`，所以玩家不需要显式调用翻转。
- **最大下落速度限制**（在 `Entity.Update()`）：
  ```csharp
  if (rb.velocity.y < -maxFallSpeed)
      this.SetVelocity(rb.velocity.x, -maxFallSpeed);
  ```
  防止长时间下落导致速度过大而穿透碰撞体（隧道效应）。
- **`Damage()` 的击退方向计算**：
  ```csharp
  if (Mathf.Approximately(transform.position.x, attackerPosition.x))
      direction = -facingDir;                        // 完全重叠 → 向后
  else
      direction = transform.position.x > attackerPosition.x ? 1 : -1;  // 背离攻击者
  ```
  并在击退方向与朝向相同时额外推 1.2 倍距离并 `Flip()`，避免贴脸时击退不明显。
- **弹反/拼刀的虚方法钩子**（默认全返回 `false`/空实现）：
  ```csharp
  public virtual bool CanCounter(AttackHitbox incomingAttack) => false;
  public virtual void OnCounterSuccess(Entity attacker) { }
  public virtual void OnCountered(Entity defender) { }
  public virtual bool CanClash(Entity opponent) => false;
  public virtual void OnClash(Entity opponent) { }
  ```
  基类提供**空实现**，子类按需 override。`AttackHitbox` 无需知道对方具体类型，
  只调这些通用接口——**开闭原则**的良好实践。

---

### 3.12 `AttackHitbox.cs` —— 攻击判定 / 拼刀 / 弹反

```csharp
private readonly HashSet<Entity> hitTargets = new();      // 单次挥击内去重
private readonly Collider2D[] overlapResults = new Collider2D[16];  // 预分配，零 GC
```

**要点**
- **`HashSet<Entity> hitTargets`**：保证一次挥击对同一目标**只造成一次伤害**
  （`hitTargets.Add(target)` 返回 false 即已命中过）。在 `EnableHitbox()` 时清空。
- **`overlapResults` 预分配数组**：`OverlapCollider` 的非分配重载，
  避免每次判定都产生 GC——**性能意识很好**。
- **`LateUpdate` 中判定**：
  ```csharp
  private void LateUpdate()
  {
      if (isActive) ResolveOverlaps();
  }
  ```
  放在 `LateUpdate` 保证在动画/位移更新之后再算判定，位置最准确。

```csharp
private bool TryClash(AttackHitbox otherHitbox)   // 拼刀
{
    // 排除：空 / 自己 / 对方未激活 / 同一宿主
    if (otherHitbox == null || otherHitbox == this || !otherHitbox.isActive
        || otherHitbox.ownerEntity == ownerEntity) return false;

    AttackHitbox clashOwner = null;
    if (ownerEntity.CanClash(otherHitbox.ownerEntity))        clashOwner = this;
    else if (otherHitbox.ownerEntity.CanClash(ownerEntity))   clashOwner = otherHitbox;
    if (clashOwner == null) return false;                     // 双方都无资格 → 不拼刀

    DisableHitbox();
    otherHitbox.DisableHitbox();                              // 双方判定盒同时关闭
    clashOwner.PlayHitStop(clashOwner.clashEffect);
    clashOwner.ownerEntity.OnClash(clashOwner == this ? otherHitbox.ownerEntity : ownerEntity);
    return true;
}
```

**要点**
- **双向询问**：先问自己能否拼刀，再问对方。任一方有资格即成立。
  这样设计让"只有玩家攻击动画的开窗帧能拼刀"这类规则可以单边实现。
- **拼刀优先于命中**：`ResolveOverlaps()` 先跑一遍 `TryClash`，一旦成立就 `return`，
  不再结算普通伤害——**拼刀互不受伤**。
- ⚠ **注意**：`TryClash` 在第一次循环中若返回 `true` 就 `return`，因此**一次最多只处理一对拼刀**。
  三方混战时其余碰撞会被忽略。对本项目（1v1）无影响。

```csharp
private void TryHit(Collider2D other)   // 普通命中 / 弹反
{
    if (!IsInLayerMask(other.gameObject.layer, targetLayer)) return;

    Entity target = other.GetComponent<Entity>();
    if (target == null || target == ownerEntity || !hitTargets.Add(target)) return;

    if (canBeCountered && target.CanCounter(this))   // 对方处于弹反架势
    {
        DisableHitbox();                             // 攻击方判定失效（被打断）
        PlayHitStop(counterEffect);
        target.OnCounterSuccess(ownerEntity);        // 通知对方弹反成功
        return;                                      // 不造成伤害
    }

    target.Damage(ownerEntity.transform.position);   // 正常造成伤害
    PlayHitStop(hitEffect);
}
```

**要点**
- **弹反优先于伤害**：先问 `target.CanCounter(this)`，成立则攻击方判定盒被关闭且不造成伤害。
- **`PlayHitStop`：震屏与顿帧的时间对齐**
  ```csharp
  float shakeTime = settings.freezeTime > 0f
      ? Mathf.Min(settings.shakeTime, settings.freezeTime)   // 震屏不超过顿帧时长
      : settings.shakeTime;
  CameraShaker.Instance?.RequestShake(settings.shakeAmount, shakeTime);
  TimeManager.FrameFreeze(settings.freezeTime);
  ```
  因为顿帧期间画面是静止的，若震屏时间更长，冻结结束后会出现"只剩抖动"的割裂感。
  这里取 `Min` 让两者同步结束——**细节处理专业**。
- **`[Serializable] HitStopSettings`** 三个字段（`shakeAmount` / `shakeTime` / `freezeTime`）
  打包成可在 Inspector 折叠编辑的结构，分别配置**普通命中 `hitEffect`**、
  **弹反 `counterEffect`**、**拼刀 `clashEffect`** 三套手感参数。
- **`Awake()` 自检**：
  ```csharp
  if (hitbox == null || ownerEntity == null)
  {
      Debug.LogError($"{name} 需要与 PolygonCollider2D 挂在同一物体上，且父物体需要 Entity。", this);
      enabled = false;   // 禁用组件而非抛异常，避免刷屏
      return;
  }
  ```
  挂错位置时给出**明确的中文提示并优雅降级**，比直接空引用崩溃友好得多。
- **`overlapFilter`**：
  ```csharp
  overlapFilter.SetLayerMask(targetLayer.value | (1 << gameObject.layer));
  ```
  同时检测"目标层"和"自己所在层"——**后者是为了检出对方的攻击判定盒以实现拼刀**。

---

### 3.13 `PlayerAnimationTriggers.cs` —— 动画事件桥

```csharp
public class PlayerAnimationTriggers : MonoBehaviour
{
    private Player player => GetComponentInParent<Player>();   // 表达式体属性，每次访问都查找（有轻微开销）

    private void AnimationTrigger()          // 动画最后一帧
    {
        if (player.currentHitBox != null) player.currentHitBox.DisableHitbox();
        player.CloseClashWindow();
        player.AnimationTrigger();           // → stateMachine.currentState.AnimationFinishTrigger()
    }

    private void AttackTrigger()             // 攻击判定帧
    {
        if (player.currentHitBox != null) player.currentHitBox.EnableHitbox();
    }

    private void AttackEndTrigger()          // 判定结束帧
    {
        if (player.currentHitBox != null) player.currentHitBox.DisableHitbox();
    }

    private void OpenClashWindow()  => player.OpenClashWindow();
    private void CloseClashWindow() => player.CloseClashWindow();
}
```

**要点**
- **动画事件（Animation Event）机制**：在 Animator 的关键帧上挂这些方法名，
  动画播到该帧时 Unity 反射调用。**方法必须是 `private void` 且无参**（或有 1 个基础类型参数）。
- 该组件需挂在**含 Animator 的子物体**上，靠 `GetComponentInParent<Player>()` 向上找宿主。
- **`AttackTrigger` / `AttackEndTrigger` 配对**：由动画精确控制判定盒的开启区间，
  这正是"攻击判定只在挥剑瞬间生效"的实现，比代码硬编码时间更灵活。
- `player` 是**表达式体属性**（`=>`），每次访问都执行 `GetComponentInParent`。
  在同一帧多次调用时会有重复查找开销——可改为 `Awake()` 里缓存一次。

---

### 3.14 支撑基建

**`TimeManager.cs`（顿帧）**

```csharp
public static void FrameFreeze(float duration)
{
    if (Instance == null || duration <= 0f) return;

    // 取最大值：多次顿帧请求可叠加而不互相截断
    Instance.freezeUntil = Mathf.Max(Instance.freezeUntil, Time.realtimeSinceStartup + duration);
    if (Instance.freezeRoutine == null)
        Instance.freezeRoutine = Instance.StartCoroutine(Instance.Freeze());
}

private IEnumerator Freeze()
{
    restoreTimeScale = Time.timeScale;   // 记录原值，支持慢动作叠加
    Time.timeScale = 0f;
    while (Time.realtimeSinceStartup < freezeUntil)
        yield return null;               // ⚠ 用 realtime，timeScale=0 时 deltaTime 为 0
    Time.timeScale = restoreTimeScale;
    freezeUntil = 0f;
    freezeRoutine = null;
}
```

**要点（含一个真实 Bug 隐患）**
- **必须用 `Time.realtimeSinceStartup`**：`Time.timeScale = 0` 时 `Time.time` 和 `deltaTime`
  都停止推进，只有 `realtimeSinceStartup` 照常走，否则协程会永久卡住。
- **`Mathf.Max` 让顿帧可叠加**：连续命中时取最晚的结束时间，不会因新请求而缩短旧顿帧。
- ⚠ **`restoreTimeScale` 的嵌套缺陷**：它被保存为**实例字段**。
  若在顿帧进行中（`timeScale` 已是 0）再次调用 `FrameFreeze`，
  由于 `freezeRoutine != null` 不会重启协程，`restoreTimeScale` 不会被覆盖——这点是安全的。
  但若外部在顿帧期间修改了 `timeScale`（如慢动作技能），恢复值会失真。
  新项目若要做"子弹时间+顿帧"叠加，建议改用引用计数或保存"目标 timeScale"。

**`CameraShaker.cs`（震屏）**

```csharp
[DefaultExecutionOrder(-100)]      // 保证早于其它脚本初始化
public class CameraShaker : MonoBehaviour
{
    private readonly List<ShakeRequest> requests = new();   // 支持多请求叠加
    private CinemachineBasicMultiChannelPerlin _noise;      // ⚠ 依赖 Cinemachine 包

    private void Awake()
    {
        Instance = this;
        _noise = GetComponent<CinemachineVirtualCamera>()
            .GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    private void LateUpdate()
    {
        if (_noise == null) return;

        // 倒序移除过期请求
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            requests[i].remaining -= Time.unscaledDeltaTime;   // ⚠ 用 unscaled，顿帧时仍衰减
            if (requests[i].remaining <= 0f) requests.RemoveAt(i);
        }

        // 取当前最强的一个，并随时间线性衰减
        _noise.m_AmplitudeGain = requests.Count > 0
            ? requests.Max(request => request.amount * request.remaining / request.duration)
            : 0f;
    }
}
```

**要点**
- **用 `Time.unscaledDeltaTime` 衰减**：顿帧时 `timeScale = 0`，
  若用 `deltaTime` 则震屏请求永不衰减，顿帧结束后画面会持续抖动。
- **震屏强度线性衰减**：`amount * remaining / duration` 从满强度平滑降到 0。
- **`requests` 列表**天然支持多个震源叠加，取最强值而非累加，避免画面过抖。
- ⚠⚠ **本项目未安装 Cinemachine 包**，`using Cinemachine;` 会直接导致编译失败。
  这是三大错误之一（详见第 5 节）。

**`PlayerManager.cs` / `SkillManager.cs`（单例）**

```csharp
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager instance;
    public Player player;

    private void Awake()
    {
        if (instance != null) Destroy(instance.gameObject);   // 重复则销毁旧的
        else instance = this;
    }
}
```

**要点**
- 这是"后加载覆盖"式单例：重复实例会**销毁已存在的那个**（连同其 GameObject）。
  ⚠ 注意 `Destroy` 是**延迟到帧末**执行的，同一帧内 `instance` 仍指向旧对象，
  紧接着赋值为新对象，逻辑上安全但需知晓。
- `SkillManager` 在 `Start()` 里 `GetComponent<DashSkill>()` / `GetComponent<CloneSkill>()`，
  所以 `DashSkill` 和 `CloneSkill` **必须和 `SkillManager` 挂在同一个 GameObject 上**。
- `Player.cs` 在 `Start()` 里 `skill = SkillManager.instance;`，
  依赖 `SkillManager` 单例已就绪。

**`EntityFx.cs`（受击闪白）**

```csharp
private IEnumerator FlashFx()
{
    if (sr == null) { flashCoroutine = null; yield break; }

    // 参数缺失时优雅降级：恢复原材质后直接退出
    if (originalMat == null || hitMat == null ||
        totalTime <= 0f || fxTime <= 0f || originalTime <= 0f)
    {
        if (originalMat != null) sr.material = originalMat;
        flashCoroutine = null;
        yield break;
    }

    float elapsedTime = 0f;
    while (elapsedTime < totalTime)
    {
        sr.material = hitMat;
        float waitTime = Mathf.Min(fxTime, totalTime - elapsedTime);  // 防止超时
        yield return new WaitForSeconds(waitTime);
        elapsedTime += waitTime;
        if (elapsedTime >= totalTime) break;

        sr.material = originalMat;
        waitTime = Mathf.Min(originalTime, totalTime - elapsedTime);
        yield return new WaitForSeconds(waitTime);
        elapsedTime += waitTime;
    }
    sr.material = originalMat;      // 确保收尾恢复
    flashCoroutine = null;
}
```

**要点**
- **交错闪烁**：`hitMat` 持续 `fxTime` → `originalMat` 持续 `originalTime` → 循环，总时长 `totalTime`。
- **`Mathf.Min(..., totalTime - elapsedTime)`**：防止最后一次等待超出总时长。
- **重复受击处理**：`PlayFlash()` 先 `StopCoroutine` 旧的再启新的，
  避免多个闪烁协程同时改材质导致闪烁混乱。
- **健壮性**：`sr` / 材质 / 时间参数任一缺失都能安全退出并恢复材质。

**`Skill.cs` / `CloneSkill.cs`（技能）**

```csharp
// Skill 基类：冷却 + 缓冲
public virtual bool CanUseSkill()
{
    if (coolDownTimer <= 0)
    {
        UseSkill();
        coolDownTimer = coolDown;
        return true;
    }
    Debug.Log("Skill is on coolDown");
    return false;
}
```

**要点**
- 基类提供**冷却框架**，子类只需 override `UseSkill()` 实现效果。
- ⚠ **`Skill.cs` 声明了 `buffer` / `bufferTimer` 字段但从未使用**——属于半成品，
  缓冲逻辑没有实现（真正的缓冲在 `Player.cs` 里另行实现）。
- ⚠ **`DashSkill.UseSkill()` 只打了一句 `Debug.Log("创造克隆")`**，是空壳。
- ⚠ **`CloneSkill.CreateClone()` 每次 `Instantiate` 新对象且只保存最后一个引用到 `newClone`**：
  连续冲刺会产生多个残影，但 `DestroyClone()` 只能销毁最后一个——存在对象泄漏。
  残影销毁实际靠 `CloneSkillController.Update()` 的 `Destroy(gameObject)` 自我了断，所以能work。
- ⚠ **`CloneSkillController.cs` `using System.Diagnostics;`** 与 `UnityEngine.Debug` 冲突，
  虽未直接调用 `Debug` 而无编译错误，但属应清理的残留（且它用的是 `Random.Range`，
  在同时引入 `System` 与 `UnityEngine` 时也可能产生歧义）。

---

## 4. 依赖关系图（编译必需）

箭头表示"前者引用后者"。

```
Player.cs  ──┬─→ PlayerStateMachine.cs ─→ PlayerState.cs
             ├─→ 全部 12 个 Player*State.cs
             ├─→ Entity.cs ──┬─→ AttackHitbox.cs
             │               └─→ EntityFx.cs
             ├─→ SkillManager.cs ─┬─→ Skill.cs ─┬─→ DashSkill.cs
             │                    │             └─→ CloneSkill.cs ─→ CloneSkillController.cs
             │                    └─→ CloneSkill.cs
             ├─→ PlayerState.xInput (静态成员)
             └─→ TimeManager.cs / CameraShaker.cs (经状态间接)

PlayerState.cs ─→ Player.cs / Rigidbody2D / Animator
PlayerGroundedState.cs ─→ PlayerState.cs
PlayerDashState.cs ─→ player.skill.clone (SkillManager+CloneSkill)
PlayerSuccessfulCounterAttackState.cs ─→ CameraShaker.cs, TimeManager.cs
AttackHitbox.cs ─→ Entity.cs / TimeManager.cs / CameraShaker.cs
Entity.cs ─→ EntityFx.cs / AttackHitbox.cs
PlayerAnimationTriggers.cs ─→ Player.cs
```

**若不装 Cinemachine**：`CameraShaker.cs` 编译失败 → `AttackHitbox.cs`、
`PlayerSuccessfulCounterAttackState.cs` 连带失败 → **整个程序集编译失败**，
状态机也无法运行。这是"无法运行"的**首要原因**。

---

## 5. 无法运行的根因与修复

> **修复状态：已完成并验证。** 以下 3 类问题均已修复，并用 Roslyn 编译器
> （`dotnet csc`，引用 Unity 2022.3.62f2c1 的 `UnityEngine`/`UnityEditor`/netstandard 2.1 程序集
> 以及 Cinemachine 2.10.3 源码）对全部 43 个脚本做了实际编译验证：
> **退出码 0，0 个错误。**

### 🔴 错误 1（致命）：`EnemyState.cs` 引用了 `UnityEditor`

```csharp
// Assets\Scripts\Enemy\EnemyState.cs 第 4 行
using UnityEditor;      // ← 运行时程序集不能引用编辑器程序集
```

`UnityEditor` 命名空间只存在于编辑器环境。**一旦打包构建就会报
`The type or namespace name 'UnityEditor' could not be found`**，
在编辑器中也会因 `Assembly-CSharp` 不含该引用而失败。
该文件实际并未使用 `UnityEditor` 里的任何类型，属于**导入时的误加**。

**修复**：删除该 `using`。同类问题还有 `SkeletonIdleState.cs` 的
`using UnityEditor.ProjectWindowCallback;`。
**已修复**：两处 `using` 均已删除并编译验证通过。

### 🔴 错误 2（致命）：`CameraShaker.cs` 引用未安装的 Cinemachine

```csharp
using Cinemachine;      // ← 包未安装（已核实 manifest.json 与 PackageCache 中均无）
...
private CinemachineBasicMultiChannelPerlin _noise;
_noise = GetComponent<CinemachineVirtualCamera>()
    .GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
```

已核实：`Packages\manifest.json` **没有** `com.unity.cinemachine` 条目。

**修复方案（二选一）**
- **方案 A（已采用 ✅，改动最小）**：在 `Packages\manifest.json` 中加入
  `"com.unity.cinemachine": "2.10.3"`。该版本已存在于本机 Unity 包缓存
  （`%LOCALAPPDATA%\Unity\cache\packages\packages.unity.cn\com.unity.cinemachine@2.10.3`），
  无需联网即可解析。已核实 `CameraShaker` 所用的三个 API
  （`CinemachineVirtualCamera`、`GetCinemachineComponent<T>()`、`m_AmplitudeGain`）
  在该版本中均存在，且与全部 43 个脚本联合编译通过。
- **方案 B（备选，不依赖第三方）**：把 `CameraShaker` 改写为纯 Transform 抖动。

> ⚠ 若方案 A 在编辑器中仍报缺失，请在 **Window → Package Manager** 中确认
> Cinemachine 已安装；离线环境可用 `Add package from disk` 指向上述缓存目录。

### 🟡 错误 3（非致命，但要清理）：无用/冲突的 `using`

| 文件 | 行 | 问题 |
|---|---|---|
| `PlayerWallSlideState.cs` | 3, 4 | `System.Net.NetworkInformation`、`Unity.VisualScripting` 完全无用 |
| `CloneSkillController.cs` | 3 | `System.Diagnostics` 与 `UnityEngine.Debug` 潜在冲突 |
| `EnemyState.cs` | 3 | `System.Runtime.Serialization` 无用 |
| `SkeletonBattleState.cs` / `SkeletonMoveState.cs` / `SkeletonIdleState.cs` | — | 同样有 `System.Runtime.Serialization` 等残留 |
| `SkeletonGroundedState.cs` | 3 | `System.Data` 无用 |
| `EndLessBackGrounds.cs` | 4 | `UnityEngine.UIElements` 无用 |

这些**不会导致编译失败**（除 `UnityEditor` 那处），但说明代码在导入过程中
被 IDE 自动补全污染过，建议一并清理。

**已清理清单（全部编译验证通过）**

| 文件 | 移除的 `using` |
|---|---|
| `Enemy\EnemyState.cs` | `System.Runtime.Serialization`、`UnityEditor` |
| `Enemy\Skeleton\SkeletonIdleState.cs` | `System.Runtime.Serialization`、`UnityEditor.ProjectWindowCallback` |
| `Enemy\Skeleton\SkeletonBattleState.cs` | `System.Runtime.Serialization` |
| `Enemy\Skeleton\SkeletonMoveState.cs` | `System.Runtime.Serialization` |
| `Enemy\Skeleton\SkeletonGroundedState.cs` | `System.Data` |
| `Player\PlayerWallSlideState.cs` | `System.Net.NetworkInformation`、`Unity.VisualScripting` |
| `Skill\CloneSkillController.cs` | `System.Diagnostics` |
| `BackGrounds\EndLessBackGrounds.cs` | `UnityEngine.UIElements` |

### 🟢 逻辑问题（不影响编译，但建议新项目修正）

| 位置 | 问题 | 建议 |
|---|---|---|
| `PlayerState.cs` | `static xInput/yInput` 全局共享 | 改为实例字段或独立的 `InputReader` |
| `PlayerDashState.cs` | `rb.gravityScale = 5f` 硬编码 | 改为 `player.gravityScale` |
| `PlayerAirState.cs` | 落地一律进 `idleState` | 按 `xInput` 决定进 `Idle` 或 `Move` |
| `CameraShaker.cs` | `restoreTimeScale` 单字段 | 改用引用计数支持叠加 |
| `CloneSkill.cs` | 只记录最后一个 clone 引用 | 用列表管理或依赖自销毁 |
| `Skill.cs` | `buffer`/`bufferTimer` 未使用 | 实现或删除 |
| `PlayerAnimationTriggers.cs` | 属性每次 `GetComponentInParent` | `Awake()` 缓存 |
| `Entity.cs` | 击退时直接改 `transform.position` | 改为速度驱动以配合物理 |

---

## 6. 新项目接入建议（推荐顺序）

**Step 1 — 最小可运行集（先把状态机跑起来）**

```
Entity.cs
Player.cs
PlayerStateMachine.cs
PlayerState.cs
PlayerGroundedState.cs
PlayerIdleState.cs
PlayerMoveState.cs
PlayerJumpState.cs
PlayerAirState.cs
PlayerAnimationTriggers.cs
```

此阶段需**临时删掉** `Player.cs` 中这些依赖（或后续补齐）：
`skill = SkillManager.instance;`、`CheckDashInput()`、`CheckWallJumpInput()`、
`CheckAttackInput()`、`CheckCounterAttackInput()`，以及 `Awake()` 里对应的状态实例化，
并删除 `#region 拼刀/反击` 相关内容。

**Step 2 — 补齐平台跳跃手感**

加入 `PlayerDoubleJumpState.cs`、`PlayerDashState.cs`（先删掉残影调用）、
`PlayerWallSlideState.cs`、`PlayerWallJumpState.cs`。
此时需要在 `Player.cs` 恢复 `CheckDashInput()` / `CheckWallJumpInput()`。

**Step 3 — 接入战斗**

加入 `AttackHitbox.cs`、`TimeManager.cs`、`EntityFx.cs`、`PlayerPrimaryAttackState.cs`、
`PlayerCounterAttackState.cs`、`PlayerSuccessfulCounterAttackState.cs`，
以及 `Entity.cs` 中已内置的拼刀/弹反钩子。

**Step 4 — 震屏（需先解决 Cinemachine）**

安装 Cinemachine 或改写 `CameraShaker` 为 Transform 抖动，再恢复相关调用。
`AttackHitbox.PlayHitStop()` 与 `PlayerSuccessfulCounterAttackState` 中已用 `?.` 空保护，
**即使 `CameraShaker` 缺失也不会崩溃**，只是没有震屏。

**Step 5 — 技能（可选）**

`Skill.cs` / `SkillManager.cs` / `CloneSkill.cs` / `CloneSkillController.cs` / `DashSkill.cs`。

---

## 7. 场景搭建要点（须知）

玩家对象需要挂载/配置以下内容，否则状态机虽然编译通过但运行报错：

| 项目 | 说明 |
|---|---|
| `Player` 组件 | 挂在玩家根物体 |
| `Rigidbody2D` | `gravityScale` 由代码设置；建议 `Freeze Rotation Z` |
| `Animator` | 需在**子物体**上（`Entity.Start()` 用 `GetComponentInChildren<Animator>()`） |
| `EntityFx` | 与 `Player` 同物体（`GetComponent<EntityFx>()`） |
| `groundCheck` | 空物体，置于**脚底**，赋给 `Entity.groundCheck` |
| `wallCheck` | 空物体，置于**侧面**，赋给 `Entity.wallCheck` |
| `attackCheck` | 空物体，**父物体下需有 `AttackHitbox`**（`Entity.Start()` 用 `GetComponentInChildren`） |
| `PlayerAnimationTriggers` | 挂在**含 Animator 的物体**上，并配置动画事件 |
| `whatIsGround` | LayerMask，务必勾选地面层 |
| `attackHitboxes[]` | `AttackHitbox` 数组，按连击段数顺序填入 |
| `attackMovement[]` | `Vector2` 数组，每段攻击的位移量 |

**Animator 参数清单**（代码中出现的全部）

| 类型 | 名称 | 使用者 |
|---|---|---|
| Bool | `Idle`, `Move`, `Jump`, `Dash`, `WallSlide`, `Attack`, `CounterAttack`, `SuccessfulCounterAttack` | `PlayerState.Enter/Exit` 按状态名自动设置 |
| Float | `yVelocity` | `PlayerState.Update`（跳跃/下落混合树） |
| Integer | `ComboCounter` | `PlayerPrimaryAttackState.Enter` |

**必备动画事件**（挂在攻击动画帧上）

| 方法名 | 触发时机 |
|---|---|
| `AttackTrigger` | 判定盒开启帧 |
| `AttackEndTrigger` | 判定盒关闭帧 |
| `AnimationTrigger` | 动画最后一帧（发信号给状态机） |
| `OpenClashWindow` / `CloseClashWindow` | 拼刀窗口的开/闭帧 |

---

## 8. 总体评价

**优点**
1. **分层状态机设计规范**：抽出 `PlayerGroundedState` 中间层统一地面/空中转移，是专业做法。
2. **手感细节到位**：土狼时间、跳跃/攻击/冲刺三套输入缓冲、可变跳跃高度、
   贴墙刷新二段跳、攻击带前冲、击退期间屏蔽输入。
3. **动画驱动逻辑**：用 `Animation Event` + `triggerCalled` 让状态时长跟随动画，
   而非硬编码时间，美术调整后逻辑自动适配。
4. **战斗系统扩展性好**：`Entity` 提供弹反/拼刀虚方法钩子，`AttackHitbox` 只依赖通用接口，
   新增角色类型无需改动判定代码。
5. **性能意识**：碰撞检测结果按帧缓存、`OverlapCollider` 预分配数组避免 GC、
   `HashSet` 保证单次挥击不重复伤害。
6. **健壮性**：`Debug.LogError` + `enabled = false` 优雅降级、
   `?.` 空条件保护单例、盾式双退出条件防状态卡死。

**待改进**
1. **`static` 输入字段**是明确的设计缺陷，应改为实例字段。
2. **转移关系分散在各状态内部**，规模扩大后建议引入集中式转移表或
   `CanTransitionTo()` 校验，便于总览与调试。
3. 若干**硬编码值**（`gravityScale = 5f`、`stateTimer = .1f`）应改为引用配置字段。
4. **导入残留的 `using`** 需要清理，尤其 `UnityEditor`（致命）。
5. `Skill` 体系是**半成品**，`buffer`/`bufferTimer` 未实现、`DashSkill` 为空壳。

**结论**：这是一套**质量高于平均水平的 2D 平台动作游戏状态机**，架构与手感处理都值得
作为新项目基座。当前"无法运行"**纯粹是导入残留的引用问题（Cinemachine 缺失 +
`UnityEditor` 误引用）**，与状态机设计本身无关，修复成本很低。
