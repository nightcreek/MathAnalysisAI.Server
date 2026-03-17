using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class QuestionController : ControllerBase
{
    // 动作：上传并识别错题
    [HttpPost("upload")]
    public async Task<IActionResult> UploadQuestion(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("请上传图片");

        // 1. 【保存文件】将图片存入本地 wwwroot/uploads
        var filePath = Path.Combine("wwwroot/uploads", file.FileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 2. 【调用 AI】这里后续接入 Mathpix 和 LLM
        // 伪代码示例：
        // var latex = await _mathService.Recognize(filePath);
        // var analysis = await _llmService.Analyze(latex);

        return Ok(new { message = "识别成功", path = filePath });
    }
}