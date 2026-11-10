extends SceneTree

# 朝左砍 Boss（Boss 在玩家左边），看左边攻击到底有没有判定
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame
	var box: Node2D = scn.get_node("box")
	var boss: Node = scn.get_node("SnakeBossArena/SnakeBoss")

	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	var t := 0.0
	while t < 30.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break
	await create_timer(0.8).timeout

	# 站到 Boss 右边 130（Boss 半宽 95，够了）
	box.global_position = Vector2(boss.global_position.x + 130.0, 684.0)
	await create_timer(0.6).timeout

	var bat = load("res://assets/weapons/bat.tres")
	var slots = box.Weapons
	slots[0] = bat
	box.Weapons = slots
	Input.action_press("slot1")
	await create_timer(0.2).timeout
	Input.action_release("slot1")
	await create_timer(0.2).timeout

	var sp: Vector2 = box.get_global_transform_with_canvas().origin
	var mm := InputEventMouseMotion.new()
	mm.position = sp + Vector2(-150, 0)
	Input.parse_input_event(mm)
	await create_timer(0.2).timeout
	print("[Left] 玩家屏幕=%s 鼠标=%s" % [str(sp), str(mm.position)])

	var hp0: int = boss.Hp
	for i in range(2):
		Input.action_press("attack")
		await create_timer(0.55).timeout
		Input.action_release("attack")
		await create_timer(0.5).timeout
		print("[Left] 第%d刀 boss hp=%d" % [i + 1, boss.Hp])
	print("[Left] 合计 %d -> %d" % [hp0, boss.Hp])
	quit()
