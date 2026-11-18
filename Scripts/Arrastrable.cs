using Godot;

public partial class Arrastrable : ColorRect
{
    // Se declara la señal que escuchara Bianca
    [Signal]
    public delegate void ObjetoConsumidoEventHandler(string idObjeto);

    [Export]
    public string IdObjeto { get; set; } = "fuego"; // Etiqueta para saber si es fuego, melee o cura

    public override void _Ready()
    {
        //Conectamos las señales nativas de foco de Godot
        FocusEntered += OnFocusEntered;
        FocusExited += OnFocusExited;
    }

    private void OnFocusEntered()
    {
        //Aumentamos el brillo al 150% cuando el mando selecciona la casilla
        Modulate = new Color(1.5f, 1.5f, 1.5f);
    }

    private void OnFocusExited()
    {

        //Regresa al color original al mover el D-Pad o joystick
        Modulate = new Color(1.0f, 1.0f, 1.0f);
    }
    // 1. Se ejecuta automáticamente al mantener el clic y arrastrar
    public override Variant _GetDragData(Vector2 atPosition)
    {
        // Creamos un clon visual ("fantasma") que seguirá al cursor
        var preview = new ColorRect();
        preview.CustomMinimumSize = CustomMinimumSize;
        preview.Color = this.Color;
        var modulate = preview.Modulate;
        modulate.A = 0.6f; // Lo hacemos semitransparente
        preview.Modulate = modulate;

        // Metemos el clon en un contenedor para centrarlo en la punta del ratón
        var control = new Control();
        control.AddChild(preview);
        preview.Position = -0.5f * CustomMinimumSize;

        SetDragPreview(control);

        // Devolvemos un diccionario con los datos que viajarán en el aire
        return new Godot.Collections.Dictionary
        {
            ["id"] = IdObjeto,
            ["color_origen"] = Color
        };
    }

    // Funcion para leer clics directamente desde el recuadro
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            // Clic derecho: consumir (comportamiento original de V3)
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                TryConsume();
            }
        }
        //logica del Gamepad
        if (@event.IsActionPressed("ui_accept"))
        {
            TryConsume();
        }
    }

    // La tecla F también consume. Antes la comprobación comparaba
    // button_index (de mouse) con KEY_F (de teclado) y nunca se cumplía.
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.F)
        {
            TryConsume();
        }
    }

    private void TryConsume()
    {
        if (IdObjeto == "cura")
        {
            // Emitimos la señal de que el jugador quiere usar una cura
            EmitSignal(SignalName.ObjetoConsumido, IdObjeto);
            AcceptEvent(); // Detiene el clic evitando rebote de otros nodos
        }
    }
}
