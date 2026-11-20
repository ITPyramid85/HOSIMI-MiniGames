# 音游数据层（配置 v2 / 存档 v2）

配置入口：`Resources/RhythmGame/default_config.json`。适用于当前 Unity 6000.0 项目。

当前默认玩法是 60 秒的渐进单击小游戏：Perfect / Good / Miss 三档，Miss 扣减条，角色作为陪伴与引导。旧角色升级、技能、HP 字段和四档枚举保留为兼容数据；当前玩法不启用升级加分或角色技能。存档仍为 v2，无需迁移、清档或重置金币。独立商城界面尚未制作。

## 主界面与 GameManager

打开 `Assets/Scenes/MainMenu.unity` 后进入 Play，或使用菜单 `Rhythm Game > Open Main Menu`。已将 MainMenu 注册为 Build Settings 的第一个场景，原 SampleScene 保留。

- 设置：实例化 `Assets/RhythmGame/Prefabs/SettingsPanel.prefab`，重复打开复用同一实例。弹窗期间屏蔽主菜单，关闭按钮、Esc 或手柄 B 可关闭。设置面板目前只有标题、占位说明和关闭按钮，尚未绑定语言／音量控件。
- 开始游戏：加载 `Game` 场景，显示操作说明，点击“开始演奏”后倒计时。商城仍为占位按钮；角色按钮打开可操作的角色面板。
- 退出游戏：尝试保存数据后退出；在 Unity 编辑器中停止 Play。
- 布局：UGUI Canvas + CanvasScaler，1920×1080 参考分辨率，Expand 缩放；所有按钮、位置和颜色可直接在场景中编辑。
- 输入：使用已有的 Input System UI 模块，支持鼠标、键盘导航和手柄导航。
- 菜单与弹窗文字按存档语言显示中／英／韩文。当前从系统字体获取 CJK 字形；发布到目标平台前，建议在 Canvas 和 SettingsPanel 的 `MenuTypography.fontOverride` 上指定有授权的中英韩字体资源。

`GameManager` 的 prefab 位于 `Assets/RhythmGame/Resources/RhythmGame/GameManager.prefab`。启动任意场景时自动实例化，`DontDestroyOnLoad` 保留同一个实例和 `RhythmGameData`；主场景中也放置了可见 prefab，重复实例会自动销毁。切换场景不应重新调用 `RhythmGameData.Open()`。

```csharp
RhythmGameData data = GameManager.Instance.Data;
PlayerData player = data.Player;
PlayerSettings settings = data.Settings;
// 后续场景都经由同一个 data 调用已有服务。
```

先检查 `GameManager.Instance.IsReady` 再使用数据。读取失败时记录 `InitializationError` 并显示错误，不自动覆盖原存档。游戏暂停进入后台、正常退出时尝试保存；强制结束进程不能保证收到生命周期回调，业务操作原有自动保存仍然保留。

## 角色界面

主界面点击“角色”打开 `Prefabs/CharacterPanel.prefab`。列表保留原角色及购买价格，支持购买、唯一选择与保存重试。界面不再展示等级、基础分、升级按钮或技能说明，选中的角色作为引导伙伴。旧等级数据保留，进入当前玩法统一按 100 基础分计算。现有角色立绘仍为占位；开场有临时鼓形伙伴和循环点击图示，后续可以替换正式角色美术。

## 兼容数据与旧规则

下面的角色养成、技能及四档判定配置供旧玩法与存档继续读取。新玩法的覆盖规则见文末，不能把旧字段直接删除或重排枚举数值。

## 配置规则

