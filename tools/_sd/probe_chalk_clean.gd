extends SceneTree

# 最干净的复现：冻住 Boss，苹果怪放在空地，用画布变换精确瞄准，看粉笔能不能打掉血
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var arena: Node = scn.get_node("SnakeBossArena")
	var boss: Node = arena.get_node("SnakeBoss")

	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	var t := 0.0
	while t < 30.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break
	await create_timer(0.6).timeout
	# 冻住 Boss，别让它中途打断
	boss.set_physics_process(false)
	boss.set_process(false)

	# 玩家站到空地，苹果怪放在左边 140
	box.global_position = Vector2(boss.global_position.x - 130.0, 684.0)
	await create_timer(0.4).timeout

	var m: Node2D = load("res://SnakeMinion.tscn").instantiate()
	arena.add_child(m)
	m.global_position = box.global_position + Vector2(-140, 0)
	m.HopForward = 0.0
	m.HopUp = 0.0
	m.HopInterval = 99.0
	await create_timer(0.8).timeout

	var bat = load("res://assets/weapons/chalk.tres")
	var slots = box.Weapons
	slots[0] = bat
	box.Weapons = slots
	Input.action_press("slot1")
	await create_timer(0.2).timeout
	Input.action_release("slot1")
	await create_timer(0.3).timeout

	# 用画布变换把"苹果怪胸口的世界坐标"换算成屏幕坐标，瞄得准一点
	var xf: Transform2D = box.get_viewport().get_canvas_transform()
	var aimWorld: Vector2 = m.global_position + Vector2(0, -32)
	# 事件坐标是"窗口坐标"，视口可能被 stretch 缩放，先算出缩放比
	var vpSize: Vector2 = box.get_viewport().get_visible_rect().size
	var winSize := Vector2(DisplayServer.window_get_size())
	var winScale := winSize / vpSize
	var mm := InputEventMouseMotion.new()
	mm.position = (xf * aimWorld) * winScale
	Input.warp_mouse(mm.position)
	Input.parse_input_event(mm)
	print("[Clean] 视口=%s 窗口=%s 缩放=%s" % [str(vpSize), str(winSize), str(winScale)])
	await create_timer(0.25).timeout
	print("[Clean] 视口鼠标=%s 玩家世界=%s 鼠标世界=%s 相对=%s" % [
		str(box.get_viewport().get_mouse_position()), str(box.global_position),
		str(box.get_global_mouse_position()),
		str(box.get_global_mouse_position() - box.global_position)])
	print("[Clean] 苹果怪=%s 瞄准世界=%s 屏幕=%s 玩家=%s" % [
		str(m.global_position), str(aimWorld), str(mm.position), str(box.global_position)])

	var hp0: int = m.Hp
	Input.action_press("attack")
	for i in range(24):
		await create_timer(0.05).timeout
		for c in AllNodes(scn):
			var sc = c.get_script()
			if sc != null and str(sc.resource_path).ends_with("ChalkProjectile.cs"):
				print("[Clean] t=%.2f 粉笔=%s" % [i * 0.05, str(c.global_position)])
	Input.action_release("attack")
	await create_timer(0.6).timeout
	print("[Clean] 一发之后 hp=%d -> %d" % [hp0, m.Hp])
	var img := root.get_viewport().get_texture().get_image()
	if img != null: img.save_png("res://docs/screenshots/chalk_minion.png")
	quit()

func AllNodes(n: Node) -> Array:
	var out := [n]
	for c in n.get_children():
		out += AllNodes(c)
	return out
