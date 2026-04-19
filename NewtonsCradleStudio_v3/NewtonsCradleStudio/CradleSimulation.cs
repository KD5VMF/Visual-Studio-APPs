using System;
using System.Collections.Generic;
using System.Windows;

namespace NewtonsCradleStudio;

public sealed class CradleSimulation
{
    public sealed class Bob
    {
        public double AnchorX;
        public double AnchorY;
        public double Theta;
        public double Omega;
    }

    public readonly List<Bob> Bobs = new();

    public double Width { get; private set; } = 1200;
    public double Height { get; private set; } = 800;
    public double Radius { get; private set; } = 34;
    public double RopeLength { get; private set; } = 260;
    public double SupportTopY { get; private set; } = 95;
    public double Gravity { get; set; } = 2100;
    public double Damping { get; set; } = 0.0014;
    public double Restitution { get; set; } = 0.998;
    public double TimeScale { get; set; } = 1.0;
    public bool IsPaused { get; set; }
    public bool IdealTransfer { get; set; } = true;
    public int Count { get; private set; } = 5;
    public int? DraggingIndex { get; private set; }

    private double _lastExplicitLoss = 0.003;

    public void Resize(double width, double height)
    {
        Width = Math.Max(300, width);
        Height = Math.Max(300, height);

        var previousPositions = new List<Point>();
        var previousVelocities = new List<Vector>();
        foreach (var bob in Bobs)
        {
            previousPositions.Add(GetPosition(bob));
            previousVelocities.Add(GetVelocity(bob));
        }

        RebuildAnchors();

        for (var i = 0; i < Math.Min(Bobs.Count, previousPositions.Count); i++)
        {
            var anchor = new Point(Bobs[i].AnchorX, Bobs[i].AnchorY);
            var rel = previousPositions[i] - anchor;
            if (rel.LengthSquared < 1e-6)
            {
                rel = new Vector(0, RopeLength);
            }
            rel.Normalize();
            rel *= RopeLength;
            var newPos = anchor + rel;
            SetPoseFromPoint(i, newPos, preserveOmega: false);
            SetVelocity(i, previousVelocities[i]);
        }
    }

    public void SetCount(int count)
    {
        count = Math.Clamp(count, 3, 7);
        if (count == Count && Bobs.Count == count)
        {
            return;
        }

        Count = count;
        Bobs.Clear();
        for (var i = 0; i < Count; i++)
        {
            Bobs.Add(new Bob());
        }

        RebuildAnchors();
        Reset();
    }

    public void Reset()
    {
        if (Bobs.Count == 0)
        {
            SetCount(Count);
            return;
        }

        DraggingIndex = null;
        for (var i = 0; i < Bobs.Count; i++)
        {
            Bobs[i].Theta = 0;
            Bobs[i].Omega = 0;
        }
    }

    public void SetIdealTransfer(bool enabled)
    {
        IdealTransfer = enabled;
        if (enabled)
        {
            Restitution = 0.9995;
            Damping = 0.00045;
        }
        else
        {
            Restitution = 1.0 - Math.Clamp(_lastExplicitLoss, 0.0, 0.3);
            Damping = 0.0014 + (_lastExplicitLoss * 0.35);
        }
    }

    public void SetLoss(double loss)
    {
        _lastExplicitLoss = Math.Clamp(loss, 0.0, 0.25);
        if (!IdealTransfer)
        {
            Restitution = 1.0 - _lastExplicitLoss;
            Damping = 0.0014 + (_lastExplicitLoss * 0.35);
        }
    }

    public void BeginDrag(int index, Point pointer)
    {
        if (index < 0 || index >= Bobs.Count)
        {
            return;
        }

        DraggingIndex = index;
        SetPoseFromPoint(index, pointer, preserveOmega: false);
        Bobs[index].Omega = 0;
    }

    public void UpdateDrag(Point pointer)
    {
        if (DraggingIndex is null)
        {
            return;
        }

        SetPoseFromPoint(DraggingIndex.Value, pointer, preserveOmega: false);
        Bobs[DraggingIndex.Value].Omega = 0;
    }

    public void EndDrag(Vector releaseVelocity)
    {
        if (DraggingIndex is null)
        {
            return;
        }

        var index = DraggingIndex.Value;
        SetVelocity(index, releaseVelocity);
        DraggingIndex = null;
    }

    public Point GetPosition(int index) => GetPosition(Bobs[index]);

    public Point GetPosition(Bob bob)
    {
        return new Point(
            bob.AnchorX + RopeLength * Math.Sin(bob.Theta),
            bob.AnchorY + RopeLength * Math.Cos(bob.Theta));
    }

    public Vector GetVelocity(int index) => GetVelocity(Bobs[index]);

    public Vector GetVelocity(Bob bob)
    {
        var tangent = GetTangent(bob.Theta);
        return tangent * (RopeLength * bob.Omega);
    }

    public void Step(double dt)
    {
        if (IsPaused || Bobs.Count == 0)
        {
            return;
        }

        dt = Math.Clamp(dt * TimeScale, 0.0, 0.033);
        if (dt <= 0)
        {
            return;
        }

        var maxStep = 1.0 / 480.0;
        var subSteps = Math.Max(1, (int)Math.Ceiling(dt / maxStep));
        var h = dt / subSteps;

        for (var step = 0; step < subSteps; step++)
        {
            for (var i = 0; i < Bobs.Count; i++)
            {
                if (DraggingIndex == i)
                {
                    continue;
                }

                var bob = Bobs[i];
                var angularAcceleration = -Gravity / RopeLength * Math.Sin(bob.Theta) - (Damping * bob.Omega);
                bob.Omega += angularAcceleration * h;
                bob.Theta += bob.Omega * h;
            }

            SolveCollisions(6);
        }
    }

