extends SceneTree

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame
	var box: CharacterBody2D = node.get_node("box")
	box.global_position = Vector2(620, 340)
	for i in range(12):
		await physics_frame
	print("A 站定: ", box.call("DebugClimb"))
	Input.action_press("look_up")
	for i in range(3):
		await physics_frame
		print("B 按W第%d帧: " % i, box.call("DebugClimb"))
	Input.action_release("look_up")
	quit()
