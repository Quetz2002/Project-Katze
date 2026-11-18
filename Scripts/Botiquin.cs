using Godot;
using System;

public partial class Botiquin : Area3D
{
	public void OnBodyEntered(Node3D body)
	{
		//Verificamos si el que toco el botiquin es Bianca y casteamos su clase
		if (body.Name == "Bianca" && body is Bianca bianca)
		{
			bianca.RecogerCura();
			QueueFree(); //Destruye el modelo del botiquin
		}
	}
}
