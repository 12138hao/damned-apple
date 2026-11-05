extends SceneTree

# 拍「第一次对话第一句」那一刻，看气泡是不是从老苹果身上出来的

func _press_e(n := 2) -> void:
	Input.action_press("interact")
	for i in range(n): await process_frame
	Input.action_release("interact")
	for i in range(3): await process_frame

func _initialize() -> void:
	var node: Node = load("res://jungleinside.tscn").instantiate()
	root.add_child(node); current_scene = node
	await process_frame; await physics_frame

	var box: CharacterBody2D = node.get_node("box")
	var npc: Node2D = node.get_node("OldApple")
	box.global_position = Vector2(npc.global_position.x - 80, npc.global_position.y - 60)
	for i in range(120):
		await physics_frame
		if box.is_on_floor(): break
	for i in range(20): await process_frame

	await _press_e()
	for i in range(30): await process_frame      # 让打字机打出一半
	var img := root.get_viewport().get_texture().get_image()
	img.save_png("res://docs/screenshots/oldapple_talk.png")
	var b: Node2D = npc.get_node("DialogueUI")
	print("[Shot] 气泡 position=%s  世界坐标=%s" % [b.position, b.global_position])
	quit()
