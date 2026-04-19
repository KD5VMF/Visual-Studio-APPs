using System;
using System.Collections.Generic;
using System.Linq;

namespace AICreatureLab.Core;

internal sealed class NeuralNet
{
    private readonly int[] _layers;
    private readonly double[][][] _weights;
    private readonly double[][] _biases;

    public IReadOnlyList<int> Layers => _layers;
    public double[][][] Weights => _weights;
    public double[][] Biases => _biases;

    public NeuralNet(params int[] layers)
    {
        if (layers.Length < 2)
        {
            throw new ArgumentException("Network needs at least input and output layers.", nameof(layers));
        }

        _layers = layers.ToArray();
        _weights = new double[_layers.Length - 1][][];
        _biases = new double[_layers.Length - 1][];

        for (var layer = 0; layer < _weights.Length; layer++)
        {
            _weights[layer] = new double[_layers[layer + 1]][];
            _biases[layer] = new double[_layers[layer + 1]];

            for (var neuron = 0; neuron < _layers[layer + 1]; neuron++)
            {
                _weights[layer][neuron] = new double[_layers[layer]];
            }
        }
    }

    public static NeuralNet CreateRandom(Random random, params int[] layers)
    {
        var net = new NeuralNet(layers);

        for (var layer = 0; layer < net._weights.Length; layer++)
        {
            for (var neuron = 0; neuron < net._weights[layer].Length; neuron++)
            {
                net._biases[layer][neuron] = NextWeight(random);

                for (var weight = 0; weight < net._weights[layer][neuron].Length; weight++)
                {
                    net._weights[layer][neuron][weight] = NextWeight(random);
                }
            }
        }

        return net;
    }

    public double[] Forward(ReadOnlySpan<double> inputs)
    {
        if (inputs.Length != _layers[0])
        {
            throw new ArgumentException($"Expected {_layers[0]} inputs but got {inputs.Length}.", nameof(inputs));
        }

        var activations = inputs.ToArray();

        for (var layer = 0; layer < _weights.Length; layer++)
        {
            var next = new double[_layers[layer + 1]];

            for (var neuron = 0; neuron < next.Length; neuron++)
            {
                var sum = _biases[layer][neuron];

                for (var weight = 0; weight < activations.Length; weight++)
                {
                    sum += activations[weight] * _weights[layer][neuron][weight];
                }

                next[neuron] = Math.Tanh(sum);
            }

            activations = next;
        }

        return activations;
    }

    public NeuralNet Clone()
    {
        var clone = new NeuralNet(_layers);

        for (var layer = 0; layer < _weights.Length; layer++)
        {
            for (var neuron = 0; neuron < _weights[layer].Length; neuron++)
            {
                clone._biases[layer][neuron] = _biases[layer][neuron];
                Array.Copy(_weights[layer][neuron], clone._weights[layer][neuron], _weights[layer][neuron].Length);
            }
        }

        return clone;
    }

    public NeuralNet CreateMutatedChild(Random random, SimulationConfig config)
    {
        var clone = Clone();

        for (var layer = 0; layer < clone._weights.Length; layer++)
        {
            for (var neuron = 0; neuron < clone._weights[layer].Length; neuron++)
            {
                if (random.NextDouble() < config.MutationChance)
                {
                    clone._biases[layer][neuron] += NextGaussian(random) * config.MutationStrength;
                }

                if (random.NextDouble() < config.BigMutationChance)
                {
                    clone._biases[layer][neuron] += NextGaussian(random) * config.MutationStrength * 2.6;
                }

                clone._biases[layer][neuron] = MathUtil.Clamp(clone._biases[layer][neuron], -4.5, 4.5);

                for (var weight = 0; weight < clone._weights[layer][neuron].Length; weight++)
                {
                    if (random.NextDouble() < config.MutationChance)
                    {
                        clone._weights[layer][neuron][weight] += NextGaussian(random) * config.MutationStrength;
                    }

                    if (random.NextDouble() < config.BigMutationChance)
                    {
                        clone._weights[layer][neuron][weight] += NextGaussian(random) * config.MutationStrength * 2.6;
                    }

                    clone._weights[layer][neuron][weight] = MathUtil.Clamp(clone._weights[layer][neuron][weight], -6.0, 6.0);
                }
            }
        }

        return clone;
    }

    public SavedGenome ToSavedGenome(int generation, int colorArgb, string notes)
    {
        var saved = new SavedGenome
        {
            Version = AICreatureLab.AppInfo.Version,
            CreatedUtc = DateTime.UtcNow,
            Generation = generation,
            BodyColorArgb = colorArgb,
            Notes = notes,
            Layers = _layers.ToArray(),
            Biases = _biases.Select(layer => layer.ToArray()).ToArray(),
            Weights = _weights
                .Select(layer => layer.Select(neuron => neuron.ToArray()).ToArray())
                .ToArray()
        };

        return saved;
    }

    public static NeuralNet FromSavedGenome(SavedGenome genome)
    {
        if (genome.Layers is null || genome.Layers.Length < 2)
        {
            throw new InvalidOperationException("Saved genome does not contain a valid layer definition.");
        }

        if (genome.Weights is null || genome.Biases is null)
        {
            throw new InvalidOperationException("Saved genome is missing weights or biases.");
        }

        var net = new NeuralNet(genome.Layers);

        for (var layer = 0; layer < net._weights.Length; layer++)
        {
            for (var neuron = 0; neuron < net._weights[layer].Length; neuron++)
            {
                net._biases[layer][neuron] = genome.Biases[layer][neuron];
                Array.Copy(genome.Weights[layer][neuron], net._weights[layer][neuron], net._weights[layer][neuron].Length);
            }
        }

        return net;
    }

    private static double NextWeight(Random random) => (random.NextDouble() * 2.0) - 1.0;

    private static double NextGaussian(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
