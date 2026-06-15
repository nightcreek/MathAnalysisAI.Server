# MathAnalysisAI 项目系统审计报告

审计范围：项目结构、后端分层、OCR 题目识别链路、MathLive/LaTeX 输入链路、LLM 与 Prompt、知识点体系、解答自检、数据库与用户数据、安全可靠性、产品边界。

审计结论基于当前代码与现有设计文档，重点参考了 `Program.cs`、`Controllers/*`、`Services/*`、`Models/*`、`Data/Seed/*`、`wwwroot/js/*` 及相关 `Docs/*`。

## 1. 项目定位结论

### 结论
- 当前项目**更接近“数学分析学习智能体”的雏形**，但还**没有收拢成完整、强约束的学习智能体**。
- 它已经具备题目输入、OCR、结构化分析、知识点归类、错题沉淀、统计回写等主干能力。
- 但同时也存在一些明显的“泛化数学平台”信号：可视化、符号计算、模板题生成、资料检索、开发工具页等。
- 因此，当前最准确的归类是：**架构边界不清晰的混合项目**。

### 为什么不是纯通用聊天工具
- 有明确的数学分析课程 seed：`Data/Seed/PlatformSeedData.cs`
- 有知识点归一化和错因回写：`Services/Knowledge/KnowledgePointNormalizer.cs`、`Services/Analysis/Mistakes/MistakeRecordService.cs`
- 有课程维度、章节维度、题目维度、错题维度的数据模型
- 有分析结果结构化输出，而不是纯文本聊天

### 为什么还不够“强学习智能体”
- 核心流程里缺少强制的 OCR 确认层与结构化题目中间态
- 解答自检更多是“LLM 输出一个 review 字段”，还不是独立校验层
- 知识点体系还不完整，尤其缺少重积分、曲线积分与曲面积分
- Prompt 仍偏通用结构化输出，离“课程边界内的分工型智能体”还有距离

---

## 2. 当前项目结构概览

### 后端主干
- 入口与管线：`Program.cs`
- 控制器：
  - `Controllers/LearningAnalysisController.cs`
  - `Controllers/PhotoSolutionsController.cs`
  - `Controllers/AuthController.cs`
  - `Controllers/CourseMaterialsController.cs`
  - `Controllers/ProblemTemplateController.cs`
  - `Controllers/PracticeController.cs`
  - `Controllers/SymbolicController.cs`
- 分析链路：
  - `Services/Analysis/AnalysisService.cs`
  - `Services/Analysis/LLM/LlmRequestFactory.cs`
  - `Services/Analysis/Parsing/LlmResponseParser.cs`
  - `Services/Analysis/Fallback/AnalysisFallbackService.cs`
  - `Services/Analysis/Persistence/AnalysisPersistenceService.cs`
  - `Services/Analysis/Mistakes/MistakeRecordService.cs`
  - `Services/Analysis/Stats/UserStatsUpdateService.cs`
- OCR 与 LLM：
  - `Services/OCR/LiteLLMPhotoSolutionOcrProvider.cs`
  - `Services/LLM/LLMGateway.cs`
- 知识与资料：
  - `Services/Knowledge/KnowledgeRetrievalService.cs`
  - `Services/Knowledge/KnowledgePointNormalizer.cs`
  - `Services/Analysis/Context/AnalysisContextBuilder.cs`
  - `Services/Materials/*`
- 安全与辅助：
  - `Services/Auth/CurrentUserService.cs`
  - `Services/Security/PermissionService.cs`
  - `Services/Visualization/*`
  - `Services/Symbolic/*`

### 数据层
- 主 DbContext：`Data/ApplicationDbContext.cs`
- 核心实体：
  - `Models/AppUser.cs`
  - `Models/Problem.cs`
  - `Models/StudentSolution.cs`
  - `Models/AnalysisResult.cs`
  - `Models/MistakeRecord.cs`
  - `Models/UserKnowledgeState.cs`
  - `Models/UserCourseStats.cs`
  - `Models/LLMRequestLog.cs`
  - `Models/PromptProfile.cs`
  - `Models/KnowledgePoint.cs`
  - `Models/KnowledgeDependency.cs`
  - `Models/CourseMaterial.cs`
  - `Models/MaterialChunk.cs`
  - `Models/ProblemTemplate.cs`
  - `Models/GeneratedPracticeProblem.cs`
  - `Models/PracticeAttempt.cs`

