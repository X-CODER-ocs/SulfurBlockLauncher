using System.Collections.ObjectModel;
using System.ComponentModel;
using Tio.Avalonia.Standard.Standard.Ui;
using Tio.Avalonia.Standard.Tab.Entries;

namespace Tio.Avalonia.Standard.Tab.Interface;

public interface ITioTabWindow : ITioWindow 
{
    public bool IsMainWindow { get; init; }
}