using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaveStudioAI.Rws.Models
{
    public class RwsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public List<RaveDatasetRow> CurrentRows { get; set; } = new();

        public ObservableCollection<FormPreviewResult> PreviewForms { get; set; } = new();

        private FormPreviewResult? _selectedPreviewForm;
        public FormPreviewResult? SelectedPreviewForm
        {
            get => _selectedPreviewForm;
            set
            {
                _selectedPreviewForm = value;
                OnPropertyChanged(nameof(SelectedPreviewForm));
            }
        }

        private string _pageInfo = "第 0 / 0 页";
        public string PageInfo
        {
            get => _pageInfo;
            set
            {
                _pageInfo = value;
                OnPropertyChanged(nameof(PageInfo));
            }
        }

        private DataView _queryResults = new DataTable().DefaultView;
        public DataView QueryResults
        {
            get => _queryResults;
            set
            {
                _queryResults = value;
                OnPropertyChanged(nameof(QueryResults));
            }
        }

        public ObservableCollection<Study> StudyList { get; set; } = new();
        private Study? _selectedStudy;
        public Study? SelectedStudy
        {
            get => _selectedStudy;
            set
            {
                _selectedStudy = value;
                OnPropertyChanged(nameof(SelectedStudy));
                Debug.WriteLine($"属性 SelectedStudy 已更改为: {_selectedStudy?.StudyName}");
            }
        }

        public ObservableCollection<Subject> SubjectList { get; set; } = new();
        public ObservableCollection<Form> FormList { get; set; } = new();

        public ObservableCollection<string> DataTypeList { get; set; } = new ObservableCollection<string> { "raw", "regular"};

        private string _selectedDataType = "regular";
        public string SelectedDataType
        {
            get => _selectedDataType;
            set
            {
                _selectedDataType = value;
                OnPropertyChanged(nameof(SelectedDataType));
                Debug.WriteLine($"属性 SelectedDataType 已更改为: {_selectedDataType}");
            }
        }

        public ObservableCollection<History> HistoryList { get; set; } = new();

        public ObservableCollection<QueryRow> QueryRows { get; set; } = new();

        private History? _selectedHistory;
        public History? SelectedHistory
        {
            get => _selectedHistory;
            set
            {
                _selectedHistory = value;
                OnPropertyChanged(nameof(SelectedHistory));
                Debug.WriteLine($"属性 SelectedHistory 已更改为: {_selectedHistory?.HistoryName}");
            }
        }
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public RwsViewModel()
        {
            // Initialize the ViewModel with default values if needed
            SelectedDataType = DataTypeList.FirstOrDefault() ?? "regular";
        }
    }
}