- 初始金币 0，免费获得「律」；「星」1500 金币，「燃」2000 金币。
- 三个角色均为 1～100 级，每音符基础分从 100 线性增长至 200。
- 基础分 = 100 + 100 × (等级 - 1) / 99，内部保留小数。
- 从当前等级升到下一级费用 = 100 + 20 × 当前等级。1→2 级花费 120；99→100 花费 2080；累计满级花费 108900。
- 技能枚举：0 = 所有命中加分，1 = 仅 Perfect 加分，2 = 达到 Combo 门槛加分，3 = 恢复血量。所有技能都必须先命中技能单击音符，没有被动自动触发或独立技能按钮。
- 判定枚举：0 = Perfect，1 = Great，2 = Good，3 = Miss。JSON 中使用数字，枚举次序不要更改。
- 判定边界包含等号：绝对偏差 ≤40ms 为 Perfect，≤80ms 为 Great，Good 提前侧 ≤200ms、晚按侧 ≤350ms（`rules.lateGoodWindowMs`）。判定基准为音符中心到线；按当前 96 高度、2.5 秒下落速度，整个音符落到线下后仍留约 156ms 补按，补按计 Good、照常断连。超过晚侧窗口才自动 Miss；空按不会扣血。`lateGoodWindowMs` 为 0 或旧配置未填写时，沿用 Good 的对称窗口。
- 得分／准确率权重均为 1、0.7、0.4、0；两套权重分别配置，准确率不包含角色和 Combo 加成。
- Perfect 金色 `#FFD166`；Great 紫色 `#C792EA`；Good 蓝色 `#67C8FF`；Miss 灰红色 `#B56B78`。
- Perfect、Great 延续 Combo，Good、Miss 断连。
- 先处理本次技能音符触发并更新 Combo，再计算 Combo 加成与技能。第 10 次连击起加 1%，第 100 次起封顶 10%；「燃」需要已由技能音符激活效果，且当前 Combo 达到 50 才获得加成。
- 单次原始得分 = 角色基础分 × 判定权重 × (1 + Combo 加成) × (1 + 生效技能加成)。逐次保留小数，累计显示／最终总分使用四舍五入（AwayFromZero）。不要把每次显示的整数相加作为总分。
- 通关条件：所有音符完成判定、血量大于 0、准确率 ≥60%。血量归零立即失败，即使最后一击死亡且准确率很高也不能通关。
- 评级取准确率：S ≥95%、A ≥90%、B ≥80%、C ≥70%，其余 D。是否通关单独显示，D 也可能通关。
- Full Combo = 已通关且没有 Good/Miss；All Perfect = 全部 Perfect。

## 金币与解锁

- 通关基础金币 = 向下取整[(100 + 25 × 难度等级) × (0.5 + 0.5 × 准确率)]。
- 每张谱面首次通关 +200，首次 Full Combo +300，同一局可以同时领取。
- All Perfect 保存纪录，当前没有额外金币奖励。
- 未通关：保留成绩纪录，金币为 0。中途退出：不结算、不计次数、不发奖励。
- 一首示例歌有 Easy / Normal / Hard 三张谱面，分别为 100 / 250 / 500 个音符，难度等级为 1 / 4 / 8。通关前一难度解锁后一难度。
- 同一首歌的不同难度使用不同 `chartId`，各自保存纪录和领取首次奖励。歌曲可用状态可由“至少一张谱面已解锁”派生。
- 最高分、最高准确率、最大 Combo 分别独立保存，它们不一定来自同一局。
- 默认「律」1 级 Easy 全 Perfect：10460 分、准确率 100%、S、首次金币 625；重打同样成绩获得 125 金币。「律」现在为回血技能，不再有原先的常驻 5% 加分。

以上是首轮测试数值。特别是满级周期，需要真实游玩后再调整。

## 血量、技能音符与角色台词

- `rules.maxHealth = 100`，每局满血开始，所有角色和等级共用该上限。
- `rules.missDamage = 10`，每次实际 Miss 扣 10 点；Perfect、Great、Good 不扣血，也不会自然回血。
- 在没有回血的情况下，累计 10 次 Miss 就死亡，不要求连续 Miss。长按漏掉的每个判定点也按一次 Miss 处理。
- 血量在 0～100 之间。死亡后拒绝继续提交判定，不能再靠后面的技能音符复活；调用 `Complete` 按失败结算，不发金币、不解锁歌曲。
- `IsComplete` 仍表示全部音符已判定；游戏结束判断改用 `IsFinished`（打完或死亡）。`PlayResult.FailedByHealth` 区分死亡失败，`RemainingHealth` 给出剩余血量。
- 死亡时未播放音符记录为 `UnplayedNotes`，不会伪造额外 Miss／扣血／台词；准确率分母仍是全谱面的判定数量。四种判定数加上未播放数等于 `noteCount`。

