namespace Iridium.Resources.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public sealed class CurseForgeCategoryAttribute(int categoryId) : Attribute {
    public int CategoryId { get; } = categoryId;
}