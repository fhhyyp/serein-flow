# 类库上传与节点目录

SereinFlow 的节点库由服务端提供，Vue 项目不内置本地节点实例。用户可以从节点库浮层点击“上传类库”，上传 TRAE 兼容的 ZIP 类库包。

## ZIP 约定

```text
[类库名称]-[版本号].zip
└── [类库名称]-[版本号]/
    └── [类库名称].dll
```

例如：

```text
MathLibrary-1.0.0.zip
└── MathLibrary-1.0.0/
    └── MathLibrary.dll
```

服务端按上传内容计算 SHA-256，并以 hash 作为包 ID 和存储文件名。重复上传相同内容不会生成第二份包，响应中的 `alreadyExists` 会为 `true`。

## API

规范路径：

```text
GET  /api/libraries
GET  /api/libraries/{libraryId}
POST /api/libraries/upload       multipart/form-data: file
POST /api/libraries/upload-zip   TRAE 兼容别名
DELETE /api/libraries/{libraryId}
```

为兼容 TRAE 客户端，同时保留 `/api/library` 单数别名和 `/{libraryId}/nodes` 节点目录查询。

上传成功返回：

```json
{
  "library": {
    "id": "sha256",
    "name": "MathLibrary",
    "version": "1.0.0",
    "fileName": "MathLibrary-1.0.0.zip",
    "sizeBytes": 12345,
    "sha256": "…",
    "uploadedAt": "…",
    "nodes": []
  },
  "alreadyExists": false
}
```

## 安全边界

API 不使用 `Assembly.Load`、`AssemblyLoadContext` 或任何外部插件执行入口。上传流程只做以下可信操作：

- 校验 ZIP 后缀、文件命名、大小、条目数量和解压总大小。
- 拒绝绝对路径和 `..` 路径，防止 ZIP Slip。
- 使用 `PEReader` / `MetadataReader` 读取 DLL 的程序集、类、方法、参数和返回值元数据。
- 将原始 ZIP 保存到 `SereinFlow:LibraryDirectory`，不在 API 进程解压或执行外部 DLL。

外部 DLL 的实际加载和执行仍属于独立 Worker 进程。Worker 接入后，应根据同一个 SHA-256 包 ID 装载类库并执行流程节点。
