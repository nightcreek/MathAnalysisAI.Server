namespace MathAnalysisAI.Models
{
    public class WrongQuestion
    {
        public int Id { get; set; }

        // 存储 OCR 后的 LaTeX 数学公式
        public string ContentHtml { get; set; }

        // AI 分析的错误原因：计算/概念/逻辑
        public string ErrorCategory { get; set; }

        // 详细的 AI 诊断建议
        public string AIDiagnosis { get; set; }

        // 原始图片路径
        public string ImagePath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}