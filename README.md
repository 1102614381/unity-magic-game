# unity-magic-game（魔法岛大冒险）

基于九轴 IMU（WT901WIFI）的儿童体感游戏数据采集平台。本研究用游戏化方式采集高质量 IMU 数据，用于姿态解算误差 / 漂移 / 磁干扰 / 自适应融合研究。

- **研究者（研究生）**：负责 Python 数据层（IMU 接入、解析、CSV 落盘、UDP 收发）。
- **本科生**：只负责 Unity 游戏层（游戏逻辑、UI、场景）。

---

## 目录结构

```
unity-magic-game/
├── imu_data_layer/        # 研究者 Python 数据层（真实录制 / 键盘模拟 / UDP 收发）
├── unity/                 # 本科生 Unity 游戏层脚本（IMUReceiver.cs 等）
├── 两人分工与数据契约_v1.html          # 分工与冻结的数据契约
├── 魔法岛大冒险_初步游戏设计与开发需求说明书_V0.1.docx
└── README.md
```

> 说明：真实录制数据 `imu_data_layer/data/` 与 `__pycache__/` 不进版本库（已在 `.gitignore` 排除）。

---

## 如何同步改动（重要）

**GitHub 不会自动更新。** 你在本地改了文件后，必须手动执行下面三步，GitHub 上的文件才会变化：

```bash
# 先进入项目目录（注意 Git Bash 里 D 盘写成 /d/...）
cd /d/项目/本科生游戏设计

git add 文件名            # 把改动的文件选上（或 git add . 全部选上）
git commit -m "说明改了啥"   # 在本地仓库记一笔
git push                 # 推到 GitHub（首次推送已用 -u 跟踪，之后直接 git push 即可）
```

推送完成后，去 GitHub 网页刷新即可看到文件已更新。

### 拉取别人的改动

如果协作者（彦祖 / 欢婷）在他们的电脑上 `push` 了新内容，**你这边不会自动出现**，必须主动拉一次：

```bash
git pull origin main     # 把别人 push 的改动拉到你的本地
```

反之，你 `push` 之后，他们也需要 `git pull` 才能拿到你的版本。所以「大家看到最新内容」靠的是 **你 push + 别人 pull**，没有自动同步。

### 常用命令速查

```bash
git status              # 看当前改了哪些文件
git add 文件名          # 只加某个文件（比 git add . 更安全，避免误加大文件）
git commit -m "说明"     # 提交
git push                # 推送到 GitHub
git pull origin main    # 开工前拉取最新
git checkout -b 分支名   # 新建并切换到分支
git log --oneline       # 查看提交历史
```

---

## 分支与分工策略

```
main              ← 稳定版，只有研究生审核合并
 ├─ dev/yanzu     ← 彦祖：只动 Assets/Games/G1、G2
 └─ dev/huanting  ← 欢婷：只动 Assets/Games/G3、G4
```

- 每人只在自己负责的游戏文件夹里加场景 / 脚本，**共享的 `IMUReceiver.cs` 谁都不要乱改**。
- 不要在 `main` 上直接改，改完在自己的分支 `push`，再到 GitHub 开 **Pull Request** 给研究生审核合并。

---

## Unity 必改设置（动手前先改，否则场景必冲突）

在 Unity 编辑器里：`Edit → Project Settings → Editor`

- **Version Control → Mode** = `Visible Meta Files`
- **Asset Serialization → Mode** = `Force Text`

改完后提交一次，让所有人 `git pull` 重新拉取。

---

## 本科生接手步骤（新成员）

1. 安装 Git：https://git-scm.com/downloads （默认下一步）。
2. 配置 SSH：
   ```bash
   ssh-keygen -t ed25519 -C "自己的邮箱"
   cat ~/.ssh/id_ed25519.pub        # 复制输出 → GitHub 头像→Settings→SSH and GPG keys→New SSH key
   ssh -T git@github.com            # 看到欢迎语即成功
   ```
3. 克隆仓库：
   ```bash
   git clone git@github.com:1102614381/unity-magic-game.git
   cd unity-magic-game
   ```
4. 建自己的分支：
   ```bash
   git checkout -b dev/yanzu        # 彦祖；欢婷用 dev/huanting
   ```
5. 改完按上面的「如何同步改动」三步提交并 push，最后在 GitHub 开 Pull Request。