每张谱面的 `skillNoteIndices` 指定技能单击音符的零基判定编号，包含在 `noteCount` 内，不能重复或越界。默认 Easy 在第 20、60、90 个判定点放技能音符；Normal 和 Hard 也有示例编号。`SubmitTiming` 自动查谱面编号，不提供可随意释放技能的接口。

- 技能音符命中 Perfect／Great／Good 都能触发；Miss 不触发，照常断连扣血。Good 触发技能但仍按原有规则断连。
- 「律」：即时恢复 20 点血量，最多回到 100，满血触发也会消耗该音符。
- 「星」：触发后的 20 个判定内，Perfect 加分 10%。
- 「燃」：触发后的 20 个判定内，当前 Combo ≥50 时加分 12%。
- 加分持续次数包含触发音符，Miss 也消耗一次。再次命中技能音符刷新持续次数，不叠加倍率。
- `skillHealAmount`、`skillDurationNotes`、`skillBonus`、`skillComboThreshold` 都在角色配置中；上述技能数值只是初始示例。
- `HitResult.IsSkillNote` 表示本次是技能音符；`SkillTriggered` 表示本次成功触发，回血技能也返回 true；`SkillActive` 只表示这一击实际享受得分加成。显示启动特效应使用 `SkillTriggered`，避免误把普通加分击当作新释放。

每个角色预留 `portraitResource` 用于游玩区域旁的角色立绘，以及 `missLine`、`comboLine` 台词。台词包含中／英／韩文本及三个可选语音资源路径。Game 场景显示角色名称、等级和技能说明，立绘暂为占位；尚未提供立绘和录音。

- 第一次 Miss 返回失误台词，之后至少间隔 5 个实际判定才再次返回，避免连续漏点刷屏。
- Combo 达到 25、50、100 时返回连击台词；每个门槛每局只触发一次，断连后再次到达同一门槛不重复。
- `rules.comboDialogueThresholds` 和 `missDialogueCooldownNotes` 可调整门槛与间隔。
- `HitResult.Dialogue` 为 null 表示没有台词事件。`GameController.DialogueRequested` 预留给后续角色表现系统，本版没有订阅者，不显示台词或播放语音。

## Unity 接入

现在由 `GameManager` 持有一个 `RhythmGameData` 实例，各场景共享它。不要每开一个面板就新建实例，也不要让多个实例同时写同一份存档。

```csharp
using RhythmGame;
using UnityEngine;

// GameManager 启动完成后。
RhythmGameData data = GameManager.Instance.Data;

// 商店/养成/角色选择。余额不足、重复购买、未拥有或满级返回 false。
bool bought = data.Purchase("precise");
bool upgraded = data.Upgrade("steady");
bool equipped = data.Equip("steady");

// 校准值范围 [-300, 300]ms，正数表示玩家的输入通常偏晚。
data.SetInputOffset(25);

// 开始一局。角色等级和校准值在开局时固定，局中禁止商店和换人操作。
PlaySession session = data.BeginPlay("first_beat_easy");

// rawOffsetMs = (输入的歌曲时间 - 谱面音符时间) * 1000。
// 以下仅是一个音符的调用示例，不是完整打歌循环。
HitResult hit = session.SubmitTiming(0, 32);
Color color;
ColorUtility.TryParseHtmlString(hit.ColorHtml, out color);
// 将 color 赋给判定文字，将 hit.Combo / hit.TotalScore 显示到 UI。
// hit.SkillTriggered 表示本次成功触发技能，可播放启动特效。
// hit.Health / session.MaxHealth 可用于更新血条。

// 对漏掉的音符逐个提交：session.SubmitMiss(noteIndex);
// 所有音符已判定，或血量归零时停止输入／歌曲并结算：
if (session.IsFinished)
{
    PlayResult result = data.Complete(session);
    // 显示 Score、Accuracy、Grade、四种判定次数、MaxCombo、各项金币。
}
// 玩家退出时调用 data.Abort(session)，不要再调用 Complete。
```

