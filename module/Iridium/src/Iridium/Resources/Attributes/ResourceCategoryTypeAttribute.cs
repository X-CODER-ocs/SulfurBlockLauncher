using Iridium.Enums;

namespace Iridium.Resources.Attributes;


[AttributeUsage(AttributeTargets.Enum)]
public sealed class ResourceCategoryTypeAttribute(ResourceType type) : Attribute {
    public ResourceType Type { get; } = type;
}
