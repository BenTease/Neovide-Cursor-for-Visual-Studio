# Neovide Cursor for Visual Studio

Neovide 风格平滑光标，移植到 Visual Studio 文本编辑器。动画数学（阻尼弹簧驱动的四角四边形）逐行移植自 neovide-cursor（VS Code 扩展），编辑器集成方式参考 Smooth Caret（MEF 视图监听 + AdornmentLayer + 事件驱动 + 注册表配置）。

> 独立的 VSIX 项目，不修改工作区根目录的 Smooth Caret 源码。

## 功能

- 原生光标永久隐藏，取而代之的是 neovide 标志性的四角弹簧动画光标；
- 单行内移动：短动画（`ShortAnimationLength`，默认 40ms）快速平滑；
- 跨行跳转 / 点击其他行 / PageUp/Down：拖尾效果（leading 角瞬移、trailing 角弹簧跟随），`TrailSize` 控制拖尾长度；
- 滚动（滚轮 / 拖动滚动条）：光标瞬移、无拖尾；
- 阴影：多边形边缘模糊光晕（`UseShadow` / `ShadowBlur`）；
- 光标颜色：默认粉色 `#FFC0CB`，可设 `default` 跟随主题文本色。

## 项目结构

| 文件 | 作用 |
|---|---|
| `SpringAnimation.cs` | 阻尼弹簧 `DampedSpringAnimation`、`Corner`、`CornerRanks`（neovide-cursor.js 逐行移植） |
| `NeovideCaret.cs` | 光标 `FrameworkElement`：四角动画状态、`Move`/`SetPosition`/`Update`/`OnRender`、阴影 |
| `Adornment.cs` | 每个文本视图一个：挂 `CompositionTarget.Rendering` 渲染循环、隐藏原生光标、位置/视口事件驱动 |
| `WpfTextViewsManager.cs` | MEF `IWpfTextViewCreationListener` + `AdornmentLayerDefinition`（After=Caret 置顶） |
| `Mapping.cs` | 缓冲区位置 → 视口像素坐标（Smooth Caret 移植） |
| `ICaretVisibility.cs` / `Vs*.cs` | 原生光标隐藏链（含 VsVim / Visual Assist 块光标场景） |
| `NeovideCursorPackage.cs` / `ServiceForPackageInitialization.cs` | 包 + 懒加载服务 |
| `Controller.cs` / `Options.cs` | 注册表配置读取 |
| `VSServices.cs` | 主题文本色、包探测 |
| `source.extension.vsixmanifest` | VSIX 清单（MefComponent + VsPackage 资产） |

## 构建

### 方式一：Visual Studio 内构建（推荐，也是你打算用的方式）

1. 用 VS 2022 打开 `NeovideCursor.csproj`（或整个工作区）。
2. 需要「Visual Studio 扩展开发」工作负载（装 VSSDK）。若未安装，本项目的 NuGet 包 `Microsoft.VSSDK.BuildTools` 会作为兜底提供构建工具。
3. **F5 / Ctrl+F5**：构建 VSIX 并部署到**实验实例**（Debug 配置下自动开启部署），实验实例里直接测试。
4. **生成 → 重新生成**：产物在 `bin\Debug\net472\NeovideCursor.vsix`。

VS 路径已硬编码为 `F:\VisualStudio`，可通过属性 `VSPath` 覆盖（如果你的 VS 装在别处）。

### 方式二：命令行（可选）

```powershell
# 纯 DLL（不装 VSSDK 也能构建，适合无网环境）
dotnet build NeovideCursor.csproj

# 完整 VSIX（需要 NuGet 联网还原 VSSDK.BuildTools，包已在本地缓存）
dotnet build NeovideCursor.csproj -p:BuildVsix=true
```

产物：`bin\Debug\net472\NeovideCursor.dll` / `.pkgdef` / `.vsix`。

## 安装 / 卸载

- **手动安装**：双击 `NeovideCursor.vsix`，或 `Extensions → Manage Extensions → 从磁盘安装…`。安装到正式实例。
- **实验实例（开发）**：F5 自动部署到实验实例。也可在实验实例里 `扩展 → 管理扩展` 安装同一个 vsix。
- **卸载**：`扩展 → 管理扩展`，找到 Neovide Cursor 卸载。原生光标在扩展卸载/禁用后恢复。

## 配置（注册表）

重启 VS 后生效。注册表位置：`HKEY_CURRENT_USER\Software\Vlasov Studio\Neovide Cursor`

| 值名 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `CursorColor` | String | `#FFC0CB` | 光标颜色。3/6/8 位 hex（`#f0c`、`#ffc0cb`、`#ffffc0cb`），或 `default`（跟随主题文本色） |
| `UseShadow` | DWORD | `1` | 是否启用阴影模糊 |
| `ShadowBlur` | DWORD | `20` | 阴影模糊半径（像素） |
| `AnimationLength` | DWORD | `100` | 常规动画时长（毫秒） |
| `ShortAnimationLength` | DWORD | `40` | 短跳（单行内）动画时长（毫秒） |
| `TrailSize` | DWORD | `100` | 拖尾强度 0–100（映射到 0.0–1.0，越大拖尾越长） |
| `CaretWidth` | DWORD | `2` | 光标宽度（像素） |

示例 `.reg`：

```reg
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\Vlasov Studio\Neovide Cursor]
"CursorColor"="#FFC0CB"
"UseShadow"=dword:00000001
"ShadowBlur"=dword:00000014
"AnimationLength"=dword:00000064
"ShortAnimationLength"=dword:00000028
"TrailSize"=dword:00000064
"CaretWidth"=dword:00000002
```

## 验证清单

- [ ] 打开编辑器：出现 neovide 风格光标，原生光标隐藏
- [ ] 单行内左右移动：快速平滑（短动画）
- [ ] 跨行跳转 / 点击另一行：leading 角瞬移、trailing 角拖尾跟随
- [ ] 滚动：光标瞬移无拖尾
- [ ] 阴影开启时多边形边缘有模糊光晕
- [ ] 改注册表 `CursorColor` / `ShadowBlur` / `AnimationLength` / `TrailSize` 后重启生效
