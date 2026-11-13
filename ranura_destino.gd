extends ColorRect

# Declaramos una señal que enviaremos al script principal de Bianca
# Pasaremos dos datos: qué slot se actualizó y qué arma se equipó
signal arma_asignada(numero_slot, tipo_arma)

@export var numero_slot: int = 1 # Para saber si es el Quick Slot 1 o el 2

# 2. Godot pregunta: "¿Es válido soltar esto que traigo aquí?"
func _can_drop_data(at_position: Vector2, data: Variant) -> bool:
	# Solo permitimos soltar si los datos vienen en el formato de diccionario que creamos
	if typeof(data) == TYPE_DICTIONARY and data.has("id"):
		# Solo aceptamos armas en los Quick Slots, las curas se rechazan
		return data["id"] == "fuego" or data["id"] == "melee"
	return false

# Se ejecuta cuando el jugador suelta el clic sobre esta ranura
func _drop_data(at_position: Vector2, data: Variant) -> void:
	# Pintamos la ranura del color del objeto para dar retroalimentación visual
	self.color = data["color_origen"]
	
	print("¡Objeto táctico asignado en Slot ", numero_slot, "!: ", data["id"])
	
	# EMITIMOS LA SEÑAL para que el script de Bianca se entere
	arma_asignada.emit(numero_slot, data["id"])
