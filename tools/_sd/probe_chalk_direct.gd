extends SceneTree

# 直接生成一颗粉笔打召唤物，绕开输入模拟，只看命中链路
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
	box.global_position = Vector2(boss.global_position.x - 130.0, 684.0)
	await create_timer(0.5).timeout

	var m: Node2D = load("res://SnakeMinion.tscn").instantiate()
	arena.add_child(m)
	m.global_position = box.global_position + Vector2(150, 0)
	m.HopForward = 0.0
	m.HopUp = 0.0
	m.HopInterval = 99.0
	await create_timer(0.7).timeout
	print("[Proj] 召唤物 pos=%s hp=%d" % [str(m.global_position), m.Hp])

	# 从左侧 60px 处水平射一颗，正对它身体中心（脚底上方 40px）
	var script = load("res://ChalkProjectile.cs")
	if script == null:
		print("[Proj] 加载 ChalkProjectile.cs 失败")
		quit()
		return
	var p = script.new()
	p.Damage = 50
	p.Speed = 900.0
	p.MaxDistance = 400.0
	p.Radius = 5.0
	scn.add_child(p)
	p.global_position = m.global_position + Vector2(75, -30)
	p.Launch(Vector2.LEFT)
	print("[Proj] 生成于 %s  贴图=%s" % [str(p.global_position), str(p.ProjectileTexture)])

	for i in range(10):
		await create_timer(0.05).timeout
		if not is_instance_valid(p):
			print("[Proj] t=%.2f 投射物已销毁" % [i * 0.05])
			break
		print("[Proj] t=%.2f pos=%s 召唤物hp=%d" % [i * 0.05, str(p.global_position), m.Hp])
	print("[Proj] 最终 hp=%d" % m.Hp)
	quit()
