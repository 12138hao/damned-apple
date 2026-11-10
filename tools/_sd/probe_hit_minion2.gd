extends SceneTree

# 干净复现：玩家站到 Boss 判定框外面，左边放一只小怪，朝左砍 —— 看掉血和闪红
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
	await create_timer(0.8).timeout

	# 站到 Boss 判定框左边外面（Boss 半宽 95）
	box.global_position = Vector2(boss.global_position.x - 130.0, 684.0)
	await create_timer(0.6).timeout

	var scene: PackedScene = load("res://SnakeMinion.tscn")
	var m: Node2D = scene.instantiate()
	arena.add_child(m)
	m.global_position = box.global_position + Vector2(52, 0)
	# 不让它扑过来（不然会撞到玩家自爆），但保留物理帧，闪红才跑得起来
	m.HopForward = 0.0
	m.HopUp = 0.0
	m.HopInterval = 99.0
	await create_timer(0.8).timeout
	print("[Mob] box=%s mob=%s 距离=%.1f" % [str(box.global_position), str(m.global_position),
		box.global_position.distance_to(m.global_position)])

	var bat = load("res://assets/weapons/bat.tres")
	var slots = box.Weapons
	slots[0] = bat
	box.Weapons = slots
	Input.action_press("slot1")
	await create_timer(0.2).timeout
	Input.action_release("slot1")
	await create_timer(0.2).timeout

	# 朝左瞄
	var sp: Vector2 = box.get_global_transform_with_canvas().origin
	var mm := InputEventMouseMotion.new()
	mm.position = sp + Vector2(150, 0)
	Input.parse_input_event(mm)
	await create_timer(0.2).timeout

	print("[Mob] 拿的武器=%s  box.Current=%s" % [str(bat.DisplayName), str(box.Current)])
	for i in range(3):
		Input.action_press("attack")
		await create_timer(0.55).timeout
		Input.action_release("attack")
		await create_timer(0.08).timeout
		var art := m.get_node_or_null("Art") if is_instance_valid(m) else null
		print("[Mob] 第%d刀  hp=%d  Art.modulate=%s" % [i + 1, m.Hp, str(art.modulate if art else "无")])
		var im := root.get_viewport().get_texture().get_image()
		if im != null: im.save_png("res://docs/screenshots/fb_mob_%d.png" % i)
		await create_timer(0.4).timeout
	quit()
