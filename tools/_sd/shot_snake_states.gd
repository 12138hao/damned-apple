extends SceneTree

# 黑蛇 Boss：按状态机变化抓帧（每个状态抓一帧），顺便测狂暴与死亡
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.global_position = Vector2(6900.0, 684.0)
	var boss: Node = scn.get_node_or_null("SnakeBossArena/SnakeBoss")
	if boss == null:
		print("!! 没找到 boss")
		quit()
		return

	# 等对话 + 开战
	await create_timer(8.5).timeout
	print("开战后 state=", boss.State, " hp=", boss.Hp)
	# 走真实的受击管线（不是 DrainHp），验证刀能不能打掉血
	boss.TryTakeHit(500, Vector2(6900.0, 684.0), 0.0, 0.0, true)
	await create_timer(0.3).timeout
	print("打一刀后 hp=", boss.Hp)

	var last := -1
	var shot := 0
	var t := 0.0
	while t < 26.0:
		await create_timer(0.18).timeout
		t += 0.18
		var st: int = boss.State
		if st != last:
			last = st
			shot += 1
			var img := root.get_viewport().get_texture().get_image()
			if img != null:
				var path := "res://docs/screenshots/sk_%02d_state%d.png" % [shot, st]
				img.save_png(path)
				print("[Shot] t=%.1f state=%d -> %s" % [t, st, path])
		if t > 14.0 and boss.Hp > 2500:
			boss.DrainHp(boss.Hp - 2400)
			print("-> 压到 2400 触发狂暴")

	# 打死它
	boss.DrainHp(99999)
	await create_timer(0.4).timeout
	var i2 := root.get_viewport().get_texture().get_image()
	if i2 != null:
		i2.save_png("res://docs/screenshots/sk_99_dead.png")
		print("[Shot] 死亡")
	await create_timer(2.0).timeout
	var i3 := root.get_viewport().get_texture().get_image()
	if i3 != null:
		i3.save_png("res://docs/screenshots/sk_98_dead_late.png")

	quit()