### 前端主入口
- 手动/拍照分析工作台：`wwwroot/analysis.html`
- OCR 兼容页：`wwwroot/ocr.html`
- 前端链路脚本：
  - `wwwroot/js/analysis.js`
  - `wwwroot/js/photo-solution.js`
  - `wwwroot/js/mathlive-ocr.js`
  - `wwwroot/js/auth.js`
  - `wwwroot/js/api.js`

---

## 3. 核心链路完成度

| 步骤 | 是否存在 | 当前模块 | 完整度 | 主要问题 | 优先级 |
|---|---|---|---|---|---|
| 1. 用户输入题目 | 是 | `analysis.html` / `analysis.js` | 较完整 | 题目输入仍以纯文本为主，缺少结构化题目中间态 | P1 |
| 2. 支持图片、文本、LaTeX 或 MathLive 输入 | 是 | `analysis.html` / `ocr.html` / `mathlive-ocr.js` | 部分完整 | 支持“图片 + 文本 + 公式编辑”，但没有统一的题目对象层 | P1 |
| 3. 对图片题目进行 OCR | 是 | `PhotoSolutionsController` / `LiteLLMPhotoSolutionOcrProvider` | 较完整 | OCR 结果只在前端回填，未持久化 | P0 |
| 4. OCR 或手动输入转成题目文本 | 是 | `photo-solution.js` / `AnalysisRequestDto` | 部分完整 | 缺少强制确认层，回填后可直接进入分析 | P0 |
| 5. 题目结构化 | 是 | `LlmRequestFactory` / `LlmResponseParser` | 部分完整 | 结构化的是分析结果，不是题目本身；题目结构化中间层缺失 | P0 |
| 6. 用户确认识别结果 | 是（弱） | `mathlive-ocr.js` / UI 提示 | 不完整 | 仅提示“请检查”，没有显式确认或锁定步骤 | P0 |
| 7. 判断数学分析知识点 | 是 | `PromptProfileSeeder` / `KnowledgePointNormalizer` | 部分完整 | 依赖 LLM + 少量规则，分类面不全 | P1 |
| 8. 判断题型 | 是 | `AnalysisResponseDto.problemType` | 部分完整 | 由 LLM 产出，缺少独立分类器 | P1 |
| 9. 选择解题策略 | 是（隐式） | `PromptProfileSeeder` / `AnalysisFallbackService` | 不完整 | 没有显式策略选择器，更多是 LLM 自行发挥 | P1 |
| 10. 生成分步解答 | 是 | `AnalysisResponseDto.standardSolution` | 较完整 | 结果存在，但质量高度依赖提示词与模型输出 | P1 |
| 11. 对解答进行逻辑自检 | 是（弱） | `StudentSolutionReviewDto` / `AnalysisFallbackService` | 不完整 | 没有独立自检层，只是 LLM 输出 review 字段 + 少量启发式 | P0 |
| 12. 输出最终答案 | 是 | `analysis.js` / `AnalysisResponseDto` | 较完整 | 输出有结构，但缺少“可靠性标记” | P1 |
| 13. 保存题目、解答、知识点标签和错题记录 | 是 | `AnalysisPersistenceService` / `MistakeRecordService` / `UserStatsUpdateService` | 较完整 | OCR 原始结果未保存，且没有 OCR 版本追溯 | P0 |

### 小结
- **主链路已经能跑通**，但“输入可信度 -> 结构化题目 -> 确认 -> 分析 -> 自检 -> 追溯保存”这条闭环还不够硬。
- 当前最薄弱的不是“有没有 LLM”，而是“有没有把错误输入挡在分析前面”。

---

## 4. OCR 链路审计

### 4.1 当前 OCR 实现是什么
- OCR 不是传统本地 OCR 引擎，而是通过 LiteLLM 调用视觉模型：
  - `Services/OCR/LiteLLMPhotoSolutionOcrProvider.cs`
- 前端上传图片后：
  - `PhotoSolutionsController.Ocr` 做尺寸、格式、content-type 检查
  - `photo-solution.js` 将返回的 `problemText` 和 `studentSolutionText` 回填到表单
  - `mathlive-ocr.js` 渲染 `formulas[]` 并允许逐条编辑

### 4.2 OCR 是否只是返回普通文本
- 不是。
- 当前 OCR 返回的不只是普通文本，还包括：
  - `problemText`
  - `studentSolutionText`
  - `detectedSections`
  - `formulas`
  - `warnings`
  - `confidence`
  - `rawProvider`
  - `modelName`
