using Microsoft.Extensions.FileProviders;
using System.Runtime.CompilerServices;

namespace TeamBuilder.Extensions;

public static class ImageRegistration
{
    public static void AddStaticImages(this WebApplicationBuilder builder, WebApplication app)
    {
        var leagueImagesCandidates = new[]
        {
            Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "wwwroot", "assets", "images")),
            Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "assets", "images"))
        };

        var leagueImagesPath = leagueImagesCandidates.FirstOrDefault(Directory.Exists);
        if (!string.IsNullOrWhiteSpace(leagueImagesPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(leagueImagesPath),
                RequestPath = "/assets/images"
            });
        }
    }
}
