# link-share

SimpleList 的分享社区后端。服务与桌面端位于同一仓库，但保持独立构建和部署。

## 本地运行

```powershell
Copy-Item .env.example .env
# 编辑 .env，设置至少 32 字符的 JWT_SECRET
go run .
```

服务启动时会自动读取当前目录的 `.env`，但不会覆盖系统已设置的环境变量。服务默认监听 `:3000`，SQLite 数据库保存在当前工作目录的 `data.db`。
SQLite 使用纯 Go 驱动，Windows 本地开发不需要安装 GCC 或启用 CGO。

配置项：

- `JWT_SECRET`：必填，至少 32 个字符；
- `DATABASE_PATH`：SQLite 路径，默认 `data.db`；
- `CORS_ALLOW_ORIGINS`：允许的 Web 来源，默认生产站点；
- `ADMIN_EMAIL`：分类管理账号。

## API

- `GET /api/links`：兼容旧客户端的 v1 只读路由；写操作同样要求登录。
- `GET /api/v2/providers`：列出支持的网盘及分享能力。
- `POST /api/v2/auth/register|login|refresh|logout`：注册、登录和会话轮换。
- `GET /api/v2/users/me`：获取当前用户。
- `GET /api/v2/links`：列出分享链接，可使用 `provider_type` 查询参数筛选。
- `POST /api/v2/links`：创建分享链接，需要登录。
- `GET /api/v2/links/:id`：获取分享链接。
- `POST /api/v2/links/:id/visit`：增加访问次数。
- `PATCH /api/v2/links/:id`、`DELETE /api/v2/links/:id`：需要登录并校验所有者。
- `PUT|DELETE /api/v2/links/:id/collection`、`GET /api/v2/collections`：收藏管理。

v2 使用 `provider_type` 区分网盘。OneDrive 和 Google Drive 可发布到社区；Local 与 PikPak 会出现在能力清单中，但当前不支持公开分享。

首次打开旧数据库时会创建迁移记录，并将没有 `provider_type` 的旧链接回填为 `onedrive`。上线前仍应备份 `data.db`。
