extends SceneTree

# 只回答一个问题：face=-1 时，SwordArm 的 rotation_degrees 到底是屏幕上的哪个方向？
const ANGLES := [90, 45, 0, -45, -90]
const CROP := 300
const ZOOM := 1.55
const FOCUS := Vector2(0, -80)

func _initialize() -> void:
	var root_node := Node2D.new()
	root.add_child(root_node)

	var enemy: Node2D = load("res://PirateApple.tscn").instantiate()
	root_node.add_child(enemy)
	enemy.global_position = Vector2.ZERO
	await process_frame
	enemy.process_mode = Node.PROCESS_MODE_DISABLED
	enemy.position = Vector2.ZERO
	enemy.get_node("HpBar").visible = false
	enemy.get_node("Visual").scale = Vector2(-0.55, 0.55)

	var arm: Node2D = enemy.get_node("Visual/SwordArm")

	var cam := Camera2D.new()
	cam.zoom = Vector2(2.0, 2.0)
	cam.position_smoothing_enabled = false
	root_node.add_child(cam)
	cam.global_position = FOCUS
	cam.make_current()
	await process_frame

	var w := int(CROP * ZOOM)
	var out := Image.create_empty(w * ANGLES.size(), w, false, Image.FORMAT_RGBA8)
	out.fill(Color(0.13, 0.14, 0.16))

	for ci in range(ANGLES.size()):
		arm.rotation_degrees = ANGLES[ci]
		for i in range(3):
			await process_frame
		var full := root.get_viewport().get_texture().get_image()
		if full == null:
			print("!! 拿不到视口"); quit(); return
		var img := Vector2(full.get_width(), full.get_height())
		var x0 := clampi(int(img.x * 0.5 - CROP * 0.5), 0, full.get_width() - CROP)
		var y0 := clampi(int(img.y * 0.5 - CROP * 0.5), 0, full.get_height() - CROP)
		var piece := Image.create_empty(CROP, CROP, false, Image.FORMAT_RGBA8)
		piece.blit_rect(full, Rect2i(x0, y0, CROP, CROP), Vector2i.ZERO)
		piece.resize(w, w, Image.INTERPOLATE_NEAREST)
		out.blit_rect(piece, Rect2i(0, 0, w, w), Vector2i(ci * w, 0))
		print("angle=", ANGLES[ci])

	out.save_png("res://docs/screenshots/pirate_angles.png")
	print("[Shot] 完成 ", out.get_size(), "  列=", ANGLES)
	quit()
