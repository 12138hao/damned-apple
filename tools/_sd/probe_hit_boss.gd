extends SceneTree

# 复现：站到蛇旁边用木棒砍，看 Boss 的 Hp 有没有掉
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var arena: Node = scn.get_node("SnakeBossArena")
	var boss: Node = arena.get_node("SnakeBoss")

	# 进区域触发战斗（对话可能要几秒）
	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	var t := 0.0
	while t < 30.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break
	print("[Hit] 开战 state=%d invuln=%s hp=%d/%d" % [boss.State, str(boss.Invulnerable), boss.Hp, boss.MaxHp])
	print("[Hit] box=%s  arena.monitoring=%s  overlaps=%s" % [
		str(box.global_position), str(arena.monitoring), str(arena.overlaps_body(box))])

	# 贴到树干左边
	box.global_position = Vector2(boss.global_position.x - 70.0, 684.0)
	await create_timer(0.8).timeout

	# 给一把木棒并拿在手上
	var bat = load("res://assets/weapons/bat.tres")
	var slots = box.Weapons
	slots[0] = bat
	box.Weapons = slots
	Input.action_press("slot1")
	await create_timer(0.2).timeout
	Input.action_release("slot1")
	await create_timer(0.3).timeout

	# 鼠标指到玩家右侧（用画布坐标，别猜世界坐标）
	var sp: Vector2 = box.get_global_transform_with_canvas().origin
	var mm := InputEventMouseMotion.new()
	mm.position = sp + Vector2(140, 0)
	Input.parse_input_event(mm)
	await create_timer(0.2).timeout
	print("[Hit] 鼠标=%s 玩家屏幕=%s" % [str(mm.position), str(sp)])

	var hp0: int = boss.Hp
	for swing in range(3):
		Input.action_press("attack")
		await create_timer(0.55).timeout
		Input.action_release("attack")
		await create_timer(0.5).timeout
		print("[Hit] 第%d刀后 hp=%d" % [swing + 1, boss.Hp])
	print("[Hit] 总计 %d -> %d" % [hp0, boss.Hp])
	quit()
