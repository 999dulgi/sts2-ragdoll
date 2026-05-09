#nullable enable
using System;
using System.Reflection.Emit;
using Godot;
using Microsoft.VisualBasic;

public static class RagdollSettingsPanel
{
    private static Control? _panel;
    private static Vector2 _dragOffset;
    private static bool _isDragging;

    public static void Build(Node parent)
    {
        var layer = new CanvasLayer();
        layer.Layer = 100;
        layer.ProcessMode = Node.ProcessModeEnum.Always;
        parent.AddChild(layer);

        _panel = new PanelContainer();
        _panel.CustomMinimumSize = new Vector2(360, 0);
        _panel.GlobalPosition = new Vector2(_panel.GetViewportRect().Size.X / 2, _panel.GetViewportRect().Size.Y / 2);
        _panel.Visible = false;
        _panel.ProcessMode = Node.ProcessModeEnum.Always;
        layer.AddChild(_panel);
        _panel.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    _isDragging = true;
                    _dragOffset = _panel.GlobalPosition - _panel.GetGlobalMousePosition();
                }
                else
                {
                    _isDragging = false;
                }
            }
            else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
            {
                _panel.GlobalPosition = _panel.GetGlobalMousePosition() + _dragOffset;
            }
        };

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(vbox);

        var title = new Godot.Label();
        title.Text = "Ragdoll Settings";
        vbox.AddChild(title);

        var tabs = new TabContainer();
        vbox.AddChild(tabs);

        var s = RagdollSettings.Current;

        var general = new VBoxContainer();
        general.AddThemeConstantOverride("separation", 12);
        general.Name = "General";
        tabs.AddChild(general);

        AddToggle(general, "Zero Gravity",  s.ZeroGravity, v => s.ZeroGravity = v);
        AddToggle(general, "Forced Explosion Mode", s.ForcedExplosionMode, v => s.ForcedExplosionMode = v);
        AddToggle(general, "Overkill Force", s.OverkillForce, v => s.OverkillForce = v);

        var ragdoll = new VBoxContainer();
        ragdoll.AddThemeConstantOverride("speration", 12);
        ragdoll.Name = "Ragdoll";
        tabs.AddChild(ragdoll);

        AddSlider(ragdoll, "Gravity",       s.RagdollGravity,        500f,  6000f, v => s.RagdollGravity        = v);
        AddSlider(ragdoll, "Speed",         s.RagdollSpeed,          200f,  3000f, v => s.RagdollSpeed          = v);
        AddSlider(ragdoll, "Direction Degree",  s.RagdollAngleDirectionDeg,   0f,   360f, v => s.RagdollAngleDirectionDeg = v);
        AddSlider(ragdoll, "Spread Degree",  s.RagdollAngleSpreadDeg,   0f,   360f, v => s.RagdollAngleSpreadDeg = v);
        AddSlider(ragdoll, "Angular Speed", s.RagdollAngularSpeed,   -60f,    60f, v => s.RagdollAngularSpeed   = v);

        var explosion = new VBoxContainer();
        explosion.AddThemeConstantOverride("sepreation", 12);
        explosion.Name = "Explosion";
        tabs.AddChild(explosion);

        AddSlider(explosion, "Gravity",       s.ExplodeGravity,        500f,  6000f, v => s.ExplodeGravity        = v);
        AddSlider(explosion, "Speed",         s.ExplodeSpeed,          200f,  3000f, v => s.ExplodeSpeed          = v);
        AddSlider(explosion, "Direction Degree",  s.ExplodeAngleDirectionDeg,   0f,   360f, v => s.ExplodeAngleDirectionDeg = v);
        AddSlider(explosion, "Spread Degree",  s.RagdollAngleSpreadDeg,   0f,   360f, v => s.RagdollAngleSpreadDeg = v);
        AddSlider(explosion, "Angular Speed", s.ExplodeAngularSpeed,   -60f,    60f, v => s.ExplodeAngularSpeed   = v);
        

        var closeBtn = new Button();
        closeBtn.Text = "Close";
        closeBtn.Pressed += () => { RagdollSettings.Save(); Toggle(); };
        vbox.AddChild(closeBtn);
    }

    private static void AddSlider(VBoxContainer parent, string labelText, float initial,
        float min, float max, System.Action<float> onChange)
    {
        var row = new VBoxContainer();
        parent.AddChild(row);

        var header = new HBoxContainer();
        row.AddChild(header);

        var lbl = new Godot.Label();
        lbl.Text = labelText;
        lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(lbl);

        var valLbl = new Godot.Label();
        valLbl.Text = initial.ToString("F0");
        header.AddChild(valLbl);

        var slider = new HSlider();
        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Value = initial;
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        slider.ValueChanged += v =>
        {
            valLbl.Text = ((float)v).ToString("F0");
            onChange((float)v);
        };
        row.AddChild(slider);
    }

    public static void AddToggle(VBoxContainer parent, string labelText, Boolean initial, System.Action<Boolean> onChange)
    {
        var row = new VBoxContainer();
        parent.AddChild(row);

        var header = new HBoxContainer();
        row.AddChild(header);

        var lbl = new Godot.Label();
        lbl.Text = labelText;
        lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(lbl);

        var checkbox = new CheckBox();
        checkbox.ButtonPressed = initial;
        checkbox.Toggled += v =>
        {
            onChange((Boolean)v);
        };
        row.AddChild(checkbox);
    }

    public static void Toggle()
    {
        if (_panel != null)
            _panel.Visible = !_panel.Visible;
    }
}
