extends CharacterBody3D

var health = 200

#Esta funcion se llamara cada vez que reciba daño
func get_damage(amount: int):
	health -= amount
	print("El mutante recibio ", amount, " de daño! Salud restant: ", health)
	
	if health <= 0:
		print("Mutante derrotado")
		queue_free() #Con esta funcion se destruye el nodo 
