extends ColorRect

#Se declara la señal que escuchara Bianca
signal objeto_consumido(id_objeto)

@export var id_objeto: String = "fuego" #Etiqueta para saber si es fuego, melee o cura
# 1. Se ejecuta automáticamente al mantener el clic y arrastrar
func _get_drag_data(at_position: Vector2) -> Variant:
	
	# Creamos un clon visual ("fantasma") que seguirá al cursor
	var preview = ColorRect.new()
	preview.custom_minimum_size = self.custom_minimum_size
	preview.color = self.color
	preview.modulate.a = 0.6 # Lo hacemos semitransparente
	
	# Metemos el clon en un contenedor para centrarlo en la punta del ratón
	var control = Control.new()
	control.add_child(preview)
	preview.position = -0.5 * self.custom_minimum_size
	
	set_drag_preview(control)
	
	# Devolvemos un diccionario con los datos que viajarán en el aire
	return {"id": id_objeto, "color_origen": self.color}
	
#Funcion para leer clics directamente desde el recuadro
func _gui_input(event:InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		#Detectamos el clic derecho O d-pad DOWN
		if event.button_index == MOUSE_BUTTON_RIGHT || event.button_index == JOY_BUTTON_DPAD_DOWN:
			if id_objeto == "cura":
				#Emitimos la señal de que el jugador quiere usar una cura
				objeto_consumido.emit(id_objeto)
				accept_event() # Detiene el clic evitando rebote de otros nodos
