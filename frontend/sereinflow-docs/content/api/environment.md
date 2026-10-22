# 环境设置与密钥 API

环境设置控制运行队列等服务行为；密钥控制谁能访问需要授权的接口。这两类操作通常由管理员完成。

## 运行设置

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `GET` | `/api/environment/settings` | 读取当前运行设置 |
| `PUT` | `/api/environment/settings` | 保存新的运行设置 |

设置里有队列容量、并发运行数、等待超时、类库上传上限等字段。修改之前先 GET 当前值，完整保留自己不打算更改的字段，再 PUT 回去。

## API 密钥

| 方法 | 路径 | 做什么 |
| --- | --- | --- |
| `POST` | `/api/security/api-keys/initial-admin` | 首次创建管理员密钥，仅本机可用 |
| `GET` | `/api/security/api-keys` | 列出密钥信息 |
| `POST` | `/api/security/api-keys` | 创建新密钥 |
| `POST` | `/api/security/api-keys/{id}/rotate` | 轮换密钥 |
| `DELETE` | `/api/security/api-keys/{id}` | 撤销密钥 |

除了首次设置，密钥管理接口都要求管理员密钥，即使普通 REST 鉴权关闭也一样。新建密钥时可指定项目范围、名称、权限和到期时间。创建或轮换后的完整 Secret 只显示一次，请妥善保存；列表接口不会再次返回完整 Secret。

需要调用流程的程序，通常给它 `run.execute` 和 `run.read` 中实际需要的权限。不要因为它只调用一个项目，就给它管理员权限。鉴权行为见[地址、密钥与报错](/api/basics)。
