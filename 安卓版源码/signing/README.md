# 签名密钥（不在仓库里）

仓库是公开的，所以发布签名没有上传。构建 APK 前，请把本机备份的两个文件放回这个文件夹：

- `kana-release.jks`：签名密钥
- `keystore.properties`：密钥信息，格式如下

```
storeFile=kana-release.jks
keyAlias=kana
storePassword=……
keyPassword=……
```

已发布版本的证书 SHA-256：`5119604d9e9bca02fae96f60f8b251e09f1c414bd87475bc18d8d01ccfba6e7e`。

以后每次更新都必须用同一个密钥签名，才能覆盖安装并保留手机上的进度。如果只想自己试着构建，可以用 keytool 临时生成一个新密钥，但这样打出的包无法覆盖安装已发布的版本。
