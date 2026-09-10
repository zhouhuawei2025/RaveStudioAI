using RaveStudioAI.Sds.Models;
using RaveStudioAI.Common;
using System.IO;
using System.Diagnostics;
using Xceed.Document.NET;

namespace RaveStudioAI.Sds.Services;

internal class CRFAnalyzeUtils
{
    public static Form? GetForm(Table table)
    {
        Form? form = null;
        string formOIDandName = TextExtractor.GetCellValue(table, 0, 0);
        string type = TextExtractor.GetCellValue(table, 0, 1);
        if (TextExtractor.ExtractNameAndOid(formOIDandName, out string formName, out string formOID))
            form = new Form() { Name = formName, OID = formOID, LogDirection = (type.ToLower().Contains("add") || type.ToLower().Contains("fix"))  ? "Landscape" : "" };
        return form;
    }
    public static List<string> GetDataDictionary(Table table)
    {
        string crfType = table.Rows[0].Cells[1].Paragraphs[0].Text.Trim();
        List<string> layoutList = new();

        //可添加行表单 保持三列式
        if (crfType == "ADD1")
        {
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                if (dpOIDandName.IndexOf("SPID") >= 0 || dpOIDandName.IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0 || dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                {
                    int cell = table.Rows[i].Cells.Count - 1;
                    string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                    if (dpLayout.IndexOf("1=") >= 0 || dpLayout.IndexOf("1 =") >= 0)
                    {
                        layoutList.Add($"字段OID：{dpOID}，字段类型和格式：{dpLayout}");
                        File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                    }
                    else if (dpLayout.ToLower().Contains("default"))
                    {
                        string defaultValue = TextExtractor.GetCellValueLayOutPart(table, i, cell - 1);
                        layoutList.Add($"字段OID：{dpOID}，字段类型和格式：RadioButton[1]\n Code List\n 1={defaultValue}");
                        File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                    }
                }
            }
        }

        //可添加行表单，多列式
        else if (crfType == "ADD2")
        {
            bool loglineFlag = false;
            int num = 0;

            for (int i = 1; i < table.Rows.Count; i++)
            {
                int cellNumber = table.Rows[i].Cells.Count;
                if (cellNumber > 3 || TextExtractor.GetCellValue(table, i, 0).IndexOf("SPID") >= 0)
                    loglineFlag = true;

                if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loglineFlag = false;
                    continue;
                }

                if (!loglineFlag)  // 处理普通字段
                {
                    string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                    if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                    {
                        int cell = table.Rows[i].Cells.Count - 1;
                        string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                        if (dpLayout.IndexOf("1=") >= 0 || dpLayout.IndexOf("1 =") >= 0)
                        {
                            layoutList.Add($"字段OID：{dpOID}，字段类型和格式：{dpLayout}");
                            File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                        }
                        else if (dpLayout.ToLower().Contains("default"))
                        {
                            string defaultValue = TextExtractor.GetCellValueLayOutPart(table, i, cell - 1);
                            layoutList.Add($"字段OID：{dpOID}，字段类型和格式：RadioButton[1]\n Code List\n 1={defaultValue}");
                            File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                        }
                    }
                }

                else if (num == 0)  //处理logline字段
                {
                    for (int j = 1; j < cellNumber; j++)
                    {
                        string dpOIDandName = TextExtractor.GetCellValue(table, i, j);
                        if (TextExtractor.ExtractNameOidLayout(dpOIDandName, out string dpName, out string dpOID, out string dpLayout) && (dpLayout.IndexOf("1=") >= 0 || dpLayout.IndexOf("1 =") >= 0))
                        {
                            layoutList.Add($"字段OID：{dpOID}，字段类型和格式：{dpLayout}");
                            File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                        }
                        else
                        {
                            Debug.WriteLine("没有查到codelist");
                        }
                    }
                    num++;
                }
            }
        }

        //固定行表单 保持三列式
        else if (crfType == "FIX1")
        {
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                if (dpOIDandName.IndexOf("SPID") >= 0 || dpOIDandName.IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0 || dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                {
                    int cell = table.Rows[i].Cells.Count - 1;
                    string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                    if (dpLayout.IndexOf("1=") >= 0 || dpLayout.IndexOf("1 =") >= 0)
                    {
                        layoutList.Add($"字段OID：{dpOID}，字段类型和格式：{dpLayout}");
                        File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                    }
                    else if (dpLayout.ToLower().Contains("default") && !dpLayout.ToLower().Contains("dropdown"))
                    {
                        string defaultValue = TextExtractor.GetCellValueLayOutPart(table, i, cell - 1);
                        layoutList.Add($"字段OID：{dpOID}，字段类型和格式：RadioButton[1]\n Code List\n 1={defaultValue}");
                        File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                    }
                }
            }
        }

