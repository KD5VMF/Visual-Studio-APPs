using System.Windows;
using AtomPlayground.Models;
using AtomPlayground.Utils;

namespace AtomPlayground.Simulation;

public sealed class SimulationWorld
{
    private readonly List<AtomState> _atoms = new();
    private readonly List<AtomBond> _bonds = new();
    private readonly Queue<WorldEvent> _recentEvents = new();
    private readonly object _syncRoot = new();
    private Size _bounds = new(3200, 2000);
    private double _eventCooldown;
    private double _decayAccumulator;

    public IReadOnlyList<AtomState> Atoms => _atoms;
    public IReadOnlyList<AtomBond> Bonds => _bonds;
    public IReadOnlyCollection<WorldEvent> RecentEvents => _recentEvents;
    public Size Bounds => _bounds;
    public SandboxMode Mode { get; set; } = SandboxMode.Chemistry;
    public bool Paused { get; set; }
    public bool ShowHud { get; set; } = true;
    public bool ShowShells { get; set; } = true;
    public bool ShowBonds { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public bool ShowTrails { get; set; }
    public bool ShowParticleInternals { get; set; } = true;
    public double TimeScale { get; set; } = 1.0;
    public double SmashEnergy { get; set; } = 40.0;
    public double SimulatedSeconds { get; private set; }

    public event EventHandler<WorldEvent>? EventLogged;

    public void SetBounds(Size bounds)
    {
        var normalized = new Size(
            Math.Max(500, double.IsFinite(bounds.Width) ? bounds.Width : 500),
            Math.Max(340, double.IsFinite(bounds.Height) ? bounds.Height : 340));

        lock (_syncRoot)
        {
            var old = _bounds;
            var scaleX = old.Width > 1 ? normalized.Width / old.Width : 1.0;
            var scaleY = old.Height > 1 ? normalized.Height / old.Height : 1.0;
            _bounds = normalized;

            if (_atoms.Count == 0)
            {
                return;
            }

            foreach (var atom in _atoms)
            {
                atom.Position = new Point(atom.Position.X * scaleX, atom.Position.Y * scaleY);
                EnforceBounds(atom);
            }
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _atoms.Clear();
            _bonds.Clear();
            _recentEvents.Clear();
            SimulatedSeconds = 0;
            _eventCooldown = 0;
            _decayAccumulator = 0;
        }

        SeedDefaultScene();
        LogEvent("Sandbox reset.");
    }

    public void SeedDefaultScene()
    {
        lock (_syncRoot)
        {
            _atoms.Clear();
            _bonds.Clear();

            var center = new Point(_bounds.Width * 0.5, _bounds.Height * 0.5);
            AddAtom(ElementCatalog.GetBySymbol("H"), new Point(center.X - 240, center.Y - 40), new Vector(20, 15));
            AddAtom(ElementCatalog.GetBySymbol("O"), new Point(center.X - 40, center.Y), new Vector(0, 0));
            AddAtom(ElementCatalog.GetBySymbol("H"), new Point(center.X + 160, center.Y + 40), new Vector(-20, -15));
            AddAtom(ElementCatalog.GetBySymbol("Na"), new Point(center.X + 320, center.Y - 180), new Vector(-10, 12));
            AddAtom(ElementCatalog.GetBySymbol("Cl"), new Point(center.X + 420, center.Y - 120), new Vector(10, -12));
            AddAtom(ElementCatalog.GetBySymbol("U"), new Point(center.X + 100, center.Y + 220), new Vector(0, 0));
        }
    }

    public void ClearAll()
    {
        lock (_syncRoot)
        {
            _atoms.Clear();
            _bonds.Clear();
            _recentEvents.Clear();
            SimulatedSeconds = 0;
            _eventCooldown = 0;
            _decayAccumulator = 0;
        }

        LogEvent("Sandbox cleared. Add whatever atoms you want.");
    }

    public AtomState AddAtom(ElementTemplate template, Point position, Vector velocity)
    {
        var spawnMargin = ElementCatalog.ComputeVisualRadius(template.Protons, template.DefaultNeutrons) + 28;
        var atom = ElementCatalog.CreateAtom(template, ClampPointToBounds(position, spawnMargin), velocity);
        lock (_syncRoot)
        {
            _atoms.Add(atom);
        }

        return atom;
    }

    public void AddCluster(ElementTemplate centerTemplate, Point center)
    {
        var spawnMargin = ElementCatalog.ComputeVisualRadius(centerTemplate.Protons, centerTemplate.DefaultNeutrons) + 28;
        for (var i = 0; i < 9; i++)
        {
            var angle = i / 9.0 * Math.PI * 2.0;
            var radius = 100 + Random.Shared.NextDouble() * 180;
            var pos = ClampPointToBounds(new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius), spawnMargin);
            var vel = new Vector((Random.Shared.NextDouble() - 0.5) * 30, (Random.Shared.NextDouble() - 0.5) * 30);
            AddAtom(centerTemplate, pos, vel);
        }

        LogEvent($"Spawned cluster of {centerTemplate.Name} atoms.");
    }

