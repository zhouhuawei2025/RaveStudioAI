using System.Diagnostics;
using System.Text;
using Xceed.Document.NET;

namespace RaveStudioAI.Sds.Services;

public class FormatCheckUtils
{
    //在将CRF转成SDS文件之前，对CRF的结构进行检查
    public static string CheckCrfFormat(Table table)
    {
        StringBuilder sb = new StringBuilder();    //记录解析错误的行，返回给用户修改
        string crfType = table.Rows[0].Cells[1].Paragraphs[0].Text.Trim();

        //可添加行表单 保持三列式
        if (crfType == "ADD1")
        {
            //解析表头是否符合格式要求
            FormatCheckUtils.CheckTableHeader(table, sb);

            bool spidFlag = false;
            bool loglineFlag = false;

            //解析每行字段是否符合格式要求
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                if (dpOIDandName.IndexOf("SPID") >= 0)
                {
                    loglineFlag = true;
                    spidFlag = true;
                    continue;
                }
                if (dpOIDandName.IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loglineFlag = false;
                    continue;
                }
                if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                FormatCheckUtils.CheckRegularField(table, dpOIDandName, sb, i);
            }

            FormatCheckUtils.CheckFlags(spidFlag, loglineFlag, sb);
        }

        //可添加行表单，多列式
        else if (crfType == "ADD2")
        {
            //解析表头是否符合格式要求
            FormatCheckUtils.CheckTableHeader(table, sb);

            bool spidFlag = false;
            bool loglineFlag = false;
            int num = 0;

            //检查每行字段是否符合格式要求
            for (int i = 1; i < table.Rows.Count; i++)
            {
                int cellNumber = table.Rows[i].Cells.Count;
                if (cellNumber > 3 || TextExtractor.GetCellValue(table, i, 0).IndexOf("SPID") >= 0)
                {
                    spidFlag = true;
                    loglineFlag = true;
                }
                if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loglineFlag = false;
                    continue;
                }


                if (!loglineFlag)  // 检查普通字段
                {
                    string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                    if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;                                        

                    FormatCheckUtils.CheckRegularField(table, dpOIDandName, sb, i);
                }
                else if (num == 0)  //检查logline字段， 且确保只检查logline的第一行
                {
                    for (int j = 1; j < cellNumber; j++)
                    {
                        string dpOIDandName = TextExtractor.GetCellValue(table, i, j);
                        FormatCheckUtils.CheckLoglineField(dpOIDandName, sb);
                    }
                    num++;
                }
            }