`noteIndex` 必须对应谱面中稳定的音符序号 `[0, noteCount)`，同一音符不能重复提交。允许不同轨道的音符以实际完成判定的顺序提交。输入层负责匹配有效音符；没有可匹配音符的空按不要调用 `SubmitTiming`。超时漏判由输入／谱面层调用 `SubmitMiss`；考虑校准后，晚侧超时条件为 `当前歌曲时间 - 音符时间 > (rules.LateGoodWindowMs + inputOffsetMs) / 1000`。不要在未走到歌曲结尾时为剩余音符批量提交 Miss，除非确实想结算一局失败成绩。

音乐时间建议基于 `AudioSettings.dspTime` 和预约播放起点计算，避免使用渲染帧累计时间。此数据层只处理时间差，不负责播放或输入采样。

`Config` 和 `Player` 是可序列化 DTO，对 UI 视为只读。通过服务方法修改玩家数据；配置应在重新开局／重新启动服务前从 JSON 加载，不要运行中直接改字段。未知 ID、锁定谱面、重复判定、重复结算等调用错误会抛异常；正常余额不足等购买条件返回 false。

## 存档

- 位置：`Application.persistentDataPath/rhythm-player-v1.json`，路径可通过 `data.SavePath` 获取。
- 购买、升级、换人、校准、语言／音量更改、结算成功后自动保存。
- 先写 `.tmp` 并刷新到磁盘，再替换主存档；`.bak` 保留上一个版本。
- 主存档损坏时尝试备份；`RecoveredFromBackup` 提示 UI 展示“已恢复上一份存档”。两份均无法读取会抛异常，保留原文件，不自动清档。
- 存储空间不足或无写入权限时保留本次内存结果，`HasUnsavedChanges` 为 true，并输出错误日志。UI 应提示重试 `data.Save()`；不要重新调用购买或结算。进程退出前仍未保存的进度会丢失。
- 当前存档版本为 v2，读取旧 v1 存档时自动补上默认设置并在内存中升级，下次保存写入 v2。文件名仍为 `rhythm-player-v1.json`，以便继续找到原存档；配置版本已升为 v2。已有 v2 设置不会被重新初始化，音量 0 会保留。血量、技能持续状态和台词触发纪录只属于本局，不写入玩家进度。
- 旧金币、角色等级和歌曲纪录继续保留；旧规则下取得的最高分不会自动清除，可能与新规则下的分数不同。
- 以后删除／更改已有 ID、改变已记录谱面的音符数量或变更结构时需要迁移方案。追加新的角色／谱面 ID 无需迁移。
- 这是单机本地存档，无服务器校验或防改档功能。

## 语言与音量设置

数据定义：`Runtime/Data/PlayerSettings.cs`；保存在玩家存档的 `settings` 内。默认简体中文，三项音量均为 1（100%），不自动跟随系统语言。

- `language`：0 = 简体中文（`zh-Hans`），1 = English（`en`），2 = 한국어（`ko`）。数字是稳定存档值，不要调整枚举顺序。
- `masterVolume`：主音量，0～1。
- `musicVolume`：音乐音量，0～1。
- `soundEffectsVolume`：音效音量，0～1。
- 0 表示静音，1 表示 100%；非法值、NaN 和无穷大会被拒绝。修改主音量不改变音乐／音效的独立设置。
- `EffectiveMusicVolume` = 主音量 × 音乐音量；`EffectiveSoundEffectsVolume` = 主音量 × 音效音量。

```csharp
data.SetLanguage(GameLanguage.English);
data.SetLanguage(GameLanguage.Korean);
data.SetLanguage(GameLanguage.SimplifiedChinese);
data.SetVolumes(0.8f, 0.6f, 1f); // 主音量 80%、音乐 60%、音效 100%，整组校验后保存。
string locale = data.Settings.LanguageCode;
float musicGain = data.Settings.EffectiveMusicVolume; // 0.48
```

接口返回是否发生变化；重复选择相同设置不触发写盘。语言和音量可以在打歌过程中修改，不影响计分或判定。UI 将滑条百分数除以 100 后提交，建议在拖动结束或按“应用”时调用保存接口，避免每帧写盘。`Settings` 同样视为只读，通过接口修改。

