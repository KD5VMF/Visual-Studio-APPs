using System;

namespace AICreatureLab.Core;

internal sealed class SavedGenome
{
    public string Version { get; set; } = AICreatureLab.AppInfo.Version;
    public DateTime CreatedUtc { get; set; }
    public int Generation { get; set; }
    public int BodyColorArgb { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int[]? Layers { get; set; }
    public double[][][]? Weights { get; set; }
    public double[][]? Biases { get; set; }
}
