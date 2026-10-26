# 假名小步 · JPLearningApp

面向中文母语、零基础的假名（平假名 + 片假名）学习应用，目标是能读懂漫画和轻小说。有 **Windows 桌面版**和**安卓版**两个版本，功能和学习规则保持同步，都能完全离线使用。

当前版本：**2.5.1**。下载安装包请到 [Releases](../../releases)。

## 功能

- **今日计划**：根据学习进度给出当天建议。有错题先复习，再学下一行，然后做一轮测验，最后完成每日专注时长。平假名学完后接着推荐片假名。
- **五十音表 / 分组学习**：平假名和片假名各有清音 46 个、浊音和半浊音 25 个、拗音 33 个。可以切换「平假名 / 片假名 / 平片对照」；对照模式一格里同时显示两种写法。点开任意假名可以查看读音、听发音，两种写法分别标记熟悉。
- **记忆卡**：先回想，再翻开答案核对。默认按五十音表的顺序出卡，也可以一键打乱。卡片旁的**笔顺面板**会自动逐笔书写，并标出起笔处和第几画；也可以逐笔分解查看。
- **教科书体字形**：假名使用课本楷书风格的 Klee One 字体，笔画的起笔、收笔和方向都看得清楚。
- **辨认测验**：四选一，每轮最多 10 题。有「看字选读音」和「平片配对」两种题型。答错的假名自动进入错题本，连续答对两次才会移出。
- **专注计时**：可选 5、10、15、25 分钟，可设每日目标，有近 7 天柱状图。时间到会提醒休息。
- **护眼外观**：可选亮色护眼、深色护眼，或跟随系统。两套配色都用暖色调，不用纯白和纯黑。
- **离线发音**：104 段真人录音内置在应用中（片假名与平假名共用），不需要联网，也不需要安装语音包。

## 仓库结构

```
源码/              Windows 桌面版（C# 5 · WinForms · .NET Framework 4）
安卓版源码/         安卓版（Java 外壳 + 离线 WebView 页面，核心逻辑 core.js 与桌面版一一对应）
版本信息.md         开发与维护文档：版本历史、结构、构建、进度文件格式、配色规范、发布清单
```

## 构建

**Windows 桌面版**：双击 `源码/build.cmd`。只需要系统自带的 .NET Framework 4，编译完会自动运行自检。

**安卓版**：在 Linux 或 WSL 中运行 `安卓版源码/build.sh`。依赖如下：

```
sudo apt-get install openjdk-21-jdk-headless nodejs android-sdk-platform-23 aapt zipalign apksigner dalvik-exchange
```

签名密钥不在仓库里，原因和处理方法见 [安卓版源码/signing/README.md](安卓版源码/signing/README.md)。

更多细节都写在 [版本信息.md](版本信息.md) 里。

## 自检

- 桌面版：运行 `假名小步-独立桌面版.exe --self-test <输出目录>`。
- 安卓版：运行 `node 安卓版源码/tests/core.test.js`。

## 录音来源与授权

- 录音：FUN Japanese Learning（https://funjapaneselearning.com）
- 数据集整理：Arsalan Anwari，[Kana Sounds](https://huggingface.co/datasets/arsalan-anwari/kana-sounds)
- 授权：[Creative Commons Attribution 4.0（CC BY 4.0）](https://creativecommons.org/licenses/by/4.0/)

录音按原样内置，只调整了文件名，以匹配本应用的罗马音显示。

## 笔顺与字体

- 笔顺数据：[KanjiVG](https://kanjivg.tagaini.net)，Copyright (C) 2009/2010/2011 Ulrich Apel，按 [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) 授权。
- 假名字体：[Klee One](https://github.com/fontworks-fonts/Klee)，Copyright 2020 The Klee Project Authors，按 [SIL Open Font License 1.1](https://openfontlicense.org) 授权。这里只内置了假名子集。

## 许可证

本项目源代码采用 [MIT 许可证](LICENSE)，可以自由使用、修改和再发布，但需保留版权声明。

发音录音、笔顺数据和字体都不在 MIT 许可范围内，分别按 CC BY 4.0、CC BY-SA 3.0 和 SIL OFL 1.1 授权，详见 [NOTICE](NOTICE)。
