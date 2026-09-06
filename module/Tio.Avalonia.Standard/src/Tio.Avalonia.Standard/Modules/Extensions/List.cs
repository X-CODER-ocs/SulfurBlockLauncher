using System.Collections.ObjectModel;

namespace Tio.Avalonia.Standard.Modules.Extensions;

public static class List
{
    public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}