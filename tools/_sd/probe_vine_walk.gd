extends SceneTree

# 滑到地面之后，左右键必须能走（不能被攀爬状态锁住）

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame
	var box: CharacterBody2D = node.get_node("box")

	box.global_position = Vector2(620, 250)
	for i in range(6):
		await physics_frame
	Input.action_press("look_up")
	for i in range(10):
		await physics_frame
	Input.action_release("look_up")
	Input.action_press("look_down")
	for i in range(60):
		await physics_frame
		if box.is_on_floor(): break
	Input.action_release("look_down")
	for i in range(6):
		await physics_frame
	print("滑到地面：", box.call("DebugClimb"), "  y=", box.global_position.y)

	var x0: float = box.global_position.x
	Input.action_press("move_right")
	for i in range(24):
		await physics_frame
	Input.action_release("move_right")
	print("按右 24 帧：x %.1f → %.1f（要走开才对）" % [x0, box.global_position.x])
	print("  ", box.call("DebugClimb"))
	quit()
