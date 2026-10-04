using Microsoft.AspNetCore.Mvc;
using Video_Downloader_for_Facebook.Services;

namespace Video_Downloader_for_Facebook.Controllers;

public class DownloaderController : Controller
{
    private readonly FacebookExtractorService _extractor;

    public DownloaderController(FacebookExtractorService extractor)
    {
        _extractor = extractor;
    }

    // User opens the site
    public IActionResult Index()
    {
        return View();
    }

    // User clicked Get Video button
    [HttpPost]
    public async Task<IActionResult> GetVideo(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            ViewBag.Error = "Please paste a link";
            return View("Index");
        }

        if (!url.Contains("facebook.com") && !url.Contains("fb.watch"))
        {
            ViewBag.Error = "That doesn't look like a Facebook URL";
            return View("Index");
        }

        var video = await _extractor.GetVideoAsync(url);

        if (video == null)
        {
            ViewBag.Error = "Could not fetch video. Make sure it's public and yt-dlp is installed.";
            return View("Index");
        }

        return View("Result", video);
    }

    [HttpGet("/download")]
    public IActionResult DownloadPage()
    {
        return View();
    }

    [HttpGet("/Downloader/DownloadFile")]
    public async Task<IActionResult> DownloadFile(string url, string filename)
    {
        if (string.IsNullOrEmpty(url)) return BadRequest();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "facebookexternalhit/1.1");

        var bytes = await client.GetByteArrayAsync(url);

        if (string.IsNullOrEmpty(filename)) filename = "facebook_video.mp4";
        if (!filename.EndsWith(".mp4")) filename += ".mp4";

        // Replace invalid filename characters
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            filename = filename.Replace(c, '_');
        }

        return File(bytes, "video/mp4", filename);
    }
}