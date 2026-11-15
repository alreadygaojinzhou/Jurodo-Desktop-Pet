# Jurodo Desktop Pet

Windows 多角色桌宠，使用 C# WPF 和
[VPet-Simulator.Core](https://github.com/LorisYounger/VPet) 驱动。

程序支持角色切换、独立状态、摸头、拖拽、投喂、换装、AI 对话、托盘、
开机自启动和剧情检索。角色动画由项目内的 .NET 生成器从用户提供的透明立绘合成。

> 本仓库只公开代码。游戏立绘、生成后的动画包、完整剧情文本和角色素材不包含在
> Git 历史中。除非你拥有明确的分发授权，不要公开上传这些素材。

## 功能

- 一个程序内切换多个带立绘角色
- 每个角色独立保存饱食、心情和好感
- 四状态表情、呼吸动画、摸头和拖拽
- 投喂、换装、盘腿和角色专属台词
- OpenAI 兼容接口的 AI 对话
- 根据角色资料和关键词从完整剧情库中检索相关上下文
- Windows 开机自启动、总在最前、托盘运行和位置记忆

## 环境

- Windows 10/11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Git

## 克隆

VPet 核心以 Git 子模块接入：

```powershell
git clone --recurse-submodules https://github.com/你的用户名/你的仓库.git
cd 你的仓库
```

已经克隆但目录为空时：

```powershell
git submodule update --init --recursive
```

## 构建

```powershell
dotnet build TojirouPet/TojirouPet.csproj -c Release -p:Platform=x64
dotnet publish TojirouPet/TojirouPet.csproj -c Release -p:Platform=x64 -o .\out
```

首次运行后，在 `out\config.json` 中填写 OpenAI 兼容接口：

```json
{
  "ApiBaseUrl": "https://api.deepseek.com/v1",
  "ApiKey": "你的 API Key",
  "Model": "deepseek-chat",
  "Temperature": 0.9
}
```

不要把填写真实 Key 的 `config.json` 提交到 Git。

## 角色素材

本仓库不附带受版权保护的游戏资源。你需要自行准备有使用和分发权的：

```text
素材/角色/<角色>/立绘/*.png
素材/角色/<角色>/人设卡.md
素材/角色/<角色>/角色资料.json
素材/角色/<角色>/对话记录/我的台词.jsonl
素材/全游戏对话/全游戏对话.jsonl
```

生成动画包和角色数据：

```powershell
dotnet run --project TojirouPet/tools/CharacterPackGenerator/CharacterPackGenerator.csproj -c Release
```

命令会生成：

```text
TojirouPet/PetMod/characters/
TojirouPet/CharacterData/
```

这两个目录默认被 `.gitignore` 忽略。若要发布完整安装包，请把有授权的素材包放到
GitHub Releases，而不是提交进 Git 历史。

## 使用

`out\TojirouPet.exe` 启动。右键桌宠可以选择角色、投喂、换装、聊天、调整大小、
查看互动规则或切换开机自启动。

## 项目结构

```text
TojirouPet/
  MainWindow.xaml.cs          桌宠窗口、菜单和交互
  CharacterCatalog.cs         多角色清单
  PetState.cs                 独立角色状态与存档
  Persona.cs                  动态 AI 人设提示词
  AiChatClient.cs             AI 接口和剧情检索
  tools/CharacterPackGenerator/
                              多角色 VPet 动画包生成器
VPet/                         上游 Git 子模块
```

## 素材与许可证

项目代码使用 Apache License 2.0。VPet 保留其原有 Apache License 2.0。
游戏立绘、角色资料、剧情文本和其他第三方内容不因本项目采用 Apache License 而
改变授权状态。详见 [NOTICE](NOTICE)。

本项目的 AI 功能由第三方 OpenAI 兼容接口提供；API 费用、数据发送和隐私规则
由所选服务商决定。成人向剧情材料应由使用者确认年龄与本地法律要求，并尊重明确的
互动边界。

## 发布到 GitHub

```powershell
git init
git add .
git commit -m "Initial public release"
git branch -M main
git remote add origin https://github.com/你的用户名/你的仓库.git
git push -u origin main
```

如需完整 Windows 安装包，在 GitHub 的 Releases 页面上传有授权的发布 ZIP。
