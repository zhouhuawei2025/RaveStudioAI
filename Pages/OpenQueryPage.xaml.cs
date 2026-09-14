using Microsoft.Win32;
using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;
using RaveStudioAI.EditCheck.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RaveStudioAI.Pages;
public partial class OpenQueryPage : UserControl
{
    public ObservableCollection<QueryRow> Rows { get; } = [];
    private readonly string _instruction;
    private readonly string _outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output", "EditCheck");
    private bool _showingExamples = true;
    private bool _isRunning;
    public OpenQueryPage() { InitializeComponent(); DataContext = this; _instruction = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Prompts", "EditCheck", "Rave提示词.txt")); Rows.Add(new QueryRow { QueryOid="SV_SQ_001", FieldOid="VISDAT", FolderOid="UNS", FormOid="SV", LogicText="VISDAT<>ICF.ICDAT", MessageText="Date of the Visit' is not equal to 'Date of Initial informed consent signed', please correct or confirm." }); AttachRows(); ProgressText.Text="格式示例：上传 Excel 后会自动替换"; RunButton.IsEnabled=false; Loaded += (_, _) => RefreshStatus(); }
    private void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureSds()) return;
        var d = new OpenFileDialog { Title="选择 Query Excel", Filter="Excel|*.xlsx;*.xlsm" }; if (d.ShowDialog()!=true) return;
        LogManager.BeginRun(LogCategory.EditCheck,"editcheck.log","读取并校验 Query Excel");
        try
        {
            var values=EditCheckExcelReader.ReadQueries(d.FileName); DetachRows(); Rows.Clear(); foreach(var row in values) { OpenQueryValidator.Validate(row); Rows.Add(row); }
            foreach(var group in Rows.Where(x=>!string.IsNullOrWhiteSpace(x.QueryOid)).GroupBy(x=>x.QueryOid,StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()>1))
                foreach(var row in group) OpenQueryValidator.AddError(row,$"QueryOID {group.Key} 重复");
            AttachRows(); _showingExamples=false;
            var errors=Rows.Count(x=>x.HasError); UpdateActionState();
            ProgressText.Text=$"共 {Rows.Count} 条｜错误 {errors} 条｜已生成建议规范结果 {Rows.Count(x=>!string.IsNullOrWhiteSpace(x.NormalizedLogicText))} 条";
            if(errors==0) Notice.Success("OpenQuery 基础校验完成。"); else Notice.Warning($"发现 {errors} 条错误；仍可由用户决定是否调用 AI。");
        }
        catch(Exception ex) { RunButton.IsEnabled=false; Error("Query 文件读取或校验失败",ex); }
    }
    private async void Run_Click(object sender, RoutedEventArgs e) { if(_showingExamples){Notice.Warning("当前显示的是格式示例，请先上传 Query 文件。");return;} if(!EnsureSds()||Rows.Count==0)return; var forced=Rows.Count(x=>x.HasError); LogManager.BeginRun(LogCategory.EditCheck,"query.log",forced>0?$"OpenQuery 强制调用 AI（{forced} 条校验错误）":"OpenQuery 批量生成"); _isRunning=true; RunButton.IsEnabled=false; var b=new StringBuilder(); var n=0; var failed=0; try { foreach(var row in Rows) { ProgressText.Text=$"正在处理 {++n}/{Rows.Count}：{row.QueryOid}"; try { b.AppendLine(await EditCheckConverter.ConvertQueryAsync(row,_instruction)); } catch(Exception ex) { failed++; row.HasError=true; OpenQueryValidator.AddError(row,$"生成失败：{ex.Message}"); b.AppendLine($"|{row.QueryOid}|生成失败：{ex.Message}"); LogManager.WriteException(LogCategory.EditCheck,"query.log",ex,row.QueryOid); } b.AppendLine("------------------------------------------------------------------------"); ResultTextBox.Text=b.ToString(); } ProgressText.Text=$"处理完成：{Rows.Count-failed} 成功，{failed} 失败"; } finally { _isRunning=false; UpdateActionState(); } }
    private void ExportValidation_Click(object sender, RoutedEventArgs e)
    {
        if (_showingExamples || Rows.Count == 0) { Notice.Warning("请先上传并校验 Query 文件。"); return; }
        try
        {
            var path=Path.Combine(_outputDirectory,"OpenQueryValidation.xlsx");
            OpenQueryExcelExporter.Export(Rows,path);
            Notice.Success($"校验结果已导出：{path}");
            OpenOutputDirectory();
        }
        catch(Exception ex) { Error("导出 OpenQuery 校验结果失败",ex); }
    }
    private void OpenOutputDirectory(){Directory.CreateDirectory(_outputDirectory);Process.Start(new ProcessStartInfo { FileName=_outputDirectory, UseShellExecute=true });}
    private void AttachRows(){foreach(var row in Rows)row.PropertyChanged+=Row_PropertyChanged;} private void DetachRows(){foreach(var row in Rows)row.PropertyChanged-=Row_PropertyChanged;} private void Row_PropertyChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(EditCheckRow.HasError))UpdateActionState();}
    private void UpdateActionState(){var errors=Rows.Count(x=>x.HasError);RunButton.Content=errors>0?$"强制调用 AI（{errors} 条错误）":"调用 AI 生成";if(errors>0){RunButton.Background=new SolidColorBrush(Color.FromRgb(220,38,38));RunButton.BorderBrush=new SolidColorBrush(Color.FromRgb(185,28,28));RunButton.Foreground=Brushes.White;}else{RunButton.ClearValue(Button.BackgroundProperty);RunButton.ClearValue(Button.BorderBrushProperty);RunButton.ClearValue(Button.ForegroundProperty);}RunButton.IsEnabled=!_isRunning&&!_showingExamples&&Rows.Count>0;}
    private void ClearRows_Click(object s,RoutedEventArgs e){DetachRows();Rows.Clear();_showingExamples=false;UpdateActionState();ProgressText.Text="";} private void ClearResult_Click(object s,RoutedEventArgs e)=>ResultTextBox.Clear(); private void Copy_Click(object s,RoutedEventArgs e){if(!string.IsNullOrWhiteSpace(ResultTextBox.Text))Clipboard.SetText(ResultTextBox.Text);}
    private void RefreshStatus()=>SdsStatusText.Text=CurrentProject.Instance.HasSds?$"当前 SDS：{CurrentProject.Instance.FormCount} Forms · {CurrentProject.Instance.FieldCount} Fields":"请先从项目入口上传人工确认的 SDS。";
    private bool EnsureSds(){RefreshStatus();if(CurrentProject.Instance.HasSds)return true;Notice.Warning("请先从项目入口上传人工确认的 SDS。");return false;} private static void Error(string title,Exception ex){LogManager.WriteException(LogCategory.EditCheck,"editcheck.log",ex,title);Notice.Error($"{title}：{ex.Message}");}
}
