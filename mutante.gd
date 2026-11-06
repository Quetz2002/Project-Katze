extends CharacterBody3D

var salud = 100
const SPEED = 3.5
var objetivo : Node3D = null
var puede_atacar = true

@onready var attack_cooldown = $AttackCooldown

func _physics_process(delta):
	# Gravedad
	if not is_on_floor():
		velocity += get_gravity() * delta

	if objetivo:
		var distancia = global_position.distance_to(objetivo.global_position)
		
		# Perseguir a Bianca si está lejos
		if distancia > 1.8:
			var direccion = global_position.direction_to(objetivo.global_position)
			direccion.y = 0 
			direccion = direccion.normalized()
			
			velocity.x = direccion.x * SPEED
			velocity.z = direccion.z * SPEED
			
			var pos_mirar = Vector3(objetivo.global_position.x, global_position.y, objetivo.global_position.z)
			look_at(pos_mirar, Vector3.UP)
		else:
			# Detenerse y golpear si está cerca
			velocity.x = 0
			velocity.z = 0
			_atacar_jugador()
	else:
		velocity.x = move_toward(velocity.x, 0, SPEED)
		velocity.z = move_toward(velocity.z, 0, SPEED)

	move_and_slide()

func _atacar_jugador():
	if puede_atacar and objetivo.has_method("recibir_dano"):
		print("¡El Mutante te dio un zarpazo!")
		objetivo.recibir_dano(20) 
		puede_atacar = false
		attack_cooldown.start()

func recibir_dano(cantidad: int):
	salud -= cantidad
	print("¡Mutante recibió ", cantidad, " de daño! Salud restante: ", salud)
	if salud <= 0:
		print("¡Mutante derrotado!")
		queue_free()

# --- SEÑALES PARA LA IA ---
func _on_vision_area_body_entered(body):
	if body.is_in_group("Jugador"):
		objetivo = body

func _on_vision_area_body_exited(body):
	if body == objetivo:
		objetivo = null

func _on_attack_cooldown_timeout():
	puede_atacar = true
