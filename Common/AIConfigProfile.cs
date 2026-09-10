using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RaveStudioAI.Common;

public sealed class AIConfigProfile : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _url = string.Empty;
    private string _model = string.Empty;
    private string _apiKey = string.Empty;
    private int _batchSize = 20;

    public string Name { get => _name; set => Set(ref _name, value); }
    public string Url { get => _url; set => Set(ref _url, value); }
    public string Model { get => _model; set => Set(ref _model, value); }
    public string ApiKey { get => _apiKey; set => Set(ref _apiKey, value); }
    public int BatchSize { get => _batchSize; set => Set(ref _batchSize, value); }

    public AIConfigProfile Copy() => new()
    {
        Name = Name,
        Url = Url,
        Model = Model,
        ApiKey = ApiKey,
        BatchSize = BatchSize
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
