using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathAnalysisAI.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        private const string P2T_PATH =
            @"C:\Users\zhoux\AppData\Roaming\Python\Python314\Scripts\p2t.exe";

        // 使用普通 Regex，不依赖源生成器，避免 partial 问题
        private static readonly Regex AnsiRegex = new Regex(@"\x1B\[[0-9;]*[mK]", RegexOptions.Compiled);
        private static readonly Regex OutsRegex = new Regex(@"Outs:\s*(.+)", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex CodeBlockRegex = new Regex(@"^```json\s*|\s*```$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly char[] NewLineChars = new[] { '\r', '\n' };

        public QuestionController(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("文件为空");

            try
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsPath, fileName);
                var relativePath = "/uploads/" + fileName;

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                string rawLatex = await RunPix2Text(filePath);
                string cleanLatex = NormalizeLatex(rawLatex);

                var question = new WrongQuestion
                {
                    ImagePath = relativePath,
                    RawLatex = rawLatex,
                    CleanLatex = cleanLatex,
                    OverallEvaluation = "",
                    StudentAnswer = "",
                    ErrorAnalysis = "",
                    StandardSolution = "",
                    ImprovementSuggestion = "",
                    KnowledgePoint = "",
                    CreatedAt = DateTime.Now
                };

                _db.WrongQuestions.Add(question);
                await _db.SaveChangesAsync();

                return Ok(new { question.Id, question.ImagePath, question.RawLatex, question.CleanLatex });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"上传失败: {ex.Message}");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("analyze/{id}")]
        public async Task<IActionResult> Analyze(int id)
        {
            var question = await _db.WrongQuestions.FindAsync(id);
            if (question == null) return NotFound("题目不存在");

            try
            {
                string apiKey = _config["DeepSeek:ApiKey"];
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var prompt = $@"
你是数学AI错题老师。请分析这道题：
{question.CleanLatex}

**严格**返回纯JSON：
{{
  ""overallEvaluation"": ""..."",
  ""knowledgePoint"": ""..."",
  ""studentAnswer"": ""..."",
  ""errorAnalysis"": ""..."",
  ""standardSolution"": ""..."",
  ""improvementSuggestion"": ""...""
}}";

                var requestBody = new
                {
                    model = "deepseek-chat",
                    messages = new[] { new { role = "user", content = prompt } }
                };

                var response = await client.PostAsync(
                    "https://api.deepseek.com/chat/completions",
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                );
                var resultJson = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(resultJson);
                var content = doc.RootElement
                                .GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content")
                                .GetString();

                string cleanJson = CodeBlockRegex.Replace(content, "").Trim();
                var analysis = JsonSerializer.Deserialize<WrongQuestion>(cleanJson);

                question.OverallEvaluation = analysis?.OverallEvaluation;
                question.KnowledgePoint = analysis?.KnowledgePoint;
                question.StudentAnswer = analysis?.StudentAnswer;
                question.ErrorAnalysis = analysis?.ErrorAnalysis;
                question.StandardSolution = analysis?.StandardSolution;
                question.ImprovementSuggestion = analysis?.ImprovementSuggestion;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    question.Id,
                    question.OverallEvaluation,
                    question.KnowledgePoint,
                    question.StudentAnswer,
                    question.ErrorAnalysis,
                    question.StandardSolution,
                    question.ImprovementSuggestion
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"AI分析失败: {ex.Message}");
                return StatusCode(500, "AI分析失败，请稍后重试");
            }
        }

        [HttpGet("list")]
        public IActionResult GetList()
        {
            var list = _db.WrongQuestions
                .OrderByDescending(x => x.Id)
                .ToList();
            return Ok(list);
        }

        // 标记为 static 消除 CA1822
        private static async Task<string> RunPix2Text(string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = P2T_PATH,
                Arguments = $"predict -i \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
                Console.Error.WriteLine($"OCR Error: {error}");

            return ExtractLatex(output);
        }

        private static string ExtractLatex(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            // 去掉 ANSI 转义字符
            raw = AnsiRegex.Replace(raw, "");

            // 按行分割，过滤掉明显是日志的行
            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(line => line.Trim())
                           .Where(line =>
                               !string.IsNullOrWhiteSpace(line) &&
                               !line.StartsWith("Loading") &&
                               !line.StartsWith("Using ONNX") &&
                               !line.StartsWith("[INFO]") &&
                               !line.StartsWith("[DEBUG]") &&
                               !line.Contains("ONN X Runtime")  // 有些行可能包含 "ONN X Runtime"
                           )
                           .ToList();

            // 如果只剩一行，直接返回
            if (lines.Count == 1)
                return lines[0];

            // 如果有多行，尝试提取连续的非日志内容块
            var contentLines = lines.Where(l =>
                !l.StartsWith("Loading") &&
                !l.StartsWith("Using") &&
                !l.Contains("inference...")
            ).ToList();

            return string.Join("\n", contentLines).Trim();
        }

        private static string NormalizeLatex(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = AnsiRegex.Replace(text, "");
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            text = text.Trim();
            return text;
        }
    }
}