- 这比“纯文本 OCR”强一些，已经有分区和公式字段。

### 4.3 是否能识别数学公式、保留 LaTeX
- 能，且这是当前 OCR 链路的亮点之一。
- `LiteLLMPhotoSolutionOcrProvider` 明确要求公式输出 LaTeX。
- 前端 `mathlive-ocr.js` 会把 `formulas[]` 以 MathLive 形式编辑和回填。
- 但它仍依赖模型把公式识别成正确 LaTeX，**不是结构化 OCR 引擎级别的保证**。

### 4.4 是否能标记不确定区域
- 能，但只有弱提示：
  - `warnings` 中可出现 `section_split_uncertain`
  - `studentSolutionText` 可能被写成 `[unclear]`
- 问题是：
  - 这个不确定性没有被强制传播到后续分析层
  - 也没有形成“低置信度禁止直接分析”的机制

### 4.5 OCR 失败兜底
- 有基础兜底：
  - provider 失败返回 fallback DTO
  - JSON 解析失败会尝试修复反斜杠
  - 仍失败时返回 warnings
- 但兜底只是在“别崩”，**不是“别错”**。

### 4.6 OCR 结果是否直接进入 LLM
- 是。
- 前端把 OCR 结果直接回填到分析输入框，用户随后点击分析就进入 `LearningAnalysisController`。
- 当前没有“必须确认 OCR 结果后才能分析”的硬门槛。

### 4.7 是否存在用户确认或修正 OCR 结果的步骤
- 有 UI 层面的修正能力：
  - 用户可以直接改题目文本和解答文本
  - `mathlive-ocr.js` 可以编辑单个公式
- 但这不是一个真正的确认层：
  - 没有“确认完成”状态
  - 没有“锁定 OCR 结果”的持久标记
  - 没有把“用户确认过/没确认过”写入数据库

### 4.8 是否保存 OCR 原始结果
- **没有。**
- 当前看不到独立的 OCR 结果表或 OCR 审计字段。
- 这意味着：
  - OCR 原始文本、分区、公式、置信度、warnings 都不可追溯
  - 后续很难定位“是 OCR 错了，还是 LLM 错了”

### 4.9 关键风险判断
- **当前确实存在“识别错误直接导致错误解答”的风险。**
- 风险等级：**High**
- 原因：
  1. OCR 结果直接回填到分析输入
  2. 没有强制确认层
  3. OCR 原始结果不保存
  4. 后续分析又完全依赖这份回填文本

### 4.10 最小修复路线
1. 新增 OCR 结果持久化记录，至少保存：
   - 原始图片元信息
   - `problemText`
   - `studentSolutionText`
   - `formulas`
   - `warnings`
   - `confidence`
2. 在前端增加显式“确认并使用”步骤，而不是识别完直接进入分析。
3. 将 `section_split_uncertain`、`[unclear]`、低置信度结果上升为“需要人工确认”状态。
4. 让分析接口接收“已确认 OCR”标记，未确认时只能保存、不能分析。

---

## 5. Prompt 与 LLM 调用审计

### 5.1 Prompt 相关模块
- `Data/Seed/PromptProfileSeeder.cs`
- `Models/PromptProfile.cs`
- `Services/Analysis/LLM/LlmRequestFactory.cs`
- `Services/LLM/LLMGateway.cs`
- `Services/Analysis/Parsing/LlmResponseParser.cs`
- `Services/Analysis/Fallback/AnalysisFallbackService.cs`

### 5.2 当前 Prompt 的特点
- 有课程/模式维度：
  - `solve`
  - `review_solution`
  - `hint`
  - `exam_mode`
  - `concept_explain`
- 有严格 JSON 输出要求。
- 顶层 schema 比普通聊天更结构化：
  - `course`
  - `chapter`
  - `problemType`
  - `difficulty`
  - `knowledgePoints`
  - `solutionOverview`
  - `standardSolution`
  - `studentSolutionReview`
  - `mistakeTags`
  - `reviewSuggestions`
  - `visualization`

### 5.3 Prompt 是否过于通用
- **有通用化倾向。**
- 虽然是数学分析课程 prompt，但它仍然更像“让模型按 schema 输出一个结构化解题报告”。
- 它还没有把智能体拆成明确角色：
  - 题目复述/校验器
  - 知识点分类器
  - 解题策略选择器
  - 逐步讲解器
  - 自检器

