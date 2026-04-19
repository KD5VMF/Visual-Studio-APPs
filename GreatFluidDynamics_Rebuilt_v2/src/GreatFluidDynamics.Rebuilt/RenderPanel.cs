using System.Windows.Forms;

namespace GreatFluidDynamics.Rebuilt;

internal sealed class RenderPanel : Panel
{
    public RenderPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }
}
