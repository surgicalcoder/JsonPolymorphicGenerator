# JsonPolymorphicGenerator
c# / .net Source Code Generator for System.Text.Json JsonDerivedType attributes on polymorphic classes

## Usage

For this, your base classes need the `partial` and `abstract` key words, and be decorated with `JsonPolymorphic`, and there need to be derived types in that same assembly for this to work.

An example of this is:

```
[JsonPolymorphic]
public abstract partial class BaseClass
{
    public string Property1 { get; set; }
}
```

This will then generate a partial class, that is decorated with the `JsonDerivedType` attribute, and use the class name as the discriminator.

```
[JsonDerivedType(typeof(GoLive.JsonPolymorphicGenerator.Playground.InheritedClass1), "InheritedClass1")]
[JsonDerivedType(typeof(GoLive.JsonPolymorphicGenerator.Playground.InheritedClass2), "InheritedClass2")]
public partial class BaseClass
{
}
```