### 5.4 是否只是让模型直接解题
- 不是完全裸解题，因为它要求结构化字段和 review 结果。
- 但本质上仍然是“一个模型直接产出大块答案”，没有明确的多阶段任务编排。

### 5.5 是否要求模型复述题目、检查 OCR、判断知识点、判断题型、说明策略、逐步推理、自检
- **复述题目 / 检查 OCR：没有明确硬要求。**
- **判断知识点：有，但不是独立任务，只是 schema 字段。**
- **判断题型：有，但依赖模型自己填 `problemType`。**
- **说明策略：有一点 `solutionOverview`，但不是显式策略选择器。**
- **分步解答：有 `standardSolution`。**
- **最终自检：有 `studentSolutionReview`，但缺少独立 verifier。**

### 5.6 是否区分讲解模式、答案模式、提示模式
- **有模式名，但没有完全形成不同执行链。**
- `PromptProfileSeeder` 预留了多模式。
- 但当前主前端流程主要还是 `review_solution`。
- 所以这是“配置上有分层，实际链路上还不够分层”。

### 5.7 兜底能力
- 有：
  - `LLMGateway` 会记录请求日志
  - `AnalysisService` 会保存失败、解析失败、schema invalid 的结果
  - `AnalysisFallbackService` 能处理 legacy shape 和部分启发式修正
- 缺少：
  - 明确超时控制
  - 重试策略
  - 多模型回退
  - 面向用户的可解释错误层

### 5.8 结论
- 当前 LLM 链路**更像“课程边界内的结构化解题工具”**，而不是纯聊天。
- 但它还**不够像严格分工的数学分析智能体**，因为：
  - 没有显式 OCR 校验角色
  - 没有独立知识点分类角色
  - 没有独立自检角色
  - 没有可靠性分级与重生成闭环

---

## 6. 数学分析知识点体系审计

### 6.1 已覆盖的知识点
`Data/Seed/PlatformSeedData.cs` 中已经覆盖了较多数学分析内容，尤其是：
- 数列极限
- 函数极限
- 连续性
- 一元函数微分学
- 中值定理
- 不定积分
- 定积分
- 反常积分
- 数项级数
- 函数列与函数项级数
- 幂级数
- 多元函数微分学

### 6.2 覆盖情况判断
- **有知识点枚举：有。**
- **有题型分类：有，但较粗。**
- **有常见方法库：只有少量隐式体现。**
- **有常见错误库：没有独立库。**
- **有定理依赖关系：有，但很薄。**
- **能根据题目自动归类：部分能。**
- **能根据知识点选择解题策略：部分能。**
- **能把错题归入对应知识点：能，但依赖 LLM 标签 + 少量归一化。**

### 6.3 明显缺口
当前 seed 中**没有完整覆盖**以下重要内容：
- 重积分
- 曲线积分与曲面积分

另外还有一些常见但未系统化的主题：
- 更细的函数列/函数项级数错误模式
- 更完整的多元函数积分与向量分析相关条目
- 更丰富的“常见误区/定理条件检查”知识点

### 6.4 归一化能力现状
- `KnowledgePointNormalizer` 只对少量表达做映射，尤其偏向反常积分场景。
- 这说明当前体系的“分类能力”还是**窄规则 + LLM 标签**，不是完整课程分类器。

### 6.5 最小建设方案
1. 补齐两个缺口章节：
   - 重积分
   - 曲线积分与曲面积分
2. 给每个章节至少补 4~6 个最常见知识点。
3. 扩展 `KnowledgePointNormalizer` 的映射表，不要一上来做复杂本体系统。
4. 把“常见错误”做成轻量表，而不是再造一套大系统。
5. 先支持“题目 -> 章节/知识点”的粗分，再逐步细化到“方法/误区/定理条件”。

---

## 7. 解答自检能力审计

### 7.1 现状
- 当前解答自检主要落在：
  - `studentSolutionReview`
  - `mistakeTags`
  - `reviewSuggestions`
  - `AnalysisFallbackService`
- `AnalysisService.ValidateParsedResponse` 只做了很基础的 schema 校验。

### 7.2 是否主动检查常见错误
- **不是主动、系统性地检查。**
- 现有机制更像：
  - LLM 自己说“我检查过了”
  - 后端在一些极少数模式下做启发式补救

### 7.3 当前能覆盖的错误类型
- 能部分捕捉一些明显错误：
  - `IsCorrect` null 的兜底
  - 反常积分“趋于 0 不等于收敛”的启发式修正
  - legacy 负向格式映射
