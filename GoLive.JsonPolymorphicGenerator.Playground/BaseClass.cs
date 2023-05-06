using System.Text.Json.Serialization;

namespace GoLive.JsonPolymorphicGenerator.Playground;

[JsonPolymorphic]
public abstract partial class BaseClass
{
    public string Property1 { get; set; }
}