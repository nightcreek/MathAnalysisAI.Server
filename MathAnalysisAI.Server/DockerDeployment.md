# MathAnalysisAI Docker 生产部署指南

## 概述

MathAnalysisAI 是一个基于 .NET 10 + SQL Server 的数学分析智能体平台。本文档提供完整的 Docker 生产部署步骤。

## 架构

```
                    ┌──────────────┐
                    │   Nginx /    │
                    │  Reverse     │  ← TLS 终止 (可选)
                    │   Proxy     │
                    └──────┬───────┘
                           │ :5131
                    ┌──────▼───────┐
                    │   server     │  .NET 10 ASP.NET Core
                    │  (容器)      │
                    └──┬────────┬──┘
                       │        │
              ┌────────▼──┐ ┌──▼──────────┐
              │ sqlserver  │ │  litellm    │
              │ (容器)     │ │  (容器)     │
              │ SQL 2022   │ │  LLM 代理   │
              └────────────┘ └─────────────┘
```

**三个容器**：
- `sqlserver` — SQL Server 2022，持久化数据卷
- `litellm` — LiteLLM 代理，统一管理 DeepSeek/DashScope 等 LLM API Key
- `server` — .NET 10 ASP.NET Core 应用，含前端静态文件

## 前置条件

| 依赖 | 最低版本 | 说明 |
|------|---------|------|
| Docker | 24.0+ | 需要 Compose V2 (`docker compose`) |
| 服务器内存 | 4 GB+ | SQL Server 最低 2 GB，应用 ~512 MB，LiteLLM ~256 MB |
| 磁盘 | 20 GB+ | SQL Server 数据卷会持续增长 |

## 第一步：准备配置文件

创建目录 `/etc/mathanalysis-ai/` 并准备三个配置文件：

### 1. `/etc/mathanalysis-ai/sqlserver.env`

```env
ACCEPT_EULA=Y
MSSQL_SA_PASSWORD=YourStrong!Passw0rd
MSSQL_PID=Express
```

> `MSSQL_PID=Express` 限制 CPU/内存，测试环境适用。生产环境可用 `Developer` 或 `Enterprise`。

### 2. `/etc/mathanalysis-ai/litellm.env`

```env
DEEPSEEK_API_KEY=sk-your-deepseek-key
DASHSCOPE_API_KEY=sk-your-dashscope-key
LITELLM_MASTER_KEY=your-liteLLM-master-key
```

> 这些 Key 会通过 LiteLLM 代理转发给 LLM，server 容器不直接持有 API Key。

### 3. `/etc/mathanalysis-ai/server.env`

```env
# ── 认证 ──
Auth__Mode=LocalPassword
Auth__AllowRegistration=true
Auth__MinPasswordLength=6
Auth__BcryptWorkFactor=12

# ── 管理员 ──
Admin__Username=admin
Admin__Password=your-admin-password

# ── 数据库（容器间通讯用容器名） ──
ConnectionStrings__DefaultConnection=Server=mathanalysis-sqlserver,1433;Database=MathAnalysisAI;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true

# ── LiteLLM ──
LLMGateway__Mode=litellm
LiteLLM__BaseUrl=http://mathanalysis-litellm:4000/v1/chat/completions
PhotoSolutionOcr__Provider=litellm

# ── SymPy ──
Symbolic__Enabled=true
Symbolic__Provider=sympy
Symbolic__PythonExecutable=python3
```

## 第二步：配置加密（推荐）

敏感值（如管理员密码、数据库密码）可使用 AES-256-GCM 加密存储，避免明文出现在配置文件中。

### 生成加密密钥

```bash
export MATHANALYSIS_ENCRYPTION_KEY=$(openssl rand -hex 32)
echo "加密密钥: $MATHANALYSIS_ENCRYPTION_KEY"
# 保存此密钥，丢失后无法解密配置
```

### 加密敏感值

```bash
cd MathAnalysisAI.Server/Tools/ConfigEncryptor
dotnet run -- "your-admin-password"
# 输出: ENC:base64ciphertext...
```

### 使用加密值

在 `server.env` 中将明文替换为加密值：

```env
Admin__Password=ENC:eyJ...(加密工具输出的完整值)
ConnectionStrings__DefaultConnection=ENC:eyJ...(加密的完整连接串)
```

### 传递密钥到容器

在 `docker-compose.prod.yml` 的 server 服务中添加：

```yaml
server:
  environment:
    - MATHANALYSIS_ENCRYPTION_KEY=${MATHANALYSIS_ENCRYPTION_KEY}
```

> 或在宿主机上通过 systemd 环境变量、Docker secrets 等方式管理密钥，不写入 compose 文件。

## 第三步：构建与启动

```bash
# 1. 进入项目目录
cd MathAnalysisAI.Server

# 2. 设置加密密钥（如使用加密配置）
export MATHANALYSIS_ENCRYPTION_KEY="你的64位hex密钥"

# 3. 构建镜像
docker compose -f docker-compose.prod.yml build

# 4. 启动所有服务（后台运行）
docker compose -f docker-compose.prod.yml up -d

# 5. 查看启动日志
docker compose -f docker-compose.prod.yml logs -f server
```

