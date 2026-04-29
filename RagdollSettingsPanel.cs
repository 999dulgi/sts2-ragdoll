#nullable enable
using Godot;

public static class RagdollSettingsPanel
{
    private static Control? _panel;

    public static void Build(Node parent)
    {
        var layer = new CanvasLayer();
        layer.Layer = 100;
        layer.ProcessMode = Node.ProcessModeEnum.Always;
        parent.AddChild(layer);

        _panel = new PanelContainer();
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _panel.CustomMinimumSize = new Vector2(360, 0);
        _panel.Visible = false;
        _panel.ProcessMode = Node.ProcessModeEnum.Always;
        layer.AddChild(_panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(vbox);

        var title = new Label();
        title.Text = "Ragdoll Settings";
        vbox.AddChild(title);

        var s = RagdollSettings.Current;

        AddSlider(vbox, "Gravity",       s.Gravity,        500f,  6000f, v => s.Gravity        = v);
        AddSlider(vbox, "Speed",         s.Speed,          200f,  3000f, v => s.Speed          = v);
        AddSlider(vbox, "Angle Spread",  s.AngleSpreadDeg,   0f,   360f, v => s.AngleSpreadDeg = v);
        AddSlider(vbox, "Angular Speed", s.AngularSpeed,     0f,    60f, v => s.AngularSpeed   = v);

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

        var lbl = new Label();
        lbl.Text = labelText;
        lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(lbl);

        var valLbl = new Label();
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

    public static void Toggle()
    {
        if (_panel != null)
            _panel.Visible = !_panel.Visible;
    }
}
