# OEMAuditDemo（代工厂评鉴系统 Demo）

本 Demo 用于在 **SQL Server 2014 + Windows Server/IIS** 内网环境快速跑通“年度评鉴/复用去年默认值/模块协作录入/自动判级/自动生成整改项(CAPA)/Excel导出”的核心流程。

## 技术栈
- 后端：.NET Framework 4.5 + Web API 2 + ADO.NET
- 数据库：SQL Server 2014（脚本兼容 2014）
- 前端：Vue 3 + Vite + Axios + Vue Router
- 文件：照片保存到后端项目内 `App_Data/Uploads`（可随项目迁移）

---

## 1. 初始化数据库（SQL Server 2014）
1) 新建数据库：`OEMAuditDemo`

2) 在该数据库中执行脚本：
- `db/db_init.sql`

脚本会：
- 建表 + 存储过程
- 插入示例工厂/证照数据 + 示例模板（织造类型，含 A/B/C/D/E 条款）
- 插入少量示例评鉴（上一年度与本年度），用于演示看板指标（趋势/维度对比/闭环率/证照到期/风险预警）

---

## 2. 运行后端（Visual Studio）
1) 打开解决方案：`AuditDemo.sln`

2) 修改连接串：
- `AuditDemo.WebApi/Web.config` → `connectionStrings/Default`

3) F5 运行（IIS Express），或发布到 IIS。

### 上传限制
- Web.config 已配置：单请求允许到 50MB
- 前端限制：单张图片 ≤ 10MB
- 图片处理：自动缩放宽度 ≤ 1920，统一输出 JPEG（不支持 HEIC）

---

## 3. 运行前端（Vue3 + Vite）
> 你可以开发态跑，也可以 build 后把产物放到后端站点下。

### 3.1 开发态
```bash
cd frontend
npm install
npm run dev
```
> 说明：开发态已在 `vite.config.js` 配置 `/api` 代理到 `http://localhost:5100`。
> 如果你的后端端口不同，请修改 `vite.config.js -> server.proxy["/api"].target`。

然后在浏览器打开 Vite 输出的地址。

### 3.2 发布态（推荐：把前端产物放入后端）
```bash
cd frontend
npm install
npm run build
```

Vite 已配置输出目录到：
- `AuditDemo.WebApi/app`

部署后直接访问：
- `http://<host>/` （IIS 默认文档指向 `app/index.html`）

---

## 4. Demo 账号
登录页支持首次登录自动创建账号：
- 管理员：`admin / admin123`
- 评鉴人员：`auditor / auditor123`

登录后会拿到 Token 并存到 localStorage，请求头使用 `X-Token`。

---

## 5. 核心业务流程（跑通一次）
1) 登录
2) 点击顶部【看板】查看 Demo 指标（可切换年度/证照到期窗口）
3) 进入【评鉴单】→【新建评鉴单】
   - 类型：年度评鉴 / 新引入
   - 年度：例如 2026
   - 复制去年结果：是/否（年度评鉴建议选是）
4) 进入评鉴详情：看到模块进度（已完成/总条款数）
5) 进入模块【录入】
   - 首次进入会尝试获取模块锁：同一模块同一时间只允许一人编辑
   - 条款选择：符合 / 部分不符合 / 不符合 / 不适用
   - 可填写文字描述，上传照片证据（每条≤3张，支持手机拍照/相册选择）
6) 模块录入完成后点【提交】
   - 提交会校验：该模块所有条款都必须已填写（不能为“未填写”）
   - 支持撤回（未判级前）
7) 所有模块都提交后，在评鉴详情点【判级并生成整改项】
   - 判级规则：任意 E 触发=E；否则 D/C/B；否则 A
   - 触发条件：条款状态为“部分不符合/不符合”
   - A 条款不参与判级，但触发仍生成整改项
8) 进入【整改项】
   - 填写整改措施、责任人（外部联系人姓名+电话）、截止日期（必须人工填，无默认）
   - 上传证据（≤5张）
   - 提交证据 → 关闭
9) 导出 Excel（评鉴详情页）
   - 明细 Sheet + 不合格汇总 Sheet
   - 不合格条款红色加粗，汇总按模块分组并输出小计
---

## 6. 关键接口（便于联调）
- 登录：`POST /api/auth/login`
- 工厂：`GET /api/factories`
- 评鉴：
  - `GET /api/audits`
  - `POST /api/audits`
  - `GET /api/audits/{auditId}`
  - `GET /api/audits/{auditId}/modules/{moduleId}`
  - `PUT /api/audits/{auditId}/clauses/{clauseId}`
  - `POST /api/audits/{auditId}/clauses/{clauseId}/photos`
  - `POST /api/audits/{auditId}/modules/{moduleId}/submit|withdraw`
  - `POST /api/audits/{auditId}/rate`
  - `GET /api/audits/{auditId}/export/detail`
- 锁：
  - `POST /api/audits/{auditId}/modules/{moduleId}/lock`
  - `POST /api/audits/{auditId}/modules/{moduleId}/heartbeat`
  - `POST /api/audits/{auditId}/modules/{moduleId}/unlock`
- CAPA：
  - `GET /api/capa/by-audit/{auditId}`
  - `PUT /api/capa/{capaId}`
  - `POST /api/capa/{capaId}/evidence`
  - `POST /api/capa/{capaId}/submit-evidence`
  - `POST /api/capa/{capaId}/close`
- 文件：`GET /api/files/{id}`（返回 JPEG）
- 报表：
  - `GET /api/reports/overview?year=2026&days=60`
  - `GET /api/reports/factory-history/{factoryId}`

### 证照（正式模块）
- 列表/筛选：`GET /api/certificates?factoryId=&q=&days=&includeFiles=true`
- 详情：`GET /api/certificates/{certId}`
- 新增：`POST /api/certificates`
- 编辑：`PUT /api/certificates/{certId}`
- 停用：`POST /api/certificates/{certId}/deactivate`
- 上传附件（图片或 PDF，单证照≤5）：`POST /api/certificates/{certId}/files`
- 删除附件：`DELETE /api/certificates/files/{fileId}`
- 到期提醒：`GET /api/certificates/expiring?days=60`
- 到期提醒导出：`GET /api/certificates/export-expiring?days=60`

> 文件回传：`GET /api/files/{id}`（图片返回 JPEG；PDF 返回 application/pdf）

---

## 7. 下一步建议（从 Demo 到正式版本）
- 权限：按“评鉴创建者可分配模块负责人 + 管理员可重开/重置”完善
- 模板：完善模板 Excel 导入导出、版本发布后不可编辑的约束
- 复评：基于整改项/不合格条款生成“复评单”，只复查不合格条款
- 报表：本 Demo 已提供看板页与接口（overview + factory-history），可再补充图表/筛选维度（模块/工厂类型/区域/责任人等）
