namespace MathAnalysisAI.Server.Services.Knowledge;

public static class KnowledgePointLabelHelper
{
    public static string NormalizeLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();
        if (!value.StartsWith("ma.", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return value switch
        {
            "ma.multiple_integral.concept" => "重积分",
            "ma.multiple_integral.double_integral" => "二重积分",
            "ma.multiple_integral.triple_integral" => "三重积分",
            "ma.multiple_integral.region_order" => "积分区域与积分次序",
            "ma.multiple_integral.change_of_variables" => "重积分变量替换",
            "ma.multiple_integral.coordinate_systems" => "极坐标 / 柱坐标 / 球坐标",
            "重积分概念" => "重积分",
            "二重积分定义与计算" => "二重积分",
            "三重积分定义与计算" => "三重积分",
            "积分区域与积分次序" => "积分区域与积分次序",
            "重积分变量替换" => "重积分变量替换",
            "坐标系下的重积分" => "极坐标 / 柱坐标 / 球坐标",
            "ma.line_integral.concept" => "曲线积分",
            "ma.line_integral.scalar" => "第一类曲线积分",
            "ma.line_integral.vector" => "第二类曲线积分",
            "ma.line_integral.path_independence" => "路径无关性 / 保守场",
            "ma.line_integral.green_formula" => "Green 公式",
            "曲线积分概念" => "曲线积分",
            "第一类曲线积分" => "第一类曲线积分",
            "第二类曲线积分" => "第二类曲线积分",
            "路径无关性与保守场" => "路径无关性 / 保守场",
            "Green 公式" => "Green 公式",
            "ma.surface_integral.concept" => "曲面积分",
            "ma.surface_integral.scalar" => "第一类曲面积分",
            "ma.surface_integral.flux" => "第二类曲面积分",
            "ma.surface_integral.gauss_formula" => "Gauss 公式",
            "ma.surface_integral.stokes_formula" => "Stokes 公式",
            "曲面积分概念" => "曲面积分",
            "第一类曲面积分" => "第一类曲面积分",
            "第二类曲面积分" => "第二类曲面积分",
            "Gauss 公式" => "Gauss 公式",
            "Stokes 公式" => "Stokes 公式",
            "ma.improper_integral.singularity_split" => "反常积分瑕点拆分",
            "ma.power_series.endpoint_convergence" => "幂级数端点收敛",
            "ma.function_series.uniform_convergence.criteria" => "函数项级数一致收敛",
            "ma.function_series.pointwise_convergence" => "逐点收敛",
            "ma.function_series.pointwise_vs_uniform" => "逐点收敛与一致收敛区分",
            "ma.power_series.taylor_remainder" => "泰勒公式余项",
            "ma.mean_value_theorem.conditions_check" => "中值定理条件检查",
            "ma.function_series.limit_exchange_conditions" => "极限与积分交换条件",
            "ma.improper_integral.convergence_criteria" => "反常积分收敛与发散判定",
            "ma.improper_integral.comparison_test" => "反常积分比较判别法",
            "反常积分瑕点拆分" => "反常积分瑕点拆分",
            "幂级数端点收敛" => "幂级数端点收敛",
            "一致收敛定义" => "函数项级数一致收敛",
            "函数列逐点收敛" => "逐点收敛",
            "逐点收敛与一致收敛的区别" => "逐点收敛与一致收敛区分",
            "泰勒公式余项" => "泰勒公式余项",
            "使用中值定理前的条件检查" => "中值定理条件检查",
            "极限与积分交换的条件" => "极限与积分交换条件",
            "收敛与发散判定" => "反常积分收敛与发散判定",
            "比较判别法" => "反常积分比较判别法",
            _ => value
        };
    }

    public static int GetPriority(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return 99;
        }

        var specificTerms = new[]
        {
            "二重积分", "三重积分", "第一类曲线积分", "第二类曲线积分", "第一类曲面积分", "第二类曲面积分",
            "路径无关性", "保守场", "Gauss 公式", "Stokes 公式", "幂级数端点收敛", "泰勒公式余项",
            "反常积分瑕点拆分", "中值定理条件检查", "极限与积分交换条件"
        };

        if (specificTerms.Any(term => label.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        var conditionTerms = new[] { "条件", "定理", "公式", "交换", "收敛" };
        if (conditionTerms.Any(term => label.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }

        var confusionTerms = new[] { "逐点收敛", "一致收敛", "端点", "瑕点", "余项" };
        if (confusionTerms.Any(term => label.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return 2;
        }

        return 3;
    }

    public static bool IsTheoremCondition(string label)
    {
        return label.Contains("条件", StringComparison.OrdinalIgnoreCase)
            || label.Contains("定理", StringComparison.OrdinalIgnoreCase)
            || label.Contains("公式", StringComparison.OrdinalIgnoreCase)
            || label.Contains("交换", StringComparison.OrdinalIgnoreCase)
            || label.Contains("路径无关", StringComparison.OrdinalIgnoreCase)
            || label.Contains("保守场", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsEasyConfusion(string label)
    {
        return label.Contains("一致收敛", StringComparison.OrdinalIgnoreCase)
            || label.Contains("逐点收敛", StringComparison.OrdinalIgnoreCase)
            || label.Contains("端点", StringComparison.OrdinalIgnoreCase)
            || label.Contains("瑕点", StringComparison.OrdinalIgnoreCase)
            || label.Contains("余项", StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> NormalizeAndSort(IEnumerable<string>? points)
    {
        return (points ?? Enumerable.Empty<string>())
            .Select(NormalizeLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetPriority)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