            FormatCheckUtils.CheckFlags(spidFlag, loglineFlag, sb);
        }

        //固定行表单 保持三列式
        else if (crfType == "FIX1")
        {
            //解析表头是否符合格式要求
            FormatCheckUtils.CheckTableHeader(table, sb);

            bool spidFlag = false;
            bool loglineFlag = false;
            bool defaultValueFlag = false;

            //解析每行字段是否符合格式要求
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                if (dpOIDandName.IndexOf("SPID") >= 0)
                {
                    loglineFlag = true;
                    spidFlag = true;
                    continue;
                }
                if (dpOIDandName.IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loglineFlag = false;
                    continue;
                }
                if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
               
                if (TextExtractor.ExtractNameAndOid(dpOIDandName))
                {
                    int cell = table.Rows[i].Cells.Count - 1;
                    string dpLayout = TextExtractor.GetCellValue(table, i, cell);
                    Debug.WriteLine($"顺利解析字段：{dpOIDandName}，字段类型和格式：{dpLayout}");
                    if (dpLayout.ToLower().Contains("default") && dpLayout.ToLower().Contains("dropdown"))
                        defaultValueFlag = true;
                }
                else
                {
                    sb.AppendLine($"错误：【{dpOIDandName}】不符合【文本(OID)】的格式，请及时更正");
                }
            }

            FormatCheckUtils.CheckFlags(spidFlag, loglineFlag, sb);

            if (!defaultValueFlag)
                sb.AppendLine($"错误：用于确定固定行每行的defalutVaule不存在，请及时更正");

        }

        //固定行表单 保持多列式
        else if (crfType == "FIX2")
        {
            //解析表头是否符合格式要求
            FormatCheckUtils.CheckTableHeader(table, sb);

            bool spidFlag = false;
            bool loglineFlag = false;
            bool defaultValueFlag = false;
            int num = 0;

            //检查每行字段是否符合格式要求
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string colorName = table.Rows[i].Cells[0].ShadingPattern.Fill.Name;

                if (table.Rows[i].Cells.Count > 3 || (colorName != "#0" && colorName != "#ffffffff" && colorName != "#00ffffff" && colorName != "#00000000"))
                {
                    spidFlag = true;
                    loglineFlag = true;
                }
                if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loglineFlag = false;
                    continue;
                }


                if (!loglineFlag)  // 检查普通字段
                {
                    string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                    if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    
                    FormatCheckUtils.CheckRegularField(table, dpOIDandName, sb, i);
                }
                else if (num == 0) //检查logline字段, 且确保只检查logline的第一行
                {
                    for (int j = 0; j < table.Rows[i].Cells.Count; j++) //注意：需从第一个单元格开始解析,直到第n个单元格
                    {
                        string dpOIDandName = TextExtractor.GetCellValue(table, i, j);
                        if (dpOIDandName.IndexOf("SPID") >= 0) continue;

                        if (TextExtractor.ExtractNameOidLayout(dpOIDandName, out string name, out string oid, out string dpLayout))
                        {
                            if (dpLayout.ToLower().Contains("default"))
                                defaultValueFlag = true;
                        }
                        else
                            sb.AppendLine($"错误：【{dpOIDandName}】不符合【文本(OID)】的格式，请及时更正");

                    }
                    num++;
                }
            }

            FormatCheckUtils.CheckFlags(spidFlag, loglineFlag, sb);

            if (!defaultValueFlag)
                sb.AppendLine($"错误：用于确定固定行每行的defaultVaule不存在，请及时更正");
        }

        //LAB表单 药业格式
        else if (crfType == "LAB1")
        {
            //解析表头是否符合格式要求
            FormatCheckUtils.CheckTableHeader(table, sb);

            bool spidFlag = false;
            bool loglineFlag = false;
            int num = 0;

            //检查每行字段是否符合格式要求
            for (int i = 1; i < table.Rows.Count; i++)
            {
                if (table.Rows[i].Cells.Count > 3)
                {
                    spidFlag = true;
                    loglineFlag = true;
                    num++;
                }
                if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loglineFlag = false;
                    continue;
                }

                if (!loglineFlag)  // 检查普通字段
                {
                    string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                    if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    
                    FormatCheckUtils.CheckRegularField(table, dpOIDandName, sb, i);
                }
                else if (num > 1)  //只检查Analyte字段(begin with 2nd line)
                {
                    FormatCheckUtils.CheckAnalyteField(TextExtractor.GetCellValue(table, i, 0), sb);
                }

            }

            if (!spidFlag)
                sb.AppendLine($"错误：缺少【LBTEST - LBCOM】行，请及时更正");

            if (loglineFlag)
                sb.AppendLine($"错误：缺少【[Add Row]】行，请及时更正");
        }

        //LAB表单 再明格式
        else if (crfType == "LAB2")
        {
            //解析表头是否符合格式要求
            FormatCheckUtils.CheckTableHeader(table, sb);

            //解析每行字段是否符合格式要求
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                FormatCheckUtils.CheckRegularField(table, dpOIDandName, sb, i);
            }
        }

        //普通表单
        else
        {
            //解析表头是否符合格式要求
            FormatCheckUtils.CheckTableHeader(table, sb);

            //解析每行字段是否符合格式要求
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                FormatCheckUtils.CheckRegularField(table, dpOIDandName, sb, i);
            }
        }
        sb.AppendLine($"==========完成检查");
        sb.AppendLine();

        return sb.ToString();
    }

    public static void CheckTableHeader(Table table, StringBuilder sb)
    {
        string formOIDandName = TextExtractor.GetCellValue(table, 0, 0);
        sb.AppendLine($"==========正在检查表单：【{formOIDandName}】......");

        if (!TextExtractor.ExtractNameAndOid(formOIDandName))
            //    Debug.WriteLine($"顺利解析表单：{formOIDandName}");

            //else            
            sb.AppendLine($"表头【{formOIDandName}】 的格式有错误，请及时更正");

    }

    public static void CheckRegularField(Table table, string dpOIDandName, StringBuilder sb, int row)
    {
        if (TextExtractor.ExtractNameAndOid(dpOIDandName))
        {
            if (table.Rows[row].Cells.Count == 2 && TextExtractor.GetCellValueLayOutPart(table, row, 1).ToLower().Trim() != "label")
            {
                sb.AppendLine($"错误：【{dpOIDandName}】疑似缺少 LABEL，请及时更正");
            }
            else if (table.Rows[row].Cells.Count == 3)
            {
                string dpLayout = TextExtractor.GetCellValueLayOutPart(table, row, 2);
                if ((dpLayout.IndexOf("radio", StringComparison.OrdinalIgnoreCase) >= 0 || dpLayout.IndexOf("dropdown", StringComparison.OrdinalIgnoreCase) >= 0)
                    && dpLayout.IndexOf("1=") == -1
                    && dpLayout.IndexOf("1 =") == -1)
                    sb.AppendLine($"错误：【{dpOIDandName}】疑似缺少Code list，请及时更正");
            }
        }
        else
        {
            sb.AppendLine($"错误：【{dpOIDandName}】不符合【文本(OID)】的格式，请及时更正");
        }

    }

    public static void CheckLoglineField(string dpOIDandName, StringBuilder sb)
    {
        if (TextExtractor.ExtractNameOidLayout(dpOIDandName, out string name, out string oid, out string dpLayout))
        {
            if ((dpLayout.IndexOf("radio", StringComparison.OrdinalIgnoreCase) >= 0 || dpLayout.IndexOf("dropdown", StringComparison.OrdinalIgnoreCase) >= 0)
                && dpLayout.IndexOf("1=") == -1
                && dpLayout.IndexOf("1 =") == -1)
                sb.AppendLine($"错误：【{dpOIDandName}】疑似缺少Code list，请及时更正");
        }

        else
            sb.AppendLine($"错误：【{dpOIDandName}】不符合【文本(OID)】的格式，请及时更正");

    }

    public static void CheckAnalyteField(string dpOIDandName, StringBuilder sb)
    {
        if (!TextExtractor.ExtractNameAndOid(dpOIDandName))
            //{
            //    Debug.WriteLine($"顺利检查分析物：{dpOIDandName}");
            //}
            //else            
            sb.AppendLine($"错误：【{dpOIDandName}】不符合【文本(OID)】的格式，请及时更正");

    }

    public static void CheckFlags(bool spidFlag, bool loglineFlag, StringBuilder sb)
    {
        if (!spidFlag)
            sb.AppendLine($"错误：缺少【编号(SPID)】行，请及时更正");

        if (loglineFlag)
            sb.AppendLine($"错误：缺少【[Add Row]】行，请及时更正");
    }
}

