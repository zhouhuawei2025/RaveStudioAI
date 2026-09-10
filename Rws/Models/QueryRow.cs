using System.ComponentModel;

namespace RaveStudioAI.Rws.Models
{
    public class QueryRow : INotifyPropertyChanged
    {
        public int RowNumber { get; set; }
        public string SiteOID { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string FolderOID { get; set; } = string.Empty;
        public string FolderRepeatNumber { get; set; } = string.Empty;
        public string FormOID { get; set; } = string.Empty;
        public string FormRepeatNumber { get; set; } = string.Empty;
        public string RecordPosition { get; set; } = string.Empty;
        public string FieldOID { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public string? Specify { get; set; }
        public string Query { get; set; } = string.Empty;

        private string _status = string.Empty;
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

