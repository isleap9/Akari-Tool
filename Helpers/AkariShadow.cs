using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace AkariTool.Helpers;

/// <summary>
/// Restores the WPF <c>UIElement.Effect = DropShadowEffect</c> glows/shadows that the
/// WinUI migration dropped (WinUI has no <c>Effect</c> property). A Composition
/// <see cref="DropShadow"/> is rendered into a dedicated, empty <paramref name="host"/>
/// element that overlays the target's container and sits BEHIND the target in z-order,
/// so the layering is: host-shadow &lt; target. The sprite is positioned under the
/// target via <see cref="UIElement.TransformToVisual"/>, so the host only has to overlap
/// the target's container — it does not have to match the target's exact box.
///
/// Colored glows (crimson dot / pill, green status dot) use a colour WinUI's neutral
/// <c>ThemeShadow</c> cannot produce, which is why this is Composition, not ThemeShadow.
/// The returned <see cref="DropShadow"/> lets callers retune <see cref="DropShadow.Opacity"/>
/// on a theme change (the black card shadows differ dark vs light, matching WPF).
/// </summary>
public static class AkariShadow
{
    /// <param name="host">Empty element overlaying the target's container, declared/added
    /// BEFORE the target so it renders behind it.</param>
    /// <param name="target">The element whose silhouette casts the shadow. A
    /// <see cref="Shape"/> is masked to its alpha (round glow for an ellipse); anything
    /// else casts a rectangular shadow of its bounds (cards, pills).</param>
    /// <param name="offsetY">Downward offset in px — the Composition equivalent of WPF
    /// <c>ShadowDepth</c> with <c>Direction=270</c>.</param>
    /// <returns>The live <see cref="DropShadow"/>, so a caller can update its Opacity on a
    /// theme change.</returns>
    public static DropShadow Attach(FrameworkElement host, FrameworkElement target,
        Color color, float blurRadius, float opacity, float offsetY = 0)
    {
        var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;

        var shadow = compositor.CreateDropShadow();
        shadow.Color = color;
        shadow.BlurRadius = blurRadius;
        shadow.Opacity = opacity;
        shadow.Offset = new Vector3(0, offsetY, 0);

        var sprite = compositor.CreateSpriteVisual();
        sprite.Shadow = shadow;

        void Place()
        {
            if (target.ActualWidth <= 0 || target.ActualHeight <= 0) return;
            sprite.Size = new Vector2((float)target.ActualWidth, (float)target.ActualHeight);
            try
            {
                var p = target.TransformToVisual(host).TransformPoint(new Point(0, 0));
                sprite.Offset = new Vector3((float)p.X, (float)p.Y, 0);
            }
            catch { /* not in the same visual tree yet — re-placed on next SizeChanged */ }

            // A masked shadow takes the shape's silhouette (round for an ellipse). Only
            // Shapes expose GetAlphaMask; everything else casts a rectangular shadow.
            if (target is Shape shape) shadow.Mask = shape.GetAlphaMask();
        }

        Place();
        target.SizeChanged += (_, _) => Place();
        host.SizeChanged += (_, _) => Place();
        if (target.ActualWidth <= 0)
            target.Loaded += (_, _) => Place();

        ElementCompositionPreview.SetElementChildVisual(host, sprite);
        return shadow;
    }
}
