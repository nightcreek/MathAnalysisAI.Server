# 前端页面拆分与学生端信息架构方案

## 1. 拆分前问题
- `index.html` 同时承载：
  - OCR 上传
  - OCR 结果回填
  - MathLive 公式校对
  - 手动分析输入
  - 分析结果展示
- 首页职责过重，导致：
  - 页面过长
  - 首屏不清晰
  - OCR / analyze DOM 耦合度高
  - 公网演示时首页信息密度过高

## 2. 拆分后页面职责
- `index.html`
  - 作为学生首页入口
  - 以学习起点而不是功能清单的方式呈现
  - Hero 直接说明“拍照或输入解答，快速找出数学分析题中的问题”
  - 展示更学生化的学习流程、“你可以这样使用”场景区与 3 张主卡片：
    - 先分析一道题
    - 学习统计
    - 课程资料
  - 不再承载完整 OCR / analyze 流程
- `analysis.html`
  - 作为统一分析工作台
  - 承载“手动输入 / 拍照识别”两种输入模式
  - 承载题目与解答确认、公式校对、手动分析与结果展示
  - 学生主任务统一收口到这一页
- `ocr.html`
  - 保留为兼容入口
  - 仍可独立使用 OCR + MathLive
  - 同时提示用户优先进入统一分析工作台
  - 兼容旧链接，不再作为首页和导航主入口
- `stats.html`
  - 保持学习统计与排行榜
- `materials.html`
  - 学生端文案和入口为只读导向
  - 本轮采用保守前端策略：student 隐藏上传、列表和检索调试管理区，显示只读说明
  - 教师 / 管理员保留上传与检索调试入口
- `dev.html`
  - 保持开发工具、symbolic 调试和 MathLive 试验

## 3. 页面加载的 JS/CSS
### 3.1 `index.html`
- `/css/app.css`
- `/js/config.js`
- `/js/api.js`
- `/js/ui.js`
- `/js/auth.js`
- `/js/nav.js`

### 3.2 `analysis.html`
- `/css/app.css`
- MathJax CDN
- `/vendor/mathlive/*`
- `/js/config.js`
- `/js/api.js`
- `/js/ui.js`
- `/js/auth.js`
- `/js/nav.js`
- `/js/knowledge-points.js`
- `/js/analysis.js`
- `/js/photo-solution.js`
- `/js/mathlive-ocr.js`

### 3.3 `ocr.html`
- `/css/app.css`
- MathJax CDN
- `/vendor/mathlive/*`
- `/js/config.js`
- `/js/api.js`
- `/js/ui.js`
- `/js/auth.js`
- `/js/nav.js`
- `/js/knowledge-points.js`
- `/js/analysis.js`
- `/js/photo-solution.js`
- `/js/mathlive-ocr.js`

## 4. JS 拆分结果
- `analysis.js`
  - 保持原有 `window.analyzeText`
  - 新增工作台模式切换
  - 支持 `analysis.html?mode=ocr`
  - 新增 DOM 存在检查
  - 仅在分析页面相关 DOM 存在时运行
- `photo-solution.js`
  - 保持原有 `window.recognizePhotoSolution`
  - 新增 DOM 存在检查
  - 仅在 OCR 页面相关 DOM 存在时运行
- `mathlive-ocr.js`
  - 保持每条公式一个独立 `math-field`
  - 未重写逻辑，仅继续挂载在 OCR 页面
- `nav.js`
  - 导航统一收口到共享脚本
  - `/ocr.html` 在导航上归并到“解题分析”
  - 默认不向学生展示“开发工具”
  - 只有明确 `teacher/admin` 才显示开发工具入口
  - 学生导航统一为：首页 / 解题分析 / 课程资料 / 学习统计
- `api.js`
  - 未改公开方法名
- `ui.js`
  - 未改公共接口

## 5. 未完成事项
- 还未做浏览器点击级完整回归
- 首页入口文案与卡片视觉仍可继续微调
- 首页已完成第一轮“任务导向”表达，但仍可继续打磨学习场景与结果预期的表达层次
- MathJax 仍未本地 vendor 化
- `ocr.html` 目前仍保留完整兼容页，后续可进一步弱化为纯跳转入口
- 学生端课程资料“真实只读列表”尚未单独打通；当前只是入口与文案学生化，管理区在前端隐藏
- OCR / analysis 共用表单结构目前为静态复制，后续如有需要可再提取为更细粒度模板

## 6. 手动验证清单
- `/`
  - 首页只展示学习入口，不展示 OCR / analyze 大表单
- `/analysis.html`
  - 可切换“手动输入 / 拍照识别”
  - 手动模式可输入题目与解答
  - 拍照模式可做 OCR、公式校对和手动分析
  - 可点击开始分析
  - 可展示分析结果
- `/ocr.html`
  - 页面应提示“已合并到解题分析工作台”
  - 仍可上传图片
  - 仍可展示 OCR 回填和公式卡片
  - 作为兼容入口可跳转到 `analysis.html?mode=ocr`
- `/stats.html`
  - 页面正常打开
- `/materials.html`
  - student 视角下应显示只读说明
  - teacher/admin 视角下仍应保留管理入口
- `/dev.html`
  - 页面正常打开
  - 学生导航默认不展示该入口

## 7. 决策结论
- 本轮拆分优先保证：
  - 不改后端 API
  - 不改数据库
  - 不引入前端框架
  - 不重写 OCR / MathLive 逻辑
- 拆分结果是一个最小、安全、可回滚的页面级重组：
  - 首页变成学生学习入口页
  - “手动输入”和“拍照识别”被归并为同一分析工作台的两种模式
  - `ocr.html` 保留为兼容入口
  - 学生端默认不显示“开发工具”
  - 原有 JS 保持基本兼容，仅增加 DOM 守卫和工作台模式切换