- 但你列出的这些典型错误大部分都**没有独立检查**：
  - 定义域遗漏
  - 端点遗漏
  - 极限与积分交换
  - 逐点收敛 / 一致收敛混淆
  - 条件不足却乱用定理
  - 瑕点拆分遗漏
  - 级数判别条件不完整
  - Taylor 余项不说明
  - 多元函数概念混淆

### 7.4 是否能把自检结果展示给用户
- 部分能。
- 现在前端可以显示：
  - `mainIssue`
  - `logicGaps`
  - `mistakeTags`
  - `reviewSuggestions`
- 但没有“答案可靠性等级”。

### 7.5 是否能标记“答案可能不可靠”
- **没有独立、明确的可靠性字段。**
- 当前只能通过：
  - `isCorrect = null`
  - `mainIssue` 文案
  - `LLM failed / parse failed / schema invalid`
  来间接表达。

### 7.6 是否能在发现问题后重新生成或修正答案
- **目前没有形成明确的自动重生成闭环。**
- 有 fallback 和失败保存，但没有“二次审查 -> 修正输出”的强制流程。

### 7.7 最小修复方案
1. 新增一个显式的 `answerReliability` 或 `needsReview` 字段。
2. 在分析后加一个独立 verifier：
   - 可先用规则检查高频错误
   - 再逐步引入第二次 LLM 审核
3. 让 UI 明确展示：
   - “可信”
   - “需复核”
   - “不可判定”
4. 对 `review_solution` 模式，若自检失败，优先触发二次生成而不是直接展示最终答案。

---

## 8. 数据库与用户数据审计

### 8.1 已支持的数据
当前数据库模型已经覆盖了较多业务对象：
- 用户信息：`AppUser`
- 会话归属：session 中的 `auth_user_id`
- 题目：`Problem`
- 学生解答：`StudentSolution`
- 结构化分析：`AnalysisResult`
- 错题：`MistakeRecord`
- 题目/知识点状态：`UserKnowledgeState`
- 用户课程统计：`UserCourseStats`
- 模型调用日志：`LLMRequestLog`
- 知识点体系：`KnowledgePoint`、`KnowledgeDependency`
- 课程资料：`CourseMaterial`、`MaterialChunk`、`MaterialChunkKnowledgePoint`
- 模板题与练习：`ProblemTemplate`、`GeneratedPracticeProblem`、`PracticeAttempt`

### 8.2 是否支持你列出的 15 类内容
| 内容 | 当前支持 | 说明 |
|---|---|---|
| 1. 用户信息 | 是 | `AppUser` |
| 2. 登录认证信息 | 部分 | 仅 session + 开发期用户名登录；无密码/外部身份表 |
| 3. 会话记录 | 否 | 只有 session 过程，没有独立会话历史表 |
| 4. 原始题目输入 | 是 | `Problem.ContentMarkdown`、`StudentSolution.SolutionText` |
| 5. OCR 原始结果 | 否 | 没有 OCR 审计表 |
| 6. 用户修正后的题目 | 部分 | 只是在前端编辑后再提交，没有单独版本表 |
| 7. 结构化题目 | 否/弱 | 当前结构化更多发生在分析结果，而不是题目实体层 |
| 8. 知识点标签 | 是 | `AnalysisResult.KnowledgePointsJson`、`MistakeRecord.KnowledgePointId` |
| 9. 题型标签 | 是 | `Problem.ProblemType`、`AnalysisResult.ProblemType` |
| 10. 解答记录 | 是 | `AnalysisResult`、`StudentSolution`、`PracticeAttempt` |
| 11. 自检记录 | 部分 | `StudentSolutionReview`，但不是独立审计表 |
| 12. 错题标记 | 是 | `MistakeRecord` |
| 13. 用户薄弱知识点统计 | 是 | `UserKnowledgeState` |
| 14. 历史题目检索 | 部分 | 有资料检索和题目实体，但没有统一历史搜索 API |
| 15. 重新生成答案版本记录 | 部分 | `AnalysisResult` 可形成多次记录，但没有显式 version 字段 |

### 8.3 认证账户是否和业务用户过度耦合
- **是。**
- `AppUser` 同时承担了：
  - 业务用户画像
  - 登录主体
  - 角色主体
  - 统计归属主体
- 这在 MVP 阶段可以接受，但不是生产级认证最佳形态。

