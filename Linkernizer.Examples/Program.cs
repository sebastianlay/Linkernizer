using Linkernizer;
using Linkernizer.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// you can either use the default:
builder.Services.AddLinkernizer();

// or some custom configuration instead:
// builder.Services.AddLinkernizer(options =>
// {
//   options.InternalHost = "www.example.com";
//   options.OpenExternalLinksInNewTab = true;
//   options.NoReferrerOnExternalLinks = true;
// });

var app = builder.Build();

app.UseDefaultFiles();
app.UseRouting();
app.MapStaticAssets();

// inject the ILinkernizer interface
app.MapPost("/linkernize", async (HttpRequest request, ILinkernizer linkernizer) =>
{
  using var reader = new StreamReader(request.Body);
  var input = await reader.ReadToEndAsync();

  // use it like this
  return linkernizer.Linkernize(input);
});

app.Run();