    public int HitTest(Point point)
    {
        for (var i = Bobs.Count - 1; i >= 0; i--)
        {
            var p = GetPosition(i);
            if ((p - point).LengthSquared <= Radius * Radius * 1.15)
            {
                return i;
            }
        }

        return -1;
    }

    private void RebuildAnchors()
    {
        if (Bobs.Count == 0)
        {
            return;
        }

        Radius = Math.Clamp(Math.Min(Width / (Count * 3.1), Height * 0.050), 20, 44);
        RopeLength = Math.Clamp(Math.Min(Height * 0.50, Width * 0.19), 180, 320);
        SupportTopY = Math.Max(72, Height * 0.12);

        var spacing = Radius * 2.0;
        var totalWidth = spacing * (Count - 1);
        var firstX = Width * 0.5 - totalWidth * 0.5;

        for (var i = 0; i < Bobs.Count; i++)
        {
            Bobs[i].AnchorX = firstX + spacing * i;
            Bobs[i].AnchorY = SupportTopY + 26;
        }
    }

    private void SolveCollisions(int iterations)
    {
        var minimumGap = Radius * 2.0;
        var contactTolerance = Math.Max(0.20, Radius * 0.015);

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            // Keep the balls packed along the horizontal collision line.
            for (var i = 0; i < Bobs.Count - 1; i++)
            {
                var first = GetPosition(i);
                var second = GetPosition(i + 1);
                var gap = second.X - first.X;
                var overlap = minimumGap - gap;
                if (overlap > 0)
                {
                    ApplyHorizontalSeparation(i, i + 1, overlap + 0.02);
                }
            }

            // For equal masses, a clean Newton's cradle is much closer to a 1D
            // velocity transfer across the horizontal line of centers than to a
            // generic 2D impulse between arbitrary circle centers.
            for (var i = 0; i < Bobs.Count - 1; i++)
            {
                var first = GetPosition(i);
                var second = GetPosition(i + 1);
                var gap = second.X - first.X;
                if (gap > minimumGap + contactTolerance)
                {
                    continue;
                }

                var v1x = GetVelocity(i).X;
                var v2x = GetVelocity(i + 1).X;
                var relative = v1x - v2x;
                if (relative <= 0)
                {
                    continue;
                }

                if (IdealTransfer)
                {
                    // Equal-mass ideal collision: swap horizontal velocities.
                    SetHorizontalVelocity(i, v2x);
                    SetHorizontalVelocity(i + 1, v1x);
                }
                else
                {
                    // Equal masses with restitution < 1.0.
                    var e = Math.Clamp(Restitution, 0.0, 1.0);
                    var newV1x = ((1.0 - e) * v1x + (1.0 + e) * v2x) * 0.5;
                    var newV2x = ((1.0 + e) * v1x + (1.0 - e) * v2x) * 0.5;
                    SetHorizontalVelocity(i, newV1x);
                    SetHorizontalVelocity(i + 1, newV2x);
                }
            }
        }
    }

    private void ApplyHorizontalSeparation(int leftIndex, int rightIndex, double overlap)
    {
        var left = GetPosition(leftIndex);
        var right = GetPosition(rightIndex);

        var moveLeft = overlap * 0.5;
        var moveRight = overlap * 0.5;

        if (DraggingIndex == leftIndex)
        {
            moveLeft = 0;
            moveRight = overlap;
        }
        else if (DraggingIndex == rightIndex)
        {
            moveLeft = overlap;
            moveRight = 0;
        }

        SetXPosition(leftIndex, left.X - moveLeft);
        SetXPosition(rightIndex, right.X + moveRight);
    }

    private void SetPoseFromPoint(int index, Point targetPoint, bool preserveOmega)
    {
        var bob = Bobs[index];
        var anchor = new Point(bob.AnchorX, bob.AnchorY);
        var relative = targetPoint - anchor;
        if (relative.LengthSquared < 1e-6)
        {
            relative = new Vector(0, RopeLength);
        }

        relative.Normalize();
        relative *= RopeLength;
        bob.Theta = Math.Atan2(relative.X, relative.Y);
        bob.Theta = Math.Clamp(bob.Theta, -1.12, 1.12);
        if (!preserveOmega)
        {
            bob.Omega = 0;
        }
    }

    private void SetVelocity(int index, Vector velocity)
    {
        var bob = Bobs[index];
        var tangent = GetTangent(bob.Theta);
        bob.Omega = Vector.Multiply(velocity, tangent) / RopeLength;
    }

    private void SetHorizontalVelocity(int index, double vx)
    {
        var bob = Bobs[index];
        var tangentX = Math.Max(0.08, Math.Abs(Math.Cos(bob.Theta)));
        bob.Omega = vx / (RopeLength * tangentX);
    }

    private void SetXPosition(int index, double x)
    {
        var bob = Bobs[index];
        var normalized = (x - bob.AnchorX) / RopeLength;
        var maxNormalized = Math.Sin(1.12);
        normalized = Math.Clamp(normalized, -maxNormalized, maxNormalized);
        bob.Theta = Math.Asin(normalized);
    }

    private static Vector GetTangent(double theta)
    {
        var tangent = new Vector(Math.Cos(theta), -Math.Sin(theta));
        tangent.Normalize();
        return tangent;
    }
}