        //固定行表单 保持多列式
        else if (crfType == "FIX2")
        {
            bool loglineFlag = false;
            int num = 0;

            for (int i = 1; i < table.Rows.Count; i++)
            {
                string colorName = table.Rows[i].Cells[0].ShadingPattern.Fill.Name;

                if (table.Rows[i].Cells.Count > 3 || (colorName != "#0" && colorName != "#ffffffff" && colorName != "#00ffffff" && colorName != "#00000000"))
                {
                    loglineFlag = true;
                }
                if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loglineFlag = false;
                    continue;
                }

                if (!loglineFlag)  // 处理普通字段
                {
                    string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                    if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                    {
                        int cell = table.Rows[i].Cells.Count - 1;
                        string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                        if (dpLayout.IndexOf("1=") >= 0 || dpLayout.IndexOf("1 =") >= 0)
                        {
                            layoutList.Add($"字段OID：{dpOID}，字段类型和格式：{dpLayout}");
                            File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                        }
                        else if (dpLayout.ToLower().Contains("default"))
                        {
                            string defaultValue = TextExtractor.GetCellValueLayOutPart(table, i, cell - 1);
                            layoutList.Add($"字段OID：{dpOID}，字段类型和格式：RadioButton[1]\n Code List\n 1={defaultValue}");
                            File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                        }
                    }
                }
                else if (num == 0) //检查logline字段, 且确保只检查logline的第一行
                {
                    for (int j = 0; j < table.Rows[i].Cells.Count; j++) //注意：需从第一个单元格开始解析,直到第n个单元格
                    {
                        string dp = TextExtractor.GetCellValue(table, i, j);
                        if (dp.IndexOf("SPID", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;
                        if (TextExtractor.ExtractNameOidLayout(dp, out string dpName, out string dpOID, out string dpLayout) && (dpLayout.IndexOf("1=") >= 0 || dpLayout.IndexOf("1 =") >= 0))
                        {
                            layoutList.Add($"字段OID：{dpOID}，字段类型和格式：{dpLayout}");
                            File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                        }
                    }
                    num++;
                }
            }


            //提取PETEST的词典
            int startRow = 0;
            int endRow = 0;
            List<int> defaultColumn = new List<int>();
            List<string> keys = new List<string>();
            List<string> values = new List<string>();

            //定位固定行的范围
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string colorName = table.Rows[i].Cells[0].ShadingPattern.Fill.Name;

                if (table.Rows[i].Cells.Count > 3 || (colorName != "#0" && colorName != "#ffffffff" && colorName != "#00ffffff" && colorName != "#00000000"))
                    startRow = (startRow == 0 ? i : startRow);

                if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    endRow = i - 1; break;
                }
            }

