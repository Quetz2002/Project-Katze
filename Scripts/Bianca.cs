using Godot;

public partial class Bianca : CharacterBody3D
{
	// --- NODOS ---
	private Node3D _camRoot;
	private SpringArm3D _springArm;
	private Camera3D _camera;
	private CenterContainer _reticle;
	private RayCast3D _raycast;
	private Area3D _meleeHitbox; // Nodo para el golpe cuerpo a cuerpo
	private ProgressBar _healthBar;
	private ProgressBar _staminaBar;
	private Panel _inventoryPanel;
	private Label _inventarioLabel;

	// --- DATOS DE INVENTARIO ---
	private bool _inventarioAbierto = false;
	private int _memoriasRecuperadas = 0;
	private int _objetosCurativos = 3;

	private RanuraDestino _slot1;
	private RanuraDestino _slot2;
	private Control _primerSlotMochila;
	private string _armaPendienteId = "";
	private Color _armaPendienteColor;


	// --- INVENTARIO V3: ROTACION DEL MODELO 3D ---
	private bool _isRotatingModel = false;
	private float _modelRotationSpeed = 0.01f;
	private MeshInstance3D _capsuleModel;

	// --- ESTADISTICAS ---
	private float _maxHealth = 100.0f;
	private float _currentHealth;
	private float _maxStamina = 100.0f;
	private float _currentStamina;
	private float _staminaRegen = 15.0f; // Cuanta stamina recupera por segundo
	private float _dashCost = 25.0f;
	private float _heavyAttackCost = 35.0f;

	// --- ESTADOS DE ARMAS ---
	private enum Weapon { Ranged, Melee }
	private Weapon _currentWeapon = Weapon.Ranged;

	// --- CÁMARA Y APUNTADO ---
	private const float NormalCamLength = 3.0f;
	private const float AimCamLength = 1.2f;
	private const float NormalHOffset = 0.0f; // Posición centrada normal
	private const float AimHOffset = 1.0f;    // Cuánto se mueve a la derecha al apuntar
	private bool _isAiming = false;
	private float _mouseSensitivity = 0.003f;
	private float _joySensitivity = 2.5f; //Velocidad de giro con el mando

	// --- MOVIMIENTO ---
	private const float JumpVelocity = 4.5f;
	private int _saltosRestantes = 2;
	private const int MaxSaltos = 2;
	private float _speed = 5.0f;
	private const float WalkSpeed = 5.0f;
	private const float DashSpeed = 15.0f;
	private float _dashTimer = 0.0f;

	// --- VARIABLES PARA GUARDAR QUÉ ARMA HAY EN CADA SLOT ---
	private string _armaEnSlot1 = "fuego"; // Por defecto, la pistola
	private string _armaEnSlot2 = "melee"; // Por defecto, la espada