### 8.4 是否存在临时密码或本地密码设计
- 当前代码里**没有真正落地本地密码表**。
- `AuthOptions` 和设计文档里已经预留了：
  - `DevelopmentUsername`
  - `LocalPassword`
  - `Oidc`
  - `Disabled`
- 但 `LocalPassword` 仍是“配置可识别、功能未实现”状态。

### 8.5 是否适合后续接入 OIDC
- **适合，但前提是先解耦认证与业务用户。**
- 当前已经有：
  - `AppUser` 业务主表
  - `AuthOptions` 模式抽象
  - 生产 fail-fast
- 下一步需要补的是 `AuthAccounts` / `ExternalLogin` 这一层。

### 8.6 teacher scope 是否混入认证表
- 目前 teacher scope 还没有独立授权域模型。
- `PermissionService` 仍写着临时规则：
  - teacher 可以看课程范围内数据
  - TODO：后续应替换为 `TeacherCourseAssignment`
- 这意味着：
  - 角色已经有了
  - 课程/班级授权边界还没有真正落表

### 8.7 题目和答案是否可追溯
- **可以追溯，但不完整。**
- 可追溯的有：
  - `Problem`
  - `StudentSolution`
  - `AnalysisResult`
  - `MistakeRecord`
  - `LLMRequestLog`
- 不可追溯的关键缺口：
  - OCR 原始结果
  - OCR 置信度与 warnings
  - 用户确认/修正前的中间态

### 8.8 是否可能被错误覆盖或丢失
- 主分析结果多数是“追加写入”，覆盖风险不高。
- 但以下地方有轻微覆盖风险：
  - 模板题更新时会清空并重建知识点链接
  - 题目结构化中间态未版本化
  - OCR 结果未持久化，等同于“丢失”

---

## 9. 安全与可靠性审计

### 9.1 API Key 与敏感配置
- API Key 采用环境变量式配置思路，没有直接写进前端。
- `LLMGateway` 会校验 API Key 是否为空、是否为 ASCII。
- `appsettings.json` 中只放了 URL / 模式 / alias，符合基本分离原则。

### 9.2 当前未发现的高危泄露
- 没看到前端直接暴露模型密钥。
- 没看到图片 base64 或原始 token 被直接返回前端。
- 前端渲染对分析内容做了 HTML escape，降低了 XSS 风险。

### 9.3 需要关注的中高风险点
#### Medium
- `AnalysisResult.RawResponseJson` 会保存成功或失败的模型返回内容，可能包含较多用户题目上下文。
- `LLMRequestLog.ErrorMessage` 会记录上游错误体的截断内容，仍可能带有敏感上下文。
- `CourseMaterialsController.Upload` 只做了类型与大小限制，没有做更深的内容安全扫描。
- `AllowedHosts = "*"` 如果被错误部署到公网，边界会较宽。

#### Low
- `/api/health` 是公开浅健康检查，适合部署探活，但信息量有限。
- 前端开发 fallback 只在 localhost/127.0.0.1 下启用，当前风险可控。

### 9.4 可靠性问题
- **LLM / OCR 没有显式超时与重试策略。**
  - `SymPySymbolicMathService` 有较明确 timeout
  - 但 `LLMGateway` 和 OCR provider 没有明确的业务级重试策略
- `LLMGateway` / OCR provider 失败时只做单次调用兜底
- 这会让网络抖动或 provider 短暂故障直接转化为用户失败

### 9.5 风险分级
#### High
- OCR 识别结果无持久化、无强制确认，容易把错误输入直接送进分析链路
- 生产场景若绕过环境校验继续使用开发式登录，会造成严重身份风险

#### Medium
- LLM / OCR 无显式业务级 timeout + retry
- `RawResponseJson` / `LLMRequestLog.ErrorMessage` 可能记录过多上下文
- teacher course scope 仍未落地，授权边界偏粗

#### Low
- 健康检查浅
- 模型 alias / provider 配置仍偏演示化
- 前端 dev fallback 仅本地可用，短期可接受

---

## 10. 产品边界风险

### 10.1 当前已经出现的边界外倾向
1. **符号计算**
   - `Services/Symbolic/*`
   - `Controllers/SymbolicController.cs`
   - 这更像 CAS/辅助工具，不是核心学习闭环

2. **可视化 / GeoGebra**
   - `Services/Visualization/*`
   - `AnalysisResponseDto.Visualization`
   - 当前更多是辅助展示，但如果继续扩张，很容易滑向“图形创作平台”

