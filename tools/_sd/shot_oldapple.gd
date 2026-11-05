extends SceneTree

# 拍两张：① 平时站在房间里（看贴图和 E 提示）② 四选一菜单打开时

func _press_e(n := 2) -> void:
	Input.action_press("interact")
	for i in range(n):
		await process_frame
	Input.action_release("interact")
	for i in range(3):
		await process_frame


func _initialize() -> void:
	var node: Node = load("res://jungleinside.tscn").instantiate()
	root.add_child(node)
	current_scene = node
	await process_frame
	await physics_frame

	var gs: Node = root.get_node_or_null("GameState")
	var box: CharacterBody2D = node.get_node("box")
	var npc: Node2D = node.get_node("OldApple")

	box.global_position = Vector2(npc.global_position.x - 80, npc.global_position.y - 60)
	for i in range(120):
		await physics_frame
		if box.is_on_floor():
			break
	for i in range(20):
		await process_frame

	var img := root.get_viewport().get_texture().get_image()
	if img == null:
		print("!! 拿不到视口"); quit(); return
	img.save_png("res://docs/screenshots/oldapple_idle.png")
	print("[Shot] 平时站位 ok")

	# 先按 E 开始首次对话，走完它，再按 E 打开菜单
	await _press_e()
	var guard := 0
	while guard < 40 and bool(gs.get("DialogueActive")):
		await _press_e()
		guard += 1
	await _press_e()
	for i in range(20):
		await process_frame

	img = root.get_viewport().get_texture().get_image()
	img.save_png("res://docs/screenshots/oldapple_menu.png")
	var m: Node = npc.get_node("ChoiceMenu")
	print("[Shot] 菜单 ok  可见=%s 当前项=%s" % [m.visible, m.get("Current")])
	quit()
