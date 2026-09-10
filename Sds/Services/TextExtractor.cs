using System.Text.RegularExpressions;
using Xceed.Document.NET;


namespace RaveStudioAI.Sds.Services;

public class TextExtractor
{
    /// <summary>
    /// 用于提取【访视信息(SV)】类型文本中的Name和OID
    /// </summary>
    /// <param name="inputText"></param>
    /// <param name="prefix"></param>
    /// <param name="OID"></param>
    /// <returns></returns>
    public static bool ExtractNameAndOid(string inputText, out string prefix, out string OID)
    {
        prefix = string.Empty;
        OID = string.Empty;

        if (string.IsNullOrEmpty(inputText))
            return false;

        // 核心正则：^匹配开头，(.*)捕获前缀，\(匹配左括号，([A-Z0-9]+)捕获括号内内容，\)匹配右括号，$确保括号在末尾
        string pattern = @"^(.*?)[\(（]([A-Z0-9_]+)[\)）]$";
        Regex regex = new Regex(pattern, RegexOptions.None);
        Match match = regex.Match(inputText);

        // 匹配成功则提取内容，失败则返回false
        if (match.Success && match.Groups.Count >= 3)
        {
            prefix = match.Groups[1].Value.Trim();
            OID = match.Groups[2].Value.Trim();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 判断CRF表头或字段如【访视信息(SV)】之类的文本中的格式是否符合要求
    /// </summary>
    /// <param name="inputText"></param>
    /// <returns></returns>
    public static bool ExtractNameAndOid(string inputText)
    {

        if (string.IsNullOrEmpty(inputText))
            return false;

        // 核心正则：^匹配开头，(.*)捕获前缀，\(匹配左括号，([A-Z0-9]+)捕获括号内内容，\)匹配右括号，$确保括号在末尾
        string pattern = @"^(.*?)[\(（]([A-Z0-9_]+)[\)）]$";
        Regex regex = new Regex(pattern, RegexOptions.None);
        Match match = regex.Match(inputText);

        // 匹配成功则提取内容，失败则返回false
        if (match.Success && match.Groups.Count >= 3)
            return true;

        return false;
    }


    public static bool ExtractNameOidLayout(string inputText, out string prefix, out string OID, out string layout)
    {
        prefix = string.Empty;
        OID = string.Empty;
        layout = string.Empty;

        if (string.IsNullOrEmpty(inputText))
            return false;

        //string pattern = @"^[^()（）]*[\(（]([A-Z0-9_]+)[\)）][^()（）]*$";
        //string pattern = @"^([^()（）]+)[\(（]([A-Z0-9_]+)[\)）]([^()（）]*)$";  //易迪希目前用的是这种
        string pattern = @"^([^()]+)\(([A-Z0-9_]+)\)(.*)$";
        Regex regex = new Regex(pattern, RegexOptions.None);
        Match match = regex.Match(inputText);

        // 匹配成功则提取内容，失败则返回false
        if (match.Success && match.Groups.Count >= 3)
        {
            prefix = match.Groups[1].Value.Trim();
            OID = match.Groups[2].Value.Trim();
            layout = match.Groups[3].Value.Trim();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取单元格内的值
    /// </summary>
    /// <param name="table"></param>
    /// <param name="row"></param>
    /// <param name="cell"></param>
    /// <returns></returns>
    public static string GetCellValue(Table table, int row, int cell)
    {
        return string.Join("", table.Rows[row].Cells[cell].Paragraphs.Select(e => e.Text)).Trim();
    }

    /// <summary>
    /// 获取普通字段的LayOut
    /// </summary>
    /// <param name="table"></param>
    /// <param name="row"></param>
    /// <param name="cell"></param>
    /// <returns></returns>

    public static string GetCellValueLayOutPart(Table table, int row, int cell)
    {
        return string.Join("\n", table.Rows[row].Cells[cell].Paragraphs.Select(e => e.Text)).Trim();
    }

}