            //定位default列的位置
            for (int j = 0; j < table.Rows[startRow].Cells.Count; j++)
            {
                if (TextExtractor.GetCellValue(table, startRow, j).IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                    defaultColumn.Add(j);    //标记带gridDictionary的列
            }

            //整理固定行词典
            for (int j = 0; j < defaultColumn.Count; j++)
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, startRow, defaultColumn[j]);
                if (TextExtractor.ExtractNameOidLayout(dpOIDandName, out string dpName, out string dpOID, out string layout))
                    keys.Add(dpOID);
                values.Add("");
            }

            num = 1;
            for (int i = startRow + 1; i <= endRow; i++)
            {
                for (int j = 0; j < defaultColumn.Count; j++)
                {
                    string defaultValue = TextExtractor.GetCellValue(table, i, defaultColumn[j]);
                    values[j] += $"{num}={defaultValue}\n";
                }
                num++;
            }

            //输出
            for (int i = 0; i < keys.Count; i++)
            {
                layoutList.Add($"字段OID：{keys[i]}，字段类型和格式：DropdownList[2] \r\n {values[i]}");
                File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{keys[i]}，字段类型和格式：{values[i]}" + "\r\n");                
            }
        }

        //LAB表单 
        else if (crfType == "LAB1")
        {
            bool loglineFlag = false;

            for (int i = 1; i < table.Rows.Count; i++)
            {
                if (table.Rows[i].Cells.Count > 3)
                {
                    loglineFlag = true;
                }
                if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loglineFlag = false;
                    continue;
                }

                if (!loglineFlag)  // 处理普通字段
                {
                    string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                    if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                    {
                        int cell = table.Rows[i].Cells.Count - 1;
                        string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);

                        if (dpLayout.IndexOf("1=") >= 0 || dpLayout.IndexOf("1 =") >= 0)
                        {
                            layoutList.Add($"字段OID：{dpOID}，字段类型和格式：{dpLayout}");
                            File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                        }
                        else if (dpLayout.ToLower().Contains("default"))
                        {
                            string defaultValue = TextExtractor.GetCellValueLayOutPart(table, i, cell - 1);
                            layoutList.Add($"字段OID：{dpOID}，字段类型和格式：RadioButton[1]\n Code List\n 1={defaultValue}");
                            File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                        }
                    }
                }
            }
        }

        //普通表单 + 再明格式的Lab表单
        else
        {
            for (int i = 1; i < table.Rows.Count; i++)
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);

                if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                {
                    int cell = table.Rows[i].Cells.Count - 1;
                    string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);                    
                    if (dpLayout.IndexOf("1=") >= 0 || dpLayout.IndexOf("1 =") >= 0)
                    {
                        layoutList.Add($"字段OID：{dpOID}，字段类型和格式：{dpLayout}");
                        File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                    }
                    else if(dpLayout.ToLower().Contains("default"))
                    {
                        string defaultValue = TextExtractor.GetCellValueLayOutPart(table, i, cell - 1);
                        layoutList.Add($"字段OID：{dpOID}，字段类型和格式：RadioButton[1]\n Code List\n 1={defaultValue}");
                        File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "codelistLog.txt"), $"成功解析字段OID：{dpOID}，字段类型和格式：{dpLayout}" + "\r\n");
                    }
                }

            }
        }

        return layoutList;
    }

    public static List<string> GetFieldInNormalForm(Table table, string formOID)
    {
        List<string> fieldList = new List<string>();        
        int ordinal = 1;
        for (int i = 1; i < table.Rows.Count; i++)
        {
            string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
            if (dpOIDandName.IndexOf("SPID") >= 0 || dpOIDandName.IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0 || dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
            {
                int cell = table.Rows[i].Cells.Count - 1;
                string dpLayout = string.Join("\n", table.Rows[i].Cells[cell].Paragraphs.Select(e => e.Text));
                if (dpLayout.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  所属表单：{formOID} Ordinal：{ordinal}  字段详情：RadioButton[2], 默认值：1");
                }
                else
                {
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  所属表单：{formOID} Ordinal：{ordinal}  字段详情：{dpLayout}");
                }

                File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
                ordinal++;
            }
        }
        return fieldList;
    }

    public static List<string> GetFieldInADD1Form(Table table, string formOID)
    {
        List<string> fieldList = new List<string>();
        int ordinal = 0;
        bool loglineFlag = false;
        for (int i = 1; i < table.Rows.Count; i++)
        {
            string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
            if (dpOIDandName.IndexOf("SPID") >= 0)
            {
                loglineFlag = true;
                continue;
            }
            if (dpOIDandName.IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                loglineFlag = false;
                continue;
            }
            if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            
            //ordinal赋值规则
            ordinal++;

            if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
            {
                int cell = table.Rows[i].Cells.Count - 1;
                string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                string IsGrid = loglineFlag ? "IsGrid：true" : "IsGrid：false";

                if (dpLayout.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  所属表单：{formOID}  Ordinal：{ordinal}, {IsGrid}, 字段详情：RadioButton[2], 默认值：1");
                }
                else
                {
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  所属表单：{formOID}  Ordinal：{ordinal}, {IsGrid}, 字段详情：{dpLayout}");
                }

                File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
            }

        }
        return fieldList;
    }

    public static List<string> GetFieldInADD2Form(Table table, string formOID)
    {
        List<string> fieldList = new List<string>();
        int ordinal = 0;
        bool loglineFlag = false;
        int num = 0;

        for (int i = 1; i < table.Rows.Count; i++)
        {
            int cellNumber = table.Rows[i].Cells.Count;
            if (cellNumber > 3 || TextExtractor.GetCellValue(table, i, 0).IndexOf("SPID") >= 0) loglineFlag = true;

            if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0) { loglineFlag = false; continue; }
            string IsGrid = loglineFlag ? "IsGrid：true" : "IsGrid：false";


            if (!loglineFlag)  // 处理普通字段
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                {
                    ordinal++;
                    int cell = table.Rows[i].Cells.Count - 1;
                    string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                    if (dpLayout.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}  Ordinal：{ordinal}, {IsGrid}, 字段详情：RadioButton[2], 默认值：1");
                    }

                    else
                    {
                        fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID} Ordinal：{ordinal}, {IsGrid}, 字段详情：{dpLayout}");
                    }

                    File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
                }
            }

            else if (num == 0)  //处理logline字段
            {
                for (int j = 1; j < cellNumber; j++)
                {
                    string dpOIDandName = TextExtractor.GetCellValue(table, i, j);
                    if (TextExtractor.ExtractNameOidLayout(dpOIDandName, out string dpName, out string dpOID, out string dpLayout))
                    {
                        ordinal++;
                        if (dpLayout.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}  Ordinal：{ordinal}, {IsGrid}, 字段详情：RadioButton[2], 默认值：1");
                        }

                        else
                        {
                            fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID} Ordinal：{ordinal}, {IsGrid}, 字段详情：{dpLayout}");
                        }
                        
                        File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
                    }
                }
                num++;
            }

        }

        return fieldList;
    }

    public static List<string> GetFieldInFIX1Form(Table table, string formOID)
    {
        List<string> fieldList = new List<string>();
        
        int ordinal = 0;
        bool loglineFlag = false;
       
        for (int i = 1; i < table.Rows.Count; i++)
        {
            string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
            if (dpOIDandName.IndexOf("SPID") >= 0) { loglineFlag = true; continue; }
            if (dpOIDandName.IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0) { loglineFlag = false; continue; }
            if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            string IsGrid = loglineFlag ? "IsGrid：true" : "IsGrid：false";

            //ordinal赋值规则
            ordinal++;

            if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
            {
                int cell = table.Rows[i].Cells.Count - 1;
                string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                if(dpLayout.ToLower().Contains("dropdown") && dpLayout.ToLower().Contains("default"))
                {
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID} Ordinal：{ordinal},  {IsGrid}, 字段详情：{dpLayout}, 默认值：1|2|3|...n| (提取字段词典中每一项的code值，以|分隔) ");
                }
                else if(dpLayout.ToLower().Contains("default") && !dpLayout.ToLower().Contains("dropdown"))
                {
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID} Ordinal：{ordinal},  {IsGrid}, 字段详情：RadioButton[2], 默认值：1");
                }
                else
                {
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID} Ordinal：{ordinal},  {IsGrid}, 字段详情：{dpLayout}");
                }

                File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
            }
        }
        return fieldList;
    }

    public static List<string> GetFieldInFIX2Form(Table table, string formOID)
    {
        List<string> fieldList = new List<string>();
       
        int ordinal = 0;
        bool loglineFlag = false;
        int num = 0;

        int startRow = 0;
        int endRow = 0;

        //定位固定行的范围
        for (int i = 1; i < table.Rows.Count; i++)
        {
            string colorName = table.Rows[i].Cells[0].ShadingPattern.Fill.Name;

            if (table.Rows[i].Cells.Count > 3 || (colorName != "#0" && colorName != "#ffffffff" && colorName != "#00ffffff" && colorName != "#00000000"))
                startRow = (startRow == 0 ? i : startRow);

            if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                endRow = i - 1; break;
            }
        }

        string defaultValue = string.Concat(Enumerable.Range(1, endRow - startRow).Select(n => $"{n}|"));

        for (int i = 1; i < table.Rows.Count; i++)
        {
            string colorName = table.Rows[i].Cells[0].ShadingPattern.Fill.Name;
            int cellNumber = table.Rows[i].Cells.Count;

            if (cellNumber > 3 || (colorName != "#0" && colorName != "#ffffffff" && colorName != "#00ffffff" && colorName != "#00000000"))
                loglineFlag = true;

            if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                loglineFlag = false;
                continue;
            }

            string IsGrid = loglineFlag ? "IsGrid：true" : "IsGrid：false";

            if (!loglineFlag)  // 处理普通字段
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                {
                   
                    ordinal++;
                    int cell = table.Rows[i].Cells.Count - 1;
                    string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                    if (dpLayout.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}   Ordinal：{ordinal},  {IsGrid}, 字段详情：RadioButton[2], 默认值：1");
                    }

                    else
                    {
                        fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}   Ordinal：{ordinal},  {IsGrid}, 字段详情：{dpLayout}");

                    }
                   
                    File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
                }
            }
            else if (num == 0)  //处理logline字段
            {
                for (int j = 0; j < cellNumber; j++)
                {
                    string dp = TextExtractor.GetCellValue(table, i, j);
                    if (dp.IndexOf("SPID") >= 0) continue;

                    if (TextExtractor.ExtractNameOidLayout(dp, out string dpName, out string dpOID, out string dpLayout))
                    {
                        ordinal++;
                        if (dpLayout.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)  // 固定行标识字段
                            fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}  Ordinal：{ordinal},  {IsGrid}, 字段详情：DropDownList[2], 默认值：{defaultValue}");
                        else
                            fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}  Ordinal：{ordinal},  {IsGrid}  字段详情：{dpLayout}");


                        File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
                    }
                }
                num++;
            }
        }

        return fieldList;
    }

    public static List<string> GetFieldInLAB1Form(Table table, string formOID)
    {
        List<string> fieldList = new List<string>();       
        int ordinal = 1;
        bool loglineFlag = false;
        int num = 0;

        for (int i = 1; i < table.Rows.Count; i++)
        {
            int cellNumber = table.Rows[i].Cells.Count;
            if (cellNumber > 3 || TextExtractor.GetCellValue(table, i, 0).IndexOf("SPID") >= 0) loglineFlag = true;

            if (TextExtractor.GetCellValue(table, i, 0).IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0) { loglineFlag = false; continue; }


            if (!loglineFlag)  // 处理普通字段
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
                if (dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                {                  
                    ordinal++;
                    int cell = table.Rows[i].Cells.Count - 1;
                    string dpLayout = TextExtractor.GetCellValueLayOutPart(table, i, cell);
                    if (dpLayout.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}  Ordinal：{ordinal} IsLab：false, 字段详情：RadioButton[2], 默认值：1");
                    }

                    else
                    {
                        fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}  Ordinal：{ordinal} IsLab：false, 字段详情：{dpLayout}");
                    }

                    File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
                }
            }

            else if (num == 0)
            {
                num++;
            }
            else //处理lab字段
            {
                string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);

                if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
                {                   
                    ordinal++;
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  所属表单：{formOID}  Ordinal：{ordinal}  IsLab = true, 字段详情：Text [8.3]");
                    File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
                }
            }

        }

        return fieldList;
    }

    public static List<string> GetFieldInLAB2Form(Table table, string formOID)
    {
        List<string> fieldList = new List<string>();        
        int ordinal = 1;
        for (int i = 1; i < table.Rows.Count; i++)
        {
            string dpOIDandName = TextExtractor.GetCellValue(table, i, 0);
            if (dpOIDandName.IndexOf("SPID") >= 0 || dpOIDandName.IndexOf("add row", StringComparison.OrdinalIgnoreCase) >= 0 || dpOIDandName.IndexOf("add page", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (TextExtractor.ExtractNameAndOid(dpOIDandName, out string dpName, out string dpOID))
            {
                int cell = table.Rows[i].Cells.Count - 1;
                string dpLayout = string.Join("\n", table.Rows[i].Cells[cell].Paragraphs.Select(e => e.Text));

                if (dpLayout.IndexOf("LAB", StringComparison.OrdinalIgnoreCase) >= 0)
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  所属表单：{formOID} Ordinal：{ordinal}  IsLab = true, 字段详情：{dpLayout}");

                else if (dpLayout.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string value = TextExtractor.GetCellValue(table, i, 1);
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID}  Ordinal：{ordinal} IsLab：false, 字段详情：RadioButton[2], 默认值：1");
                }

                else
                {
                    fieldList.Add($"字段OID：{dpOID}  字段Name：{dpName}  FormOID：{formOID} Ordinal：{ordinal} IsLab：false, 字段详情：{dpLayout}");
                }


                File.AppendAllText(LogManager.GetPath(LogCategory.Sds, "fieldlistLog.txt"), fieldList[fieldList.Count - 1] + "\r\n");
               
                ordinal++;
            }
        }
        return fieldList;
    }


    public static async Task<string> UsingAiTransferListToJson(string instructions, List<string> list, string path)
    {
        try
        {
            string fieldResult = await AIHelper.AskAsync(instructions, string.Join("\n", list));
            File.AppendAllText(path, $"====================AI提取的json如下：" + "\r\n");
            File.AppendAllText(path, fieldResult + "\r\n");
            return fieldResult;
        }
        catch (Exception ex)
        {
            // 捕获具体异常并写入日志，返回包含异常详情的字符串
            string errorMsg = $"AI解析时发生异常：{ex.Message}（类型：{ex.GetType().Name}）";
            File.AppendAllText(path, $"===================={errorMsg} {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");
            File.AppendAllText(path, $"异常详情：{ex.StackTrace}\r\n");
            return errorMsg;
        }


    }
}

