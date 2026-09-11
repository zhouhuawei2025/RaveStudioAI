using Microsoft.Win32;
using HandyControl.Data;
using RaveStudioAI.Common;
using RaveStudioAI.Rws.Models;
using RaveStudioAI.Rws.Services;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace RaveStudioAI.Pages;

public partial class RwsPage : UserControl
{
    private const int PreviewPageSize = 50;
    private readonly RaveRwsService _service = new();
    private readonly QueryExcelService _queryExcel = new();
    private readonly RwsExcelExporter _exporter = new();
    private readonly ObservableCollection<Study> _studies = [];
    private readonly ObservableCollection<Subject> _subjects = [];
    private readonly ObservableCollection<Form> _forms = [];
    private readonly ObservableCollection<QueryRow> _queries = [];
    private readonly ObservableCollection<History> _history = [];
    private readonly ObservableCollection<FormPreviewResult> _previewForms = [];
    private RwsProfileFile _configuration = new();
    private RwsTenantProfile? _tenantProfile;
    private List<RaveDatasetRow> _rows = [];
    private List<RaveDatasetRow> _filteredRows = [];
    private bool _isFilterActive;
    private int _currentPreviewPage = 1;
    private bool _loadingProfile;

    public RwsPage()
    {
        InitializeComponent();
        StudyComboBox.ItemsSource = _studies;
        SubjectListBox.ItemsSource = _subjects;
        FormListBox.ItemsSource = _forms;
        QueryDataGrid.ItemsSource = _queries;
        HistoryListBox.ItemsSource = _history;
        PreviewFormComboBox.ItemsSource = _previewForms;
        ConfigureListFilters();
        LoadProfiles();
    }

