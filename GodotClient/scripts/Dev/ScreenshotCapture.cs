using System;
using System.IO;
using Godot;
using WarriorsOfEverdawn.Main;

namespace WarriorsOfEverdawn.Dev;

// --screenshot: saves what the window shows, then quits unless the session has its own length (the playtest). One
// frame goes to _staging/screenshot.png, a series to _staging/screenshot_<n>.png; each also gets a timestamped copy in
// _staging/screenshots/ to compare against later. Ported from Everdawn's ScreenshotHelper. Needs a real renderer:
// headless runs draw nothing.
public partial class ScreenshotCapture : Node
{
    private static readonly string Staging =
        Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "_staging"));

    private readonly ScreenshotOptions _options;
    private readonly Action<int> _quit;
    private readonly bool _quitWhenDone;
    private string _stamp = "";
    private ulong _startedMs;
    private int _taken;

    public ScreenshotCapture(ScreenshotOptions options, Action<int> quit, bool quitWhenDone)
    {
        _options = options;
        _quit = quit;
        _quitWhenDone = quitWhenDone;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public ScreenshotCapture()
        : this(null!, null!, true)
    {
    }

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless")
        {
            Fail("headless runs draw nothing to capture; launch without --headless (./dev.sh screenshot does)");
            return;
        }

        _stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _startedMs = Time.GetTicksMsec();
        Directory.CreateDirectory(Path.Combine(Staging, "screenshots"));
        GetTree().CreateTimer(_options.At).Timeout += Capture;
    }

    // Waits for the frame to finish drawing, so the image is the whole frame rather than whatever was last drawn.
    private async void Capture()
    {
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        var image = GetViewport().GetTexture().GetImage();
        if (image == null || image.IsEmpty())
        {
            Fail("the viewport gave back no image");
            return;
        }

        _taken++;
        string name = _options.Frames == 1 ? "screenshot" : $"screenshot_{_taken}";
        string path = Path.Combine(Staging, name + ".png");
        if (!Save(image, path) || !Save(image, Path.Combine(Staging, "screenshots", $"{_stamp}_{name}.png")))
        {
            return;
        }

        GD.Print($"[screenshot] {path} {image.GetWidth()}x{image.GetHeight()} at {(Time.GetTicksMsec() - _startedMs) / 1000f:F1} s");
        if (_taken < _options.Frames)
        {
            GetTree().CreateTimer(_options.Interval).Timeout += Capture;
        }
        else if (_quitWhenDone)
        {
            _quit(0);
        }
    }

    private bool Save(Image image, string path)
    {
        var error = image.SavePng(path);
        if (error != Error.Ok)
        {
            Fail($"could not save {path}: {error}");
        }

        return error == Error.Ok;
    }

    private void Fail(string message)
    {
        GD.PushError($"[screenshot] {message}");
        _quit(1);
    }
}
