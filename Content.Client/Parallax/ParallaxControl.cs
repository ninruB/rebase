using System.Numerics;
using Content.Client.Parallax.Data;
using Content.Client.Parallax.Managers;
using Content.Shared._White.CCVar;
using Content.Shared.NukeOps;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.ViewVariables;

namespace Content.Client.Parallax;

/// <summary>
///     Renders the parallax background as a UI control.
/// </summary>
public sealed class ParallaxControl : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IParallaxManager _parallaxManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    //WD EDIT START
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private IRenderTexture? _buffer;
    private ShaderInstance _grainShader;

    private string _parallaxPrototype = "FastSpace";

    [ViewVariables(VVAccess.ReadWrite)] public Vector2 Offset { get; set; }
    [ViewVariables(VVAccess.ReadWrite)] public float SpeedX { get; set; } = 0.0f;
    [ViewVariables(VVAccess.ReadWrite)] public float SpeedY { get; set; } = 0.0f;
    [ViewVariables(VVAccess.ReadWrite)] public float ScaleX { get; set; } = 1.0f;
    [ViewVariables(VVAccess.ReadWrite)] public float ScaleY { get; set; } = 1.0f;
    [ViewVariables(VVAccess.ReadWrite)] public string ParallaxPrototype
    {
        get => _parallaxPrototype;
        set
        {
            _parallaxPrototype = value;
            _parallaxManager.LoadParallaxByName(value);
        }
    }

    public ParallaxControl()
    {
        IoCManager.InjectDependencies(this);

        Offset = new Vector2(_random.Next(0, 1000), _random.Next(0, 1000));

        RectClipContent = true;
        _parallaxManager.LoadParallaxByName(_parallaxPrototype);
        //WD EDIT START
        _grainShader = _prototype.Index<ShaderPrototype>("Grain").Instance().Duplicate();
        //WD EDIT END
    }

    //WD EDIT START
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _buffer?.Dispose();
    }

    protected override void Resized()
    {
        base.Resized();

        _buffer?.Dispose();
        _buffer = _clyde.CreateRenderTarget(PixelSize, RenderTargetColorFormat.Rgba8Srgb, default, "parallax");
    }
    //WD EDIT END

    protected override void Draw(DrawingHandleScreen handle)
    {
        var currentTime = (float) _timing.RealTime.TotalSeconds;
        var offset = Offset + new Vector2(currentTime * SpeedX, currentTime * SpeedY);

        foreach (var layer in _parallaxManager.GetParallaxLayers(_parallaxPrototype))
        {
            var tex = layer.Texture;
            var texSize = new Vector2i(
                (int)(tex.Size.X * Size.X * layer.Config.Scale.X / 1920 * ScaleX),
                (int)(tex.Size.Y * Size.X * layer.Config.Scale.Y / 1920 * ScaleY)
            );
            var ourSize = PixelSize;

            //Protection from division by zero.
            texSize.X = Math.Max(texSize.X, 1);
            texSize.Y = Math.Max(texSize.Y, 1);

            if (layer.Config.Tiled)
            {
                // Multiply offset by slowness to match normal parallax
                var scaledOffset = (offset * layer.Config.Slowness).Floored();

                // Then modulo the scaled offset by the size to prevent drawing a bunch of offscreen tiles for really small images.
                scaledOffset.X %= texSize.X;
                scaledOffset.Y %= texSize.Y;

                // Note: scaledOffset must never be below 0 or there will be visual issues.
                // It could be allowed to be >= texSize on a given axis but that would be wasteful.

                for (var x = -scaledOffset.X; x < ourSize.X; x += texSize.X)
                {
                    for (var y = -scaledOffset.Y; y < ourSize.Y; y += texSize.Y)
                    {
                        handle.DrawTextureRect(tex, UIBox2.FromDimensions(new Vector2(x, y), texSize));
                    }
                }
            }
            else
            {
                var origin = ((ourSize - texSize) / 2) + layer.Config.ControlHomePosition;
                handle.DrawTextureRect(tex, UIBox2.FromDimensions(origin, texSize));
            }
        }
        //WD EDIT START
        _grainShader.SetParameter("SCREEN_TEXTURE", _buffer!.Texture);
        _grainShader.SetParameter("strength", _cfg.GetCVar(WhiteCVars.FilmGrainStrength) + 50.0f);
        handle.UseShader(_grainShader);
        handle.DrawTextureRect(_buffer.Texture, PixelSizeBox);
        handle.UseShader(null);
        //WD EDIT END
    }
}

