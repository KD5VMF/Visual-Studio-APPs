using System.Windows.Forms;

namespace PredatorPreyEvolutionCS;

public sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Black;
    }
}
