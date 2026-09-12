using Microsoft.Win32;
using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;
using RaveStudioAI.EditCheck.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RaveStudioAI.Pages;
public partial class SetDataPointVisiblePage : UserControl
{
    public ObservableCollection<BlindRow> Rows { get; }=[];
    private bool _showingExamples=true;
    public SetDataPointVisiblePage(){InitializeComponent();DataContext=this;Rows.Add(new BlindRow{BlindOid="SV_BL_001",FieldOid="VISDAT/SVREASND",FolderOid="UNS",FormOid="SV",LogicText="SVOCCUR = 1, set VISDAT/SVREASND visible."});Rows.Add(new BlindRow{BlindOid="VS2_BL_001",FieldOid="SYSBPAE/SYSBPMH/SYSBPOTH",FolderOid="All visit",FormOid="VS2",LogicText="VSPERF = 1 and SBPCLSIG >=3, set SYSBPAE/SYSBPMH/SYSBPOTH visible."});ProgressText.Text="格式示例：上传 Excel 后会自动替换";RunButton.IsEnabled=false;Loaded+=(_,_)=>RefreshStatus();}
    private void Upload_Click(object sender,RoutedEventArgs e)
    {
        if(!EnsureSds())return;
        var d=new OpenFileDialog{Title="选择 Blind Excel",Filter="Excel|*.xlsx;*.xlsm"};if(d.ShowDialog()!=true)return;
        LogManager.BeginRun(LogCategory.EditCheck,"editcheck.log","读取并校验 Blind Excel");
        try
        {
            var values=EditCheckExcelReader.ReadBlinds(d.FileName);
            BlindValidator.ValidateAndNormalize(values);
            Rows.Clear();foreach(var row in values)Rows.Add(row);
            _showingExamples=false;
            var errors=Rows.Count(x=>x.HasError);
            RunButton.IsEnabled=Rows.Count>0&&errors==0;
            ProgressText.Text=errors==0?$"校验及依赖补全通过：{Rows.Count} 条 Blind，可以生成 ECS":$"校验未通过：{errors}/{Rows.Count} 条有误，请线下修改后重新上传";
            if(Rows.Count==0)Notice.Warning("Blind Excel 中没有数据行。");
            else if(errors==0)Notice.Success("SetDataPointVisible 前置校验和依赖补全全部通过。");
            else Notice.Warning($"发现 {errors} 条错误，已停止生成。");
        }
        catch(Exception ex)
        {
            RunButton.IsEnabled=false;
            LogManager.WriteException(LogCategory.EditCheck,"editcheck.log",ex,"Blind Excel 读取或校验失败");
            Notice.Error($"Blind Excel 读取或校验失败：{ex.Message}");
        }
    }
    private void Run_Click(object sender,RoutedEventArgs e)
    {
        if(_showingExamples){Notice.Warning("当前显示的是格式示例，请先上传 Blind Excel。");return;}
        if(Rows.Any(x=>x.HasError)||Rows.Any(x=>string.IsNullOrWhiteSpace(x.NormalizedLogicText))){Notice.Warning("前置校验或依赖补全尚未全部通过，请修改源文件后重新上传。");return;}
        if(!EnsureSds()||Rows.Count==0)return;
        LogManager.BeginRun(LogCategory.EditCheck,"blind.log","SetDataPointVisible 批量生成");
        RunButton.IsEnabled=false;var b=new StringBuilder();var n=0;
        foreach(var row in Rows)
        {
            ProgressText.Text=$"正在处理 {++n}/{Rows.Count}：{row.BlindOid}";
            try{b.AppendLine(EditCheckConverter.ConvertBlind(row));}
            catch(Exception ex){row.HasError=true;row.ValidationMessage=$"生成失败：{ex.Message}";b.AppendLine($"|{row.BlindOid}|生成失败：{ex.Message}");LogManager.WriteException(LogCategory.EditCheck,"blind.log",ex,row.BlindOid);}
            b.AppendLine("------------------------------------------------------------------------");
        }
        ResultTextBox.Text=b.ToString();ProgressText.Text=$"处理完成：{Rows.Count-Rows.Count(x=>x.HasError)} 成功，{Rows.Count(x=>x.HasError)} 失败";
        RunButton.IsEnabled=!Rows.Any(x=>x.HasError);
    }
    private void ClearRows_Click(object s,RoutedEventArgs e){Rows.Clear();_showingExamples=false;RunButton.IsEnabled=false;ProgressText.Text="";}private void ClearResult_Click(object s,RoutedEventArgs e)=>ResultTextBox.Clear();private void Copy_Click(object s,RoutedEventArgs e){if(!string.IsNullOrWhiteSpace(ResultTextBox.Text))Clipboard.SetText(ResultTextBox.Text);}
    private void RefreshStatus()=>SdsStatusText.Text=CurrentProject.Instance.HasSds?$"当前 SDS：{CurrentProject.Instance.FormCount} Forms · {CurrentProject.Instance.FieldCount} Fields":"请先从项目入口上传人工确认的 SDS。";private bool EnsureSds(){RefreshStatus();if(CurrentProject.Instance.HasSds)return true;Notice.Warning("请先从项目入口上传人工确认的 SDS。");return false;}
}