    public void RemoveSelectedAtoms()
    {
        lock (_syncRoot)
        {
            _atoms.RemoveAll(a => a.Selected);
            _bonds.Clear();
        }
    }

    public IReadOnlyList<AtomState> GetSelectedAtoms()
    {
        lock (_syncRoot)
        {
            return _atoms.Where(a => a.Selected).ToList();
        }
    }

    public void ClearSelection()
    {
        lock (_syncRoot)
        {
            foreach (var atom in _atoms)
            {
                atom.Selected = false;
            }
        }
    }

    public void SelectSingle(AtomState atom)
    {
        lock (_syncRoot)
        {
            foreach (var item in _atoms)
            {
                item.Selected = item == atom;
            }
        }
    }

    public void ToggleSelection(AtomState atom)
    {
        atom.Selected = !atom.Selected;
    }

    public void Update(double deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return;
        }

        var dt = Math.Min(0.03, deltaSeconds) * TimeScale;
        if (Paused)
        {
            _eventCooldown = Math.Max(0, _eventCooldown - deltaSeconds);
            return;
        }

        lock (_syncRoot)
        {
            SimulatedSeconds += dt;
            _eventCooldown = Math.Max(0, _eventCooldown - dt);
            _decayAccumulator += dt;

            var pairCache = new List<(AtomState A, AtomState B, Vector Delta, double Distance)>();
            for (var i = 0; i < _atoms.Count; i++)
            {
                for (var j = i + 1; j < _atoms.Count; j++)
                {
                    var a = _atoms[i];
                    var b = _atoms[j];
                    var delta = b.Position - a.Position;
                    var dist = Math.Max(1.0, delta.Length);
                    pairCache.Add((a, b, delta, dist));
                }
            }

            foreach (var pair in pairCache)
            {
                ApplyPairForces(pair.A, pair.B, pair.Delta, pair.Distance, dt);
            }

            RebuildBonds();
            ApplyBondSprings(dt);

            foreach (var atom in _atoms)
            {
                atom.Position = new Point(atom.Position.X + atom.Velocity.X * dt, atom.Position.Y + atom.Velocity.Y * dt);
                atom.Velocity *= 0.999;
                atom.RotationPhase += dt * (0.4 + atom.Protons * 0.015);
                EnforceBounds(atom);

                if (ShowTrails)
                {
                    atom.PushTrailPoint(atom.Position, 42);
                }
                else
                {
                    atom.Trail.Clear();
                }
            }

            if (Mode != SandboxMode.Chemistry && _decayAccumulator >= 0.2)
            {
                _decayAccumulator = 0;
                ApplyNuclearDecayPass();
            }
        }
    }

    public void SmashSelected(double energyScale)
    {
        lock (_syncRoot)
        {
            var selected = _atoms.Where(a => a.Selected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var center = new Point(selected.Average(a => a.Position.X), selected.Average(a => a.Position.Y));
            foreach (var atom in selected)
            {
                var towardCenter = center - atom.Position;
                if (towardCenter.Length < 0.001)
                {
                    towardCenter = new Vector(Random.Shared.NextDouble() - 0.5, Random.Shared.NextDouble() - 0.5);
                }

                towardCenter.Normalize();
                atom.Velocity += towardCenter * energyScale;
            }
        }

        LogEvent($"Injected collision energy into selected atoms ({energyScale:0}).");
    }

    public void CoolVelocities()
    {
        lock (_syncRoot)
        {
            foreach (var atom in _atoms)
            {
                atom.Velocity *= 0.15;
            }
        }

        LogEvent("Velocities damped.");
    }


    public void LaunchAtom(AtomState atom, Point launchPosition, Vector launchVelocity)
    {
        var logged = false;
        lock (_syncRoot)
        {
            if (!_atoms.Contains(atom))
            {
                return;
            }

            var margin = Math.Max(26, atom.RenderRadius + 26);
            atom.Position = ClampPointToBounds(launchPosition, margin);
            atom.Velocity = launchVelocity;
            logged = launchVelocity.Length >= 1;
        }

        if (logged)
        {
            LogEvent($"Sling launched {atom.Symbol} at {launchVelocity.Length:0}." );
        }
    }

    public void NudgeSelectedParticleCounts(int dProtons, int dNeutrons, int dElectrons)
    {
        lock (_syncRoot)
        {
            foreach (var atom in _atoms.Where(a => a.Selected))
            {
                atom.Protons = Math.Max(1, atom.Protons + dProtons);
                atom.Neutrons = Math.Max(0, atom.Neutrons + dNeutrons);
                atom.Electrons = Math.Max(0, atom.Electrons + dElectrons);
                ElementCatalog.RefreshIdentity(atom);
            }
        }

        var parts = new List<string>();
        if (dProtons != 0) parts.Add($"protons {(dProtons > 0 ? "+" : string.Empty)}{dProtons}");
        if (dNeutrons != 0) parts.Add($"neutrons {(dNeutrons > 0 ? "+" : string.Empty)}{dNeutrons}");
        if (dElectrons != 0) parts.Add($"electrons {(dElectrons > 0 ? "+" : string.Empty)}{dElectrons}");
        LogEvent($"Adjusted selected atoms: {string.Join(", ", parts)}.");
    }

    public string DescribeChemistry(AtomState atom)
    {
        var charge = atom.Charge;
        var shellState = atom.ValenceGoal == 0
            ? "closed shell"
            : charge switch
            {
                > 0 => "electron-deficient / cation-like",
                < 0 => "electron-rich / anion-like",
                _ => "neutral shell balance"
            };

        var bondCount = _bonds.Count(b => b.A == atom || b.B == atom);
        return $"{shellState}; bonds: {bondCount}";
    }

    public string EstimateStability(AtomState atom)
    {
        if (atom.Protons <= 2)
        {
            return atom.Neutrons is 0 or 2 ? "stable-ish" : "unstable";
        }

        var neutronTarget = GetPreferredNeutronCount(atom.Protons);
        var delta = atom.Neutrons - neutronTarget;
        var magnitude = Math.Abs(delta);

        if (atom.Protons >= 84)
        {
            return magnitude switch
            {
                < 10 => "radioactive",
                < 22 => "very unstable",
                _ => "extreme decay pressure"
            };
        }

        return magnitude switch
        {
            < 2 => "stable-ish",
            < 6 => "metastable",
            < 12 => delta > 0 ? "neutron-rich unstable" : "proton-rich unstable",
            _ => delta > 0 ? "very neutron-rich" : "very proton-rich"
        };
    }

    private void ApplyPairForces(AtomState a, AtomState b, Vector delta, double distance, double dt)
    {
        var dir = delta;
        dir.Normalize();

        var minDist = a.RenderRadius + b.RenderRadius + 18.0;
        var overlap = minDist - distance;
        if (overlap > 0)
        {
            var repelStrength = overlap * 8.0;
            a.Velocity -= dir * repelStrength * dt;
            b.Velocity += dir * repelStrength * dt;
        }

        var electroRepulsion = (a.Protons * b.Protons) / (distance * distance * 0.004 + 1400.0);
        var chargeInfluence = (a.Charge * b.Charge) / (distance * distance + 4000.0);
        var force = electroRepulsion + chargeInfluence * 6.0;
        a.Velocity -= dir * force * dt;
        b.Velocity += dir * force * dt;

        if (Mode != SandboxMode.Nuclear)
        {
            MaybeTransferElectron(a, b, distance);
        }

        if (Mode != SandboxMode.Chemistry)
        {
            MaybeDoNuclearCollision(a, b, distance);
        }

        MaybeRecombineOppositeIons(a, b, distance);
    }

    private void MaybeTransferElectron(AtomState a, AtomState b, double distance)
    {
        if (distance > a.RenderRadius + b.RenderRadius + 38)
        {
            return;
        }

        if (a.DonorLike && !b.DonorLike && !b.NobleGas && a.Electrons > 0 && b.Electrons < b.Protons + 2 && b.Electronegativity > a.Electronegativity + 1.2)
        {
            a.Electrons--;
            b.Electrons++;
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.7;
                LogEvent($"Electron transfer: {a.Symbol} → {b.Symbol}.");
            }
        }
        else if (b.DonorLike && !a.DonorLike && !a.NobleGas && b.Electrons > 0 && a.Electrons < a.Protons + 2 && a.Electronegativity > b.Electronegativity + 1.2)
        {
            b.Electrons--;
            a.Electrons++;
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.7;
                LogEvent($"Electron transfer: {b.Symbol} → {a.Symbol}.");
            }
        }
    }

    private void MaybeRecombineOppositeIons(AtomState a, AtomState b, double distance)
    {
        if (Mode == SandboxMode.Nuclear)
        {
            return;
        }

        if (distance > a.RenderRadius + b.RenderRadius + 28)
        {
            return;
        }

        if (a.Charge > 0 && b.Charge < 0 && b.Electrons > 0)
        {
            a.Electrons++;
            b.Electrons--;
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.55;
                LogEvent($"Charge recombination: {b.Symbol} shared an electron with {a.Symbol}.");
            }
        }
        else if (b.Charge > 0 && a.Charge < 0 && a.Electrons > 0)
        {
            b.Electrons++;
            a.Electrons--;
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.55;
                LogEvent($"Charge recombination: {a.Symbol} shared an electron with {b.Symbol}.");
            }
        }
    }

    private void MaybeDoNuclearCollision(AtomState a, AtomState b, double distance)
    {
        var nuclearDistance = (a.RenderRadius + b.RenderRadius) * 0.55;
        if (distance > nuclearDistance)
        {
            return;
        }

        var relativeEnergy = (a.Velocity - b.Velocity).Length;
        if (relativeEnergy < 12)
        {
            MaybeDoCaptureReaction(a, b);
            return;
        }

        if ((a.Protons + b.Protons) <= 8 && relativeEnergy >= SmashEnergy * 0.55)
        {
            FuseAtoms(a, b);
            return;
        }

        if ((a.HeavyFissionCapable || b.HeavyFissionCapable || a.Protons >= 84 || b.Protons >= 84) && relativeEnergy >= SmashEnergy * 0.80)
        {
            var target = a.Protons >= b.Protons ? a : b;
            FissionAtom(target);
            return;
        }

        if (relativeEnergy >= SmashEnergy * 0.92)
        {
            var target = a.MassNumber >= b.MassNumber ? a : b;
            if (TryEjectAlpha(target))
            {
                return;
            }
        }

        if (relativeEnergy >= SmashEnergy * 0.72)
        {
            var target = a.MassNumber >= b.MassNumber ? a : b;
            if (TryParticleSpallation(target, preferNeutron: true))
            {
                return;
            }
        }

        if (relativeEnergy >= SmashEnergy * 0.60)
        {
            MaybeDoIsotopeExchange(a, b);
        }
    }

    private void FuseAtoms(AtomState a, AtomState b)
    {
        if (!_atoms.Contains(a) || !_atoms.Contains(b))
        {
            return;
        }

        var newProtons = a.Protons + b.Protons;
        var newNeutrons = a.Neutrons + b.Neutrons;
        var newElectrons = Math.Min(newProtons + 2, a.Electrons + b.Electrons);
        var template = ElementCatalog.GetClosestByProtons(newProtons);
        var fused = ElementCatalog.CreateAtom(template, MidPoint(a.Position, b.Position), (a.Velocity + b.Velocity) * 0.35);
        fused.Protons = newProtons;
        fused.Neutrons = newNeutrons;
        fused.Electrons = newElectrons;
        ElementCatalog.RefreshIdentity(fused);

        _atoms.Remove(a);
        _atoms.Remove(b);
        _atoms.Add(fused);
        _bonds.Clear();

        if (_eventCooldown <= 0)
        {
            _eventCooldown = 1.1;
            LogEvent($"Fusion: {a.Symbol} + {b.Symbol} → {fused.Symbol}.");
        }
    }

    private void FissionAtom(AtomState atom)
    {
        if (!_atoms.Contains(atom) || atom.MassNumber < 40)
        {
            return;
        }

        var firstP = Math.Max(1, atom.Protons / 2 + Random.Shared.Next(-3, 4));
        firstP = Math.Clamp(firstP, 1, atom.Protons - 1);
        var secondP = atom.Protons - firstP;

        var firstN = Math.Max(0, atom.Neutrons / 2 + Random.Shared.Next(-5, 6));
        firstN = Math.Clamp(firstN, 0, atom.Neutrons);
        var secondN = Math.Max(0, atom.Neutrons - firstN - 2);
        if (secondN < 0)
        {
            secondN = 0;
        }

        var t1 = ElementCatalog.GetClosestByProtons(firstP);
        var t2 = ElementCatalog.GetClosestByProtons(secondP);
        var dir = new Vector(Random.Shared.NextDouble() - 0.5, Random.Shared.NextDouble() - 0.5);
        if (dir.Length < 0.001)
        {
            dir = new Vector(1, 0);
        }
        dir.Normalize();
        var ortho = new Vector(-dir.Y, dir.X);

        var a = ElementCatalog.CreateAtom(t1, atom.Position + dir * 50, atom.Velocity + dir * 60 + ortho * 20);
        var b = ElementCatalog.CreateAtom(t2, atom.Position - dir * 50, atom.Velocity - dir * 60 - ortho * 20);
        a.Protons = firstP;
        a.Neutrons = firstN;
        a.Electrons = Math.Max(0, Math.Min(a.Protons + 3, atom.Electrons / 2));
        b.Protons = secondP;
        b.Neutrons = secondN;
        b.Electrons = Math.Max(0, Math.Min(b.Protons + 3, atom.Electrons - a.Electrons));
        ElementCatalog.RefreshIdentity(a);
        ElementCatalog.RefreshIdentity(b);

        _atoms.Remove(atom);
        _atoms.Add(a);
        _atoms.Add(b);
        _bonds.Clear();

        if (_eventCooldown <= 0)
        {
            _eventCooldown = 1.4;
            LogEvent($"Fission: {atom.Symbol} split into {a.Symbol} and {b.Symbol} fragments.");
        }
    }

    private void ApplyNuclearDecayPass()
    {
        foreach (var atom in _atoms.ToList())
        {
            var stability = EstimateStability(atom);
            if (stability == "stable-ish")
            {
                continue;
            }

            var chance = stability switch
            {
                "metastable" => 0.018,
                "radioactive" => 0.05,
                "very unstable" => 0.08,
                "extreme decay pressure" => 0.12,
                "neutron-rich unstable" => 0.045,
                "proton-rich unstable" => 0.045,
                "very neutron-rich" => 0.08,
                "very proton-rich" => 0.08,
                _ => 0.035
            };

            if (Random.Shared.NextDouble() > chance)
            {
                continue;
            }

            if (atom.Protons >= 84 && atom.Neutrons >= 2 && TryEjectAlpha(atom))
            {
                continue;
            }

            if (IsVeryNeutronRich(atom) && TryNeutronEmission(atom))
            {
                continue;
            }

            if (IsVeryProtonRich(atom) && TryProtonEmission(atom))
            {
                continue;
            }

            if (atom.Neutrons > GetPreferredNeutronCount(atom.Protons) && atom.Neutrons > 0)
            {
                atom.Neutrons--;
                atom.Protons++;
                atom.Electrons++;
                ElementCatalog.RefreshIdentity(atom);
                if (_eventCooldown <= 0)
                {
                    _eventCooldown = 1.0;
                    LogEvent($"Beta-minus-like decay nudged {atom.Symbol} toward stability.");
                }
                continue;
            }

            if (atom.Protons > 1 && atom.Electrons > 0)
            {
                atom.Protons--;
                atom.Neutrons++;
                atom.Electrons--;
                ElementCatalog.RefreshIdentity(atom);
                if (_eventCooldown <= 0)
                {
                    _eventCooldown = 1.0;
                    LogEvent($"Electron-capture / beta-plus-like step shifted {atom.Symbol} toward stability.");
                }
                continue;
            }

            atom.Electrons = Math.Max(0, atom.Electrons - 1);
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.9;
                LogEvent($"Ionization-like excitation energized {atom.Symbol}.");
            }
        }
    }

    private void MaybeDoCaptureReaction(AtomState a, AtomState b)
    {
        var heavy = a.MassNumber >= b.MassNumber ? a : b;
        var light = ReferenceEquals(heavy, a) ? b : a;

        if (!_atoms.Contains(heavy) || !_atoms.Contains(light))
        {
            return;
        }

        if (light.Protons == 1 && heavy.Protons >= 6 && light.Neutrons >= 0 && heavy.Neutrons < GetPreferredNeutronCount(heavy.Protons) + 4)
        {
            heavy.Neutrons += 1;
            heavy.Electrons = Math.Min(heavy.Protons + 4, heavy.Electrons + Math.Min(1, light.Electrons));
            ElementCatalog.RefreshIdentity(heavy);
            _atoms.Remove(light);
            _bonds.Clear();
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.85;
                LogEvent($"Capture-like reaction: {heavy.Symbol} absorbed a light collision partner and became a heavier isotope.");
            }
        }
    }

    private void MaybeDoIsotopeExchange(AtomState a, AtomState b)
    {
        if (a.Neutrons <= 0 && b.Neutrons <= 0)
        {
            return;
        }

        var preferredA = GetPreferredNeutronCount(a.Protons);
        var preferredB = GetPreferredNeutronCount(b.Protons);
        var deltaA = a.Neutrons - preferredA;
        var deltaB = b.Neutrons - preferredB;

        if (deltaA >= 2 && deltaB <= -1 && a.Neutrons > 0)
        {
            a.Neutrons--;
            b.Neutrons++;
            ElementCatalog.RefreshIdentity(a);
            ElementCatalog.RefreshIdentity(b);
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.7;
                LogEvent($"Isotope exchange: neutron moved from {a.Symbol} to {b.Symbol}.");
            }
        }
        else if (deltaB >= 2 && deltaA <= -1 && b.Neutrons > 0)
        {
            b.Neutrons--;
            a.Neutrons++;
            ElementCatalog.RefreshIdentity(a);
            ElementCatalog.RefreshIdentity(b);
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.7;
                LogEvent($"Isotope exchange: neutron moved from {b.Symbol} to {a.Symbol}.");
            }
        }
    }

    private bool TryParticleSpallation(AtomState atom, bool preferNeutron)
    {
        if (!_atoms.Contains(atom))
        {
            return false;
        }

        if (preferNeutron && atom.Neutrons > 0)
        {
            atom.Neutrons--;
            ElementCatalog.RefreshIdentity(atom);
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.5;
                LogEvent($"Spallation: knocked a neutron off {atom.Symbol} nucleus.");
            }
            return true;
        }

        if (atom.Protons > 1 && atom.Electrons > 0)
        {
            atom.Protons--;
            atom.Electrons = Math.Max(0, atom.Electrons - 1);
            ElementCatalog.RefreshIdentity(atom);
            if (_eventCooldown <= 0)
            {
                _eventCooldown = 0.55;
                LogEvent($"Spallation: knocked a proton off {atom.Symbol} nucleus.");
            }
            return true;
        }

        return false;
    }

    private bool TryEjectAlpha(AtomState atom)
    {
        if (!_atoms.Contains(atom) || atom.Protons < 4 || atom.Neutrons < 2)
        {
            return false;
        }

        atom.Protons -= 2;
        atom.Neutrons -= 2;
        atom.Electrons = Math.Max(0, atom.Electrons - 2);
        ElementCatalog.RefreshIdentity(atom);

        var alphaTemplate = ElementCatalog.GetBySymbol("He");
        var dir = new Vector(Random.Shared.NextDouble() - 0.5, Random.Shared.NextDouble() - 0.5);
        if (dir.Length < 0.001)
        {
            dir = new Vector(1, 0);
        }
        dir.Normalize();
        var alpha = ElementCatalog.CreateAtom(alphaTemplate, ClampPointToBounds(atom.Position + dir * (atom.RenderRadius + 28), 30), atom.Velocity + dir * 110);
        alpha.Neutrons = 2;
        alpha.Electrons = 2;
        alpha.Protons = 2;
        ElementCatalog.RefreshIdentity(alpha);
        _atoms.Add(alpha);
        _bonds.Clear();

        if (_eventCooldown <= 0)
        {
            _eventCooldown = 1.0;
            LogEvent($"Alpha-like emission observed from {atom.Symbol}.");
        }

        return true;
    }

    private bool TryNeutronEmission(AtomState atom)
    {
        if (atom.Neutrons <= 0)
        {
            return false;
        }

        atom.Neutrons--;
        ElementCatalog.RefreshIdentity(atom);
        if (_eventCooldown <= 0)
        {
            _eventCooldown = 0.8;
            LogEvent($"Neutron-emission-like decay observed from {atom.Symbol}.");
        }
        return true;
    }

    private bool TryProtonEmission(AtomState atom)
    {
        if (atom.Protons <= 1)
        {
            return false;
        }

        atom.Protons--;
        atom.Electrons = Math.Max(0, atom.Electrons - 1);
        ElementCatalog.RefreshIdentity(atom);
        if (_eventCooldown <= 0)
        {
            _eventCooldown = 0.8;
            LogEvent($"Proton-emission-like decay observed from {atom.Symbol}.");
        }
        return true;
    }

    private static int GetPreferredNeutronCount(int protons)
    {
        return protons <= 20 ? protons : (int)Math.Round(protons * 1.25);
    }

    private static bool IsVeryNeutronRich(AtomState atom)
    {
        return atom.Neutrons - GetPreferredNeutronCount(atom.Protons) >= 8;
    }

    private static bool IsVeryProtonRich(AtomState atom)
    {
        return GetPreferredNeutronCount(atom.Protons) - atom.Neutrons >= 8;
    }

    private void RebuildBonds()
    {
        _bonds.Clear();
        if (Mode == SandboxMode.Nuclear)
        {
            return;
        }

        var candidatePairs = new HashSet<string>();
        for (var i = 0; i < _atoms.Count; i++)
        {
            for (var j = i + 1; j < _atoms.Count; j++)
            {
                var a = _atoms[i];
                var b = _atoms[j];
                if (a.NobleGas || b.NobleGas)
                {
                    continue;
                }

                var distance = (b.Position - a.Position).Length;
                var maxBondDistance = a.RenderRadius + b.RenderRadius + 65;
                if (distance > maxBondDistance)
                {
                    continue;
                }

                var desiredA = Math.Max(0, a.ValenceGoal - CountExistingBondOrders(a));
                var desiredB = Math.Max(0, b.ValenceGoal - CountExistingBondOrders(b));
                if (desiredA == 0 && desiredB == 0)
                {
                    continue;
                }

                var ionic = Math.Abs(a.Electronegativity - b.Electronegativity) >= 1.5 || (a.DonorLike && !b.DonorLike) || (b.DonorLike && !a.DonorLike);
                var order = ionic ? 1 : Math.Min(3, Math.Min(Math.Max(1, desiredA), Math.Max(1, desiredB)));
                var key = a.Id.CompareTo(b.Id) < 0 ? $"{a.Id}:{b.Id}" : $"{b.Id}:{a.Id}";
                if (candidatePairs.Add(key))
                {
                    _bonds.Add(new AtomBond { A = a, B = b, Order = order, Ionic = ionic });
                }
            }
        }
    }

    private int CountExistingBondOrders(AtomState atom)
    {
        return _bonds.Where(b => b.A == atom || b.B == atom).Sum(b => b.Order);
    }

    private void ApplyBondSprings(double dt)
    {
        foreach (var bond in _bonds)
        {
            var delta = bond.B.Position - bond.A.Position;
            var length = Math.Max(1, delta.Length);
            var desired = bond.A.RenderRadius + bond.B.RenderRadius + (bond.Ionic ? 34 : 22);
            var stretch = length - desired;
            var dir = delta;
            dir.Normalize();
            var spring = stretch * (bond.Ionic ? 0.14 : 0.22) * bond.Order;
            bond.A.Velocity += dir * spring * dt;
            bond.B.Velocity -= dir * spring * dt;
        }
    }

    private void EnforceBounds(AtomState atom)
    {
        var margin = Math.Max(26, atom.RenderRadius + 26);
        var x = atom.Position.X;
        var y = atom.Position.Y;

        if (x < margin)
        {
            x = margin;
            atom.Velocity = new Vector(Math.Abs(atom.Velocity.X) * 0.75, atom.Velocity.Y);
        }
        else if (x > _bounds.Width - margin)
        {
            x = _bounds.Width - margin;
            atom.Velocity = new Vector(-Math.Abs(atom.Velocity.X) * 0.75, atom.Velocity.Y);
        }

        if (y < margin)
        {
            y = margin;
            atom.Velocity = new Vector(atom.Velocity.X, Math.Abs(atom.Velocity.Y) * 0.75);
        }
        else if (y > _bounds.Height - margin)
        {
            y = _bounds.Height - margin;
            atom.Velocity = new Vector(atom.Velocity.X, -Math.Abs(atom.Velocity.Y) * 0.75);
        }

        atom.Position = new Point(x, y);
    }

    private void LogEvent(string message)
    {
        var evt = new WorldEvent
        {
            Timestamp = DateTime.Now,
            Message = message,
        };

        lock (_syncRoot)
        {
            _recentEvents.Enqueue(evt);
            while (_recentEvents.Count > 24)
            {
                _recentEvents.Dequeue();
            }
        }

        EventLogged?.Invoke(this, evt);
    }

    private Point ClampPointToBounds(Point point, double margin)
    {
        var clampedX = Math.Clamp(point.X, margin, Math.Max(margin, _bounds.Width - margin));
        var clampedY = Math.Clamp(point.Y, margin, Math.Max(margin, _bounds.Height - margin));
        return new Point(clampedX, clampedY);
    }

    private static Point MidPoint(Point a, Point b)
    {
        return new Point((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
    }
}