    private void LoadProfiles()
    {
        _configuration = RwsProfileStore.Load();
        var tenantNames = _configuration.Tenants.Keys.OrderBy(x => x).ToList();
        ProfileComboBox.ItemsSource = tenantNames;
        ProfileComboBox.SelectedItem = tenantNames.FirstOrDefault(x =>
            x.Equals(_configuration.SelectedTenant, StringComparison.OrdinalIgnoreCase))
            ?? tenantNames.FirstOrDefault();
    }

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is not string tenantName ||
            !_configuration.Tenants.TryGetValue(tenantName, out var profile)) return;
        _loadingProfile = true;
        _tenantProfile = profile;
        ProfileNameTextBox.Text = tenantName;
        TenantTextBox.Text = tenantName;
        UsernameTextBox.Text = profile.Username;
        PasswordBox.Password = profile.Password;
        var first = FirstStudyConfiguration(profile);
        ConfiguredStudyTextBox.Text = first?.Study ?? string.Empty;
        EnvironmentTextBox.Text = DisplayEnvironment(first?.Environment);
        ConfiguredFormsTextBox.Text = string.Join(Environment.NewLine, first?.Forms ?? []);
        LoadConfiguredForms(first?.Forms);
        _loadingProfile = false;
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var tenantName = TenantTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            Notice.Warning("请填写租户名称。");
            return;
        }

        if (!_configuration.Tenants.TryGetValue(tenantName, out var profile))
        {
            profile = new RwsTenantProfile();
            _configuration.Tenants[tenantName] = profile;
        }

        profile.Username = UsernameTextBox.Text.Trim();
        profile.Password = PasswordBox.Password;
        var studyOid = ConfiguredStudyTextBox.Text.Trim();
        var environment = NormalizeEnvironment(EnvironmentTextBox.Text);
        if (!string.IsNullOrWhiteSpace(studyOid))
        {
            var studyKey = profile.Studies.Keys.FirstOrDefault(x => x.Equals(studyOid, StringComparison.OrdinalIgnoreCase)) ?? studyOid;
            if (!profile.Studies.TryGetValue(studyKey, out var environments))
                profile.Studies[studyKey] = environments = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            environments[environment] = ParseForms(ConfiguredFormsTextBox.Text);
        }
        _configuration.SelectedTenant = tenantName;
        LogManager.BeginRun(LogCategory.Rws, "rws.log", "保存 RWS 配置");
        RwsProfileStore.Save(_configuration);
        LogManager.Write(LogCategory.Rws, "rws.log", $"已保存 RWS 配置：{tenantName}");
        Notice.Success($"RWS 配置已保存：{tenantName}");
        LoadProfiles();
        ProfileComboBox.SelectedItem = tenantName;
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateCredentials()) return;
        LogManager.BeginRun(LogCategory.Rws, "rws.log", "连接 RWS");
        SetBusy(true, "正在连接 RWS……");
        try
        {
            var studies = await _service.GetStudiesAsync(TenantTextBox.Text.Trim(), UsernameTextBox.Text.Trim(), PasswordBox.Password);
            Replace(_studies, studies);
            var expected = _tenantProfile is null ? null : FirstStudyConfiguration(_tenantProfile);
            var expectedStudy = expected?.Study ?? string.Empty;
            var expectedEnvironment = expected?.Environment ?? string.Empty;
            StudyComboBox.SelectedItem = _studies.FirstOrDefault(x =>
                (x.Oid.Equals(expectedStudy, StringComparison.OrdinalIgnoreCase) ||
                 x.ProtocolName.Equals(expectedStudy, StringComparison.OrdinalIgnoreCase)) &&
                x.Environment.Equals(expectedEnvironment, StringComparison.OrdinalIgnoreCase))
                ?? _studies.FirstOrDefault();
            DownloadStatusText.Text = $"连接成功，共 {_studies.Count} 个可用试验";
            LogManager.Write(LogCategory.Rws, "rws.log", $"RWS 连接成功：{TenantTextBox.Text.Trim()}，{_studies.Count} 个试验");
        }
        catch (Exception ex) { ShowError("RWS 登录失败", ex); }
        finally { SetBusy(false); }
    }

    private async void StudyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingProfile || StudyComboBox.SelectedItem is not Study study || !ValidateCredentials(false)) return;
        EnvironmentTextBox.Text = DisplayEnvironment(study.Environment);
        ConfiguredStudyTextBox.Text = study.ProtocolName;
        var configuredForms = FindStudyConfiguration(study);
        ConfiguredFormsTextBox.Text = string.Join(Environment.NewLine, configuredForms ?? []);
        SetBusy(true, "正在加载受试者……");
        try
        {
            var subjects = await _service.GetSubjectsAsync(TenantTextBox.Text.Trim(), UsernameTextBox.Text.Trim(),
                PasswordBox.Password, study.ProtocolName, study.Environment);
            Replace(_subjects, subjects);
            SubjectSearchTextBox.Clear();
            FormSearchTextBox.Clear();
            LoadConfiguredForms(configuredForms);
            DownloadStatusText.Text = $"已加载 {_subjects.Count} 个受试者、{_forms.Count} 个表单";
            if (_forms.Count == 0)
                DownloadStatusText.Text += "；该试验尚未在配置文件中维护 Forms";
        }
        catch (Exception ex) { ShowError("加载试验资源失败", ex); }
        finally { SetBusy(false); }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (StudyComboBox.SelectedItem is not Study study)
        {
            Notice.Warning("请先连接并选择试验。");
            return;
        }
        var subjects = _subjects.Where(x => x.IsSelected).Select(x => x.SubjectKey).ToList();
        var forms = _forms.Where(x => x.IsSelected).Select(x => x.FormName).ToList();
        if (subjects.Count == 0 || forms.Count == 0)
        {
            Notice.Warning("请至少选择一个受试者和一个表单。");
            return;
        }
        LogManager.BeginRun(LogCategory.Rws, "download.log", "RWS 数据查询");
        await RunDatasetQueryAsync(study, subjects, forms, DatasetType(), true);
    }

    private async Task RunDatasetQueryAsync(Study study, IReadOnlyList<string> subjects, IReadOnlyList<string> forms,
        string datasetType, bool addHistory)
    {
        _rows = [];
        ClearPreview();
        DatasetFilterTextBox.Clear();
        var total = subjects.Count * forms.Count;
        var completed = 0;
        SetBusy(true);
        try
        {
            foreach (var form in forms)
            foreach (var subject in subjects)
            {
                DownloadStatusText.Text = $"正在查询 {++completed}/{total}：{subject} · {form}";
                var batch = await _service.DownloadSubjectFormRowsAsync(TenantTextBox.Text.Trim(), UsernameTextBox.Text.Trim(),
                    PasswordBox.Password, study.ProtocolName, study.Environment, subject, form, datasetType);
                _rows.AddRange(batch);
            }
            BuildPreviewForms(_rows);
            DownloadStatusText.Text = $"查询完成：{_rows.Count} 行";
            if (addHistory)
            {
                _history.Insert(0, new History
                {
                    HistoryId = Guid.NewGuid().ToString(),
                    HistoryName = $"{DateTime.Now:HH:mm:ss}_{study.ProtocolName}_{(string.IsNullOrWhiteSpace(study.Environment) ? "Prod" : study.Environment)}_{datasetType}",
                    TenantName = TenantTextBox.Text.Trim(), StudyName = study.DisplayName, Project = study.ProtocolName,
                    Environment = study.Environment, DataType = datasetType,
                    SubjectKeys = subjects.ToList(), FormNames = forms.ToList()
                });
            }
            LogManager.Write(LogCategory.Rws, "download.log", $"{study.DisplayName} 查询完成：{_rows.Count} 行");
            Notice.Success($"查询完成，共 {_rows.Count} 行。");
        }
        catch (Exception ex)
        {
            BuildPreviewForms(_rows);
            ShowError($"查询中断，已保留 {_rows.Count} 行", ex);
        }
        finally { SetBusy(false); }
    }

    private async void HistoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryListBox.SelectedItem is not History history) return;
        if (!history.TenantName.Equals(TenantTextBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Notice.Warning($"该记录属于租户 {history.TenantName}，请先切换租户配置。");
            return;
        }
        var study = _studies.FirstOrDefault(x =>
            x.ProtocolName.Equals(history.Project, StringComparison.OrdinalIgnoreCase) &&
            x.Environment.Equals(history.Environment, StringComparison.OrdinalIgnoreCase));
        if (study is null)
        {
            Notice.Warning("当前已连接的试验列表中找不到该历史试验。");
            return;
        }
        LogManager.BeginRun(LogCategory.Rws, "download.log", "历史记录重新查询");
        await RunDatasetQueryAsync(study, history.SubjectKeys, history.FormNames, history.DataType, false);
    }

    private void SubjectSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        CollectionViewSource.GetDefaultView(_subjects).Refresh();

    private void FormSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        CollectionViewSource.GetDefaultView(_forms).Refresh();

    private void ConfigureListFilters()
    {
        CollectionViewSource.GetDefaultView(_subjects).Filter = item => item is Subject subject &&
            (string.IsNullOrWhiteSpace(SubjectSearchTextBox.Text) ||
             subject.SubjectKey.Contains(SubjectSearchTextBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        CollectionViewSource.GetDefaultView(_forms).Filter = item => item is Form form &&
            (string.IsNullOrWhiteSpace(FormSearchTextBox.Text) ||
             form.FormName.Contains(FormSearchTextBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyDatasetFilter_Click(object sender, RoutedEventArgs e)
    {
        var expression = DatasetFilterTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(expression))
        {
            ClearFilterState();
            BuildPreviewForms(_rows);
            return;
        }
        if (_rows.Count == 0) { Notice.Warning("当前没有可筛选的数据。"); return; }
        try
        {
            var fields = new[] { "StudyOID", "Subject", "SubjectKey", "SiteOID", "FolderOID", "StudyEventOID",
                "FolderRepeatKey", "StudyEventRepeatKey", "FormOID", "FormRepeatKey", "ItemGroupOID", "RecordPosition", "ItemGroupRepeatKey" }
                .Concat(_rows.SelectMany(x => x.Values.Keys)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var predicate = DatasetWhereFilter.Parse(expression, fields);
            _filteredRows = _rows.Where(predicate).ToList();
            _isFilterActive = true;
            BuildPreviewForms(_filteredRows);
            Notice.Success($"筛选完成：{_filteredRows.Count} 行。");
        }
        catch (Exception ex) { Notice.Error(UserMessage(ex)); }
    }

    private void ClearDatasetFilter_Click(object sender, RoutedEventArgs e)
    {
        DatasetFilterTextBox.Clear();
        ClearFilterState();
        BuildPreviewForms(_rows);
    }

    private void PreviewFormComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentPreviewPage = 1;
        RefreshPreview();
    }

    private void PreviewPagination_PageUpdated(object sender, FunctionEventArgs<int> e)
    {
        _currentPreviewPage = e.Info;
        RefreshPreview();
    }

    private void BuildPreviewForms(IReadOnlyList<RaveDatasetRow> rows)
    {
        var previousForm = (PreviewFormComboBox.SelectedItem as FormPreviewResult)?.FormOID;
        _previewForms.Clear();
        foreach (var group in rows.GroupBy(x => x.FormOID ?? "Unknown").OrderBy(x => x.Key))
        {
            _previewForms.Add(new FormPreviewResult { FormOID = group.Key, RowCount = group.Count() });
        }

        PreviewFormComboBox.SelectedItem = _previewForms.FirstOrDefault(x =>
            x.FormOID.Equals(previousForm, StringComparison.OrdinalIgnoreCase)) ?? _previewForms.FirstOrDefault();
        _currentPreviewPage = 1;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (PreviewFormComboBox.SelectedItem is not FormPreviewResult selectedForm)
        {
            DataGrid.ItemsSource = new DataTable().DefaultView;
            PreviewPageInfoText.Text = "第 0 / 0 页";
            UpdatePreviewPagination(0);
            return;
        }

        var formRows = (_isFilterActive ? _filteredRows : _rows)
            .Where(x => string.Equals(x.FormOID ?? "Unknown", selectedForm.FormOID, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(formRows.Count / (double)PreviewPageSize));
        _currentPreviewPage = Math.Clamp(_currentPreviewPage, 1, totalPages);
        var pageRows = formRows.Skip((_currentPreviewPage - 1) * PreviewPageSize).Take(PreviewPageSize).ToList();

        DataGrid.ItemsSource = BuildTable(pageRows).DefaultView;
        PreviewPageInfoText.Text = $"第 {_currentPreviewPage} / {totalPages} 页，共 {formRows.Count} 行";
        UpdatePreviewPagination(totalPages);
    }

    private void ClearPreview()
    {
        ClearFilterState();
        _previewForms.Clear();
        DataGrid.ItemsSource = new DataTable().DefaultView;
        PreviewPageInfoText.Text = "第 0 / 0 页";
        _currentPreviewPage = 1;
        UpdatePreviewPagination(0);
    }

    private void ClearFilterState()
    {
        _filteredRows = [];
        _isFilterActive = false;
    }

    private void UpdatePreviewPagination(int totalPages)
    {
        PreviewFormComboBox.IsEnabled = _previewForms.Count > 0;
        PreviewPagination.IsEnabled = totalPages > 1;
        PreviewPagination.MaxPageCount = Math.Max(1, totalPages);
        PreviewPagination.DataCountPerPage = PreviewPageSize;
        if (PreviewPagination.PageIndex != _currentPreviewPage)
            PreviewPagination.PageIndex = _currentPreviewPage;
    }

    private void UploadQueries_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择 Query Excel", Filter = "Excel|*.xlsx;*.xlsm" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            using var stream = File.OpenRead(dialog.FileName);
            var rows = _queryExcel.ReadRows(stream);
            foreach (var row in rows)
            {
                row.Status = "Pending";
                row.ErrorMessage = null;
            }
            Replace(_queries, rows);
            UpdateQueryStatus();
        }
        catch (Exception ex) { ShowError("Query Excel 读取失败", ex); }
    }

    private async void SendQueries_Click(object sender, RoutedEventArgs e)
    {
        if (StudyComboBox.SelectedItem is not Study study || _queries.Count == 0) return;
        var recipient = RecipientTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            Notice.Warning("请填写接收组。");
            return;
        }
        LogManager.BeginRun(LogCategory.Rws, "query.log", "RWS Query 发送");
        SetBusy(true);
        try
        {
            var completed = 0;
            foreach (var row in _queries)
            {
                row.Status = "Sending";
                row.ErrorMessage = null;
                try
                {
                    await _service.SendQueryAsync(TenantTextBox.Text.Trim(), UsernameTextBox.Text.Trim(), PasswordBox.Password,
                        study.Oid, recipient, row);
                    row.Status = "Success";
                }
                catch (Exception ex)
                {
                    row.Status = "Failed";
                    row.ErrorMessage = UserMessage(ex);
                    LogManager.WriteException(LogCategory.Rws, "query.log", ex, $"Query 第 {row.RowNumber} 行");
                }
                QueryStatusText.Text = $"正在发送 {++completed}/{_queries.Count}";
            }
            UpdateQueryStatus();
        }
        finally { SetBusy(false); }
    }

    private void ExportData_Click(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
        {
            Notice.Warning("当前没有可导出的数据。");
            return;
        }

        var fileName = StudyComboBox.SelectedItem is Study study
            ? $"{study.ProtocolName}_{DisplayEnvironment(study.Environment)}_DataSet_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
            : $"RWS_Data_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        SaveBytes(_exporter.ExportByForm(_rows), fileName);
    }

    private void ExportQueries_Click(object sender, RoutedEventArgs e) =>
        SaveBytes(_queries.Count == 0 ? null : _queryExcel.ExportRows(_queries, true), "RWS_Queries.xlsx");

    private void ExportFailedQueries_Click(object sender, RoutedEventArgs e)
    {
        var failed = _queries.Where(x => x.Status == "Failed").ToList();
        SaveBytes(failed.Count == 0 ? null : _queryExcel.ExportRows(failed, true), "RWS_Failed_Queries.xlsx");
    }

    private void ToggleSubjects_Click(object sender, RoutedEventArgs e) => Toggle(_subjects);
    private void ToggleForms_Click(object sender, RoutedEventArgs e) => Toggle(_forms);

    private List<string>? FindStudyConfiguration(Study study)
    {
        if (_tenantProfile is null) return null;
        var studyEntry = _tenantProfile.Studies.FirstOrDefault(x =>
            x.Key.Equals(study.ProtocolName, StringComparison.OrdinalIgnoreCase) ||
            x.Key.Equals(study.Oid, StringComparison.OrdinalIgnoreCase));
        if (studyEntry.Value is null) return null;
        return studyEntry.Value.FirstOrDefault(x =>
            x.Key.Equals(study.Environment, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private void LoadConfiguredForms(List<string>? forms)
    {
        var configured = forms ?? [];
        Replace(_forms, configured.Distinct(StringComparer.OrdinalIgnoreCase).Select(x => new Form { FormName = x }));
    }

    private static (string Study, string Environment, List<string> Forms)? FirstStudyConfiguration(RwsTenantProfile profile)
    {
        foreach (var study in profile.Studies)
        foreach (var environment in study.Value)
            return (study.Key, environment.Key, environment.Value);
        return null;
    }

    private static string DisplayEnvironment(string? environment) =>
        string.IsNullOrWhiteSpace(environment) ? "Prod" : environment;

    private static string NormalizeEnvironment(string? environment) =>
        string.IsNullOrWhiteSpace(environment) || environment.Trim().Equals("Prod", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : environment.Trim();

    private bool ValidateCredentials(bool showMessage = true)
    {
        var valid = !string.IsNullOrWhiteSpace(TenantTextBox.Text) && !string.IsNullOrWhiteSpace(UsernameTextBox.Text) &&
                    !string.IsNullOrWhiteSpace(PasswordBox.Password);
        if (!valid && showMessage) Notice.Warning("请填写租户、用户名和密码。");
        return valid;
    }

    private void SetBusy(bool busy, string? status = null)
    {
        LoginButton.IsEnabled = !busy;
        DownloadButton.IsEnabled = !busy;
        SendQueriesButton.IsEnabled = !busy;
        if (status is not null) DownloadStatusText.Text = status;
    }

    private string DatasetType() => (DatasetTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "regular";

    private static void Toggle<T>(IEnumerable<T> values) where T : class
    {
        if (typeof(T) == typeof(Subject))
        {
            var list = values.Cast<Subject>().ToList(); var selected = list.Any(x => !x.IsSelected);
            list.ForEach(x => x.IsSelected = selected);
        }
        else
        {
            var list = values.Cast<Form>().ToList(); var selected = list.Any(x => !x.IsSelected);
            list.ForEach(x => x.IsSelected = selected);
        }
    }

    private static List<string> ParseForms(string text) => text
        .Split(['\r', '\n', ',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear(); foreach (var item in source) target.Add(item);
    }

    private static DataTable BuildTable(IReadOnlyList<RaveDatasetRow> rows)
    {
        var table = new DataTable();
        var fixedColumns = new[] { "StudyOID", "Subject", "SiteOID", "FolderOID", "FolderRepeatKey", "FormOID", "FormRepeatKey", "RecordPosition" };
        foreach (var column in fixedColumns) table.Columns.Add(column);
        var valueColumns = rows.SelectMany(x => x.Values.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var column in valueColumns) if (!table.Columns.Contains(column)) table.Columns.Add(column);
        foreach (var item in rows)
        {
            var row = table.NewRow();
            row[0] = item.StudyOID ?? ""; row[1] = item.SubjectKey ?? ""; row[2] = item.SiteOID ?? "";
            row[3] = item.StudyEventOID ?? ""; row[4] = item.StudyEventRepeatKey ?? ""; row[5] = item.FormOID ?? "";
            row[6] = item.FormRepeatKey ?? ""; row[7] = item.ItemGroupRepeatKey ?? "";
            foreach (var value in item.Values) if (table.Columns.Contains(value.Key)) row[value.Key] = value.Value ?? "";
            table.Rows.Add(row);
        }
        return table;
    }

    private static void SaveBytes(byte[]? bytes, string name)
    {
        if (bytes is null) return;
        var dialog = new SaveFileDialog { FileName = name, Filter = "Excel|*.xlsx" };
        if (dialog.ShowDialog() == true)
        {
            File.WriteAllBytes(dialog.FileName, bytes);
            Notice.Success($"已导出：{dialog.FileName}");
        }
    }

    private void UpdateQueryStatus()
    {
        QueryStatusText.Text = $"{_queries.Count} 条，{_queries.Count(x => x.Status == "Success")} 成功，{_queries.Count(x => x.Status == "Failed")} 失败";
    }

    private static string UserMessage(Exception ex)
    {
        if (ex.Message.Contains("RWS00005", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("locked out", StringComparison.OrdinalIgnoreCase))
            return "账号被锁定，请联系管理员。";
        if (ex.Message.Contains("RWS00008", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("Incorrect login", StringComparison.OrdinalIgnoreCase))
            return "Rave 账号或密码错误。";
        return ex.Message;
    }

    private static void ShowError(string title, Exception ex)
    {
        LogManager.WriteException(LogCategory.Rws, "rws.log", ex, title);
        Notice.Error($"{title}：{UserMessage(ex)}");
    }
}
