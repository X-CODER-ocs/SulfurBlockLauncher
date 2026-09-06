using System.Collections.Generic;
using Avalonia.Controls;

namespace Tio.Avalonia.Standard.Tab.Interface;

public interface IContextMenuTabPage
{
    void BuildContextMenu(IList<MenuItem> menuItems);
}