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
@onready var inventory_panel = $UI/InventoryPanel
@onready var inventario_label = $UI/InventoryPanel/HBoxContainer/VBoxContainer/Label


# --- DATOS DE INVENTARIO ---
var inventario_abierto = false
var memorias_recuperadas = 0
var objetos_curativos = 3

# --- INVENTARIO V3: ROTACION DEL MODELO 3D ---
var is_rotating_model = false
var model_rotation_speed = 0.01
@onready var capsule_model = $UI/InventoryPanel/HBoxContainer/SubViewportContainer/SubViewport/MeshInstance3D


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
	
	# --- CONECTAR SEÑALES DEL INVENTARIO ---
	var slot_1 = $UI/InventoryPanel/HBoxContainer/VBoxContainer/QuickSlots/QuickSlot1
	var slot_2 = $UI/InventoryPanel/HBoxContainer/VBoxContainer/QuickSlots/QuickSlot2
	
	# Conectamos la señal 'arma_asignada' a la función '_on_arma_asignada'
	slot_1.arma_asignada.connect(_on_arma_asignada)
	slot_2.arma_asignada.connect(_on_arma_asignada)
	
	#Conectamos las señales de la mochila
	#La ruta debe apuntar correctamente al cuadro de la mochila (el verde)
	var slot_cura = $UI/InventoryPanel/HBoxContainer/GridContainer/SlotCura
	slot_cura.objeto_consumido.connect(_on_objeto_consumido)

func _unhandled_input(event):
	if event is InputEventMouseMotion and not inventario_abierto:
		# Calculamos la sensibilidad: si apunta, es más lenta
		var current_sensitivity = mouse_sensitivity * 0.4 if is_aiming else mouse_sensitivity
		
		# Gira a los lados (eje Y)
		cam_root.rotate_y(-event.relative.x * current_sensitivity)
		# Gira arriba/abajo (eje X)
		cam_root.rotation.x -= event.relative.y * current_sensitivity
		# Limita para que la cámara no dé vueltas completas sobre sí misma
		cam_root.rotation.x = clamp(cam_root.rotation.x, deg_to_rad(-70), deg_to_rad(70))

func _physics_process(delta: float) -> void:
	#abrir inventario
	if Input.is_action_just_pressed("open_inventory"):
		_manejar_inventario()
	#Si el inventario esta abierto bloqueamos los movimientos del personaje
	if inventario_abierto:
		SPEED = WALK_SPEED * 0.4
		is_aiming = false
		
		# --- Rotacion de la capsula con el mando usando el stick derecho
		# Leyendo directamente el stick del dispositivo 0
		var joy_x = Input.get_joy_axis(0, JOY_AXIS_RIGHT_X)
		var joy_y = Input.get_joy_axis(0, JOY_AXIS_RIGHT_Y)
		
		var rotacion_mando = Vector2(joy_x, joy_y)
		
		#Aplicamos una zona muerta de 0.2 para que no gire solo si el stick funciona mal
		if rotacion_mando.length() > 0.2:
			#Multiplicamos por 50 para compensar la diferencia de sensibilidad con respecto al mouse
			capsule_model.rotate_y(-rotacion_mando.x * model_rotation_speed * 50.0)
			capsule_model.rotation.x -= rotacion_mando.y * model_rotation_speed * 50.0
			capsule_model.rotation.x = clamp(capsule_model.rotation.x, deg_to_rad(-25), deg_to_rad(25))
		#Nota: No usamos 'return'para que el movimiento de abajo se siga ejecutando
	
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
		if not inventario_abierto:
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
					
					#Verificamos si le dio al enemigo
					if objetivo.has_method("get_damage"):
						objetivo.get_damage(25) #el disparo bajara 25 de vida
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
	
	#Definimos el daño (15 para el ligero y 35 para el pesado)
	var damage = 15 if tipo_golpe == "GOLPE LIGERO" else 35
	
	for cuerpo in cuerpos_golpeados:
		if cuerpo != self:
			print("¡Impacto de ", tipo_golpe, " a: ", cuerpo.name, "!")
			if cuerpo.has_method("get_damage"):
				cuerpo.get_damage(damage)
