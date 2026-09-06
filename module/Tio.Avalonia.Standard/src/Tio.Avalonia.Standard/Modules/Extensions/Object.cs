namespace Tio.Avalonia.Standard.Modules.Extensions;

public static class Object
{
    public static bool IsNull(this object? obj)
    {
        return obj == null;
    }
    
    public static string ToBase64(this byte[] bytes)
    {
        var base64String = Convert.ToBase64String(bytes);
        return base64String;
    }
}