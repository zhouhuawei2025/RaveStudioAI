using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaveStudioAI.Rws.Models
{
    public class Subject: INotifyPropertyChanged
    {
        private string _subjectKey = string.Empty;
        public string SubjectKey
        {
            get => _subjectKey;
            set
            {
                _subjectKey = value;
                OnPropertyChanged(nameof(SubjectKey));
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                Debug.WriteLine($"Subject 属性 IsSelected 已更改为: {_isSelected}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