Game 场景开场时将 Effective 值分别应用到音乐和音效 AudioSource，界面文案按存档语言选择中／英／韩文。设置弹窗已提供蓝牙延迟补偿，语言／音量控件尚未绑定。字体当前使用系统 fallback；正式发布需提供有授权的中英韩字体。如果以后使用 AudioMixer 独立应用主音量，各子通道只使用自身音量，避免重复乘主音量。

### 蓝牙耳机延迟补偿

- 设置弹窗提供独立开关、0～1000 ms 滑条、每次 ±10 ms、归零、保存与取消。音乐比亮灯晚时调大数值；保存后仅对检测到的蓝牙音频启用。不是自动测量耳机延迟，也无法消除点击音效本身的输出延迟。
- `PlayerSettings` 新增可选的 `bluetoothCompensationEnabled` / `bluetoothDelayMs`。旧存档缺失时默认关闭、0 ms；保存版本仍为 2。角色、等级、金币、历史成绩和普通 `inputOffsetMs` 不变。
- 生效偏移 = 普通 `inputOffsetMs` +（开启且检测到蓝牙时的保存延迟）。关闭蓝牙／断开耳机后恢复普通偏移，保存值仍保留，下次连接重新生效。无法检测输出时不加蓝牙偏移。开关关闭也不会清空数值。
- `AudioLatencyMonitor` 随 GameManager 跨场景保持，约每 0.5 秒检测，回前台和音频配置变化时立即刷新。游戏中切换输出会自动暂停；重新定位音频并平移判定时钟后由玩家继续，已亮灯的剩余反应时间不会重置，也不补算额外分数。
- Android 13（API 33）起使用 AudioManager 的 `getAudioDevicesForAttributes(USAGE_GAME)` 查询预期输出；Android 6～12 使用 `getDevices(GET_DEVICES_OUTPUTS)` 判断蓝牙音频设备连接状态，此时界面明确显示估计状态，连接耳机却手动使用扬声器时需关闭补偿。无需申请蓝牙扫描权限。参考 [Android AudioManager 文档](https://developer.android.com/reference/android/media/AudioManager#getAudioDevicesForAttributes(android.media.AudioAttributes))。
- iOS 使用原生 `AVAudioSession.currentRoute.outputs` 判断 A2DP、HFP、LE 音频端口，插件仅启用 iOS。参考 [Apple currentRoute](https://developer.apple.com/documentation/avfaudio/avaudiosession/currentroute)。编辑器和其他平台不假装检测蓝牙；测试通过编辑器专用覆盖值模拟输出变化。
- `BluetoothTimingTests` / `BluetoothSettingsTests` 覆盖旧设置、保存重读、普通偏移保留、接通／断开／重连、取消草稿、三语言、亮灯后切换暂停与继续。真实耳机延迟、Android 厂商路由差异和 iOS 原生插件仍需手机实测。

## 编辑器检查

- 菜单 `Rhythm Game > Validate Default Data` 校验配置，并选中 JSON。
- 菜单 `Rhythm Game > Simulate Perfect Run (No Save)` 在内存中模拟一局全 Perfect，不修改正式存档。
- 打开 `Window > General > Test Runner`，运行 EditMode 中的 `RhythmGame.Tests`。测试覆盖养成交易、计分、判定窗口、技能、Combo、结算幂等、奖励、解锁、纪录和存档恢复。
- PlayMode 中的 `MainMenuTests` 覆盖设置弹窗复用与关闭、商城占位按钮无副作用，以及 GameManager 和数据跨场景保持唯一。`CharacterMenuTests` 覆盖购买、升级、唯一出战、金币不足、满级、多语言、保存重读、弹窗互斥和角色带入 Game。

原来的 Easy / Normal / Hard 数据仍作为示例保留，没有对应音频。新增可玩的 `training_01` 使用独立 ID，避免改变旧纪录的谱面长度。

## Game 模式（当前初调版）

从 MainMenu 进入，或打开 `Assets/Scenes/Game.unity`。默认谱面 `training_casual_01` 使用新的 ID 保存成绩，旧 `training_01`、`training_01_single`、`training_01_pads` 的配置和成绩全部保留。

- 8 个按钮仍按 2 / 3 / 3 排布，当前初调回到企划的单灯单击。普通单击之外不生成技能、长按、滑动或同时输入要求。运行时最多显示一个亮灯。
- 一局 60 秒，41 个判定：前 20 秒只使用下排三个，约每 2 秒一拍；20～40 秒使用中下排六个，每 1.5 秒一拍；40～60 秒使用八个，每秒一拍。重试从最简单阶段重新开始。这版完成一个难度周期，尚未接无限循环与周期奖励倍率。
- 初期 Perfect ≤450ms，Good ≤900ms；中期 Perfect ≤350ms，Good ≤700ms；后期 Perfect ≤250ms，Good ≤500ms。超时 Miss。窗口从实际显示亮灯的帧开始；校准只改变亮灯时刻。参数是初调值，在 `Runtime/Logic/CasualMode.cs` 集中调整。
- Perfect 100 基础分，Good 70 基础分，沿用 Combo 加分。Perfect / Good 均延续 Combo，只有 Miss 断连。角色与历史等级不影响本模式得分；旧模式仍使用旧规则。所有 Good 可以通关并取得 Full Combo，All Perfect 必须全部 Perfect。
- 底层继续使用 `Health` / `maxHealth` / `missDamage` 保存本局剩余失误额度，上限 100、每次 Miss 减 10、用尽结束；界面只显示 MISS 次数和递减条，不显示 HP、回血或角色技能。技能字段不触发，旧 Great 字段在本模式结算中为 0，界面只列三档。
- 按钮预告改成亮灯前一拍内的外框闪烁，不再显示容易被误认为长条的进度条。命中或过期后熄灭，按住不自动得分，空按不扣额度、不消费后续节拍。
- 开场用临时鼓形角色、8 格点亮示范和手指点击动画说明，只保留一句中英韩提示。教程为观看示范，不扣额度、不计分。正式角色美术、自由练习与更多角色叙事后续补齐。
- 鼠标／触屏直接点对应按钮。电脑调试键盘仍为 Q E / A S D / Z X C；Esc 暂停。开局 3 秒倒计时，音乐与谱面用同一个 DSP 时钟，暂停冻结判定，切后台自动暂停。
- `CasualBeat.wav` 为独立生成的 60 秒原创测试伴奏，120 BPM；节拍密度随阶段增加，伴奏速度固定。这是初调练习音轨，不是正式歌曲。
- 完成后显示三档判定、分数、最高 Combo 和金币；中途退出不结算，不发奖励。换角色不更改难度与判定窗口。

### 保留的数据结构

`Judgement` 的 Perfect=0 / Great=1 / Good=2 / Miss=3 保持不变；`CharacterConfig`、`PlayerData`、等级、技能、HP、历史成绩字段均保留。`CasualMode` 只是新谱面的运行时规则，不改变存档结构。新旧谱面使用独立成绩 ID，避免直接比较不同规则的分数。角色升级 API 为兼容旧逻辑继续存在，当前角色面板不再提供升级入口。

### 写谱与验证

- `kind`、`time`、`pad` 沿用旧格式；当前模式仅使用 `kind: 0`，`pad` 取 0～7，对应按钮 1～8。`time` 单位为秒。`duration`、`tickInterval`、`travelSeconds` 仍兼容旧格式，新玩法不用长按字段。
- 当前模式按时间排序，逐个出现，间隔要大于前一个判定点的 Good 窗口，以免遮挡后续提示。初期只选 5～7，中期选 2～7，后期可选 0～7。
- 配置中的 `noteCount` 必须等于展开后的判定总数，`skillNoteIndices` 为空。已发布谱面改变规则或数量时新增 ID。
- `CasualModeTests` 验证三档边界、难度分段、无技能／单灯、跨角色与等级计分一致、旧数据保留和失误结束；旧 EditMode 测试保留兼容覆盖。
- PlayMode 测试覆盖图示教程、触摸／键盘点击、宽松反应时间、暂停、完整 60 秒演奏、重试、Miss 条、角色收集与多语言。测试在独立 Unity 项目运行，避免影响真实存档。