首次启动流程：
1. SQL Server 容器启动（约 30 秒）
2. LiteLLM 容器启动
3. Server 容器启动 → 自动执行 EF Core 迁移 → 种子课程/管理员数据 → 启动 HTTP 服务

> 首次启动可能需要 60-90 秒，因为需要创建数据库表结构和种子数据。

## 第四步：验证部署

```bash
# 健康检查
curl http://localhost:5131/health

# 预期返回
# {"status":"Healthy","checks":[{"name":"database","status":"Healthy",...}],"duration":"..."}

# API 信息
curl http://localhost:5131/api/auth/info

# 前端页面
curl -I http://localhost:5131/login.html
```

在浏览器中访问 `http://<服务器IP>:5131/login.html` 可以看到登录页面。

## 第五步：Nginx 反向代理（可选）

```nginx
server {
    listen 443 ssl;
    server_name math.example.com;
    ssl_certificate     /etc/ssl/certs/math.crt;
    ssl_certificate_key /etc/ssl/private/math.key;

    location / {
        proxy_pass http://127.0.0.1:5131;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

配置 `server.env` 中的转发头信任：

```env
ForwardedHeaders__KnownNetworks=172.16.0.0/12
```

## 常用运维命令

```bash
# 查看运行状态
docker compose -f docker-compose.prod.yml ps

# 查看 server 日志
docker compose -f docker-compose.prod.yml logs -f server

# 重启 server（如更新配置后）
docker compose -f docker-compose.prod.yml restart server

# 停止所有服务
docker compose -f docker-compose.prod.yml down

# 停止并删除数据卷（⚠ 会清空数据库）
docker compose -f docker-compose.prod.yml down -v

# 更新镜像
docker compose -f docker-compose.prod.yml build --no-cache
docker compose -f docker-compose.prod.yml up -d
```

## 数据备份

```bash
# 备份 SQL Server 数据卷
docker run --rm \
  -v mathanalysisai_server_mathanalysis_sql_data:/data \
  -v $(pwd)/backups:/backup \
  alpine tar czf /backup/sqlserver-backup-$(date +%Y%m%d).tar.gz -C /data .

# 定时备份（crontab 示例，每天凌晨 3 点）
# 0 3 * * * cd /opt/mathanalysis && ./backup.sh
```

## 升级步骤

```bash
# 1. 拉取最新代码
git pull

# 2. 备份数据卷（见上方）

# 3. 重新构建并启动
docker compose -f docker-compose.prod.yml build --no-cache
docker compose -f docker-compose.prod.yml up -d

# 4. 查看迁移日志确认成功
docker compose -f docker-compose.prod.yml logs server | grep "migration"
```

容器启动时会自动执行 `Database.MigrateAsync()`，应用所有待执行的 EF Core 迁移。

## 环境变量参考

| 变量名 | 用途 | 示例值 |
|--------|------|--------|
| `MATHANALYSIS_ENCRYPTION_KEY` | 64位 hex AES-256 加密密钥 | `a1b2c3d4...`（64字符） |
| `MATHANALYSIS_SKIP_RUNTIME_STARTUP` | 跳过迁移/种子数据（仅 EF 工具用） | `true` |
| `ASPNETCORE_ENVIRONMENT` | 运行环境 | `Production` |
| `ASPNETCORE_URLS` | 监听地址 | `http://0.0.0.0:5131` |

## 故障排查

### Server 无法连接 SQL Server

```bash
# 检查 SQL Server 是否就绪
docker compose -f docker-compose.prod.yml logs sqlserver | grep "Recovery is complete"

# 检查网络连通性
docker exec mathanalysis-server curl -s mathanalysis-sqlserver:1433 && echo "可达" || echo "不可达"
```

### 迁移失败

容器日志中查看 "EF Core migration" 相关行。常见原因：
- SQL Server 尚未完全启动（健康检查的 `start_period: 30s` 可能不够，首次启动可调整为 `60s`）
- 连接串中 `TrustServerCertificate=True` 缺失

### 端口冲突

```bash
# 检查 5131/1433/4000 是否被占用
lsof -i :5131
```

## 安全建议

1. **使用加密配置**：管理员密码和数据库密码使用 `ENC:` 前缀加密存储
2. **限制端口暴露**：当前 compose 中端口绑定 `127.0.0.1`，仅本地访问。如需外部访问，建议通过 Nginx 反向代理
3. **定期更新基础镜像**：`.NET 10.0` 当前为预览版，发布正式版后需更新 Dockerfile 中的镜像 tag
4. **SA 密码强度**：SQL Server 的 SA 密码务必使用强密码（≥16 字符，含大小写+数字+符号）
5. **会话密钥**：生产环境建议将 `AddDistributedMemoryCache()` 替换为 Redis 分布式缓存
