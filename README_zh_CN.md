# SimpleList

[English](./README.md) | 简体中文

![simplelist](https://socialify.git.ci/aiguoli/simplelist/image?description=1&font=Raleway&language=1&name=1&stargazers=1&theme=Light)

SimpleList 是一个使用 WinUI3 开发的 OneDrive 文件索引应用。

# 使用

解压后双击运行。

# 发布版本说明

GitHub Release 提供两种安装包：

| 版本 | 说明 | 典型体积 | 适用场景 |
| --- | --- | --- | --- |
| Portable | 自包含版本（self-contained），目标机器通常不需要预装 .NET 运行时。 | 较大 | 希望开箱即用的用户 |
| Slim | 依赖运行时版本（framework-dependent），目标机器需要满足 .NET/Windows App SDK 运行时依赖。 | 较小 | 更在意下载体积的用户 |

每个平台都会产出两类压缩包：

- `SimpleList-vVERSION-x64-Portable.zip`
- `SimpleList-vVERSION-x64-Slim.zip`

## 本地发布命令示例

Portable（自包含）:

```powershell
dotnet publish .\SimpleList\SimpleList.csproj -c Release -r win-x64 -p:PublishFlavor=Portable
```

Slim（轻量）:

```powershell
dotnet publish .\SimpleList\SimpleList.csproj -c Release -r win-x64 -p:PublishFlavor=Slim
```

# 不上线验证 CI 的方法

你可以在本地先验证 workflow，再决定是否推送。

## 1) 校验 workflow 语法与 Action 用法

使用 actionlint：

```powershell
docker run --rm -v "${PWD}:/repo" -w /repo rhysd/actionlint:latest
```

## 2) 本地模拟 CI 的发布矩阵

```powershell
$platforms = @("x64", "x86", "arm64")
$flavors = @("Portable", "Slim")
foreach ($p in $platforms) {
  foreach ($f in $flavors) {
    dotnet publish .\SimpleList\SimpleList.csproj -c Release -r "win-$($p.ToLower())" -p:Platform=$p -p:PublishFlavor=$f
  }
}
```

## 3) 本地检查 Release 描述文件

workflow 会生成 `RELEASE_BODY.md` 并追加 `CHANGELOG.md`。可在本地执行同样的脚本片段（Git Bash/WSL）并检查该文件内容，确认中英说明与变更日志符合预期。

# 设置

修改 `SimpleList/appsettings.json` 可自定义配置。

# 功能

- [x] 列表
- [x] 下载
- [x] 分享
- [x] 预览
- [x] 下载进度
- [x] 上传
- [ ] 自动同步
- [x] 重命名
- [x] 删除
- [x] 属性
- [x] 总容量信息
- [x] 转换为 PDF
- [ ] 新标签页打开
- [ ] 自定义主题
- [x] 多账号
- [x] 国际化
- [x] 工具页

# 截图（可能不是最新版本）

![HomePage](./ScreenShots/HomePage.png)
![CloudPage](./ScreenShots/CloudPage.png)
![DrivePage](./ScreenShots/DrivePage.png)
![CreateFolder](./ScreenShots/CreateFolder.png)
![GridLayout](./ScreenShots/GridLayout.png)
![Download](./ScreenShots/Download.png)
![Sahre](./ScreenShots/Share.png)
![ImageViewing](./ScreenShots/ImageViewing.png)
![ToolsPage](./ScreenShots/ToolsPage.png)
![ShareCommunityLinkDetails](./ScreenShots/ShareCommunityLinkDetails.png)
![DarkMode](./ScreenShots/DarkMode.png)

# Stargazers over time

[![Stargazers over time](https://starchart.cc/aiguoli/SimpleList.svg)](https://starchart.cc/aiguoli/SimpleList)
