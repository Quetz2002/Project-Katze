using Godot;

public partial class Mutante : CharacterBody3D
{
    private int _salud = 100;
    private const float Speed = 3.5f;
    private Node3D _objetivo;
    private bool _puedeAtacar = true;

    private Timer _attackCooldown;

    public override void _Ready()
    {
        _attackCooldown = GetNode<Timer>("AttackCooldown");
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaF = (float)delta;

        // Gravedad
        if (!IsOnFloor())
        {
            Velocity += GetGravity() * deltaF;
        }

        if (_objetivo != null)
        {
            float distancia = GlobalPosition.DistanceTo(_objetivo.GlobalPosition);

            // Perseguir a Bianca si está lejos
            if (distancia > 1.8f)
            {
                Vector3 direccion = GlobalPosition.DirectionTo(_objetivo.GlobalPosition);
                direccion.Y = 0;
                direccion = direccion.Normalized();

                var velocity = Velocity;
                velocity.X = direccion.X * Speed;
                velocity.Z = direccion.Z * Speed;
                Velocity = velocity;

                Vector3 posMirar = new Vector3(_objetivo.GlobalPosition.X, GlobalPosition.Y, _objetivo.GlobalPosition.Z);
                LookAt(posMirar, Vector3.Up);
            }
            else
            {
                // Detenerse y golpear si está cerca
                var velocity = Velocity;
                velocity.X = 0;
                velocity.Z = 0;
                Velocity = velocity;
                AtacarJugador();
            }
        }
        else
        {
            var velocity = Velocity;
            velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
            Velocity = velocity;
        }

        MoveAndSlide();
    }

    private void AtacarJugador()
    {
        if (_puedeAtacar && _objetivo.HasMethod("RecibirDano"))
        {
            GD.Print("¡El Mutante te dio un zarpazo!");
            _objetivo.Call("RecibirDano", 20);
            _puedeAtacar = false;
            _attackCooldown.Start();
        }
    }

    public void GetDamage(int cantidad)
    {
        _salud -= cantidad;
        GD.Print("¡Mutante recibió ", cantidad, " de daño! Salud restante: ", _salud);
        if (_salud <= 0)
        {
            GD.Print("¡Mutante derrotado!");
            QueueFree();
        }
    }

    // --- SEÑALES PARA LA IA ---
    public void OnVisionAreaBodyEntered(Node3D body)
    {
        if (body.IsInGroup("Jugador"))
        {
            _objetivo = body;
        }
    }

    public void OnVisionAreaBodyExited(Node3D body)
    {
        if (body == _objetivo)
        {
            _objetivo = null;
        }
    }

    public void OnAttackCooldownTimeout()
    {
        _puedeAtacar = true;
    }
}
