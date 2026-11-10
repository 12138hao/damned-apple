extends SceneTree

# 复现：召唤出来的蛇小怪能不能被打、有没有受击反馈
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
	await create_timer(1.0).timeout

	# 手动放一只小怪，冻住它（不然它扑上来会自爆）
	var scene: PackedScene = load("res://SnakeMinion.tscn")
	var m: Node2D = scene.instantiate()
	arena.add_child(m)
	m.global_position = box.global_position + Vector2(40, 0)
	m.set_physics_process(false)
	await create_timer(0.5).timeout
	print("[Mob] 小怪 hp=%d/%d  血条节点=%s" % [m.Hp, m.MaxHp, str(m.get_node_or_null("HpBar"))])
	print("[Mob] box=%s mob=%s 距离=%.1f boss=%s" % [str(box.global_position), str(m.global_position),
		box.global_position.distance_to(m.global_position), str(boss.global_position)])

	# 木棒
	var bat = load("res://assets/weapons/bat.tres")
	var slots = box.Weapons
	slots[0] = bat
	box.Weapons = slots
	Input.action_press("slot1")
	await create_timer(0.2).timeout
	Input.action_release("slot1")
	await create_timer(0.3).timeout

	var sp: Vector2 = box.get_global_transform_with_canvas().origin
	var mm := InputEventMouseMotion.new()
	mm.position = sp + Vector2(140, 0)
	Input.parse_input_event(mm)
	await create_timer(0.2).timeout

	for i in range(3):
		Input.action_press("attack")
		await create_timer(0.55).timeout
		Input.action_release("attack")
		await create_timer(0.08).timeout
		var art := m.get_node_or_null("Art")
		print("[Mob]   Art.modulate=%s" % str(art.modulate if art != null else "无"))
		var im := root.get_viewport().get_texture().get_image()
		if im != null: im.save_png("res://docs/screenshots/fb_minion_%d.png" % i)
		await create_timer(0.37).timeout
		if not is_instance_valid(m):
			print("[Mob] 第%d刀后小怪没了" % [i + 1])
			break
		print("[Mob] 第%d刀后 hp=%d  boss=%d  cur=%s" % [i + 1, m.Hp, boss.Hp, str(box.Current)])
	quit()