3. **模板题与练习生成**
   - `Controllers/ProblemTemplateController.cs`
   - `Controllers/PracticeController.cs`
   - 这一块若继续泛化，容易演化成通用题库平台

4. **课程资料知识库**
   - `Services/Materials/*`
   - `Services/Knowledge/*`
   - 这是合理的学习辅助，但若过度扩张会变成通用知识库产品

### 10.2 风险判断
- 当前存在的不是“已经跑偏”，而是**产品边界膨胀风险**。
- 风险点主要在：
  - 给数学分析学习智能体增加了很多旁路能力
  - 但这些能力的边界还没被明确隔离

### 10.3 应该如何处理
- **保留**：
  - 课程资料检索
  - 题目模板/练习（只要严格课程绑定）

- **冻结/隔离**：
  - 通用符号计算能力
  - 更复杂的可视化创作能力
  - 任何插件式扩展方向

- **延后**：
  - 更广泛的数学平台化
  - 通用 CAS 化
  - 复杂图形绘制/创作能力

- **删除或不推进**：
  - 与数学分析学习无关的功能
  - “通用数学工具平台”叙事

### 10.4 边界判断总结
- 目前边界风险**中等**，不是立刻失控，但已经需要控边。
- 若继续沿当前架构推进，必须明确：
  - 哪些是学习智能体核心链路
  - 哪些是辅助能力
  - 哪些只是开发调试工具

---

## 11. P0 / P1 / P2 优先级任务表

| 优先级 | 任务 | 原因 | 最小改法 |
|---|---|---|---|
| P0 | OCR 结果持久化与确认层 | 当前最大风险是“错识别直接进入错误解答” | 新增 OCR 审计记录 + 显式确认按钮 + 未确认不能分析 |
| P0 | 题目结构化中间层 | 目前只有纯文本输入，没有可追溯的结构化题目对象 | 在 OCR/手输到 LLM 之间增加结构化 DTO / 存储层 |
| P0 | 独立自检/可靠性标记 | 现在 self-check 只是 LLM 字段，不够硬 | 增加 `needsReview` / `answerReliability`，必要时二次审核 |
| P0 | LLM/OCR 显式 timeout + retry | provider 短暂抖动会直接失败 | 在网关层加入业务超时与有限重试 |
| P1 | 补齐知识点体系缺口 | 重积分、曲线积分、曲面积分缺失 | 先补 seed，不急着重构整套知识树 |
| P1 | 扩展知识点正常化映射 | 当前归一化太窄，影响错题沉淀 | 扩展 `KnowledgePointNormalizer` 映射表 |
| P1 | 题型/策略更明确分层 | 目前 prompt 太像一次性答题 | 把题型、策略、自检拆成更明确的输出段 |
| P1 | teacher/course 授权关系落表 | 现在 teacher scope 还是临时规则 | 增加课程归属或教师课程分配关系 |
| P2 | OCR/分析结果的用户可回溯历史页 | 有数据但缺少清晰的查看入口 | 先做查询页，不必大改模型 |
| P2 | 更丰富的错误库 | 有助于教学反馈，但不阻塞主链路 | 先做轻量字典表即可 |

---

## 12. 最小修改路线

### 第一阶段：把输入链路收紧
1. OCR 返回后，先保存一份可追溯记录。
2. 用户必须点击“确认识别结果”后，才能把内容送进分析链路。
3. 未确认或低置信度 OCR 只允许编辑，不允许直接分析。

### 第二阶段：把分析链路拆清
1. 题目结构化从“文本输入”中抽出来，形成独立中间态。
2. Prompt 分成三段：
   - 题目识别校验
   - 知识点/题型分类
   - 解答生成/自检
3. 给结果加上可靠性标记，而不是只给正确/错误。

### 第三阶段：把知识体系补齐
1. 先补齐数学分析缺口章节。
2. 再扩充常见方法与错误模式。
3. 继续沿用当前 seed + normalizer 的轻量做法，不要上来做大本体系统。

### 第四阶段：把边界收住
1. Symbolic、GeoGebra、模板练习都保留，但只作为辅助能力。
2. 不继续向通用数学平台方向扩张。
3. 新增功能先问一句：它是否直接服务“数学分析题目识别、分析、讲解、错题沉淀”。

---

## 13. 最终结论

### 13.1 当前项目更接近哪一种
**结论：4. 架构边界不清晰的混合项目。**

### 13.2 理由
- 它已经有明显的数学分析学习智能体主干：
  - OCR
  - 结构化分析
  - 知识点
  - 错题
  - 统计
