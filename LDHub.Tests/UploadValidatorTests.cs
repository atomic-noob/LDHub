using LDHub.Helpers;
using Microsoft.AspNetCore.Http;

namespace LDHub.Tests;

public class UploadValidatorTests
{
    private static IFormFile File(string name, long length = 1024) =>
        new FormFile(new MemoryStream(new byte[16]), 0, length, "file", name);

    [Theory]
    [InlineData("lecture.pdf")]
    [InlineData("slides.PPTX")]
    [InlineData("video.mp4")]
    [InlineData("Handout.Docx")]
    public void Accepts_allowed_module_files(string name)
    {
        Assert.Null(UploadValidator.Validate(File(name), UploadValidator.ModuleExtensions, UploadValidator.ModuleMaxBytes));
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("page.html")]
    [InlineData("shell.php")]
    [InlineData("view.aspx")]
    [InlineData("script.js")]
    [InlineData("noextension")]
    public void Rejects_files_that_could_run_on_the_server_or_in_a_browser(string name)
    {
        var error = UploadValidator.Validate(File(name), UploadValidator.ModuleExtensions, UploadValidator.ModuleMaxBytes);

        Assert.NotNull(error);
    }

    [Fact]
    public void Rejects_files_over_the_size_limit()
    {
        var error = UploadValidator.Validate(
            File("big.pdf", UploadValidator.ModuleMaxBytes + 1),
            UploadValidator.ModuleExtensions, UploadValidator.ModuleMaxBytes);

        Assert.NotNull(error);
        Assert.Contains("exceed", error);
    }

    [Fact]
    public void Accepts_a_file_exactly_at_the_size_limit()
    {
        Assert.Null(UploadValidator.Validate(
            File("max.pdf", UploadValidator.ModuleMaxBytes),
            UploadValidator.ModuleExtensions, UploadValidator.ModuleMaxBytes));
    }

    [Theory]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.PNG", true)]
    [InlineData("photo.webp", true)]
    [InlineData("anim.gif", false)]
    [InlineData("doc.pdf", false)]
    public void Image_uploads_only_accept_jpg_png_and_webp(string name, bool allowed)
    {
        var error = UploadValidator.Validate(File(name), UploadValidator.ImageExtensions, UploadValidator.ImageMaxBytes);

        Assert.Equal(allowed, error == null);
    }

    [Fact]
    public void Image_limit_is_5_MB()
    {
        Assert.NotNull(UploadValidator.Validate(
            File("photo.jpg", 5L * 1024 * 1024 + 1),
            UploadValidator.ImageExtensions, UploadValidator.ImageMaxBytes));
    }

    [Fact]
    public void SafeFileName_removes_folders_from_the_name()
    {
        Assert.Equal("passwd", UploadValidator.SafeFileName("../../etc/passwd"));
    }

    [Fact]
    public void SafeFileName_replaces_spaces()
    {
        Assert.Equal("my_report_final.pdf", UploadValidator.SafeFileName("my report final.pdf"));
    }

    [Fact]
    public void SafeFileName_keeps_names_to_100_characters_and_keeps_the_extension()
    {
        var name = UploadValidator.SafeFileName(new string('a', 150) + ".pdf");

        Assert.Equal(100, name.Length);
        Assert.EndsWith(".pdf", name);
    }
}
