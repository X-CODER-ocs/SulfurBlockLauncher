# LiteSkinViewer

轻量级 Minecraft 皮肤查看器，支持 2D 截取与 3D 渲染预览。

## 技术基础

LiteSkinViewer 的 3D OpenGL 渲染部分基于 [Coloryr/MinecraftSkinRender](https://github.com/Coloryr/MinecraftSkinRender) 项目，在其实现基础上进行了封装和重写。

软件皮肤渲染（SkinRenderer 及相关类型）基于 [d3ara1n/Polymerium](https://github.com/d3ara1n/Polymerium) 项目的皮肤渲染代码，在其实现基础上进行了适配。

## 特性

- 3D 模型皮肤展示
- 2D 全身皮肤截取
- 2D 头像截取（正面、侧面简化样式）
- 软件 3D 皮肤渲染（头像、全身、半身封面、四方向视图）

## 构建与运行

开发环境要求：

- .NET SDK 8.0 或以上版本
- Avalonia UI 框架
- OpenGL 支持的图形设备（3D 查看器需要）

## 版权声明

### MinecraftSkinRender

LiteSkinViewer 使用并改进了 [Coloryr/MinecraftSkinRender](https://github.com/Coloryr/MinecraftSkinRender) 项目中的源代码。
该项目遵循 Apache License 2.0，因此本项目同样遵守该许可协议的所有要求，包括：

- 明确注明原始项目及其作者：MinecraftSkinRender by Coloryr
- 提供完整的 LICENSE 文件与 NOTICE 文件
- 不使用原始作者的商标、Logo 或名称进行暗示性背书

LiteSkinViewer 在尊重原始项目的基础上进行了封装、重构与扩展，所有基于其源代码的部分均遵循 Apache 2.0 协议进行使用和分发。

### Polymerium

LiteSkinViewer 的软件皮肤渲染部分（`LiteSkinViewer.2D/Rendering/` 目录下的 SkinRenderer、SkinGeometry、SkinCamera、SkinFace、BoundingBox、SkinViewType 等类型）移植并适配自 [d3ara1n/Polymerium](https://github.com/d3ara1n/Polymerium) 项目的皮肤渲染代码。

Polymerium 项目基于 MIT 许可证发布，相关版权声明如下：

```
MIT License

Copyright (c) d3ara1n

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
```
