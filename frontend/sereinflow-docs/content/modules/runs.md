# 运行与结果

流程定义是一张“做事的图”；**运行**是照着这张图真正做一次。每次运行都有自己的 ID、开始和结束时间、状态、事件及输出。

## 一次运行会经历什么

提交后，服务先把任务放进队列，状态可能是 `pending`。开始执行后是 `running`。结束时可能是 `succeeded`、`failed`、`cancelled`、`timedOut` 或 `interrupted`。

`POST .../runs` 返回 `202` 只表示任务已接受。要知道最终结果，需要用返回的运行 ID 再查 `GET /api/runs/{runId}`。

## 事件、输出、工件各看什么

| 想知道什么 | 去哪里看 |
| --- | --- |
| 现在运行到哪一步 | `/api/runs/{runId}/events` |
| 节点对外给出了什么值 | `/api/runs/{runId}/outputs` |
| 有没有生成文件 | `/api/runs/{runId}/workpieces` |

事件按 `sequence` 排序。只想拿新事件，可以把上次看到的序号传给 `afterSequence`。需要持续接收时，可用 `/events/stream`；断开后通过 `Last-Event-ID` 继续读取。

## 什么时候需要调试

流程结果与预期不符时，可以启动**调试会话**，给节点设断点，然后逐步执行。它会占用一条独立的调试运行，和普通运行区分开。相关地址在[调试 API](/api/debug)。

常用运行请求和返回字段见[运行 API](/api/runs)。
