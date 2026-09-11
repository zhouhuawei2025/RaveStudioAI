using Microsoft.Win32;
using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;
using RaveStudioAI.EditCheck.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RaveStudioAI.Pages;
public partial class OpenQueryPage : UserControl
{
    public ObservableCollection<QueryRow> Rows { get; } = [];
    private readonly string _instruction;
    private readonly string _outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output", "EditCheck");
    private bool _showingExamples = true;
    public OpenQueryPage() { InitializeComponent(); DataContext = this; _instruction = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Prompts", "EditCheck", "Rave提示词.txt")); Rows.Add(new QueryRow { QueryOid="SV_SQ_001", FieldOid="VISDAT", FolderOid="UNS", FormOid="SV", LogicText="VISDAT<>ICF.ICDAT", MessageText="Date of the Visit' is not equal to 'Date of Initial informed consent signed', please correct or confirm." }); ProgressText.Text="格式示例：上传 Excel 后会自动替换"; RunButton.IsEnabled=false; Loaded += (_, _) => RefreshStatus(); }
    private void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureSds()) return;
        var d = new OpenFileDialog { Title="选择 Query Excel", Filter="Excel|*.xlsx;*.xlsm" }; if (d.ShowDialog()!=true) return;
        LogManager.BeginRun(LogCategory.EditCheck,"editcheck.log","读取并校验 Query Excel");
        try
        {
            var values=EditCheckExcelReader.ReadQueries(d.FileName); Rows.Clear(); foreach(var row in values) { OpenQueryValidator.Validate(row); Rows.Add(row); }
            foreach(var group in Rows.Where(x=>!string.IsNullOrWhiteSpace(x.QueryOid)).GroupBy(x=>x.QueryOid,StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()>1))
                foreach(var row in group) OpenQueryValidator.AddError(row,$"QueryOID {group.Key} 重复");
            _showingExamples=false;
            var errors=Rows.Count(x=>x.HasError); RunButton.IsEnabled=Rows.Count>0 && errors==0;
            ProgressText.Text=errors==0?$"校验通过：{Rows.Count} 条 Query，可以生成 ECS":$"校验未通过：{errors}/{Rows.Count} 条有误，请线下修改后重新上传";
            if(errors==0) Notice.Success("OpenQuery 前置校验全部通过。"); else Notice.Warning($"发现 {errors} 条错误，已停止 AI 生成。");
        }
        catch(Exception ex) { RunButton.IsEnabled=false; Error("Query 文件读取或校验失败",ex); }
    }
    private async void Run_Click(object sender, RoutedEventArgs e) { if(_showingExamples){Notice.Warning("当前显示的是格式示例，请先上传 Query 文件。");return;} if(Rows.Any(x=>x.HasError)||Rows.Any(x=>string.IsNullOrWhiteSpace(x.NormalizedLogicText))){Notice.Warning("前置校验尚未全部通过，请修改源文件后重新上传。");return;} if(!EnsureSds()||Rows.Count==0)return; LogManager.BeginRun(LogCategory.EditCheck,"query.log","OpenQuery 批量生成"); RunButton.IsEnabled=false; var b=new StringBuilder(); var n=0; try { foreach(var row in Rows) { ProgressText.Text=$"正在处理 {++n}/{Rows.Count}：{row.QueryOid}"; try { b.AppendLine(await EditCheckConverter.ConvertQueryAsync(row,_instruction)); } catch(Exception ex) { row.HasError=true; b.AppendLine($"|{row.QueryOid}|生成失败：{ex.Message}"); LogManager.WriteException(LogCategory.EditCheck,"query.log",ex,row.QueryOid); } b.AppendLine("------------------------------------------------------------------------"); ResultTextBox.Text=b.ToString(); } ProgressText.Text=$"处理完成：{Rows.Count-xerr()} 成功，{xerr()} 失败"; } finally { RunButton.IsEnabled=!Rows.Any(x=>x.HasError); } int xerr()=>Rows.Count(x=>x.HasError); }
    private void ExportValidation_Click(object sender, RoutedEventArgs e)
    {
        if (_showingExamples || Rows.Count == 0) { Notice.Warning("请先上传并校验 Query 文件。"); return; }
        try
        {
            var path=Path.Combine(_outputDirectory,"OpenQueryValidation.xlsx");
            OpenQueryExcelExporter.Export(Rows,path);
            Notice.Success($"校验结果已导出：{path}");
        }
        catch(Exception ex) { Error("导出 OpenQuery 校验结果失败",ex); }
    }
    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_outputDirectory);
        Process.Start(new ProcessStartInfo { FileName=_outputDirectory, UseShellExecute=true });
    }
    private void ClearRows_Click(object s,RoutedEventArgs e){Rows.Clear();_showingExamples=false;RunButton.IsEnabled=false;ProgressText.Text="";} private void ClearResult_Click(object s,RoutedEventArgs e)=>ResultTextBox.Clear(); private void Copy_Click(object s,RoutedEventArgs e){if(!string.IsNullOrWhiteSpace(ResultTextBox.Text))Clipboard.SetText(ResultTextBox.Text);}
    private void RefreshStatus()=>SdsStatusText.Text=CurrentProject.Instance.HasSds?$"当前 SDS：{CurrentProject.Instance.FormCount} Forms · {CurrentProject.Instance.FieldCount} Fields":"请先从项目入口上传人工确认的 SDS。";
    private bool EnsureSds(){RefreshStatus();if(CurrentProject.Instance.HasSds)return true;Notice.Warning("请先从项目入口上传人工确认的 SDS。");return false;} private static void Error(string title,Exception ex){LogManager.WriteException(LogCategory.EditCheck,"editcheck.log",ex,title);Notice.Error($"{title}：{ex.Message}");}
}