func recibir_dano(cantidad: float):
	current_health -= cantidad
	current_health = clamp(current_health, 0, max_health)
	health_bar.value = current_health
	print("¡Bianca recibió daño! Salud actual: ", current_health)
	
	if current_health <= 0:
		print("¡Bianca ha caído!")

func _manejar_inventario():
	inventario_abierto = !inventario_abierto
	if inventario_abierto:
		inventory_panel.show()
		Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE) #Se libera el cursor
		#get_tree().paused = true #Se pausa todo el juego
		inventario_label.text = "INVENTARIO - Memorias: " + str(memorias_recuperadas) + " | Curas: " + str(objetos_curativos)
	else:
		inventory_panel.hide()
		Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED) #Se captura el mouse nuevamente
		#get_tree().paused = false #Se reanuda el juego


func _on_btn_cura_pressed() -> void:
	# Verificamos si tenemos curas y si Bianca no tiene ya la vida al máximo
	if objetos_curativos > 0 and current_health < max_health:
		objetos_curativos -= 1
		current_health += 40.0 # Cada cura restaura 40 puntos de vida
		current_health = clamp(current_health, 0.0, max_health)
		
		# Actualizamos la barra roja de la interfaz
		health_bar.value = current_health
		
		# Actualizamos el texto del inventario
		inventario_label.text = "INVENTARIO - Memorias: " + str(memorias_recuperadas) + " | Curas: " + str(objetos_curativos)
		print("¡Bianca usó una cura! Salud actual: ", current_health)
		
	elif current_health >= max_health:
		print("¡La salud ya está al máximo, no desperdicies curas!")
	else:
		print("¡El inventario de curas está vacío!")


func _on_sub_viewport_container_gui_input(event: InputEvent) -> void:
	#Detectar si se presiona o se suelta el clic izquierdp
	if event is InputEventMouseButton and is_rotating_model:
		is_rotating_model = event.pressed
	#Rotar la capsula al arrastrar
	if event is InputEventMouseMotion and is_rotating_model:
		capsule_model.rotate_y(-event.relative.x * model_rotation_speed)
		capsule_model.rotation.x -= event.relative.y * model_rotation_speed
		# Limitamos el giro vertical para que la capsula no de vueltas
		capsule_model.rotation.x = clamp(capsule_model.rotation.x, deg_to_rad(-25), deg_to_rad(25))
		
# --- VARIABLES PARA GUARDAR QUÉ ARMA HAY EN CADA SLOT ---
var arma_en_slot_1 = "fuego" # Por defecto, la pistola
var arma_en_slot_2 = "melee" # Por defecto, la espada

# --- FUNCIÓN QUE RECIBE LA SEÑAL DESDE LA UI ---
func _on_arma_asignada(numero_slot: int, tipo_arma: String):
	if numero_slot == 1:
		arma_en_slot_1 = tipo_arma
	elif numero_slot == 2:
		arma_en_slot_2 = tipo_arma
		
	print("El inventario actualizó el Slot ", numero_slot, " con: ", tipo_arma)

func _on_objeto_consumido(tipo_objeto: String) -> void:
	if tipo_objeto == "cura":
		#Validamos que hayan curas disponibles
		if objetos_curativos > 0 and current_health < max_health:
			objetos_curativos -= 1
			current_health += 40.0
			current_health = clamp(current_health, 0.0, max_health)
			
			#Actualizamos las barras de la interfaz
			health_bar.value = current_health
			inventario_label.text = "INVENTARIO - Memorias: " + str(memorias_recuperadas) + " | Curas: " + str(objetos_curativos)
			print("Bianca uso cura rapida desde la mochila - Salud actual: ", current_health)
			
		elif current_health >= max_health:
			print("Salud al maximo, bianca guardo cura")
			
		else:
			print("mochila vacia sin objetos curativos")
