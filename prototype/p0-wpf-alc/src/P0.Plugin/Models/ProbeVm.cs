using System.ComponentModel;

namespace P0.Plugin.Models;

/// <summary>插件自有视图模型：用于验证「绑定到插件 CLR 属性」是否 root 插件程序集。</summary>
public sealed class ProbeVm : INotifyPropertyChanged
{
    private string _title = "probe-view";

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    public string Badge { get; set; } = "badge";

    public int Ticks { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