- 但它同时又有不少偏平台化/工具化的旁路能力：
  - 符号计算
  - 可视化
  - 模板题生成
  - 课程资料知识库
- 这些能力不一定错误，但目前**没有被严格约束在“数学分析学习智能体”边界内**。

### 13.3 下一步最应该修什么
- **先修 OCR 确认层 + 题目结构化层 + 自检层。**
- 如果只选一个最关键的起点：**先修 OCR 确认层**，因为它是最容易把错误放大到后续所有步骤的入口。

### 13.4 哪些问题属于 P0
- OCR 结果未持久化、未强制确认
- 题目结构化中间层缺失
- 自检能力不独立、缺少可靠性标记
- LLM/OCR 缺少显式 timeout / retry

### 13.5 哪些问题只是后续优化
- 更丰富的错误库
- 历史题目检索体验
- 更细的题型分类
- 更丰富的统计图表

### 13.6 是否存在产品边界膨胀
- **存在。**
- 当前主要风险是从“数学分析学习智能体”慢慢滑向：
  - 通用数学平台
  - CAS 工具
  - 图形创作工具
  - 插件化平台
- 这些方向应该冻结、隔离或延后，而不是顺手继续扩。

### 13.7 是否建议继续沿当前架构推进
- **建议继续推进，但必须先收紧边界。**
- 也就是说：
  - 主链路可以继续演进
  - 辅助能力可以保留
  - 但不能再任由旁路功能无界扩张

### 13.8 是否建议先补齐 OCR 确认层、题目结构化层、知识点分类层和自检层
- **建议，而且顺序应该就是：**
  1. OCR 确认层
  2. 题目结构化层
  3. 知识点分类层
  4. 自检层

这四层补齐后，项目才更像一个真正的数学分析学习智能体，而不是一个“能解题的通用 AI 工具集合”。

---

## 14. 审计后整改记录（2026-06-08）

> 本节仅用于同步当前已完成的整改状态，避免后续接手时继续沿用本审计初稿中的旧判断。

### 14.1 已完成的 P0 主链路整改
- OCR 结果已持久化保存，并具备审计追溯能力。
- OCR 已增加显式确认层，未确认 OCR 不能直接进入分析。
- 后端已对未确认 OCR 做 `409 Conflict` 强拦截。
- `StructuredProblem` 已作为 OCR 确认后、分析前的题目结构化中间层落地。
- `AnalysisResult` 已增加可靠性标记字段。
- `AnalysisVerificationService` 已落地轻量自检与可靠性判定。
- 结果页已能显示 `Reliable / NeedsReview / Uncertain / UnsafeToUse`。
- LLM / OCR 已加入显式 timeout + retry + 结构化错误返回。
- OCR / StructuredProblem / 自检链路的关键测试已补齐并通过。

### 14.2 已完成的 P1 课程边界整改
- 数学分析课程 seed 已补齐：
  - 重积分
  - 曲线积分
  - 曲面积分
  - 高频误区知识点
- `KnowledgePointNormalizer` 已扩展新章节与同义表达映射。
- `KnowledgeRetrievalService` 已扩展新知识点检索召回覆盖。
- `AnalysisContextBuilder` 已整理为更清晰的课程化上下文。
- 分析页“关联知识点”已改为课程标签化展示。
- `PromptProfile` / Prompt 文案已升级为 v4，强化数学分析课程边界与条件检查。

### 14.3 当前验证状态
- 最近一次验证结果：
  - `dotnet build` 通过
  - `dotnet test` 通过
  - `49 passed, 0 failed`
- 已知 `NU1900` / `CS8618` / `ASP0014` 为仓内既有警告，不影响本轮整改结论。

### 14.4 当前项目定位更新
- 经过本轮整改后，项目已经明显从“边界不清晰的混合项目”收拢到**数学分析课程学习智能体**方向。
- 但它仍保留少量旁路能力（符号计算、可视化、课程资料、模板题练习），因此更准确的当前状态是：
  - **已明显收拢的数学分析学习智能体**
  - **仍需继续收束边界，避免向通用数学平台扩张**

### 14.5 后续建议优先级
1. 错误码前端文案统一
2. 首页 / 题目页知识点标签风格统一
3. 进一步补充数学分析题型策略库
4. teacher scope 落表
5. OIDC / AuthAccounts 生产级认证解耦
6. 历史题目检索页面
7. 更丰富的错因库
