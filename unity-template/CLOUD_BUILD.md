# 云端打包 Unity 网页版：一次性配置

配好以后的流程：

1. 模板代码有改动时，GitHub 的服务器会自动用 Unity 6 打包网页版，每次大约 15 到 30 分钟。
2. 打包结果放到仓库的 `play/` 文件夹，然后发布到 GitHub Pages。
3. 在 RPG 工坊网页里点「▶ Unity 试玩」，浏览器里就会打开 Unity 做的游戏，读的是当前项目的内容。

做游戏的人和玩家都**不用装 Unity**。只有第一次配置时，需要你本人在电脑上用 Unity Hub 领取一个免费许可证。

## 第 1 步：领取免费个人许可证（大约 5 分钟）

1. 双击 `~/Downloads/UnityHubSetup-arm64.dmg`，同意许可协议，把 Unity Hub 拖进「应用程序」。
2. 打开 Unity Hub，用你的 Unity 账号登录。
3. 点左上角头像，打开 **Settings（设置）→ Licenses（许可证）**，点 **Add**，选 **Get a free personal license**。
4. 确认这个文件已经生成。在终端运行下面的命令，能看到文件信息就说明有了：

```bash
ls -l "/Library/Application Support/Unity/Unity_lic.ulf"
```

不需要在 Hub 里安装 Unity 编辑器，云端会自己装。

## 第 2 步：把许可证存进 GitHub 仓库的密钥里

在终端里**你自己**运行下面三条命令。账号和密码只存在 GitHub 的加密密钥里，任何人（包括我）都看不到内容。

```bash
gh secret set UNITY_LICENSE -R salt-byte/rpg-workshop-standalone < "/Library/Application Support/Unity/Unity_lic.ulf"
```

```bash
gh secret set UNITY_EMAIL -R salt-byte/rpg-workshop-standalone
```

```bash
gh secret set UNITY_PASSWORD -R salt-byte/rpg-workshop-standalone
```

后两条命令运行后会提示你粘贴内容：分别粘贴 Unity 账号的邮箱和密码，然后回车。

## 第 3 步：开始第一次打包

打开 https://github.com/salt-byte/rpg-workshop-standalone/actions ，点左侧「Unity 网页版打包」，再点右边的 **Run workflow**。

第一次要下载 Unity 和插件，大约 30 分钟。之后有缓存，会快一些。

打包成功后，仓库里会多出 `play/` 文件夹。再等一两分钟，打开 https://salt-byte.github.io/rpg-workshop-standalone/play/ 就能玩示例游戏了。

## 常见问题

- **报错「还没有配置 Unity 许可证」**：说明第 2 步的密钥没设好，重新运行那三条命令。
- **激活失败**：如果 Unity 账号开了两步验证，云端可能登不上。可以注册一个只用来打包、不开两步验证的 Unity 账号，用它重做第 1 步和第 2 步。
- **找不到 `Unity_lic.ulf`**：在 Hub 的 Licenses 页面再点一次 Add，把激活流程走完。新版 Hub 有时显示已经有许可证，但文件并没有生成。
