# AC8 Integrated Mod

AC8 MOD **1.0 正式版**。提供鼠标飞控、驾驶舱鼠标控制、平滑镜头跟随、FOV 设置和外置图形启动器。

## 下载

前往 [Releases](https://github.com/TiiCiano1207/AC8-Integrated-Mod/releases/latest)，下载 **AC8Workbench-1.0-external.zip**。
同时提供 SHA256 校验文件，可核对下载包是否完整。

GitHub 自动生成的 Source code 压缩包是源代码；游玩请下载完整 MOD 安装包。

## 安装

1. 将完整 ZIP 解压到游戏目录之外。
2. 双击 **AC8Workbench.exe**，在“安装与启动”选择游戏位置、Steam 程序和安装方案。
3. 点击左下角“保存设置”，首次使用或移动文件夹后，将生成的启动选项粘贴到 Steam 的游戏属性。
4. 点击右下角“开始游戏”。默认勾选“从 Steam 启动”。

EXE 是启动与设置入口。请保留旁边的 `payload`、`runtime`、配置与脚本文件，分享和复制时使用完整文件夹。

详细步骤见 [安装说明](docs/INSTALL.md)。本包适配 Windows x64、游戏 Build **25201480**；启动前会检查版本与文件归属。

## 功能

- 鼠标飞控与 PEACE / WAR 模式。
- 驾驶舱鼠标控制、C 自由观察和控制圆圈的平滑镜头跟随。
- 驾驶舱、第一人称和机外 FOV 设置。
- 准星颜色、显示设置、鼠标灵敏度与键位调整。
- 三种安装方案：仅比例引导、完整导弹强化、仅鼠标飞控。
- 游戏内实际调整过的灵敏度、观察缩放、飞控模式、机外相机模式和机头十字自动回写。
- 模组启停、会话备份与恢复、运行记录和内置分析。

外部设置在下次启动应用。工具保持打开时会读取游戏内的设置变化，同项以游戏内调整为准，其他未保存的菜单调整保留。

## 1.0 更新

- “开始游戏”为主按钮，“保存设置”和 Steam 启动勾选项位于左下角。
- 帮助页精简为安装说明，移除界面主题切换和灵敏度快捷值。
- 修复内置 FOV 模块被历史清理误判的问题，接受合法的个人 FOV 参数。
- 清理和模组管理使用界面选择的游戏路径，支持安装方案尚未保存的情况。

## 源码与修改

`source/SettingsTool` 是 WPF 启动器，`source/MouseAim` 是原生鼠标飞控与镜头模块；`source/CockpitFOV` 和 `source/SourceInit` 保留 FOV 与导弹初始化脚本。
本仓库不把分析运行时和编译产物放入 Git；运行所需的完整外置资源随 Release 安装包提供。

修改 INI 无需重编 EXE。修改 MOD 资源后运行 `Update-Resources.cmd` 更新校验；C++ 修改需重编相应 DLL。

## 署名与许可

本项目基于 [FletcherMiya/AC8-Mouse-Aim](https://github.com/FletcherMiya/AC8-Mouse-Aim)，感谢原作者及上游贡献者。
原始整合代码与修改沿用 [MIT 许可](LICENSE)；第三方材料保留各自许可和版权声明，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 与 `licenses`。

这是独立维护的非官方社区项目，沿用原 MOD 的离线单人使用范围。