	public override void _Ready()
	{
		_camRoot = GetNode<Node3D>("CamRoot");
		_springArm = GetNode<SpringArm3D>("CamRoot/SpringArm3D");
		_camera = GetNode<Camera3D>("CamRoot/SpringArm3D/Camera3D");
		_reticle = GetNode<CenterContainer>("UI/CenterContainer");
		_raycast = GetNode<RayCast3D>("CamRoot/SpringArm3D/Camera3D/RayCast3D");
		_meleeHitbox = GetNode<Area3D>("MeleeHitbox");
		_healthBar = GetNode<ProgressBar>("UI/HealthBar");
		_staminaBar = GetNode<ProgressBar>("UI/StaminaBar");
		_inventoryPanel = GetNode<Panel>("UI/InventoryPanel");
		_inventarioLabel = GetNode<Label>("UI/InventoryPanel/HBoxContainer/VBoxContainer/Label");
		_capsuleModel = GetNode<MeshInstance3D>("UI/InventoryPanel/HBoxContainer/SubViewportContainer/SubViewport/MeshInstance3D");

		_currentHealth = _maxHealth;
		_currentStamina = _maxStamina;

		Input.MouseMode = Input.MouseModeEnum.Captured;
		_reticle.Hide();

        // --- CONECTAR SEÑALES DEL INVENTARIO ---
        _slot1 = GetNode<RanuraDestino>("UI/InventoryPanel/HBoxContainer/VBoxContainer/QuickSlots/QuickSlot1");
        _slot2 = GetNode<RanuraDestino>("UI/InventoryPanel/HBoxContainer/VBoxContainer/QuickSlots/QuickSlot2");

        _slot1.ArmaAsignada += OnArmaAsignada;
        _slot2.ArmaAsignada += OnArmaAsignada;
        _slot1.SlotConfirmadoConMando += OnSlotConfirmadoConMando;
        _slot2.SlotConfirmadoConMando += OnSlotConfirmadoConMando;

        var gridMochila = GetNode<GridContainer>("UI/InventoryPanel/HBoxContainer/GridContainer");
        foreach (Node hijo in gridMochila.GetChildren())
        {
            if (hijo is Arrastrable item)
            {
                _primerSlotMochila ??= item;
                item.ObjetoConsumido += OnObjetoConsumido;
                item.ArmaSeleccionada += OnArmaSeleccionada;
            }
        }
    }

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && !_inventarioAbierto)
		{
			// Calculamos la sensibilidad: si apunta, es más lenta
			float currentSensitivity = _isAiming ? _mouseSensitivity * 0.4f : _mouseSensitivity;

			// Gira a los lados (eje Y)
			_camRoot.RotateY(-mouseMotion.Relative.X * currentSensitivity);
			// Gira arriba/abajo (eje X)
			var rotation = _camRoot.Rotation;
			rotation.X -= mouseMotion.Relative.Y * currentSensitivity;
			// Limita para que la cámara no dé vueltas completas sobre sí misma
			rotation.X = Mathf.Clamp(rotation.X, Mathf.DegToRad(-70), Mathf.DegToRad(70));
			_camRoot.Rotation = rotation;
		}

		// D-pad ABAJO con el inventario abierto: usar cura. Antes la
		// comprobación comparaba button_index (de mouse) con
		// JOY_BUTTON_DPAD_DOWN y nunca se cumplía.
		if (@event is InputEventJoypadButton joyButton && joyButton.Pressed
			&& joyButton.ButtonIndex == JoyButton.DpadDown && _inventarioAbierto)
		{
			OnObjetoConsumido("cura");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float deltaF = (float)delta;

		// Abrir inventario
		if (Input.IsActionJustPressed("open_inventory"))
		{
			ManejarInventario();
		}
		// Si el inventario esta abierto bloqueamos los movimientos del personaje
		if (_inventarioAbierto)
		{
			_speed = WalkSpeed * 0.4f;
			_isAiming = false;

			// --- Rotacion de la capsula con el mando usando el stick derecho
			// Leyendo directamente el stick del dispositivo 0
			float joyX = Input.GetJoyAxis(0, JoyAxis.RightX);
			float joyY = Input.GetJoyAxis(0, JoyAxis.RightY);

			Vector2 rotacionMando = new Vector2(joyX, joyY);

			// Aplicamos una zona muerta de 0.2 para que no gire solo si el stick funciona mal
			if (rotacionMando.Length() > 0.2f)
			{
				// Multiplicamos por 50 para compensar la diferencia de sensibilidad con respecto al mouse
				_capsuleModel.RotateY(-rotacionMando.X * _modelRotationSpeed * 50.0f);
				var rotation = _capsuleModel.Rotation;
				rotation.X -= rotacionMando.Y * _modelRotationSpeed * 50.0f;
				rotation.X = Mathf.Clamp(rotation.X, Mathf.DegToRad(-25), Mathf.DegToRad(25));
				_capsuleModel.Rotation = rotation;
			}
			// Nota: No usamos 'return' para que el movimiento de abajo se siga ejecutando
		}
		else
		{
			//Leemos el sitck derecho del mando
			float joyX = Input.GetJoyAxis(0, JoyAxis.RightX);
			float joyY = Input.GetJoyAxis(0, JoyAxis.RightY);
			Vector2 lookInput = new Vector2(joyX, joyY);

			//Zona muerta 0.2 para evitar drift si el stick esta desgastado
			if (lookInput.Length() > 0.2f)
			{
				//Si apunta la sensibilidad tambien baja al 40%
				float currentJoySens = _isAiming ? _joySensitivity * 0.4f : _joySensitivity;

				//Multiplicamos por deltaF para que gire igual a 60 FPS
				_camRoot.RotateY(-lookInput.X * currentJoySens * deltaF);

				var rotation = _camRoot.Rotation;
				rotation.X -= lookInput.Y * currentJoySens * deltaF;
				rotation.X = Mathf.Clamp(rotation.X, Mathf.DegToRad(-70), Mathf.DegToRad(70));
				_camRoot.Rotation = rotation;
			}
		}

		// Gravedad
		if (!IsOnFloor())
		{
			Velocity += GetGravity() * deltaF;
		}

		if (IsOnFloor())
		{
			_saltosRestantes = MaxSaltos;
		}

		//Doble Salto solo si el inventario esta cerrado
		if (!_inventarioAbierto && Input.IsActionJustPressed("Jump") && _saltosRestantes > 0)
		{
			var velocity = Velocity;
			velocity.Y = JumpVelocity;
			Velocity = velocity;
			_saltosRestantes--;
		}

		// Regenerar stamina
		if (_currentStamina < _maxStamina)
		{
			_currentStamina += _staminaRegen * deltaF;
			_currentStamina = Mathf.Clamp(_currentStamina, 0.0f, _maxStamina);
		}

		// Actualizar UI
		_staminaBar.Value = _currentStamina;
		_healthBar.Value = _currentHealth;

		// Lógica para esquivar (Dash)
		if (_dashTimer > 0)
		{
			_dashTimer -= deltaF;
			_speed = DashSpeed;
		}
		else
		{
			_speed = WalkSpeed;
		}

		if (!_inventarioAbierto && Input.IsActionJustPressed("sprint") && IsOnFloor() && _dashTimer <= 0 && _currentStamina >= _dashCost)
		{
			_dashTimer = 0.25f; // El impulso dura un cuarto de segundo
			_currentStamina -= _dashCost;
		}

		// Movimiento base
		Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 direction = (_camRoot.Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

		// Para que no flote ni se entierre al mirar arriba/abajo:
		direction.Y = 0;
		direction = direction.Normalized();

		if (direction != Vector3.Zero)
		{
			var velocity = Velocity;
			velocity.X = direction.X * _speed;
			velocity.Z = direction.Z * _speed;
			Velocity = velocity;
		}
		else
		{
			var velocity = Velocity;
			velocity.X = Mathf.MoveToward(Velocity.X, 0, _speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, _speed);
			Velocity = velocity;
		}

		// --- CAMBIO DE ARMAS ---
		if (Input.IsActionJustPressed("switch_weapon"))
		{
			if (_currentWeapon == Weapon.Ranged)
			{
				_currentWeapon = Weapon.Melee;
				_isAiming = false; // Cancela el apuntado al cambiar a melee
				_reticle.Hide();
				GD.Print("Arma equipada: Cuerpo a Cuerpo ⚔️");
			}
			else
			{
				_currentWeapon = Weapon.Ranged;
				GD.Print("Arma equipada: Fuego a Distancia 🔫");
			}
		}

		// --- LÓGICA DE ARMAS ---
		// Con el inventario abierto no se puede atacar ni disparar.
		if (_inventarioAbierto)
		{
			_isAiming = false;
			_reticle.Hide();
		}
		else if (_currentWeapon == Weapon.Ranged)
		{
			// Detectar apuntado
			if (!_inventarioAbierto)
			{
				if (Input.IsActionPressed("aim"))
				{
					_isAiming = true;
					_reticle.Show();
					_speed = WalkSpeed * 0.6f; // Camina más lento al apuntar
				}
				else
				{
					_isAiming = false;
					_reticle.Hide();
				}

				// Disparar
				if (Input.IsActionJustPressed("shoot") && _isAiming)
				{
					if (_raycast.IsColliding())
					{
						GodotObject objetivo = _raycast.GetCollider();
						GD.Print("¡Pum! Le diste a: ", (objetivo as Node)?.Name);

						// Verificamos si le dio al enemigo
						if (objetivo.HasMethod("GetDamage"))
						{
							objetivo.Call("GetDamage", 25); // El disparo bajara 25 de vida
						}
					}
					else
					{
						GD.Print("¡Pum! Disparo al aire...");
					}
				}
			}
		}
		// --- LÓGICA DE ARMA CUERPO A CUERPO ---
		else if (_currentWeapon == Weapon.Melee)
		{
			_isAiming = false; // Evita que la cámara haga zoom
			_reticle.Hide();    // Oculta la mira por seguridad

			// GOLPE LIGERO: Reutilizamos el botón de Disparar (Clic Izquierdo / RT)
			if (Input.IsActionJustPressed("shoot"))
			{
				EjecutarGolpeMelee("GOLPE LIGERO");
			}

			// GOLPE PESADO: Reutilizamos el botón de Apuntar (Clic Derecho / LT)
			if (Input.IsActionJustPressed("aim"))
			{
				if (_currentStamina >= _heavyAttackCost)
				{
					_currentStamina -= _heavyAttackCost;
					EjecutarGolpeMelee("GOLPE PESADO");
				}
				else
				{
					GD.Print("No hay suficiente estamina");
				}
			}
		}

		// Movimiento suave de la cámara (Zoom y Desplazamiento lateral)
		float targetCamLength = _isAiming ? AimCamLength : NormalCamLength;
		float targetHOffset = _isAiming ? AimHOffset : NormalHOffset;

		// Acercamos la cámara acortando el brazo
		_springArm.SpringLength = Mathf.Lerp(_springArm.SpringLength, targetCamLength, deltaF * 12.0f);
		// Movemos la base del brazo hacia la derecha
		var springPos = _springArm.Position;
		springPos.X = Mathf.Lerp(_springArm.Position.X, targetHOffset, deltaF * 12.0f);
		_springArm.Position = springPos;

		MoveAndSlide();
	}

	private void EjecutarGolpeMelee(string tipoGolpe)
	{
		GD.Print("¡Bianca lanza un ", tipoGolpe, "!");
		var cuerposGolpeados = _meleeHitbox.GetOverlappingBodies();

		// Definimos el daño (15 para el ligero y 35 para el pesado)
		int damage = tipoGolpe == "GOLPE LIGERO" ? 15 : 35;

		foreach (Node3D cuerpo in cuerposGolpeados)
		{
			if (cuerpo != this)
			{
				GD.Print("¡Impacto de ", tipoGolpe, " a: ", cuerpo.Name, "!");
				if (cuerpo.HasMethod("GetDamage"))
				{
					cuerpo.Call("GetDamage", damage);
				}
			}
		}
	}

	public void RecibirDano(float cantidad)
	{

		//i-frames porque sino no es un souls
		if (_dashTimer > 0)
		{
			GD.Print("Esquivaste como gato maullame (i-frames activos)");
			return;
		}
		_currentHealth -= cantidad;
		_currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
		_healthBar.Value = _currentHealth;
		GD.Print("¡Bianca recibió daño! Salud actual: ", _currentHealth);

		if (_currentHealth <= 0)
		{
			GD.Print("¡Bianca ha caído!");
			Morir();
		}
	}

	private void ManejarInventario()
	{

		_inventarioAbierto = !_inventarioAbierto;
		_armaPendienteId = "";

		if (_inventarioAbierto)
		{
			_inventoryPanel.Show();
			Input.MouseMode = Input.MouseModeEnum.Visible; // Se libera el cursor
			// GetTree().Paused = true; // Se pausa todo el juego
			_inventarioLabel.Text = "INVENTARIO - Memorias: " + _memoriasRecuperadas + " | Curas: " + _objetosCurativos;
			_primerSlotMochila?.GrabFocus();
		}
		else
		{
			_inventoryPanel.Hide();
			Input.MouseMode = Input.MouseModeEnum.Captured; // Se captura el mouse nuevamente
			GetViewport().GuiReleaseFocus();
			// GetTree().Paused = false; // Se reanuda el juego
		}
	}

	private void OnBtnCuraPressed()
	{
		// Verificamos si tenemos curas y si Bianca no tiene ya la vida al máximo
		if (_objetosCurativos > 0 && _currentHealth < _maxHealth)
		{
			_objetosCurativos -= 1;
			_currentHealth += 40.0f; // Cada cura restaura 40 puntos de vida
			_currentHealth = Mathf.Clamp(_currentHealth, 0.0f, _maxHealth);

			// Actualizamos la barra roja de la interfaz
			_healthBar.Value = _currentHealth;

			// Actualizamos el texto del inventario
			_inventarioLabel.Text = "INVENTARIO - Memorias: " + _memoriasRecuperadas + " | Curas: " + _objetosCurativos;
			GD.Print("¡Bianca usó una cura! Salud actual: ", _currentHealth);
		}
		else if (_currentHealth >= _maxHealth)
		{
			GD.Print("¡La salud ya está al máximo, no desperdicies curas!");
		}
		else
		{
			GD.Print("¡El inventario de curas está vacío!");
		}
	}

	// Conexión en bianca.tscn: gui_input del SubViewportContainer
	public void OnSubViewportContainerGuiInput(InputEvent @event)
	{
		if (!_inventarioAbierto) return;
		// Detectar si se presiona o se suelta el clic izquierdo
		if (@event is InputEventMouseButton mouseButton && _isRotatingModel)
		{
			_isRotatingModel = mouseButton.Pressed;
		}
		// Rotar la capsula al arrastrar
		if (@event is InputEventMouseMotion mouseMotion && _isRotatingModel)
		{
			_capsuleModel.RotateY(-mouseMotion.Relative.X * _modelRotationSpeed);
			var rotation = _capsuleModel.Rotation;
			rotation.X -= mouseMotion.Relative.Y * _modelRotationSpeed;
			// Limitamos el giro vertical para que la capsula no de vueltas
			rotation.X = Mathf.Clamp(rotation.X, Mathf.DegToRad(-25), Mathf.DegToRad(25));
			_capsuleModel.Rotation = rotation;
		}
	}

	// --- FUNCIÓN QUE RECIBE LA SEÑAL DESDE LA UI ---
	private void OnArmaAsignada(int numeroSlot, string tipoArma)
	{
		if (numeroSlot == 1)
		{
			_armaEnSlot1 = tipoArma;
		}
		else if (numeroSlot == 2)
		{
			_armaEnSlot2 = tipoArma;
		}

		GD.Print("El inventario actualizó el Slot ", numeroSlot, " con: ", tipoArma);
	}

	private void OnObjetoConsumido(string tipoObjeto)
	{
		if (tipoObjeto == "cura")
		{
			// Validamos que hayan curas disponibles
			if (_objetosCurativos > 0 && _currentHealth < _maxHealth)
			{
				_objetosCurativos -= 1;
				_currentHealth += 40.0f;
				_currentHealth = Mathf.Clamp(_currentHealth, 0.0f, _maxHealth);

				// Actualizamos las barras de la interfaz
				_healthBar.Value = _currentHealth;
				_inventarioLabel.Text = "INVENTARIO - Memorias: " + _memoriasRecuperadas + " | Curas: " + _objetosCurativos;
				GD.Print("Bianca uso cura rapida desde la mochila - Salud actual: ", _currentHealth);
			}
			else if (_currentHealth >= _maxHealth)
			{
				GD.Print("Salud al maximo, bianca guardo cura");
			}
			else
			{
				GD.Print("mochila vacia sin objetos curativos");
			}
		}
	}

	public void RecogerCura()
	{
		_objetosCurativos += 1;
		//Actualizamos el texto de la interfaz al instante
		_inventarioLabel.Text = "INVENTARIO - Memorias: " + _memoriasRecuperadas + " | Curas: " + _objetosCurativos;
		GD.Print("¡Botiquin recogido! Curas totales: ", _objetosCurativos);
	}

	private void OnArmaSeleccionada(string idObjeto, Color colorOrigen)
	{
		_armaPendienteId = idObjeto;
		_armaPendienteColor = colorOrigen;
		GD.Print("Arma seleccionada con mando: ", idObjeto, ". Elige un quick slot y presiona A");
		_slot1.GrabFocus();

	}

	private void OnSlotConfirmadoConMando(RanuraDestino ranura)
	{
		if (string.IsNullOrEmpty(_armaPendienteId)) return;

		ranura.AsignarArma(_armaPendienteId, _armaPendienteColor);
		_armaPendienteId = "";
		_primerSlotMochila?.GrabFocus();
	}


}
