using System.Diagnostics;
using System.Text.Json;

namespace Video_Downloader_for_Facebook.Services;

public class FacebookVideo
{
    public string Title { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string SdUrl { get; set; } = "";
    public string HdUrl { get; set; } = "";
    public string Duration { get; set; } = "";
}

public class FacebookExtractorService
{
    public async Task<FacebookVideo?> GetVideoAsync(string fbUrl)
    {
        try
        {
            // --- NEW: Handle fb.watch short links ---
            if (fbUrl.Contains("fb.watch"))
            {
                try
                {
                    using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                    using var client = new HttpClient(handler);
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                    var response = await client.GetAsync(fbUrl);

                    // fb.watch returns 302 redirect to the real facebook.com URL
                    if (response.Headers.Location != null)
                    {
                        fbUrl = response.Headers.Location.ToString();
                    }
                    else if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                    {
                        // Sometimes Location is in a different place, try with auto-redirect
                        using var client2 = new HttpClient();
                        client2.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                        var finalResponse = await client2.GetAsync(fbUrl);
                        fbUrl = finalResponse.RequestMessage?.RequestUri?.ToString() ?? fbUrl;
                    }
                }
                catch
                {
                    // If resolving fails, yt-dlp can often still handle fb.watch directly, so we keep original
                }
            }

            // Fix reel URL -> watch URL (your working logic)
            string fixedUrl = fbUrl;
            if (fbUrl.Contains("/reel/"))
            {
                var id = fbUrl.Split("/reel/")[1].Split("/")[0].Split("?")[0];
                fixedUrl = $"https://www.facebook.com/watch/?v={id}";
            }

            var psi = new ProcessStartInfo
            {
                FileName = File.Exists(@"C:\Users\wandi\AppData\Local\Microsoft\WinGet\Links\yt-dlp.exe")
    ? @"C:\Users\wandi\AppData\Local\Microsoft\WinGet\Links\yt-dlp.exe"
    : "yt-dlp",
                Arguments = $"--dump-json --no-playlist \"{fixedUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (string.IsNullOrWhiteSpace(output)) return null;

            var root = JsonDocument.Parse(output).RootElement;

            string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Facebook Video" : "Facebook Video";
            string thumb = root.TryGetProperty("thumbnail", out var th) ? th.GetString() ?? "" : "";
            string duration = root.TryGetProperty("duration_string", out var d) ? d.GetString() ?? "" : "";

            string sdUrl = "";
            string hdUrl = "";

            if (root.TryGetProperty("formats", out var formats))
            {
                foreach (var f in formats.EnumerateArray())
                {
                    if (!f.TryGetProperty("format_id", out var fidProp)) continue;
                    if (!f.TryGetProperty("url", out var urlProp)) continue;

                    string fid = fidProp.GetString() ?? "";
                    string url = urlProp.GetString() ?? "";
                    if (string.IsNullOrEmpty(url)) continue;

                    if (fid == "hd") hdUrl = url;
                    if (fid == "sd") sdUrl = url;
                }

                // Fallback if hd/sd not found
                if (string.IsNullOrEmpty(hdUrl) || string.IsNullOrEmpty(sdUrl))
                {
                    foreach (var f in formats.EnumerateArray())
                    {
                        if (f.TryGetProperty("url", out var urlProp))
                        {
                            var url = urlProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(url))
                            {
                                if (string.IsNullOrEmpty(sdUrl)) sdUrl = url;
                                hdUrl = url;
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(hdUrl)) hdUrl = sdUrl;
            if (string.IsNullOrEmpty(sdUrl)) sdUrl = hdUrl;

            if (string.IsNullOrEmpty(hdUrl) && string.IsNullOrEmpty(sdUrl)) return null;

            return new FacebookVideo
            {
                Title = title,
                ThumbnailUrl = thumb,
                SdUrl = sdUrl,
                HdUrl = hdUrl,
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ERROR: {ex.Message}");
            return null;
        }
    }
}