using Godot;

public partial class RanuraDestino : ColorRect
{
    // Declaramos una señal que enviaremos al script principal de Bianca
    // Pasaremos dos datos: qué slot se actualizó y qué arma se equipó
    [Signal]
    public delegate void ArmaAsignadaEventHandler(int numeroSlot, string tipoArma);

    [Export]
    public int NumeroSlot { get; set; } = 1; // Para saber si es el Quick Slot 1 o el 2

    // 2. Godot pregunta: "¿Es válido soltar esto que traigo aquí?"
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        // Solo permitimos soltar si los datos vienen en el formato de diccionario que creamos
        if (data.VariantType == Variant.Type.Dictionary)
        {
            var dict = data.AsGodotDictionary();
            if (dict.ContainsKey("id"))
            {
                // Solo aceptamos armas en los Quick Slots, las curas se rechazan
                string id = dict["id"].AsString();
                return id == "fuego" || id == "melee";
            }
        }
        return false;
    }

    // Se ejecuta cuando el jugador suelta el clic sobre esta ranura
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var dict = data.AsGodotDictionary();
        // Pintamos la ranura del color del objeto para dar retroalimentación visual
        this.Color = dict["color_origen"].AsColor();

        GD.Print("¡Objeto táctico asignado en Slot ", NumeroSlot, "!: ", dict["id"].AsString());

        // EMITIMOS LA SEÑAL para que el script de Bianca se entere
        EmitSignal(SignalName.ArmaAsignada, NumeroSlot, dict["id"].AsString());
    }
}
