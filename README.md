# Neovide-Cursor-for-Visual-Studio
移植于VSC扩展-Neovide Cursor
## 配置（菜单栏-工具-选项-Neovide Cursor-general）

无需重启，自动重载生效。

| 值名 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `CursorColor` | String | `#FFC0CB` | 光标颜色。3/6/8 位 hex（`#f0c`、`#ffc0cb`、`#ffffc0cb`），或 `default`（跟随主题文本色） |
| `UseShadow` | DWORD | `1` | 是否启用阴影模糊 |
| `ShadowBlur` | DWORD | `20` | 阴影模糊半径（像素） |
| `AnimationLength` | DWORD | `100` | 常规动画时长（毫秒） |
| `ShortAnimationLength` | DWORD | `40` | 短跳（单行内）动画时长（毫秒） |
| `TrailSize` | DWORD | `100` | 拖尾强度 0–100（映射到 0.0–1.0，越大拖尾越长） |
| `CaretWidth` | DWORD | `2` | 光标宽度（像素） |
