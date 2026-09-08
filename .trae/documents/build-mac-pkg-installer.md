# 在 CI 中构建 macOS .pkg 安装包

## Context
用户希望用 GitHub Actions 构建 .pkg 安装包，无需卸载程序。

## 方案

### 修改 CI workflow
在 `publish-commit.yml` 和 `publish-nightly.yml` 的 `mac-dmg` job 中增加一步：

1. 在 `Build DMG` 步骤之后，用 `pkgbuild` 从已构建的 `.app` 生成 `.pkg`
2. 将 `.pkg` 添加到上传产物和 Release 发布文件列表中

### 关键命令
```bash
pkgbuild \
  --root "./src/SulfurLauncher.Desktop/bin/Release/net10.0/${{ matrix.rid }}/publish/SulfurLauncher.app" \
  --install-location "/Applications/SulfurLauncher.app" \
  --identifier "cc.tiouo.SulfurLauncher" \
  --version "${{ needs.prepare.outputs.app_version }}" \
  --timestamp \
  "SulfurLauncher.osx.mac.${{ matrix.dmg_arch }}.pkg"
```

## 需要修改的文件
- `.github/workflows/publish-commit.yml` - mac-dmg job 增加 pkg 构建步骤
- `.github/workflows/publish-nightly.yml` - mac-dmg job 增加 pkg 构建步骤

## 验证方法
提交后检查 CI 构建产物中是否包含 `.pkg` 文件，以及 Release 页面是否发布 `.pkg`。