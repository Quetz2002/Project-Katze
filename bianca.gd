extends CharacterBody3D

# --- NODOS ---
@onready var cam_root = $CamRoot
@onready var spring_arm = $CamRoot/SpringArm3D
@onready var camera = $CamRoot/SpringArm3D/Camera3D
@onready var reticle = $UI/CenterContainer
@onready var raycast = $CamRoot/SpringArm3D/Camera3D/RayCast3D
@onready var melee_hitbox = $MeleeHitbox # Nodo para el golpe cuerpo a cuerpo
@onready var health_bar = $UI/HealthBar
@onready var stamina_bar = $UI/StaminaBar

# --- ESTADISTICAS ---
var max_health = 100.0
var current_health = max_health
var max_stamina = 100.0
var current_stamina = max_stamina
var stamina_regen = 15.0 #Cuanta stamina recupera por segundo
var dash_cost = 25.0
var heavy_attack_cost = 35.0

# --- ESTADOS DE ARMAS ---
enum Weapon { RANGED, MELEE }
var current_weapon = Weapon.RANGED

# --- CÁMARA Y APUNTADO ---
const NORMAL_CAM_LENGTH = 3.0
const AIM_CAM_LENGTH = 1.2
const NORMAL_H_OFFSET = 0.0 # Posición centrada normal
const AIM_H_OFFSET = 1.0    # Cuánto se mueve a la derecha al apuntar
var is_aiming = false
var mouse_sensitivity = 0.003

# --- MOVIMIENTO ---
const JUMP_VELOCITY = 4.5
var SPEED = 5.0 
const WALK_SPEED = 5.0
const DASH_SPEED = 15.0
var dash_timer = 0.0


func _ready():
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
	reticle.hide()

func _unhandled_input(event):
	if event is InputEventMouseMotion:
		# Calculamos la sensibilidad: si apunta, es más lenta
		var current_sensitivity = mouse_sensitivity * 0.4 if is_aiming else mouse_sensitivity
		
		# Gira a los lados (eje Y)
		cam_root.rotate_y(-event.relative.x * current_sensitivity)
		# Gira arriba/abajo (eje X)
		cam_root.rotation.x -= event.relative.y * current_sensitivity
		# Limita para que la cámara no dé vueltas completas sobre sí misma
		cam_root.rotation.x = clamp(cam_root.rotation.x, deg_to_rad(-70), deg_to_rad(70))

func _physics_process(delta: float) -> void:
	# Gravedad
	if not is_on_floor():
		velocity += get_gravity() * delta

	# Regenerar stamina
	if current_stamina < max_stamina:
		current_stamina += stamina_regen * delta
		current_stamina = clamp(current_stamina, 0.0, max_stamina)
		
	# Actualizar UI
	stamina_bar.value = current_stamina
	health_bar.value = current_health
	
	# Lógica para esquivar (Dash)
	if dash_timer > 0:
		dash_timer -= delta
		SPEED = DASH_SPEED
	else:
		SPEED = WALK_SPEED

	if Input.is_action_just_pressed("ui_accept") and is_on_floor() and dash_timer <= 0 and current_stamina >= dash_cost:
		dash_timer = 0.25 # El impulso dura un cuarto de segundo
		current_stamina -= dash_cost

	# Movimiento base
	var input_dir := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	var direction = (cam_root.transform.basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
	
	# Para que no flote ni se entierre al mirar arriba/abajo:
	direction.y = 0 
	direction = direction.normalized()
	
	if direction:
		velocity.x = direction.x * SPEED
		velocity.z = direction.z * SPEED
	else:
		velocity.x = move_toward(velocity.x, 0, SPEED)
		velocity.z = move_toward(velocity.z, 0, SPEED)

	# --- CAMBIO DE ARMAS ---
	if Input.is_action_just_pressed("switch_weapon"):
		if current_weapon == Weapon.RANGED:
			current_weapon = Weapon.MELEE
			is_aiming = false # Cancela el apuntado al cambiar a melee
			reticle.hide()
			print("Arma equipada: Cuerpo a Cuerpo ⚔️")
		else:
			current_weapon = Weapon.RANGED
			print("Arma equipada: Fuego a Distancia 🔫")

	# --- LÓGICA DE ARMA A DISTANCIA ---
	if current_weapon == Weapon.RANGED:
		# Detectar apuntado
		if Input.is_action_pressed("aim"):
			is_aiming = true
			reticle.show()
			SPEED = WALK_SPEED * 0.6 # Camina más lento al apuntar
		else:
			is_aiming = false
			reticle.hide()
			
		# Disparar
		if Input.is_action_just_pressed("shoot") and is_aiming:
			if raycast.is_colliding():
				var objetivo = raycast.get_collider()
				print("¡Pum! Le diste a: ", objetivo.name)
			else:
				print("¡Pum! Disparo al aire...")

	# --- LÓGICA DE ARMA CUERPO A CUERPO ---
	elif current_weapon == Weapon.MELEE:
		is_aiming = false # Evita que la cámara haga zoom
		reticle.hide()    # Oculta la mira por seguridad
		
		# GOLPE LIGERO: Reutilizamos el botón de Disparar (Clic Izquierdo / RT)
		if Input.is_action_just_pressed("shoot"):
			_ejecutar_golpe_melee("GOLPE LIGERO")
			
		# GOLPE PESADO: Reutilizamos el botón de Apuntar (Clic Derecho / LT)
		if Input.is_action_just_pressed("aim"):
			if current_stamina >= heavy_attack_cost:
				current_stamina -= heavy_attack_cost
				_ejecutar_golpe_melee("GOLPE PESADO")
			else:
				print("No hay suficiente estamina")

	# Movimiento suave de la cámara (Zoom y Desplazamiento lateral)
	var target_cam_length = AIM_CAM_LENGTH if is_aiming else NORMAL_CAM_LENGTH
	var target_h_offset = AIM_H_OFFSET if is_aiming else NORMAL_H_OFFSET
	
	# Acercamos la cámara acortando el brazo
	spring_arm.spring_length = lerp(spring_arm.spring_length, target_cam_length, delta * 12.0)
	# Movemos la base del brazo hacia la derecha
	spring_arm.position.x = lerp(spring_arm.position.x, target_h_offset, delta * 12.0)

	move_and_slide()
func _ejecutar_golpe_melee(tipo_golpe: String):
	print("¡Bianca lanza un ", tipo_golpe, "!")
	var cuerpos_golpeados = melee_hitbox.get_overlapping_bodies()
	
	for cuerpo in cuerpos_golpeados:
		if cuerpo != self:
			print("¡Impacto de ", tipo_golpe, " a: ", cuerpo.name, "!")
