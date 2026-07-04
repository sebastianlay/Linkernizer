# Linkernizer.DependencyInjection

This package provides some convenience methods around dependency injection for the [Linkernizer](https://www.nuget.org/packages/Linkernizer) library.
It assumes that you are using the default `Microsoft.Extensions.DependencyInjection` container.

Registering the instance is super simple if you are fine with the default settings:
```c#
using Linkernizer.DependencyInjection;

services.AddLinkernizer();
```

Alternatively there are some configuration options available if you want more fine-grained control:
```c#
using Linkernizer.DependencyInjection;

services.AddLinkernizer(options => {
  options.OpenExternalLinksInNewTab = true; // inserts target="_blank" and rel="noopener"
  options.NoReferrerOnExternalLinks = true; // also adds noreferrer to the rel attribute
  options.InternalHost = "www.example.com"; // treat these as internal (no target or rel)
  options.DefaultScheme = "http://"; // use this scheme for links starting with www.
});
```

A third way would be to bind the options from a configuration source such as `appsettings.json`:
```json
{
  "Linkernizer": {
    "OpenExternalLinksInNewTab": true,
    "NoReferrerOnExternalLinks": true,
    "InternalHost": "www.example.com",
    "DefaultScheme": "http://"
  }
}
```
```c#
using Linkernizer.DependencyInjection;

services.AddLinkernizer(builder.Configuration.GetSection("Linkernizer"));
```

In any case you would then inject and use `ILinkernizer` just like this:
```c#
public class MyService(ILinkernizer linkernizer)
{
  public string Render(string input) => linkernizer.Linkernize(input);
}
```
